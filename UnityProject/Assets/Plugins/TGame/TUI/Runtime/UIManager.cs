using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UI 管理器。本类是"加载/单实例显隐注册表"的薄壳,所有业务逻辑委托给以下 BaseModule:
    ///   - UIRegistryModule:面板 prefab + layer 注册表
    ///   - UILayerRootModule:6 个 layer 根 Transform 字典(Awake 填表)
    ///   - UILoaderModule:Panel 实例化 + 缓存 + RectTransform 填充 + Init + Unload
    ///   - UIVisibilityModule:Show/Hide + SetAsLastSibling + 事件广播
    ///   - StackPanelModel:栈式 Panel 管理(Push/Pop/层级守门)
    ///
    /// 设计:旧 API(RegisterPanel/LoadPanel/ShowPanel/HidePanel/UnloadPanel/GetPanel/IsPanelLoaded/GetLayerRoot)
    ///      全部保留签名并转发到对应 Module,业务调用方零迁移。
    /// </summary>
    public sealed class UIManager : BaseManager
    {
        [SerializeField] private Transform _backgroundRoot;
        [SerializeField] private Transform _sceneRoot;
        [SerializeField] private Transform _normalRoot;
        [SerializeField] private Transform _popupRoot;
        [SerializeField] private Transform _overlayRoot;
        [SerializeField] private Transform _topRoot;

        /// <summary>
        /// 填表到 UILayerRootModule。SerializeField 字段保留在 UIManager(MonoBehaviour)上以兼容 Prefab 序列化。
        /// </summary>
        private void Awake()
        {
            var root = GetModule<UILayerRootModule>();
            root.SetLayerRoot(UILayer.Background, _backgroundRoot);
            root.SetLayerRoot(UILayer.Scene, _sceneRoot);
            root.SetLayerRoot(UILayer.Normal, _normalRoot);
            root.SetLayerRoot(UILayer.Popup, _popupRoot);
            root.SetLayerRoot(UILayer.Overlay, _overlayRoot);
            root.SetLayerRoot(UILayer.Top, _topRoot);
        }

        private void Start()
        {
            game = Game.Instance;
            game.AddManager(this);
            // 注入自身引用,供 Module 内部 _ui.GetModule<...>() 互相访问
            GetModule<StackPanelModel>().SetUIManager(this);
            GetModule<UILoaderModule>().SetUIManager(this);
            GetModule<UIVisibilityModule>().SetUIManager(this);
            // UIRegistryModule / UILayerRootModule 不需要 UIManager 引用,跳过
        }

        // ===== 转发 API(零迁移) =====

        /// <summary>注册面板类型(转发到 UIRegistryModule)</summary>
        public void RegisterPanel<T>(T prefab, UILayer layer = UILayer.Normal) where T : BaseUIPanel
            => GetModule<UIRegistryModule>().Register(prefab, layer);

        /// <summary>加载面板(泛型,转发到 UILoaderModule)</summary>
        public T LoadPanel<T>() where T : BaseUIPanel
            => GetModule<UILoaderModule>().Load<T>();

        /// <summary>按 Type 加载面板(转发到 UILoaderModule)</summary>
        public BaseUIPanel LoadPanel(Type type)
            => GetModule<UILoaderModule>().Load(type);

        /// <summary>显示面板(泛型,转发到 UIVisibilityModule)。SetAsLastSibling + 广播 PanelOpenedEvent</summary>
        public T ShowPanel<T>() where T : BaseUIPanel
            => GetModule<UIVisibilityModule>().Show<T>();

        /// <summary>按 Type 显示面板(转发到 UIVisibilityModule)</summary>
        public BaseUIPanel ShowPanel(Type type)
            => GetModule<UIVisibilityModule>().Show(type);

        /// <summary>隐藏面板(泛型,转发到 UIVisibilityModule)。广播 PanelClosedEvent</summary>
        public void HidePanel<T>() where T : BaseUIPanel
            => GetModule<UIVisibilityModule>().Hide<T>();

        /// <summary>按 Type 隐藏面板(转发到 UIVisibilityModule)</summary>
        public void HidePanel(Type type)
            => GetModule<UIVisibilityModule>().Hide(type);

        /// <summary>卸载面板(泛型,转发到 UILoaderModule)</summary>
        public void UnloadPanel<T>() where T : BaseUIPanel
            => GetModule<UILoaderModule>().Unload<T>();

        /// <summary>按 Type 卸载面板(转发到 UILoaderModule)</summary>
        public void UnloadPanel(Type type)
            => GetModule<UILoaderModule>().Unload(type);

        /// <summary>获取已加载的面板(泛型,转发到 UILoaderModule)</summary>
        public T GetPanel<T>() where T : BaseUIPanel
            => GetModule<UILoaderModule>().Get<T>();

        /// <summary>按 Type 获取已加载的面板(转发到 UILoaderModule)</summary>
        public BaseUIPanel GetPanel(Type type)
            => GetModule<UILoaderModule>().Get(type);

        /// <summary>是否已加载(泛型,转发到 UILoaderModule)</summary>
        public bool IsPanelLoaded<T>() where T : BaseUIPanel
            => GetModule<UILoaderModule>().IsLoaded<T>();

        /// <summary>按 Type 是否已加载(转发到 UILoaderModule)</summary>
        public bool IsPanelLoaded(Type type)
            => GetModule<UILoaderModule>().IsLoaded(type);

        /// <summary>查询 UILayer 根 Transform(转发到 UILayerRootModule)</summary>
        public Transform GetLayerRoot(UILayer layer)
            => GetModule<UILayerRootModule>().GetLayerRoot(layer);

        /// <summary>
        /// 销毁所有已加载面板的 GameObject + ClearModule()(逐个调 Module.Destroy())。
        /// Loader.Destroy 只清字典(不 Destroy GO),GO 销毁统一在 OnDestroy 完成。
        /// </summary>
        private void OnDestroy()
        {
            var loader = GetModule<UILoaderModule>();
            foreach (var panel in loader.GetAllLoaded())
            {
                if (panel != null) Destroy(panel.gameObject);
            }
            ClearModule();
        }
    }
}
