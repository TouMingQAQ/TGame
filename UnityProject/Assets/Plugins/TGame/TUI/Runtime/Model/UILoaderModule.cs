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
    ///   - Unload 时先 Destroy GameObject,再调 AddressableModule.Release 归还引用计数
    ///   - GameObject 销毁由 UIRoot.OnDestroy 统一触发本模块 DestroyAll(不调 Release,避免重复归还)
    /// </summary>
    public sealed class UILoaderModule : BaseModule
    {
        private UIRoot _root;

        /// <summary>由 UIRoot.Awake 调用,注入宿主以便跨 host 访问 Owner(UIManager) 上的 registry / Addressable 句柄池</summary>
        internal void Bind(UIRoot root) => _root = root;

        private UIRegistryModule Registry => _root != null ? _root.Registry : null;
        private AddressableModule Addressables => _root != null ? _root.Owner.GetModule<AddressableModule>() : null;

        private readonly Dictionary<Type, BaseUIPanel> _loaded = new();
        // Type → 加载时使用的 address,Unload 时调 AddressableModule.Release
        private readonly Dictionary<Type, string> _loadedAddresses = new();
        // 异步加载去重:Type → 进行中的加载任务
        private readonly Dictionary<Type, UniTask<BaseUIPanel>> _loading = new();

        // ===== 面板加载(异步,统一走 AddressableModule) =====

        /// <summary>异步加载(泛型),已加载则直接返回缓存</summary>
        public UniTask<T> LoadAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => LoadAsync(typeof(T), ct).ContinueWith(p => (T)p);

        /// <summary>
        /// 异步按 Type 加载。已加载则直接返回缓存。
        /// 未注册 → LogError + return null;Addressable 加载失败 → LogError + return null。
        /// 缓存命中时若 GameObject 已被外部 Destroy(Unity 假 null),清理死引用并重新加载。
        /// <para>并发同 Type 的调用共享同一次底层加载,只 Instantiate 一个 Panel。</para>
        /// </summary>
        public UniTask<BaseUIPanel> LoadAsync(Type type, CancellationToken ct = default)
        {
            if (_loaded.TryGetValue(type, out var existing))
            {
                if (existing != null && existing.gameObject != null)
                    return UniTask.FromResult(existing);
                _loaded.Remove(type);
            }

            if (_loading.TryGetValue(type, out var inflight))
                return inflight;

            var task = LoadCoreAsync(type, ct).Preserve();
            _loading[type] = task;
            return task;
        }

        private async UniTask<BaseUIPanel> LoadCoreAsync(Type type, CancellationToken ct)
        {
            try
            {
                // 双重检查
                if (_loaded.TryGetValue(type, out var cached))
                {
                    if (cached != null && cached.gameObject != null)
                        return cached;
                    _loaded.Remove(type);
                }

                var registry = Registry;
                if (registry == null)
                {
                    Debug.LogError("[UILoaderModule] UIRegistryModule not found (UIRoot not bound or UIManager missing registry)");
                    return null;
                }
                if (!registry.TryGetAddress(type, out var address))
                {
                    Debug.LogError($"[UILoaderModule] Panel {type.Name} not registered; call UIManager.RegisterPanelAsync<{type.Name}>(\"address\") or PreloadPanelsAsync(\"label\") first");
                    return null;
                }

                var addrModule = Addressables;
                if (addrModule == null)
                {
                    Debug.LogError("[UILoaderModule] AddressableModule not found on UIManager");
                    return null;
                }

                var prefab = await addrModule.LoadAsync<GameObject>(address, ct);
                if (prefab == null)
                {
                    Debug.LogError($"[UILoaderModule] Addressables load returned null for {type.Name} (address={address})");
                    return null;
                }

                var go = UnityEngine.Object.Instantiate(prefab);
                if (go.GetComponent(type) is not BaseUIPanel panel)
                {
                    Debug.LogError($"[UILoaderModule] Prefab for {type.Name} (address={address}) missing component {type.Name}");
                    UnityEngine.Object.Destroy(go);
                    return null;
                }

                var layerRoots = Host.GetModule<UILayerRootModule>();
                if (layerRoots == null)
                {
                    Debug.LogError("[UILoaderModule] UILayerRootModule not found on host");
                    UnityEngine.Object.Destroy(go);
                    return null;
                }
                var layerRoot = layerRoots.GetLayerRoot(panel.Layer);
                if (layerRoot == null)
                {
                    Debug.LogError($"[UILoaderModule] No layer root for {panel.Layer}; assign on UIRoot");
                    UnityEngine.Object.Destroy(go);
                    return null;
                }
                go.transform.SetParent(layerRoot, worldPositionStays: false);

                var rt = go.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
                panel.SetRoot(_root);
                panel.Init();
                go.SetActive(false);
                _loaded[type] = panel;
                _loadedAddresses[type] = address;
                return panel;
            }
            finally
            {
                _loading.Remove(type);
            }
        }

        // ===== 面板查询 =====

        public BaseUIPanel GetPanel(Type type)
        {
            if (_loaded.TryGetValue(type, out var p) && (p == null || p.gameObject == null))
            {
                _loaded.Remove(type);
                return null;
            }
            return p;
        }

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

        // ===== 面板卸载 =====

        public void Unload(Type type)
        {
            if (!_loaded.TryGetValue(type, out var panel)) return;
            UnityEngine.Object.Destroy(panel.gameObject);
            _loaded.Remove(type);

            if (_loadedAddresses.Remove(type, out var addr))
            {
                var addrModule = Addressables;
                if (addrModule != null)
                {
                    try { addrModule.Release<GameObject>(addr); }
                    catch (Exception e) { Debug.LogWarning($"[UILoaderModule] Release address {addr} failed: {e.Message}"); }
                }
            }
        }

        // ===== 生命周期 =====

        /// <summary>
        /// 销毁所有已加载面板 GameObject(由 UIRoot.OnDestroy 调用)。
        /// 不归还 Addressable 引用计数 —— UIRoot 销毁时 AddressableManager 通常同步销毁,
        /// 其 AddressableModule.Destroy 会统一 ReleaseAll。
        /// </summary>
        public void DestroyAll()
        {
            foreach (var panel in _loaded.Values)
            {
                if (panel != null) UnityEngine.Object.Destroy(panel.gameObject);
            }
            _loaded.Clear();
            _loadedAddresses.Clear();
            _loading.Clear();
        }
    }
}
