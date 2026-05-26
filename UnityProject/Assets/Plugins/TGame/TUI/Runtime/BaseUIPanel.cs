using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板抽象基类，每个面板自带独立 Canvas 和 CanvasGroup。
    /// 独立 Canvas 避免一个面板 UI 变化导致其他面板重建网格。
    /// CanvasGroup 用于控制面板整体透明度、交互和射线检测。
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BaseUIPanel : MonoBehaviour, IUIPanel
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _canvasGroup;

        public Canvas Canvas => _canvas;
        public CanvasGroup CanvasGroup => _canvasGroup;
        public bool IsVisible => gameObject.activeSelf;

        /// <summary>
        /// 编辑器添加组件或 Reset 时，缓存 Canvas/CanvasGroup 引用
        /// </summary>
        private void Reset()
        {
            TryGetComponent(out _canvas);
            TryGetComponent(out _canvasGroup);
        }

        /// <summary>
        /// 运行时获取 Canvas/CanvasGroup 引用
        /// </summary>
        protected virtual void Awake()
        {
            TryGetComponent(out _canvas);
            TryGetComponent(out _canvasGroup);
        }

        /// <summary>
        /// 面板加载完成后调用，子类重写用于初始化逻辑
        /// </summary>
        public virtual void Init() { }

        /// <summary>
        /// 显示面板，子类可重写添加动画
        /// </summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 隐藏面板，子类可重写添加动画
        /// </summary>
        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
