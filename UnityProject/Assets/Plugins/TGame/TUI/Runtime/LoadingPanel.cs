namespace TGame.TUI
{
    /// <summary>
    /// 加载面板抽象基类，继承自 <see cref="BaseUIPanel"/>。
    /// 用于场景切换、资源加载等需要展示进度的场景。
    /// 由 <see cref="UIManager"/> 通过 ShowLoadingPanel / HideLoadingPanel / SetLoadingProgress 控制。
    /// 子类实现 <see cref="SetProgress"/> 以更新自身 UI（进度条/文本/动画等）。
    /// </summary>
    public abstract class LoadingPanel : BaseUIPanel
    {
        /// <summary>
        /// 更新加载进度，调用方传入 [0, 1] 范围内的值。
        /// 子类实现时建议内部 <c>Mathf.Clamp01</c>，避免非法值导致 UI 异常。
        /// </summary>
        /// <param name="progress">进度值，建议范围 [0, 1]</param>
        public abstract void SetProgress(float progress);
    }
}
