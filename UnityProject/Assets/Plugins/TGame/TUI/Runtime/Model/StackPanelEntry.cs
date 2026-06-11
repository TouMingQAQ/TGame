using System;

namespace TGame.TUI
{
    /// <summary>
    /// 栈式 Panel 管理的条目,记录一次 Open 的面板类型、已加载实例和入栈深度。
    /// 用于 OnPushed/OnPopped 钩子参数。
    /// Instance 可能为 null(如已被外部 Destroy 但栈条目尚未清理)。
    /// </summary>
    public readonly struct StackPanelEntry
    {
        /// <summary>被推入栈的面板类型</summary>
        public readonly Type PanelType;

        /// <summary>已加载的面板实例,可能为 null</summary>
        public readonly BaseUIPanel Instance;

        /// <summary>入栈时的栈深度(入栈后栈的元素个数)</summary>
        public readonly int Depth;

        public StackPanelEntry(Type panelType, BaseUIPanel instance, int depth)
        {
            PanelType = panelType;
            Instance = instance;
            Depth = depth;
        }
    }
}
