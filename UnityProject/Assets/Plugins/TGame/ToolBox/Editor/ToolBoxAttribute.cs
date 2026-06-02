using System;

namespace TGame.ToolBox
{
    /// <summary>
    /// 标记类为 ToolBox 内容，自动在 ToolBox 窗口中生成左侧 Tab。    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ToolBoxAttribute : Attribute
    {
        /// <summary>左侧 Tab 显示名称</summary>
        public string Name { get; }

        /// <summary>SO 资产路径（如 "Assets/Settings/MyConfig.asset"），仅 ScriptableObject 类有效</summary>
        public string Path { get; set; }

        /// <summary>Tab 排序，默认 -1</summary>
        public int Order { get; set; } = -1;

        public ToolBoxAttribute(string name)
        {
            Name = name;
        }
    }
}

