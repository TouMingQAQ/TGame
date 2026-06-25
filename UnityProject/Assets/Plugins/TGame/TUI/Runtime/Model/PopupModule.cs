using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// 浮窗生命周期 Module。挂载在 UIRoot(通过 BaseModuleHost)上。
    /// 职责:Register 预加载 prefab / Show 显示(单实例 + 自动定位 + 翻转) / Hide 关闭 /
    /// Tick 跟随鼠标 / 广播 PopupShownEvent / PopupHiddenEvent。
    ///
    /// 状态:
    ///   - <c>Dictionary&lt;Type, GameObject&gt; _prefabs</c> — 注册表(Type → Prefab GO)
    ///   - <c>Dictionary&lt;Type, BaseUIPopup&gt; _active</c> — 单实例缓存(同 Type 同时只显示一个,隐藏后复用)
    ///   - <c>Dictionary&lt;BaseUIPopup, Action&gt; _subs</c> — Hidden 事件订阅缓存
    ///   - <c>Dictionary&lt;Type, FollowContext&gt; _follow</c> — 跟随鼠标上下文
    ///
    /// 依赖:
    ///   - UIRoot(本 Module 的宿主):通过 host.GetModule&lt;UILayerRootModule&gt;() 读 Tooltip Layer 根 Transform
    ///   - host.Call:广播 PopupShownEvent / PopupHiddenEvent
    ///
    /// 调用链:
    ///   业务方 ShowPopup&lt;T&gt;(pos, setup, followMouse) → UIRoot.ShowPopup(转发) → 本模块 Show →
    ///     单实例命中复用 / 未命中 Instantiate 到 Tooltip Root → Anchor → SetAsLastSibling + Show + host.Call;
    ///     followMouse 时由 BaseModuleHost.Update 触发本模块 Tick 重定位
    ///
    /// 关键不变量:
    ///   - 同 Type 同时只显示一个(单实例语义)
    ///   - Anchor 必须在 Show() 之前(否则动画第一帧位置跳变)
    ///   - 坐标解算统一在 popup 根本地坐标中完成,兼容 CanvasScaler / Camera Canvas
    ///   - Hidden 事件只广播隐藏事件;实例保留在 _active 中供下次 Show 复用
    ///   - Hide 时同步清 _follow
    ///   - GO 销毁由 UIRoot.OnDestroy 统一处理,Module.Destroy 只清订阅
    ///   - 走 Tooltip Layer(始终最顶,在 Modal 之上)
    /// </summary>
    public sealed class PopupModule : BaseModule
    {
        private struct FollowContext
        {
            public Type Type;
            public RectTransform PopupRoot;
            public RectTransform BoundsArea;
            public PopupFlipDirection Flip;
            public Vector2 Offset;
        }

        private const float DEFAULT_OFFSET = 8f;

        private readonly Dictionary<Type, BaseUIPopup> _prefabs = new();
        private readonly Dictionary<Type, BaseUIPopup> _active = new();
        private readonly Dictionary<Type, FollowContext> _follow = new();
        private readonly Dictionary<BaseUIPopup, Action<BaseUIPanel>> _subs = new();

        private float _defaultOffset = DEFAULT_OFFSET;

        /// <summary>由 UIRoot.Init 注入浮窗全局参数(从 UIConfig)</summary>
        public void SetDefaultOffset(float offset) => _defaultOffset = offset;

        /// <summary>当前活跃浮窗(只读快照,调试用)</summary>
        public IReadOnlyDictionary<Type, BaseUIPopup> Active => _active;

        // ===== 注册 =====

        public void Register<T>(T prefab) where T : BaseUIPopup
        {
            if (prefab == null)
            {
                Debug.LogError("[PopupModule] prefab is null");
                return;
            }
            var t = typeof(T);
            if (_prefabs.ContainsKey(t))
            {
                Debug.LogWarning($"[PopupModule] {t.Name} already registered, skipping");
                return;
            }
            _prefabs[t] = prefab;
        }

        // ===== Show(屏幕坐标) =====

        public T Show<T>(Vector2 screenAnchor, Action<T> setup = null,
                         RectTransform boundsArea = null,
                         PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                         bool followMouse = false,
                         Vector2? offset = null)
            where T : BaseUIPopup
            => Show(typeof(T), screenAnchor, p => setup?.Invoke((T)p), boundsArea, flip, followMouse, offset) as T;

        public T Show<T>(Vector2 screenAnchor, Vector2 offset, Action<T> setup = null,
                         RectTransform boundsArea = null,
                         PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                         bool followMouse = false)
            where T : BaseUIPopup
            => Show(typeof(T), screenAnchor, p => setup?.Invoke((T)p), boundsArea, flip, followMouse, offset) as T;

        // ===== Show(贴 RectTransform) =====

        public T Show<T>(RectTransform target, Action<T> setup = null,
                         RectTransform boundsArea = null,
                         PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                         Vector2? offset = null)
            where T : BaseUIPopup
            => ShowTarget(typeof(T), target, p => setup?.Invoke((T)p), boundsArea, flip, offset) as T;

        public T Show<T>(RectTransform target, Vector2 offset, Action<T> setup = null,
                         RectTransform boundsArea = null,
                         PopupFlipDirection flip = PopupFlipDirection.BottomRight)
            where T : BaseUIPopup
            => ShowTarget(typeof(T), target, p => setup?.Invoke((T)p), boundsArea, flip, offset) as T;

        // ===== Hide =====

        public void Hide<T>() where T : BaseUIPopup => Hide(typeof(T));

        public void Hide(Type type)
        {
            if (!_active.TryGetValue(type, out var popup) || popup == null) return;
            _follow.Remove(type);
            if (!popup.IsVisible) return;
            popup.Hide();
        }

        public void HideAll()
        {
            _follow.Clear();
            var keys = new List<Type>(_active.Keys);
            foreach (var t in keys) Hide(t);
        }

        // ===== 查询 =====

        public bool IsVisible<T>() where T : BaseUIPopup => IsVisible(typeof(T));

        public bool IsVisible(Type type)
            => _active.TryGetValue(type, out var p) && p != null && p.IsVisible;

        // ===== Tick(跟随鼠标) =====

        public override void Tick(float deltaTime)
        {
            if (_follow.Count == 0) return;
            Vector2 mouse = GetMousePos();
            List<Type> deadTypes = null;
            foreach (var kv in _follow)
            {
                var ctx = kv.Value;
                if (_active.TryGetValue(ctx.Type, out var popup) && popup != null && popup.IsVisible)
                {
                    popup.SetPosition(mouse, ctx.PopupRoot, ctx.BoundsArea, ctx.Flip, ctx.Offset);
                }
                else
                {
                    deadTypes ??= new List<Type>();
                    deadTypes.Add(ctx.Type);
                }
            }
            if (deadTypes == null) return;
            foreach (var type in deadTypes)
                _follow.Remove(type);
        }

        // ===== 内部 =====

        private BaseUIPopup Show(Type type, Vector2 screenAnchor, Action<BaseUIPopup> setup,
                                 RectTransform boundsArea, PopupFlipDirection flip, bool followMouse, Vector2? offset)
        {
            if (!_prefabs.TryGetValue(type, out var prefab))
            {
                Debug.LogError($"[PopupModule] Popup {type.Name} not registered; call RegisterPopup first");
                return null;
            }

            var popup = EnsureInstance(type, prefab);
            if (popup == null) return null;

            var popupRoot = popup.transform as RectTransform;
            SetupPopupRoot(popupRoot);
            Vector2 resolvedOffset = ResolveOffset(popup, offset);
            bool shouldPlayShow = !popup.IsVisible || popup.IsHiding;

            setup?.Invoke(popup);
            popup.Anchor(screenAnchor, popupRoot, boundsArea, flip, resolvedOffset);
            popup.transform.SetAsLastSibling();
            popup.ApplyPopupRaycastPolicy();

            if (shouldPlayShow) popup.Show();
            if (shouldPlayShow) BroadcastShown(type);

            if (followMouse)
            {
                _follow[type] = new FollowContext
                {
                    Type = type, PopupRoot = popupRoot,
                    BoundsArea = boundsArea, Flip = flip, Offset = resolvedOffset
                };
            }
            else
            {
                _follow.Remove(type);
            }
            return popup;
        }

        private BaseUIPopup ShowTarget(Type type, RectTransform target, Action<BaseUIPopup> setup,
                                       RectTransform boundsArea, PopupFlipDirection flip, Vector2? offset)
        {
            if (target == null)
            {
                Debug.LogError("[PopupModule] target RectTransform is null");
                return null;
            }
            if (!_prefabs.TryGetValue(type, out var prefab))
            {
                Debug.LogError($"[PopupModule] Popup {type.Name} not registered; call RegisterPopup first");
                return null;
            }

            var popup = EnsureInstance(type, prefab);
            if (popup == null) return null;

            var popupRoot = popup.transform as RectTransform;
            SetupPopupRoot(popupRoot);
            Vector2 resolvedOffset = ResolveOffset(popup, offset);
            bool shouldPlayShow = !popup.IsVisible || popup.IsHiding;

            setup?.Invoke(popup);
            popup.Anchor(target, popupRoot, boundsArea, flip, resolvedOffset);
            popup.transform.SetAsLastSibling();
            popup.ApplyPopupRaycastPolicy();

            if (shouldPlayShow)
            {
                popup.Show();
                BroadcastShown(type);
            }

            _follow.Remove(type);
            return popup;
        }

        private BaseUIPopup EnsureInstance(Type type, BaseUIPopup prefab)
        {
            if (_active.TryGetValue(type, out var popup) && popup != null && popup.gameObject != null)
                return popup;

            if (_active.ContainsKey(type)) _active.Remove(type);

            // 宿主是 UIRoot(继承 BaseModuleHost)
            if (Host == null)
            {
                Debug.LogError("[PopupModule] Manager is not a BaseModuleHost; ensure Module is mounted on a host component");
                return null;
            }
            var layerRoots = Host.GetModule<UILayerRootModule>();
            if (layerRoots == null)
            {
                Debug.LogError("[PopupModule] UILayerRootModule not found on host");
                return null;
            }
            var root = layerRoots.GetLayerRoot(UILayer.Tooltip);
            if (root == null)
            {
                Debug.LogError("[PopupModule] Tooltip layer root missing; assign _tooltipRoot on UIRoot");
                return null;
            }

            popup = UnityEngine.Object.Instantiate<BaseUIPopup>(prefab, root);
            if (popup == null)
            {
                Debug.LogError($"[PopupModule] prefab for {type.Name} missing {type.Name} component");
                UnityEngine.Object.Destroy(popup.gameObject);
                return null;
            }

            var rootRt = popup.transform as RectTransform;
            SetupPopupRoot(rootRt);
            popup.ApplyPopupRaycastPolicy();

            Action<BaseUIPanel> onHidden = p => OnPopupHidden((BaseUIPopup)p);
            popup.Hidden += onHidden;
            _subs[popup] = onHidden;

            popup.gameObject.SetActive(false);
            _active[type] = popup;
            return popup;
        }

        private void OnPopupHidden(BaseUIPopup popup)
        {
            if (popup == null) return;
            var type = popup.GetType();
            _follow.Remove(type);

            Host.GetModule<EventModule>().Call(new PopupHiddenEvent(type.Name));
        }

        private void BroadcastShown(Type type)
        {
            Host.GetModule<EventModule>().Call(new PopupShownEvent(type.Name));
        }

        private static void SetupPopupRoot(RectTransform rootRt)
        {
            if (rootRt == null) return;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.pivot = Vector2.zero;
            rootRt.localScale = Vector3.one;
        }

        private Vector2 ResolveOffset(BaseUIPopup popup, Vector2? offsetOverride)
        {
            if (offsetOverride.HasValue) return offsetOverride.Value;
            return popup != null ? popup.DefaultTargetOffset : Vector2.one * _defaultOffset;
        }

        private static Vector2 GetMousePos()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Pointer.current?.position.ReadValue() ?? Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        public override void Destroy()
        {
            foreach (var kv in _subs)
            {
                if (kv.Key != null) kv.Key.Hidden -= kv.Value;
            }
            _subs.Clear();
            _active.Clear();
            _prefabs.Clear();
            _follow.Clear();
        }
    }
}
