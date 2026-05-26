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
}
