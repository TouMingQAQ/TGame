using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.Addressable;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板加载 Module,挂载在 UIRoot 上(per-UIRoot)。
    /// 职责:Addressable 异步加载 + 单实例缓存 + 并发去重 + 卸载时归还引用计数。
    ///
    /// 状态:
    ///   - <c>Dictionary&lt;Type, BaseUIPanel&gt; _loaded</c> — 已加载面板单实例缓存
    ///   - <c>Dictionary&lt;Type, string&gt; _loadedAddresses</c> — 加载时使用的 address,Unload 时 Release
    ///   - <c>Dictionary&lt;Type, UniTask&lt;BaseUIPanel&gt;&gt; _loading</c> — 进行中加载去重
    ///
    /// 依赖:
    ///   - UIRoot(由 Bind 注入):跨 host 取 Owner.UIRegistryModule / Owner.AddressableModule,
    ///     并由调用方传入用于 panel.SetRoot(this)
    ///   - Owner.UIRegistryModule(全局,挂在 UIManager):查 Type → address
    ///   - Owner.AddressableModule(共享句柄池,挂在 UIManager):加载/释放资源
    ///   - Host.GetModule&lt;UILayerRootModule&gt;():查 panel.Layer → 根 Transform
    ///
    /// 调用链:
    ///   业务方 LoadPanelAsync&lt;T&gt;() → UIManager.LoadPanelAsync(转发) → UIRoot.LoadPanelAsync(转发) → 本模块 LoadAsync
    ///   业务方 UnloadPanel&lt;T&gt;() → UIManager.UnloadPanel(转发) → UIRoot.UnloadPanel(转发) → 本模块 Unload
    ///   业务方 GetPanel&lt;T&gt;() / IsPanelLoaded&lt;T&gt;() → UIRoot 转发 → 本模块查询
    ///   StackPanelModule.OpenAsync → UIRoot.LoadPanelAsync(转发) → 本模块 LoadAsync
    ///   UIVisibilityModule.ShowAsync → UIRoot.LoadPanelAsync(转发) → 本模块 LoadAsync
    ///
    /// 关键不变量:
    ///   - 同 Type 同时只存在一个面板实例(单实例语义)
    ///   - 并发同 Type 的 LoadAsync 共享同一次底层加载,只 Instantiate 一个 Panel
    ///   - 缓存命中时若 GameObject 已被外部 Destroy(Unity 假 null),清理死引用并重新加载
    ///   - Unload 时先 Destroy GameObject,再按加载期缓存的 address 调 AddressableModule.Release 归还引用计数(恰一次)
    ///   - UIRoot/ModuleEntity 销毁时由本模块 Destroy 统一销毁实例并按缓存 address 归还各自引用一次,幂等
    ///   - 销毁阶段不再访问 UIManager.Instance 静态单例(依赖在 Bind 时缓存),规避退出 Play 顺序不定的 NRE
    /// </summary>
    public sealed class UILoaderModule : BaseModule
    {
        // 依赖在 Bind 时缓存,销毁阶段不再访问 UIManager.Instance 静态单例
        // (退出 Play 时 UIManager.OnDestroy 可能已把 Instance 置 null)。
        private UIRegistryModule _registry;
        private AddressableModule<BaseUIPanel> _addressables;
        private bool _destroyed;

        // 运行时首次访问的兜底解析;销毁阶段只使用缓存字段,不走此路径。
        private UIRegistryModule Registry
        {
            get
            {
                if (_registry == null && UIManager.Instance != null)
                    _registry = UIManager.Instance.Registry;
                return _registry;
            }
        }

        private AddressableModule<BaseUIPanel> Addressables
        {
            get
            {
                if (_addressables == null && UIManager.Instance != null)
                    _addressables = UIManager.Instance.GetModule<AddressableModule<BaseUIPanel>>();
                return _addressables;
            }
        }

        private readonly Dictionary<Type, BaseUIPanel> _loaded = new();
        private readonly Dictionary<Type, string> _loadedAddresses = new();
        private readonly Dictionary<Type, UniTask<BaseUIPanel>> _loading = new();

        public UIRoot Root { get; set; }

        /// <summary>由 UIRoot.Initialize 注入全局依赖,避免销毁时再经静态单例解析。</summary>
        public void Bind(UIRegistryModule registry, AddressableModule<BaseUIPanel> addressables)
        {
            _registry = registry;
            _addressables = addressables;
            // 复位销毁标记:UIRoot 若被重新 Initialize(同一 ModuleEntity 未清理而复用本实例)时,
            // 不会因上一次的销毁标记导致模块永久失效。正常销毁路径下 ModuleEntity.ClearModule
            // 会清空模块表,重新 Initialize 的 AddModule 会创建全新实例。
            _destroyed = false;
        }

        /// <summary>异步加载(泛型),已加载则直接返回缓存,并发加载自动去重</summary>
        public async UniTask<T> LoadAsync<T>(UILayer layer = UILayer.Normal) where T : BaseUIPanel
        {
            var type = typeof(T);
            if (_loaded.TryGetValue(type, out var panel) && panel != null && panel.gameObject != null)
                return panel as T;

            if (_loading.TryGetValue(type, out var loadingTask))
            {
                var result = await loadingTask;
                return result as T;
            }

            if (!Registry.TryGetAddress<T>(out var address) || string.IsNullOrEmpty(address))
            {
                Debug.LogError($"[UILoaderModule] Panel {type.Name} address not found in UIRegistryModule");
                return null;
            }

            var task = LoadInternalAsync<T>(address, layer);
            _loading[type] = task;
            try
            {
                var result = await task;
                return result as T;
            }
            finally
            {
                _loading.Remove(type);
            }
        }

        private async UniTask<BaseUIPanel> LoadInternalAsync<T>(string address, UILayer layer) where T : BaseUIPanel
        {
            var type = typeof(T);
            var ui = await Addressables.LoadByKeyAsync(address);

            // 加载期间宿主已被销毁:归还刚取得的引用,避免句柄泄漏或把实例写回已清空的缓存
            if (_destroyed)
            {
                _addressables?.Release(address);
                return null;
            }

            if (ui == null)
            {
                Debug.LogError($"[UILoaderModule] Failed to load prefab for {type.Name} from address '{address}'");
                return null;
            }

            var layerRoot = Root != null && Root.LayerRoots != null ? Root.LayerRoots.GetLayerRoot(layer) : null;
            var instance = UnityEngine.Object.Instantiate(ui, layerRoot);
            instance.gameObject.SetActive(false);
            instance.SetRoot(Root);
            instance.Init();
            _loaded[type] = instance;
            _loadedAddresses[type] = address;
            return instance;
        }

        // ===== 面板查询 =====

        public BaseUIPanel GetPanel<T>() where T : BaseUIPanel => GetPanel(typeof(T));

        public BaseUIPanel GetPanel(Type type)
        {
            if (_loaded.TryGetValue(type, out var p) && (p == null || p.gameObject == null))
            {
                _loaded.Remove(type);
                return null;
            }
            return p;
        }

        public bool IsPanelLoaded<T>() where T : BaseUIPanel => IsPanelLoaded(typeof(T));

        public bool IsPanelLoaded(Type type)
        {
            if (!_loaded.TryGetValue(type, out var p)) return false;
            if (p == null || p.gameObject == null)
            {
                _loaded.Remove(type);
                return false;
            }
            return true;
        }

        public void Unload(Type type)
        {
            if (type == null) return;
            if (_loaded.TryGetValue(type, out var panel))
            {
                _loaded.Remove(type);
                if (panel != null && panel.gameObject != null)
                {
                    UnityEngine.Object.Destroy(panel.gameObject);
                }
                // address 取自加载期缓存,不依赖销毁/卸载时的注册表快照;ReferenceCount 只归还一次。
                if (_loadedAddresses.TryGetValue(type, out var address))
                {
                    _loadedAddresses.Remove(type);
                    _addressables?.Release(address);
                }
            }
        }

        public override void Destroy()
        {
            // 幂等:防止重复清理导致同一面板 GameObject 被二次 Destroy、同一 address 被二次 Release。
            if (_destroyed) return;
            _destroyed = true;

            // 快照遍历,销毁回调期间集合变动也不会抛"集合已修改"异常。
            foreach (var kv in new List<KeyValuePair<Type, BaseUIPanel>>(_loaded))
            {
                if (kv.Value != null && kv.Value.gameObject != null)
                {
                    UnityEngine.Object.Destroy(kv.Value.gameObject);
                }
                if (_loadedAddresses.TryGetValue(kv.Key, out var address))
                {
                    // 只归还本模块加载时取得的那一份引用(恰一次);AddressableModule 已先被清理时,
                    // 其内部 _handles 已空,Release 为安全的空操作,不会重复释放底层句柄。
                    _addressables?.Release(address);
                }
            }
            _loaded.Clear();
            _loadedAddresses.Clear();
            _loading.Clear();
        }
    }
}
