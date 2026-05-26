namespace TGame.TUI
{
    /// <summary>
    /// UI 面板生命周期接口
    /// </summary>
    public interface IUIPanel
    {
        /// <summary>面板加载后调用，用于初始化逻辑</summary>
        void Init();
        /// <summary>显示面板</summary>
        void Show();
        /// <summary>隐藏面板</summary>
        void Hide();
        /// <summary>面板是否可见</summary>
        bool IsVisible { get; }
    }
}
