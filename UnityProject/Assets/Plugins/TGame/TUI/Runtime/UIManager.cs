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
    /// UI 管理器。持有默认 <see cref="UIRoot"/> 组件,所有面板/浮窗 API 委托给该实例。
    ///
    /// 架构:
    ///   UIManager (sealed, BaseManager)
    ///    ├── AddressableModule     — 共享 Addressable 句柄池(底层加载器,全 UIRoot 共享)
    ///    ├── UIRegistryModule       — 全局面板注册表(Type → address),预热 label 时自动反查填充
    ///    ├── UIRootManagerModule   — 管理 UIRoot(按 Type 键索引)
    ///    └── _uiRoot : UIRoot (ModuleHost)
    ///          ├── UILoaderModule     — Addressable 加载 + per-UIRoot 单实例缓存 + 并发去重
    ///          ├── UILayerRootModule   — 7 层根 Transform
    ///          ├── StackPanelModule    — 栈式导航
    ///          ├── PopupModule          — 浮窗管理
    ///          └── UIVisibilityModule   — Show/Hide + 事件广播
    ///
    /// 面板加载:打开 UI 时 UIRoot 查 UIManager.UIRegistryModule 拿 address → 调 UIManager.AddressableModule 拿 prefab → Instantiate → 缓存。
    /// registry 与 Addressable 均挂在 UIManager 上,资产解析在 UIManager 内闭合,不再需要 per-UIRoot 重复注册。
    /// </summary>
    public sealed class UIManager : BaseManager
    {
        [SerializeField] private UIConfig _config;
        [SerializeField] private UIRoot _uiRoot;

        /// <summary>默认 UIRoot</summary>
        public UIRoot UIRoot => _uiRoot;

        /// <summary>共享 Addressable 句柄池</summary>
        public AddressableModule Addressables => GetModule<AddressableModule>();

        /// <summary>全局面板注册表(Type → address),全 UIRoot 共享</summary>
        public UIRegistryModule Registry => GetModule<UIRegistryModule>();

        private void Awake()
        {
            game = Game.Instance;
            if (game == null)
            {
                Debug.LogError("[UIManager] Game.Instance is null, ensure Game is in scene");
                return;
            }
            game.AddManager(this);
            // 物化全局 registry —— 与 AddressableModule 同 host,资产解析在此闭合
            GetModule<UIRegistryModule>();
            if (_uiRoot != null)
                _uiRoot.Initialize(this, _config);
            else
                Debug.LogError("[UIManager] Default UIRoot is not assigned");
        }

        // ===== UIRootManagerModule 快捷访问 =====

        public UIRoot GetUIRoot<T>() where T : UIRoot
            => GetModule<UIRootManagerModule>().Get<T>();

        public UIRoot GetUIRoot(Type key)
            => GetModule<UIRootManagerModule>().Get(key);

        // ===== 面板注册(委托全局 Registry) =====

        public void RegisterPanelAsync<T>(string address) where T : BaseUIPanel
            => Registry.Register<T>(address);

        // ===== 面板加载 =====

        public UniTask<T> LoadPanelAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => _uiRoot.LoadPanelAsync<T>(ct);

        public UniTask<BaseUIPanel> LoadPanelAsync(Type type, CancellationToken ct = default)
            => _uiRoot.LoadPanelAsync(type, ct);

        // ===== 面板显隐 =====

        public UniTask<T> ShowPanelAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => _uiRoot.ShowPanelAsync<T>(ct);

        public UniTask<BaseUIPanel> ShowPanelAsync(Type type, CancellationToken ct = default)
            => _uiRoot.ShowPanelAsync(type, ct);

        public UniTask<T> ShowPanelStackAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => _uiRoot.ShowPanelStackAsync<T>(ct);

        public void HidePanel<T>() where T : BaseUIPanel
            => _uiRoot.HidePanel<T>();

        public void HidePanel(Type type)
            => _uiRoot.HidePanel(type);

        // ===== 面板卸载 =====

        public void UnloadPanel<T>() where T : BaseUIPanel => _uiRoot.UnloadPanel<T>();
        public void UnloadPanel(Type type) => _uiRoot.UnloadPanel(type);

        // ===== 面板查询 =====

        public T GetPanel<T>() where T : BaseUIPanel => _uiRoot.GetPanel<T>();
        public BaseUIPanel GetPanel(Type type) => _uiRoot.GetPanel(type);
        public bool IsPanelLoaded<T>() where T : BaseUIPanel => _uiRoot.IsPanelLoaded<T>();
        public bool IsPanelLoaded(Type type) => _uiRoot.IsPanelLoaded(type);

        public Transform GetLayerRoot(UILayer layer)
            => _uiRoot.LayerRoots.GetLayerRoot(layer);

        // ===== 批量预热 =====

        /// <summary>
        /// 按 Addressables label 预热 UI 面板:暖热 Addressable 句柄池,
        /// 并从 prefab 反查 BaseUIPanel 具体子类,自动把 (Type, address) 写入全局 Registry。
        /// 预热完成后,业务方 ShowPanelAsync&lt;T&gt; 一步命中句柄池 + 注册表,无需逐面板手动注册。
        /// </summary>
        public UniTask PreloadPanelsAsync(string label, IProgress<float> progress = null, CancellationToken ct = default)
            => Registry.PreloadByLabelAsync(label, Addressables, progress, ct);

        public UniTask PreloadPanelsByLabelsAsync<T>(IEnumerable<string> labels, IProgress<float> progress = null, CancellationToken ct = default) where T : UnityEngine.Object
            => GetModule<AddressableModule>().PreloadByLabelAsync<T>(labels, progress, ct);

        public UniTask PreloadPanelsByKeysAsync<T>(IEnumerable<string> keys, IProgress<float> progress = null, CancellationToken ct = default) where T : UnityEngine.Object
            => GetModule<AddressableModule>().PreloadByKeysAsync<T>(keys, progress, ct);

        // ===== Popup 转发 API =====

        public void RegisterPopup<T>(T prefab) where T : BaseUIPopup
            => _uiRoot.RegisterPopup(prefab);

        public T ShowPopup<T>(Vector2 screenAnchor, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                              bool followMouse = false,
                              Vector2? offset = null)
            where T : BaseUIPopup
            => _uiRoot.ShowPopup(screenAnchor, setup, boundsArea, flip, followMouse, offset);

        public T ShowPopup<T>(Vector2 screenAnchor, Vector2 offset, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                              bool followMouse = false)
            where T : BaseUIPopup
            => _uiRoot.ShowPopup(screenAnchor, offset, setup, boundsArea, flip, followMouse);

        public T ShowPopup<T>(RectTransform target, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                              Vector2? offset = null)
            where T : BaseUIPopup
            => _uiRoot.ShowPopup(target, setup, boundsArea, flip, offset);

        public T ShowPopup<T>(RectTransform target, Vector2 offset, Action<T> setup = null,
                              RectTransform boundsArea = null,
                              PopupFlipDirection flip = PopupFlipDirection.BottomRight)
            where T : BaseUIPopup
            => _uiRoot.ShowPopup(target, offset, setup, boundsArea, flip);

        public void HidePopup<T>() where T : BaseUIPopup => _uiRoot.HidePopup<T>();
        public void HidePopup(Type type) => _uiRoot.HidePopup(type);
        public void HideAllPopups() => _uiRoot.HideAllPopups();
        public bool IsPopupVisible<T>() where T : BaseUIPopup => _uiRoot.IsPopupVisible<T>();
        public bool IsPopupVisible(Type type) => _uiRoot.IsPopupVisible(type);

        // ===== 销毁 =====

        private void OnDestroy()
        {
            // 销毁面板 GameObject + 模块清理由 UIRoot 自身 OnDestroy 处理
            // (UIRoot 也是这个 GameObject 上的组件,Unity 会按顺序触发 OnDestroy)
            ClearModule();
        }
    }
}
