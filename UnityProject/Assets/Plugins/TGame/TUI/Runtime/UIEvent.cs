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
}
