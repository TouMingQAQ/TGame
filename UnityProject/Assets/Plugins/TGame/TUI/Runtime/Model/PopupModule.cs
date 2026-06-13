using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// 浮窗生命周期 Module。
    /// 职责:Register 预加载 prefab / Show 显示(单实例 + 自动定位 + 翻转) / Hide 关闭 / Tick 跟随鼠标 / 广播 PopupShownEvent/PopupHiddenEvent。
    ///
    /// 状态:
    ///   - <c>Dictionary&lt;Type, GameObject&gt; _prefabs</c> — 注册表(Type → Prefab GO)
    ///   - <c>Dictionary&lt;Type, BaseUIPopup&gt; _active</c> — 单实例缓存(同 Type 同时只显示一个,隐藏后复用)
    ///   - <c>Dictionary&lt;BaseUIPopup, Action&gt; _subs</c> — Hidden 事件订阅缓存
    ///   - <c>Dictionary&lt;Type, FollowContext&gt; _follow</c> — 跟随鼠标上下文(Type → screenAnchor/bounds/flip)
    ///
    /// 依赖:
    ///   - UILayerRootModule:读 Tooltip Layer 的根 Transform
    ///   - UIManager.Call:广播 PopupShownEvent / PopupHiddenEvent
    ///
    /// 调用链:
    ///   业务方 ShowPopup&lt;T&gt;(pos, setup, followMouse) → UIManager.ShowPopup(转发) → 本模块 Show →
    ///     单实例命中复用 / 未命中 Instantiate 到 Tooltip Root → SetData → Anchor(自动定位+翻转) →
    ///     SetAsLastSibling + blocksRaycasts=false + Show + Call(PopupShownEvent);
    ///     followMouse 时由 UIManager.Update 每帧 Tick 重定位
    ///
    /// 关键不变量:
    ///   - 同 Type 同时只显示一个(单实例语义)
    ///   - Anchor 必须在 Show() 之前(否则动画第一帧位置跳变)
    ///   - Instantiate 后重置 popup 根 RT 为填满 TooltipRoot,pivot=(0,0)
    ///   - 坐标解算统一在 popup 根本地坐标中完成,兼容 CanvasScaler / Camera Canvas
    ///   - Hidden 事件只广播隐藏事件;实例保留在 _active 中供下次 Show 复用
    ///   - Hide 时同步清 _follow
    ///   - Destroy 不 Destroy GO(由 UIManager.OnDestroy 统一处理,避免双重 Destroy 警告)
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

        private UIManager _ui;
        private readonly Dictionary<Type, GameObject> _prefabs = new();
        private readonly Dictionary<Type, BaseUIPopup> _active = new();
        private readonly Dictionary<Type, FollowContext> _follow = new();
        private readonly Dictionary<BaseUIPopup, Action<BaseUIPanel>> _subs = new();

        public bool HasFollowTargets => _follow.Count > 0;

        /// <summary>由 UIManager.Start 注入自身引用</summary>
        public void SetUIManager(UIManager ui) => _ui = ui;

        // ===== 注册 =====

        /// <summary>注册浮窗 prefab(泛型版本)。同 Type 重复注册时 LogWarning 并忽略。</summary>
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
            _prefabs[t] = prefab.gameObject;
        }

        // ===== Show =====

        /// <summary>
        /// 显示浮窗(泛型版本)。单实例:同 Type 再次 Show 会复用并重新 Anchor。
        /// setup 回调在 Show 之前调用,用于写入数据(SetData / SetText 等)。
        /// </summary>
        /// <param name="screenAnchor">屏幕坐标(鼠标位置或其他锚点)</param>
        /// <param name="setup">可选 setup 回调,会传入强类型 popup 实例</param>
        /// <param name="boundsArea">边界 RectTransform(null = 全屏)</param>
        /// <param name="flip">首选翻转方向,越界时按 BR→BL→TR→TL 优先级尝试</param>
        /// <param name="followMouse">true = Module.Tick 每帧拉鼠标坐标重定位;false = 固定锚点(默认)</param>
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
            => Show(screenAnchor, setup, boundsArea, flip, followMouse, offset);

        public T Show<T>(RectTransform target, Action<T> setup = null,
                         RectTransform boundsArea = null,
                         PopupFlipDirection flip = PopupFlipDirection.BottomRight,
                         Vector2? offset = null)
            where T : BaseUIPopup
            => Show(typeof(T), target, p => setup?.Invoke((T)p), boundsArea, flip, offset) as T;

        public T Show<T>(RectTransform target, Vector2 offset, Action<T> setup = null,
                         RectTransform boundsArea = null,
                         PopupFlipDirection flip = PopupFlipDirection.BottomRight)
            where T : BaseUIPopup
            => Show(target, setup, boundsArea, flip, offset);

        /// <summary>按 Type 显示浮窗(转发自 UIManager.ShowPopup)。</summary>
        public BaseUIPopup Show(Type type, Vector2 screenAnchor, Action<BaseUIPopup> setup,
                                RectTransform boundsArea, PopupFlipDirection flip, bool followMouse = false,
                                Vector2? offset = null)
        {
            if (_ui == null)
            {
                Debug.LogError("[PopupModule] UIManager not set; call SetUIManager first");
                return null;
            }
            if (!_prefabs.TryGetValue(type, out var prefab))
            {
                Debug.LogError($"[PopupModule] Popup {type.Name} not registered; call RegisterPopup first");
                return null;
            }

            // 单实例:命中且未销毁 → 复用
            if (!_active.TryGetValue(type, out var popup) || popup == null || popup.gameObject == null)
            {
                // 清理死引用
                if (_active.ContainsKey(type)) _active.Remove(type);

                var root = _ui.GetModule<UILayerRootModule>().GetLayerRoot(UILayer.Tooltip);
                if (root == null)
                {
                    Debug.LogError("[PopupModule] Tooltip layer root missing; assign _tooltipRoot on UIManager prefab");
                    return null;
                }

                var go = UnityEngine.Object.Instantiate(prefab, root);
                popup = go.GetComponent<BaseUIPopup>();
                if (popup == null)
                {
                    Debug.LogError($"[PopupModule] prefab for {type.Name} missing {type.Name} component");
                    UnityEngine.Object.Destroy(go);
                    return null;
                }

                var rootRt = go.transform as RectTransform;
                SetupPopupRoot(rootRt);
                popup.ApplyPopupRaycastPolicy();

                // 订阅 Hidden 事件,Hide 动画完成时广播事件
                Action<BaseUIPanel> onHidden = p => OnPopupHidden((BaseUIPopup)p);
                popup.Hidden += onHidden;
                _subs[popup] = onHidden;

                // Prefab 通常是 active 的。实例化后显式关闭,让后续 Show() 真正播放入场动画。
                go.SetActive(false);
                _active[type] = popup;
            }

            var popupRoot = popup.transform as RectTransform;
            SetupPopupRoot(popupRoot);
            Vector2 resolvedOffset = GetOffset(popup, offset);
            bool shouldPlayShow = !popup.IsVisible || popup.IsHiding;

            // 1. 数据 setup(在 Anchor 之前,布局尺寸可能受数据影响)
            setup?.Invoke(popup);

            // 2. 位置(必须 Show 之前,否则动画第一帧位置跳变)
            popup.Anchor(screenAnchor, popupRoot, boundsArea, flip, resolvedOffset);

            // 3. 渲染顺序:同 Layer 末尾
            popup.transform.SetAsLastSibling();

            // 4. Tooltip 不拦截射线
            popup.ApplyPopupRaycastPolicy();

            // 5. 触发动画
            if (shouldPlayShow)
            {
                popup.Show();
            }

            // 6. 广播事件
            if (shouldPlayShow)
                _ui.Call(new PopupShownEvent(type.Name));

            // 7. 跟随鼠标:记录上下文,启用 Tick
            if (followMouse)
            {
                _follow[type] = new FollowContext
                {
                    Type = type,
                    PopupRoot = popupRoot,
                    BoundsArea = boundsArea,
                    Flip = flip,
                    Offset = resolvedOffset
                };
            }
            else
            {
                _follow.Remove(type);
            }
            return popup;
        }

        public BaseUIPopup Show(Type type, RectTransform target, Action<BaseUIPopup> setup,
                                RectTransform boundsArea, PopupFlipDirection flip, Vector2? offset = null)
        {
            if (target == null)
            {
                Debug.LogError("[PopupModule] target RectTransform is null");
                return null;
            }
            if (_ui == null)
            {
                Debug.LogError("[PopupModule] UIManager not set; call SetUIManager first");
                return null;
            }
            if (!_prefabs.TryGetValue(type, out var prefab))
            {
                Debug.LogError($"[PopupModule] Popup {type.Name} not registered; call RegisterPopup first");
                return null;
            }

            if (!_active.TryGetValue(type, out var popup) || popup == null || popup.gameObject == null)
            {
                if (_active.ContainsKey(type)) _active.Remove(type);

                var root = _ui.GetModule<UILayerRootModule>().GetLayerRoot(UILayer.Tooltip);
                if (root == null)
                {
                    Debug.LogError("[PopupModule] Tooltip layer root missing; assign _tooltipRoot on UIManager prefab");
                    return null;
                }

                var go = UnityEngine.Object.Instantiate(prefab, root);
                popup = go.GetComponent<BaseUIPopup>();
                if (popup == null)
                {
                    Debug.LogError($"[PopupModule] prefab for {type.Name} missing {type.Name} component");
                    UnityEngine.Object.Destroy(go);
                    return null;
                }

                var rootRt = go.transform as RectTransform;
                SetupPopupRoot(rootRt);
                popup.ApplyPopupRaycastPolicy();

                Action<BaseUIPanel> onHidden = p => OnPopupHidden((BaseUIPopup)p);
                popup.Hidden += onHidden;
                _subs[popup] = onHidden;

                go.SetActive(false);
                _active[type] = popup;
            }

            var popupRoot = popup.transform as RectTransform;
            SetupPopupRoot(popupRoot);
            Vector2 resolvedOffset = GetOffset(popup, offset);
            bool shouldPlayShow = !popup.IsVisible || popup.IsHiding;

            setup?.Invoke(popup);
            popup.Anchor(target, popupRoot, boundsArea, flip, resolvedOffset);
            popup.transform.SetAsLastSibling();
            popup.ApplyPopupRaycastPolicy();

            if (shouldPlayShow)
            {
                popup.Show();
                _ui.Call(new PopupShownEvent(type.Name));
            }

            _follow.Remove(type);
            return popup;
        }

        // ===== Hide =====

        /// <summary>隐藏指定类型浮窗(泛型)。动画完成后由 Hidden 事件回调广播事件。</summary>
        public void Hide<T>() where T : BaseUIPopup => Hide(typeof(T));

        /// <summary>按 Type 隐藏浮窗。未显示时直接返回。同时清理 followMouse 状态。</summary>
        public void Hide(Type type)
        {
            if (!_active.TryGetValue(type, out var popup) || popup == null) return;
            _follow.Remove(type);
            if (!popup.IsVisible) return;
            popup.Hide();
        }

        /// <summary>隐藏所有当前显示的浮窗。</summary>
        public void HideAll()
        {
            _follow.Clear();
            // 复制 keys,避免遍历期间被调用方改动集合
            var keys = new List<Type>(_active.Keys);
            foreach (var t in keys) Hide(t);
        }

        // ===== 查询 =====

        /// <summary>指定类型浮窗当前是否正在显示(泛型)</summary>
        public bool IsVisible<T>() where T : BaseUIPopup => IsVisible(typeof(T));

        /// <summary>指定类型浮窗当前是否正在显示(Type)</summary>
        public bool IsVisible(Type type)
            => _active.TryGetValue(type, out var p) && p != null && p.IsVisible;

        /// <summary>当前已创建的浮窗实例(只读快照,调试用)</summary>
        public IReadOnlyDictionary<Type, BaseUIPopup> GetActive() => _active;

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

        private static Vector2 GetMousePos()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Pointer.current?.position.ReadValue() ?? Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        // ===== 内部 =====

        /// <summary>
        /// Hide 动画完成时由 BaseUIPanel.Hidden 事件触发。
        /// 清理跟随状态 + 广播 PopupHiddenEvent。
        /// </summary>
        private void OnPopupHidden(BaseUIPopup popup)
        {
            if (popup == null) return;
            var type = popup.GetType();
            _follow.Remove(type);
            _ui?.Call(new PopupHiddenEvent(type.Name));
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

        private Vector2 GetOffset(BaseUIPopup popup, Vector2? offsetOverride)
        {
            if (offsetOverride.HasValue)
                return offsetOverride.Value;
            if (_ui != null)
                return _ui.PopupOffset;
            return popup != null ? popup.DefaultTargetOffset : Vector2.one * DEFAULT_OFFSET;
        }

        /// <summary>
        /// 清空所有状态。
        /// 不 Destroy GO — UIManager.OnDestroy 会统一销毁,避免重复 Destroy 触发警告。
        /// </summary>
        public override void Destroy()
        {
            foreach (var kv in _subs)
            {
                if (kv.Key != null) kv.Key.Hidden -= kv.Value;
            }
            _subs.Clear();
            _active.Clear();
            _prefabs.Clear();
        }
    }
}
