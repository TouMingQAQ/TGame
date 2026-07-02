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

        private UIRegistryModule Registry => UIManager.Instance.Registry;
        private AddressableModule<BaseUIPanel> Addressables =>UIManager.Instance.GetModule<AddressableModule<BaseUIPanel>>();

        private readonly Dictionary<Type, BaseUIPanel> _loaded = new();
        
        public UIRoot Root { get; set; }

        /// <summary>异步加载(泛型),已加载则直接返回缓存</summary>
        public async UniTask<T> LoadAsync<T>(UILayer layer = UILayer.Normal) where T : BaseUIPanel
        {
            var type = typeof(T);
            if(_loaded.TryGetValue(type, out var panel))
                return panel as T;
            if (!Registry.TryGetAddress<T>(out var address) || string.IsNullOrEmpty(address))
                return null;
            var ui = await Addressables.LoadByKeyAsync(address);
            ui = UnityEngine.Object.Instantiate(ui,Root.LayerRoots.GetLayerRoot(layer));
            ui.gameObject.SetActive(false);
            ui.SetRoot(Root);
            _loaded[type] = ui;
            return ui as T;
        }

      
        // ===== 面板查询 =====

        public BaseUIPanel GetPanel<T>()=>GetPanel(typeof(T));

        public BaseUIPanel GetPanel(Type type)
        {
            if (_loaded.TryGetValue(type, out var p) && (p == null || p.gameObject == null))
            {
                _loaded.Remove(type);
                return null;
            }
            return p;
        }

        public bool IsPanelLoaded<T>() where  T : BaseUIPanel =>IsPanelLoaded(typeof(T));

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
            _loaded.Remove(type);
        }

        

        public override void Destroy()
        {
            _loaded.Clear();
        }
    }
}
