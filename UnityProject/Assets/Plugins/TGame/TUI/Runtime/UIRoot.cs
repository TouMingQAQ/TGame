using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TGame.TUI
{
    /// <summary>
    /// UI 根节点组件。实现 IModuleHost,持有自身模块挂载能力。
    /// 持有 7 层根 Transform(Background ~ Tooltip),是面板、浮窗和栈式导航的容器。
    ///
    /// 每个 UIRoot 内部拥有(per-UIRoot,通过 ModuleEntity 挂载,每个 UIRoot 拥有独立 Module 实例):
    ///   - UILayerRootModule:层级根 Transform 字典
    ///   - UILoaderModule:Addressable 加载 + 单实例缓存 + 并发去重
    ///   - StackPanelModule:栈式导航(单栈)
    ///   - PopupModule:浮窗管理(注册表 + 跟随鼠标 Tick)
    ///
    /// 共享(通过 Owner(UIManager) 跨 host 访问):
    ///   - UIVisibilityModule:Show/Hide + 事件广播(per-UIRoot)
    ///   - UIRegistryModule:Type → address 全局注册表
    ///   - AddressableModule:Addressable 句柄池
    ///
    /// 面板加载:由 UILoaderModule 独立完成 ——
    ///   - 查 Owner.UIRegistryModule 拿 address
    ///   - 调 Owner.AddressableModule.LoadAsync&lt;GameObject&gt;(address, ct) 拿 prefab
    ///   - Instantiate → 挂 layer root → SetRoot → Init → 缓存到模块内 _loaded
    ///   - Unload 时 Destroy + AddressableModule.Release
    ///
    /// UIManager 持有一个默认 UIRoot,其面板 API 全部委托给该实例。
    /// 业务方可自定义 UIRoot 子类,通过 UIManager 注册,实现多 UI 上下文隔离。
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public abstract class UIRoot : MonoBehaviour
    {
        #region SerializeField

        [Header("Layer Roots")]
        [SerializeField] private Transform _backgroundRoot;
        [SerializeField] private Transform _sceneRoot;
        [SerializeField] private Transform _normalRoot;
        [SerializeField] private Transform _popupRoot;
        [SerializeField] private Transform _overlayRoot;
        [SerializeField] private Transform _topRoot;
        [SerializeField] private Transform _tooltipRoot;

        [SerializeField] private ModuleEntity _moduleEntity;
        
        [SerializeField] private List<AssetReference> _preloadUI = new List<AssetReference>();
        #endregion

        // ===== per-UIRoot 模块快捷访问 =====

        /// <summary>全局面板注册表(挂在 Owner 上,全 UIRoot 共享)</summary>
        internal UIRegistryModule Registry => Game.Instance.GetManager<UIManager>().GetModule<UIRegistryModule>();

        /// <summary>per-UIRoot:层级根字典</summary>
        internal UILayerRootModule LayerRoots => _moduleEntity.GetModule<UILayerRootModule>();

        /// <summary>per-UIRoot:Addressable 加载 + 单实例缓存</summary>
        internal UILoaderModule Loader => _moduleEntity.GetModule<UILoaderModule>();

        /// <summary>per-UIRoot:共享 Show/Hide</summary>
        internal UIVisibilityModule Visibility => _moduleEntity.GetModule<UIVisibilityModule>();

        /// <summary>per-UIRoot:浮窗管理</summary>
        internal PopupModule Popup => _moduleEntity.GetModule<PopupModule>();

        /// <summary>per-UIRoot:栈式导航</summary>
        internal StackPanelModule Stack => _moduleEntity.GetModule<StackPanelModule>();

        
        protected async UniTask Initialize()
        {
            // per-UIRoot 模块挂载
            _moduleEntity.AddModule<UILayerRootModule>();
            var loader = _moduleEntity.AddModule<UILoaderModule>();
            loader.Root = this;
            _moduleEntity.AddModule<StackPanelModule>();
            var popup = _moduleEntity.AddModule<PopupModule>();
            _moduleEntity.AddModule<UIVisibilityModule>();

            var layerRoots = _moduleEntity.GetModule<UILayerRootModule>();
            layerRoots.SetLayerRoot(UILayer.Background, _backgroundRoot);
            layerRoots.SetLayerRoot(UILayer.Scene, _sceneRoot);
            layerRoots.SetLayerRoot(UILayer.Normal, _normalRoot);
            layerRoots.SetLayerRoot(UILayer.Popup, _popupRoot);
            layerRoots.SetLayerRoot(UILayer.Overlay, _overlayRoot);
            layerRoots.SetLayerRoot(UILayer.Top, _topRoot);
            layerRoots.SetLayerRoot(UILayer.Tooltip, _tooltipRoot);

            foreach (var assetReference in _preloadUI)
            {
                var handle =  await UIManager.Instance.Addressables.PreLoadAsync(assetReference);
                var prefab = handle.Prefab;
                var type = prefab.GetType();
                var address = handle.Key;
                Registry.Register(type, address);
            }


        }
        protected virtual void Awake()
        {
        }
        protected virtual async void Start()
        {
            await Initialize();
        }
        protected virtual void OnEnable()
        {
            Game.Instance.GetManager<UIManager>().GetModule<UIRootManagerModule>().Register(RootType(), this);
        }
        
        protected virtual void OnDisable()
        {
            Game.Instance.GetManager<UIManager>().GetModule<UIRootManagerModule>().Unregister(RootType(), this);
        }

        protected virtual void OnDestroy()
        {
            _moduleEntity.ClearModule();
        }
        
        

        // ===== 面板注册(转交全局 UIManager.Registry) =====

        public void RegisterPanelAsync<T>(string address) where T : BaseUIPanel
            => Registry.Register<T>(address);

        // ===== 面板加载(委托 UILoaderModule) =====

        /// <summary>异步加载(泛型),已加载则直接返回缓存</summary>
        public UniTask<T> LoadPanelAsync<T>(UILayer layer = UILayer.Normal) where T : BaseUIPanel
            => Loader.LoadAsync<T>(layer);
        

        // ===== 面板查询 =====

        public T GetPanel<T>() where T : BaseUIPanel => GetPanel(typeof(T)) as T;

        public BaseUIPanel GetPanel(Type type) => Loader.GetPanel(type);

        public bool IsPanelLoaded<T>() where T : BaseUIPanel => IsPanelLoaded(typeof(T));

        public bool IsPanelLoaded(Type type) => Loader.IsPanelLoaded(type);

        // ===== 面板卸载 =====

        public void UnloadPanel<T>() where T : BaseUIPanel => UnloadPanel(typeof(T));

        public void UnloadPanel(Type type) => Loader.Unload(type);

        // ===== 面板显隐 =====

        public UniTask<T> ShowPanelAsync<T>(UILayer layer = UILayer.Normal) where T : BaseUIPanel
            => Visibility.ShowAsync<T>(this,layer);

        public void HidePanel<T>() where T : BaseUIPanel => HidePanel(typeof(T));

        public void HidePanel(Type type)
        {
            var panel = GetPanel(type);
            Visibility.Hide(panel);
        }

        #region Popup

        public void RegisterPopup<T>(T prefab) where T : BaseUIPopup
            => Popup.Register(prefab);

        public T ShowPopup<T>(Vector2 screenAnchor, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                              bool followMouse = false,
                              Vector2? offset = null)
            where T : BaseUIPopup
            => Popup.Show(screenAnchor, setup, boundsArea, flip, followMouse, offset);

        public T ShowPopup<T>(Vector2 screenAnchor, Vector2 offset, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                              bool followMouse = false)
            where T : BaseUIPopup
            => Popup.Show(screenAnchor, offset, setup, boundsArea, flip, followMouse);

        public T ShowPopup<T>(RectTransform target, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                              Vector2? offset = null)
            where T : BaseUIPopup
            => Popup.Show(target, setup, boundsArea, flip, offset);

        public T ShowPopup<T>(RectTransform target, Vector2 offset, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight)
            where T : BaseUIPopup
            => Popup.Show(target, offset, setup, boundsArea, flip);

        public void HidePopup<T>() where T : BaseUIPopup => Popup.Hide<T>();
        public void HidePopup(Type type) => Popup.Hide(type);
        public void HideAllPopups() => Popup.HideAll();
        public bool IsPopupVisible<T>() where T : BaseUIPopup => Popup.IsVisible<T>();
        public bool IsPopupVisible(Type type) => Popup.IsVisible(type);

        #endregion

        #region Stack

        public int StackDepth => Stack.StackDepth;
        public UniTask<T> ShowPanelStackAsync<T>() where T : BaseUIPanel
            => Stack.OpenAsync<T>();
        public Type GetStackTop() => Stack.GetStackTop();
        public bool IsStackTop<T>() where T : BaseUIPanel => Stack.IsStackTop<T>();
        public bool IsInStack<T>() where T : BaseUIPanel => Stack.IsInStack<T>();
        public bool BackTo<T>() where T : BaseUIPanel => Stack.BackTo<T>();
        public void PopToRoot() => Stack.PopToRoot();
        public void StackBack() => Stack.Back();
        public void ClearStackAndHide() => Stack.ClearStackAndHide();
        public void ClearStack() => Stack.ClearStack();

        #endregion
        
        public abstract Type RootType();
    }
}
