using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    /// <summary>
    /// 浮窗抽象基类,继承 BaseUIPanel。
    /// Prefab 内部必须有一个"内容 RectTransform"(SerializeField 标记),
    /// 浮窗的 anchoredPosition / pivot 由 PopupModule 在这个内部 RT 上操作 —
    /// panel 自身的 RT 会被 UILayer Root 强制填满,不可用。
    ///
    /// 子类必须实现 <see cref="SetData{TData}(TData)"/>,在 setup 回调里被调用以写入数据。
    /// 默认子类可重写 <see cref="BuildShowAnimation"/> 自定义入场动画(默认 FadeIn)。
    /// </summary>
    public abstract class BaseUIPopup : BaseUIPanel
    {
        [Header("Popup")]
        [Tooltip("浮窗的内容 RectTransform。Module 改它的 pivot + anchoredPosition 做定位。" +
                 "通常挂在 Panel 根下一层,包含 Background / Text 等所有可见子节点。")]
        [SerializeField] protected RectTransform _content;

        [Tooltip("距离鼠标/锚点的默认像素偏移")]
        [SerializeField] protected float _defaultOffset = 8f;

        /// <summary>内容 RectTransform(Module 拿来定位 + 翻转)</summary>
        public RectTransform Content => _content;

        /// <summary>Prefab 上配置的默认偏移量。</summary>
        public float DefaultOffset => _defaultOffset;

        /// <summary>由 PopupModule 在 setup 回调里调用,子类用来写入数据。data 为 null 时清空。</summary>
        public abstract void SetData<TData>(TData data) where TData : class;

        /// <summary>
        /// 由 PopupModule 在 Show 之前调用,计算并应用最终位置 + pivot。
        /// 默认实现:取 _content 真实尺寸(layout 失败时回退 sizeDelta),委托 PopupLayoutHelper.Solve。
        /// 子类一般不需要重写,除非有自定义吸附/磁吸逻辑。
        /// </summary>
        public virtual void Anchor(Vector2 screenAnchor, RectTransform boundsArea,
                                  PopupFlipDirection flip, float? offsetOverride = null)
            => Anchor(screenAnchor, ResolvePopupRoot(), boundsArea, flip, offsetOverride);

        /// <summary>
        /// 使用指定 Popup Root 做屏幕点到本地坐标的转换。
        /// PopupModule 会传入实例根节点,确保 CanvasScaler / Camera Canvas 下定位仍正确。
        /// </summary>
        public virtual void Anchor(Vector2 screenAnchor, RectTransform popupRoot, RectTransform boundsArea,
                                  PopupFlipDirection flip, float? offsetOverride = null)
        {
            if (_content == null)
            {
                Debug.LogError($"[BaseUIPopup] {GetType().Name} missing _content RectTransform, cannot anchor", this);
                return;
            }

            PrepareLayoutRoot(popupRoot);

            // 强制刷新 layout,获取 TMP/ContentSizeFitter 后的真实尺寸
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            ApplyLayout(screenAnchor, popupRoot, boundsArea, flip, offsetOverride, GetContentSize());
        }

        /// <summary>
        /// 鼠标跟随专用:不刷新 layout,但仍走同一套翻转/边界解算。
        /// </summary>
        internal void SetPosition(Vector2 screenAnchor, RectTransform popupRoot, RectTransform boundsArea,
                                  PopupFlipDirection flip, float? offsetOverride = null)
        {
            if (_content == null) return;
            PrepareLayoutRoot(popupRoot);
            ApplyLayout(screenAnchor, popupRoot, boundsArea, flip, offsetOverride, GetContentSize());
        }

        internal void ApplyPopupRaycastPolicy()
        {
            SetCanvasGroupNonBlocking();
        }

        public override void Show()
        {
            base.Show();
            SetCanvasGroupNonBlocking();
        }

        public override void Hide()
        {
            base.Hide();
            SetCanvasGroupNonBlocking();
        }

        protected override void AfterShow()
        {
            base.AfterShow();
            SetCanvasGroupNonBlocking();
        }

        protected override void BeforeHide()
        {
            base.BeforeHide();
            SetCanvasGroupNonBlocking();
        }

        private void ApplyLayout(Vector2 screenAnchor, RectTransform popupRoot, RectTransform boundsArea,
                                 PopupFlipDirection flip, float? offsetOverride, Vector2 size)
        {
            var (pos, pivot) = PopupLayoutHelper.Solve(
                screenAnchor, size, popupRoot, boundsArea, flip, offsetOverride ?? _defaultOffset);
            _content.pivot = pivot;
            _content.anchoredPosition = pos;
        }

        private Vector2 GetContentSize()
        {
            Vector2 size = _content.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                // 兜底:取设计尺寸
                size = _content.sizeDelta;
            }
            return size;
        }

        private RectTransform ResolvePopupRoot()
        {
            return transform as RectTransform;
        }

        private void PrepareLayoutRoot(RectTransform popupRoot)
        {
            if (popupRoot != null)
            {
                popupRoot.anchorMin = Vector2.zero;
                popupRoot.anchorMax = Vector2.one;
                popupRoot.offsetMin = Vector2.zero;
                popupRoot.offsetMax = Vector2.zero;
                popupRoot.pivot = Vector2.zero;
            }

            if (_content != null && popupRoot != null && (object)_content != (object)popupRoot)
            {
                _content.anchorMin = Vector2.zero;
                _content.anchorMax = Vector2.zero;
            }
        }

        private void SetCanvasGroupNonBlocking()
        {
            if (_canvasGroup == null)
                TryGetComponent(out _canvasGroup);

            if (_canvasGroup == null) return;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        protected virtual void Reset()
        {
            // 第一个子 RectTransform 自动作为 _content(常见用法:Panel 根 → Content 子节点)
            TryGetComponent(out _content);
            TryGetComponent(out _canvasGroup);
        }
    }
}
