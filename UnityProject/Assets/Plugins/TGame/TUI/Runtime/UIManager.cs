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
    /// UI 管理器。本类是"加载/单实例显隐注册表"的薄壳,所有业务逻辑委托给以下 BaseModule:
    ///   - UIRegistryModule:面板 prefab/address + layer 注册表(PanelConfig with RegisterMode)
    ///   - UILayerRootModule:6 个 layer 根 Transform 字典(Awake 填表)
    ///   - UILoaderModule:Panel 实例化 + 缓存 + RectTransform 填充 + Init + Unload
    ///     (Addressable 模式下走 AddressableModel.LoadAsync,Prefab 模式同步 Instantiate)
    ///   - UIVisibilityModule:Show/Hide + SetAsLastSibling + 事件广播(含 ShowAsync)
    ///   - StackPanelModel:栈式 Panel 管理(Push/Pop/层级守门,含 OpenAsync)
    ///   - AddressableModel(挂载在 UIManager 下):Addressable 模式下的 prefab 句柄池(与 AddressableManager 内的实例独立)
    ///   - PopupModule:Popup 浮窗管理
    ///
    /// 同步 / 异步双轨 API:
    ///   - 同步:RegisterPanel(prefab) [Obsolete]、LoadPanel、ShowPanel、ShowPanelStack、HidePanel、UnloadPanel、GetPanel、IsPanelLoaded
    ///   - 异步:RegisterPanelAsync(address)、LoadPanelAsync、ShowPanelAsync、ShowPanelStackAsync
    ///   - 同步 Show/Hide 作用于 Addressable 模式下面板时,Load 会返回 null(无 prefab 可同步取),调用方必须改用 Async 重载。
    /// </summary>
    public sealed class UIManager : BaseManager
    {
        [SerializeField] private Transform _backgroundRoot;
        [SerializeField] private Transform _sceneRoot;
        [SerializeField] private Transform _normalRoot;
        [SerializeField] private Transform _popupRoot;
        [SerializeField] private Transform _overlayRoot;
        [SerializeField] private Transform _topRoot;
        [SerializeField] private Transform _tooltipRoot;
        [SerializeField] private UIConfig _config;

        internal Vector2 PopupOffset => Vector2.one * (_config != null ? _config.TooltipOffset : 8f);

        /// <summary>
        /// 填表到 UILayerRootModule。SerializeField 字段保留在 UIManager(MonoBehaviour)上以兼容 Prefab 序列化。
        /// 同时读取 UIConfig(非场景配置)并自动注册默认浮窗。
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
            root.SetLayerRoot(UILayer.Tooltip, _tooltipRoot);

            // 读取 UIConfig,自动注册默认浮窗(零调用方样板)
            if (_config != null && _config.DefaultTooltip != null)
            {
                GetModule<PopupModule>().Register(_config.DefaultTooltip);
            }
        }

        private void Start()
        {
            game = Game.Instance;
            game.AddManager(this);
            // 注入自身引用,供 Module 内部 _ui.GetModule<...>() 互相访问
            GetModule<StackPanelModel>().SetUIManager(this);
            GetModule<UILoaderModule>().SetUIManager(this);
            GetModule<UIVisibilityModule>().SetUIManager(this);
            GetModule<PopupModule>().SetUIManager(this);
            // UIRegistryModule / UILayerRootModule 不需要 UIManager 引用,跳过
            // 创建挂在 UIManager 下的 AddressableModel 独立实例,事件广播走 this(BaseManager)
            // 注意:此实例与 AddressableManager 内的 AddressableModel 不共享句柄池(用户已接受)
            GetModule<AddressableModel>().SetManager(this);
        }

        private void Update()
        {
            var popupModule = GetModule<PopupModule>();
            if (popupModule.HasFollowTargets)
                popupModule.Tick(Time.unscaledDeltaTime);
        }

        // ===== 注册(同步,旧 Prefab 路径,Obsolete) =====

        /// <summary>注册面板类型(同步 Prefab 路径)。新代码请改用 <see cref="RegisterPanelAsync{T}"/></summary>
        [Obsolete("Use RegisterPanelAsync<T>(string address, UILayer) for Addressables-backed panels")]
        public void RegisterPanel<T>(T prefab, UILayer layer = UILayer.Normal) where T : BaseUIPanel
            => GetModule<UIRegistryModule>().Register(prefab, layer);

        // ===== 注册(异步,Addressable 路径) =====

        /// <summary>
        /// 异步注册一个面板类型(Addressable 路径,提供 address 字符串)。
        /// 后续请用 <see cref="LoadPanelAsync{T}"/> / <see cref="ShowPanelAsync{T}"/> 加载/显示。
        /// </summary>
        public void RegisterPanelAsync<T>(string address, UILayer layer = UILayer.Normal) where T : BaseUIPanel
            => GetModule<UIRegistryModule>().Register<T>(address, layer);

        // ===== LoadingPanel API =====

        /// <summary>
        /// 注册加载面板预制体(同步 Prefab 路径)。新代码请改用 <see cref="RegisterLoadingPanelAsync{T}"/>。
        /// 默认层级为 <see cref="UILayer.Popup"/>(加载面板应在普通内容之上)。
        /// 同 Type 二次注册会被拒绝并 LogWarning。
        /// </summary>
        [Obsolete("Use RegisterLoadingPanelAsync<T>(string address, UILayer) for Addressables-backed panels")]
        public void RegisterLoadingPanel<T>(T prefab, UILayer layer = UILayer.Popup) where T : LoadingPanel
            => GetModule<UIRegistryModule>().Register(prefab, layer);

        /// <summary>
        /// 注册加载面板(Addressable 路径,提供 address 字符串)。
        /// 默认层级为 <see cref="UILayer.Popup"/>。
        /// </summary>
        public void RegisterLoadingPanelAsync<T>(string address, UILayer layer = UILayer.Popup) where T : LoadingPanel
            => GetModule<UIRegistryModule>().Register<T>(address, layer);

        /// <summary>
        /// 加载并显示加载面板(转发到 UIVisibilityModule)。
        /// 首次调用走 Load+Show,后续调用幂等返回已可见实例。
        /// Addressable 模式下请先 <see cref="LoadPanelAsync{T}"/> 再 Show,或直接 <see cref="ShowLoadingPanelAsync{T}"/>。
        /// </summary>
        public T ShowLoadingPanel<T>() where T : LoadingPanel
            => GetModule<UIVisibilityModule>().Show<T>();

        /// <summary>
        /// 异步加载并显示加载面板(走 AddressableModel.LoadAsync)。
        /// </summary>
        public async UniTask<T> ShowLoadingPanelAsync<T>(CancellationToken ct = default) where T : LoadingPanel
            => await GetModule<UIVisibilityModule>().ShowAsync<T>(ct);

        /// <summary>
        /// 隐藏加载面板(转发到 UIVisibilityModule)。
        /// 未加载或不可见时无副作用。
        /// </summary>
        public void HideLoadingPanel<T>() where T : LoadingPanel
            => GetModule<UIVisibilityModule>().Hide<T>();

        /// <summary>
        /// 更新加载进度。建议传入 [0, 1] 范围内的值。
        /// 面板未加载时 LogWarning 并跳过;已加载时调用 <see cref="LoadingPanel.SetProgress"/>。
        /// </summary>
        public void SetLoadingProgress<T>(float progress) where T : LoadingPanel
        {
            var loader = GetModule<UILoaderModule>();
            if (!loader.IsLoaded<T>())
            {
                Debug.LogWarning($"[UIManager] LoadingPanel {typeof(T).Name} 未加载,无法设置进度");
                return;
            }
            var panel = loader.Get<T>() as LoadingPanel;
            panel?.SetProgress(progress);
        }

        // ===== 加载(同步,仅 Prefab 模式) =====

        /// <summary>同步加载面板(泛型)。Addressable 模式请改用 <see cref="LoadPanelAsync{T}"/></summary>
        public T LoadPanel<T>() where T : BaseUIPanel
            => GetModule<UILoaderModule>().Load<T>();

        /// <summary>同步按 Type 加载面板。Addressable 模式请改用 <see cref="LoadPanelAsync(Type)"/></summary>
        public BaseUIPanel LoadPanel(Type type)
            => GetModule<UILoaderModule>().Load(type);

        // ===== 加载(异步,Addressable 模式走 AddressableModel.LoadAsync) =====

        /// <summary>异步加载面板(泛型),已加载则直接返回缓存</summary>
        public UniTask<T> LoadPanelAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => GetModule<UILoaderModule>().LoadAsync<T>(ct);

        /// <summary>异步按 Type 加载面板,已加载则直接返回缓存</summary>
        public UniTask<BaseUIPanel> LoadPanelAsync(Type type, CancellationToken ct = default)
            => GetModule<UILoaderModule>().LoadAsync(type, ct);

        // ===== 显隐(同步) =====

        /// <summary>同步显示面板(泛型,转发到 UIVisibilityModule)。Addressable 模式请改用 <see cref="ShowPanelAsync{T}"/></summary>
        public T ShowPanel<T>() where T : BaseUIPanel
            => GetModule<UIVisibilityModule>().Show<T>();

        /// <summary>同步按 Type 显示面板。Addressable 模式请改用 <see cref="ShowPanelAsync(Type)"/></summary>
        public BaseUIPanel ShowPanel(Type type)
            => GetModule<UIVisibilityModule>().Show(type);

        /// <summary>同步按顺序打开 Panel(栈式)。Addressable 模式请改用 <see cref="ShowPanelStackAsync{T}"/></summary>
        public T ShowPanelStack<T>() where T : BaseUIPanel
            => GetModule<StackPanelModel>().Open<T>();

        /// <summary>同步隐藏面板(泛型)。广播 PanelClosedEvent</summary>
        public void HidePanel<T>() where T : BaseUIPanel
            => GetModule<UIVisibilityModule>().Hide<T>();

        /// <summary>同步按 Type 隐藏面板。广播 PanelClosedEvent</summary>
        public void HidePanel(Type type)
            => GetModule<UIVisibilityModule>().Hide(type);

        // ===== 显隐(异步) =====

        /// <summary>异步显示面板(泛型)。Addressable 模式专属入口</summary>
        public UniTask<T> ShowPanelAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => GetModule<UIVisibilityModule>().ShowAsync<T>(ct);

        /// <summary>异步按 Type 显示面板</summary>
        public UniTask<BaseUIPanel> ShowPanelAsync(Type type, CancellationToken ct = default)
            => GetModule<UIVisibilityModule>().ShowAsync(type, ct);

        /// <summary>异步按顺序打开 Panel(栈式)</summary>
        public UniTask<T> ShowPanelStackAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => GetModule<StackPanelModel>().OpenAsync<T>(ct);

        // ===== 卸载 =====

        /// <summary>同步卸载面板(泛型)。Addressable 模式下同时 release address handle</summary>
        public void UnloadPanel<T>() where T : BaseUIPanel
            => GetModule<UILoaderModule>().Unload<T>();

        /// <summary>同步按 Type 卸载面板</summary>
        public void UnloadPanel(Type type)
            => GetModule<UILoaderModule>().Unload(type);

        // ===== 查询 =====

        /// <summary>获取已加载的面板(泛型)</summary>
        public T GetPanel<T>() where T : BaseUIPanel
            => GetModule<UILoaderModule>().Get<T>();

        /// <summary>按 Type 获取已加载的面板</summary>
        public BaseUIPanel GetPanel(Type type)
            => GetModule<UILoaderModule>().Get(type);

        /// <summary>是否已加载(泛型)</summary>
        public bool IsPanelLoaded<T>() where T : BaseUIPanel
            => GetModule<UILoaderModule>().IsLoaded<T>();

        /// <summary>按 Type 是否已加载</summary>
        public bool IsPanelLoaded(Type type)
            => GetModule<UILoaderModule>().IsLoaded(type);

        /// <summary>查询 UILayer 根 Transform</summary>
        public Transform GetLayerRoot(UILayer layer)
            => GetModule<UILayerRootModule>().GetLayerRoot(layer);

        // ===== 批量预热(转发到 UIManager 内的 AddressableModel 独立实例) =====

        /// <summary>按 Addressables label 批量预热 UI 面板 prefab(泛型,指定具体类型)</summary>
        public UniTask PreloadPanelsByLabelAsync<T>(string label, IProgress<float> progress = null, CancellationToken ct = default) where T : UnityEngine.Object
            => GetModule<AddressableModel>().PreloadByLabelAsync<T>(label, progress, ct);

        /// <summary>按一组 Addressables address 批量预热 UI 面板 prefab(泛型,指定具体类型)</summary>
        public UniTask PreloadPanelsByKeysAsync<T>(IEnumerable<string> keys, IProgress<float> progress = null, CancellationToken ct = default) where T : UnityEngine.Object
            => GetModule<AddressableModel>().PreloadByKeysAsync<T>(keys, progress, ct);

        // ===== Popup 转发 API(零迁移) =====

        /// <summary>注册浮窗预制体(转发到 PopupModule),默认挂在 UILayer.Tooltip。</summary>
        public void RegisterPopup<T>(T prefab) where T : BaseUIPopup
            => GetModule<PopupModule>().Register(prefab);

        /// <summary>
        /// 在屏幕坐标 screenAnchor 处打开浮窗(转发到 PopupModule)。
        /// 同 Type 同时只显示一个(单实例);越界时按 preferred 翻转。
        /// followMouse 时由 PopupModule.Tick 每帧拉鼠标坐标重定位。
        /// </summary>
        /// <param name="screenAnchor">屏幕坐标(通常是鼠标位置)</param>
        /// <param name="setup">可选 setup 回调,传入强类型实例,用于 SetData / SetText 等</param>
        /// <param name="boundsArea">边界 RectTransform(null = 整个屏幕)</param>
        /// <param name="flip">首选翻转方向(默认 BottomRight)</param>
        /// <param name="followMouse">true = 每帧重定位到鼠标;false = 固定锚点(默认)</param>
        /// <param name="offset">目标到浮窗的二维偏移。null 时使用 UIConfig / Popup prefab 默认值</param>
        public T ShowPopup<T>(Vector2 screenAnchor, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                              bool followMouse = false,
                              Vector2? offset = null)
            where T : BaseUIPopup
            => GetModule<PopupModule>().Show(screenAnchor, setup, boundsArea, flip, followMouse, offset);

        /// <summary>在屏幕坐标 screenAnchor 处打开浮窗,并指定目标到浮窗的二维偏移。</summary>
        public T ShowPopup<T>(Vector2 screenAnchor, Vector2 offset, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                              bool followMouse = false)
            where T : BaseUIPopup
            => GetModule<PopupModule>().Show(screenAnchor, offset, setup, boundsArea, flip, followMouse);

        /// <summary>
        /// 贴指定 RectTransform 打开浮窗。翻转时会根据方向自动选择目标 Rect 的对应边角。
        /// </summary>
        public T ShowPopup<T>(RectTransform target, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                              Vector2? offset = null)
            where T : BaseUIPopup
            => GetModule<PopupModule>().Show(target, setup, boundsArea, flip, offset);

        /// <summary>贴指定 RectTransform 打开浮窗,并指定目标到浮窗的二维偏移。</summary>
        public T ShowPopup<T>(RectTransform target, Vector2 offset, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight)
            where T : BaseUIPopup
            => GetModule<PopupModule>().Show(target, offset, setup, boundsArea, flip);

        /// <summary>隐藏指定类型浮窗(转发到 PopupModule)。</summary>
        public void HidePopup<T>() where T : BaseUIPopup
            => GetModule<PopupModule>().Hide<T>();

        /// <summary>按 Type 隐藏浮窗(转发到 PopupModule)。</summary>
        public void HidePopup(Type type)
            => GetModule<PopupModule>().Hide(type);

        /// <summary>隐藏所有当前显示的浮窗(转发到 PopupModule)。</summary>
        public void HideAllPopups()
            => GetModule<PopupModule>().HideAll();

        /// <summary>指定类型浮窗当前是否正在显示(转发到 PopupModule)。</summary>
        public bool IsPopupVisible<T>() where T : BaseUIPopup
            => GetModule<PopupModule>().IsVisible<T>();

        /// <summary>按 Type 查询浮窗是否正在显示。</summary>
        public bool IsPopupVisible(Type type)
            => GetModule<PopupModule>().IsVisible(type);

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
