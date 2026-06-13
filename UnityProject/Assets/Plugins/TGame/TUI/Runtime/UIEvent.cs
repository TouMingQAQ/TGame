namespace TGame.TUI
{
    /// <summary>
    /// 面板打开事件，通过 BaseManager.Call 广播
    /// </summary>
    public readonly struct PanelOpenedEvent
    {
        public readonly string PanelName;
        public PanelOpenedEvent(string name) => PanelName = name;
    }

    /// <summary>
    /// 面板关闭事件，通过 BaseManager.Call 广播
    /// </summary>
    public readonly struct PanelClosedEvent
    {
        public readonly string PanelName;
        public PanelClosedEvent(string name) => PanelName = name;
    }

    /// <summary>
    /// 面板被压入 UI 栈事件，通过 BaseManager.Call 广播
    /// </summary>
    public readonly struct PanelPushedEvent
    {
        public readonly string PanelName;
        public readonly int StackDepth;
        public PanelPushedEvent(string name, int depth)
        {
            PanelName = name;
            StackDepth = depth;
        }
    }

    /// <summary>
    /// 面板从 UI 栈弹出事件，通过 BaseManager.Call 广播
    /// </summary>
    public readonly struct PanelPoppedEvent
    {
        public readonly string PanelName;
        public readonly int StackDepth;
        public PanelPoppedEvent(string name, int depth)
        {
            PanelName = name;
            StackDepth = depth;
        }
    }

    /// <summary>
    /// 浮窗显示事件，通过 BaseManager.Call 广播。
    /// 语义上区别于 PanelOpenedEvent:Popup 是"瞬时气泡",生命周期短,跟鼠标走。
    /// </summary>
    public readonly struct PopupShownEvent
    {
        public readonly string PopupName;
        public PopupShownEvent(string name) => PopupName = name;
    }

    /// <summary>
    /// 浮窗隐藏事件(动画完成),通过 BaseManager.Call 广播
    /// </summary>
    public readonly struct PopupHiddenEvent
    {
        public readonly string PopupName;
        public PopupHiddenEvent(string name) => PopupName = name;
    }
}
