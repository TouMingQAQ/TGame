using System;
using UnityEngine;
using DG.Tweening;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板抽象基类，每个面板自带独立 Canvas 和 CanvasGroup。
    /// 独立 Canvas 避免一个面板 UI 变化导致其他面板重建网格。
    /// CanvasGroup 用于控制面板整体透明度、交互和射线检测。
    /// Show 走 _showSequence.Restart，Hide 走 _hideSequence.Restart（若已重写）或 _showSequence.SmoothRewind（默认回退）。
    /// 子类可分别重写 BuildShowAnimation / BuildHideAnimation 自定义入场/离场动画。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public abstract class BaseUIPanel : MonoBehaviour, IUIPanel
    {
        private enum AnimState { None, Showing, Hiding }

        [Header("Animation")]
        [SerializeField] protected bool _ignoreTimeScale = true;

        [Header("UI")]
        [SerializeField] private UILayer _layer = UILayer.Normal;

        [SerializeField] protected CanvasGroup _canvasGroup;
        public CanvasGroup CanvasGroup => _canvasGroup;
        public bool IsVisible => gameObject.activeSelf;
        public bool IsHiding => _animState == AnimState.Hiding;
        public bool IsShowing => _animState == AnimState.Showing;
        /// <summary>面板注册/运行时层级。prefab 上 SerializeField 配置,加载时直接读取</summary>
        public UILayer Layer => _layer;

        /// <summary>所属 UIRoot(由 UILoaderModule 在 Instantiate 时设置,业务方可借此回溯访问 UIRoot)</summary>
        public UIRoot Root { get; private set; }

        private Sequence _showSequence;
        private Sequence _hideSequence;   // null = Hide 沿用 _showSequence 的 SmoothRewind
        private AnimState _animState = AnimState.None;

        /// <summary>
        /// Hide 动画完成、gameObject 已 Deactivate 时触发。
        /// 供 UIRoot 监听以实现"顶层被外部关闭时自动恢复上一层"语义。
        /// </summary>
        public event Action<BaseUIPanel> Hidden;

        private void Reset()
        {
            TryGetComponent(out _canvasGroup);
        }

        /// <summary>由 UILoaderModule 调用,记录面板归属的 UIRoot 以便业务方回溯</summary>
        internal void SetRoot(UIRoot root) => Root = root;

        protected virtual void Awake()
        {
            ConfigureSequence();
        }

        public virtual void Init() { }

        /// <summary>
        /// 构建 Show 动画 Sequence。子类重写以自定义入场动画。
        /// Sequence 已在 Awake 中装配（SetAutoKill/Pause/SetLink/SetUpdate/OnComplete/OnRewind）。
        /// 返回 null 视为实现错误，Awake 会用 FadeIn 兜底并 LogWarning。
        /// </summary>
        protected virtual Sequence BuildShowAnimation()
        {
            var seq = DOTween.Sequence();
            seq.Append(UIAnimationMaker.FadeIn(CanvasGroup));
            seq.SetLoops(1);
            return seq;
        }

        /// <summary>
        /// 构建 Hide 动画 Sequence。子类重写以自定义离场动画。
        /// 返回 null（默认）表示"Hide 走 _showSequence.SmoothRewind"，即沿用 Show 动画的反向。
        /// 返回非空 Sequence 则 Hide 走独立 Restart，完成时触发 OnHideComplete。
        /// </summary>
        protected virtual Sequence BuildHideAnimation() => null;


        void ConfigureSequence()
        {
            _showSequence = BuildShowAnimation();
            _hideSequence = BuildHideAnimation();
            if(_showSequence == null)
                _showSequence = this.BuildShowAnimation();//保证Show不为空
            _showSequence.SetAutoKill(false);
            _showSequence.Pause();
            _showSequence.SetLink(gameObject);
            _showSequence.SetUpdate(_ignoreTimeScale);
            _showSequence.OnComplete(OnShowComplete);
            if (_hideSequence == null)
                _showSequence.OnRewind(OnHideComplete);//如果
            else
            {
                _hideSequence.SetAutoKill(false);
                _hideSequence.Pause();
                _hideSequence.SetLink(gameObject);
                _hideSequence.SetUpdate(_ignoreTimeScale);
                _hideSequence.OnComplete(OnHideComplete);
            }
            
        }
        /// <summary>
        /// 显示面板，向前播放 _showSequence 动画。方法立即返回，动画异步播放。
        /// </summary>
        public virtual void Show()
        {
            if (_animState == AnimState.Showing) return;

            StopHideAnimationForShow();

            BeforeShow();
            _animState = AnimState.None;

            gameObject.SetActive(true);
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            _animState = AnimState.Showing;
            _showSequence.Restart();
        }

        /// <summary>
        /// 隐藏面板。若子类重写了 BuildHideAnimation 则正向播放 _hideSequence，
        /// 否则反向播放 _showSequence。方法立即返回，动画异步播放。
        /// GameObject 在动画完成后才 Deactivate。
        /// </summary>
        public virtual void Hide()
        {
            if (_animState == AnimState.Hiding) return;
            if (!gameObject.activeSelf && _animState == AnimState.None) return;

            BeforeHide();

            StopShowAnimationForHide();

            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            _animState = AnimState.Hiding;

            if (_hideSequence != null)
            {
                PlayHideSequence();
            }
            else
            {
                PlayShowSequenceBackward();
            }
        }

        private void StopHideAnimationForShow()
        {
            _showSequence.Pause();

            if (_hideSequence == null) return;

            _hideSequence.Pause();
            if (_hideSequence.Elapsed() > 0f)
                _hideSequence.Rewind();
        }

        private void StopShowAnimationForHide()
        {
            _showSequence.Pause();
        }

        private void PlayHideSequence()
        {
            _hideSequence.Pause();
            _hideSequence.Restart();
        }

        private void PlayShowSequenceBackward()
        {
            if (_showSequence.Elapsed() <= 0f)
                OnHideComplete();
            else
                _showSequence.SmoothRewind();
        }

        private void OnShowComplete()
        {
            if (_animState != AnimState.Showing) return;
            _animState = AnimState.None;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            AfterShow();
        }

        private void OnHideComplete()
        {
            if (_animState != AnimState.Hiding) return;
            _animState = AnimState.None;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            AfterHide();
            Hidden?.Invoke(this);
        }

        /// <summary>Show 动画开始前调用</summary>
        protected virtual void BeforeShow() { }

        /// <summary>Show 动画完成后调用，此时面板完全可见且可交互</summary>
        protected virtual void AfterShow() { }

        /// <summary>Hide 动画开始前调用</summary>
        protected virtual void BeforeHide() { }

        /// <summary>Hide 动画完成后调用，此时 GameObject 已 Deactivate</summary>
        protected virtual void AfterHide() { }

        /// <summary>
        /// 当面板被 UIRoot.OpenStack 推入栈顶时调用,在 Show 动画开始前触发。
        /// 默认空实现;不需要感知栈的面板可不重写。
        /// </summary>
        public virtual void OnPushed(StackPanelEntry entry) { }

        /// <summary>
        /// 当面板从栈顶被 UIRoot.Back/BackTo/PopToRoot 弹出时调用,在 Hide 动画开始前触发。
        /// 默认空实现;不需要感知栈的面板可不重写。
        /// </summary>
        public virtual void OnPopped(StackPanelEntry entry) { }

        protected virtual void OnDestroy()
        {
            _showSequence?.Kill();
            _showSequence = null;
            _hideSequence?.Kill();
            _hideSequence = null;
            Hidden = null;
        }
    }
}
