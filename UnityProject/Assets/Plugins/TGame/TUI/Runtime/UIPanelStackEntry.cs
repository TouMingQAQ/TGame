using System;

namespace TGame.TUI
{
    /// <summary>
    /// UI 栈条目。记录一次 PushPanel 的面板类型和已加载实例。
    /// 用于 OnPushed/OnPopped 钩子参数与 PanelPushedEvent/PanelPoppedEvent 载荷共享数据形状。
    /// </summary>
    public readonly struct UIPanelStackEntry
    {
        /// <summary>被推入栈的面板类型</summary>
        public readonly Type PanelType;

        /// <summary>已加载的面板实例，可能为 null（如已被外部 Destroy 但栈条目尚未清理）</summary>
        public readonly BaseUIPanel Instance;

        /// <summary>入栈时的栈深度（入栈后栈的元素个数）</summary>
        public readonly int Depth;

        public UIPanelStackEntry(Type panelType, BaseUIPanel instance, int depth)
        {
            PanelType = panelType;
            Instance = instance;
            Depth = depth;
        }
    }
}
