using UnityEngine;
using DG.Tweening;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板抽象基类，每个面板自带独立 Canvas 和 CanvasGroup。
    /// 独立 Canvas 避免一个面板 UI 变化导致其他面板重建网格。
    /// CanvasGroup 用于控制面板整体透明度、交互和射线检测。
    /// Show/Hide 通过 DOTween Sequence 播放动画，子类重写 BuildAnimation 自定义动画效果。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public abstract class BaseUIPanel : MonoBehaviour, IUIPanel
    {
        private enum AnimState { None, Showing, Hiding }

        [Header("Animation")]
        [SerializeField] protected bool _ignoreTimeScale = true;

        [SerializeField] protected CanvasGroup _canvasGroup;
        [SerializeField] protected RectTransform root;
        
        public CanvasGroup CanvasGroup => _canvasGroup;
        public RectTransform Root => root;
        public bool IsVisible => gameObject.activeSelf;

        private Sequence _sequence;
        private AnimState _animState = AnimState.None;

        private void Reset()
        {
            TryGetComponent(out _canvasGroup);
            TryGetComponent(out root);
        }

        protected virtual void Awake()
        {
            _canvasGroup.alpha = 0f;

            _sequence = BuildAnimation();
            _sequence.SetAutoKill(false);
            _sequence.Pause();
            _sequence.SetLink(gameObject);
            _sequence.SetUpdate(_ignoreTimeScale);
            _sequence.OnComplete(OnShowComplete);
            _sequence.OnRewind(OnHideComplete);
        }

        public virtual void Init() { }

        /// <summary>
        /// 构建动画 Sequence，子类重写以自定义 Show/Hide 动画。
        /// Sequence 通过 PlayForward 播放 Show 动画，PlayBackwards 播放 Hide 动画。
        /// 默认实现：CanvasGroup alpha 0→1 淡入动画。
        /// </summary>
        protected virtual Sequence BuildAnimation()
        {
            var seq = DOTween.Sequence();
            seq.Append(UIAnimationMaker.FadeIn(CanvasGroup));
            seq.SetLoops(1);
            return seq;
        }

        /// <summary>
        /// 显示面板，向前播放动画 Sequence。方法立即返回，动画异步播放。
        /// </summary>
        public virtual void Show()
        {
            if (_animState == AnimState.Showing) return;

            BeforeShow();

            _sequence.Pause();
            _animState = AnimState.None;

            gameObject.SetActive(true);
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _animState = AnimState.Showing;
            _sequence.PlayForward();
        }

        /// <summary>
        /// 隐藏面板，反向播放动画 Sequence。方法立即返回，动画异步播放。
        /// GameObject 在动画完成后才 Deactivate。
        /// </summary>
        public virtual void Hide()
        {
            if (_animState == AnimState.Hiding) return;
            if (!gameObject.activeSelf && _animState == AnimState.None) return;

            BeforeHide();

            _sequence.Pause();
            _animState = AnimState.None;

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            _animState = AnimState.Hiding;
            _sequence.SmoothRewind();
        }

        private void OnShowComplete()
        {
            _animState = AnimState.None;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            AfterShow();
        }

        private void OnHideComplete()
        {
            _animState = AnimState.None;
            gameObject.SetActive(false);
            AfterHide();
        }

        /// <summary>Show 动画开始前调用</summary>
        protected virtual void BeforeShow() { }

        /// <summary>Show 动画完成后调用，此时面板完全可见且可交互</summary>
        protected virtual void AfterShow() { }

        /// <summary>Hide 动画开始前调用</summary>
        protected virtual void BeforeHide() { }

        /// <summary>Hide 动画完成后调用，此时 GameObject 已 Deactivate</summary>
        protected virtual void AfterHide() { }

        protected virtual void OnDestroy()
        {
            _sequence?.Kill();
            _sequence = null;
        }
    }
}
