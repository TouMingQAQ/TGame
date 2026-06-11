// CustomToolbarDesignerTypes.cs
// 顶部自定义工具栏插件 — 核心类型定义
// 包含:ToolbarSlot 枚举、ToolbarItemConfig、ToolbarItemParameter、
//       ICustomToolbarItem、CustomToolbarItemAttribute、CustomToolbarContext

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.EditorToolBar
{
    /// <summary>
    /// 工具栏组件逻辑槽位。物理注入只到左右两个区域。
    /// </summary>
    public enum ToolbarSlot
    {
        LeftStart,
        LeftEnd,
        Center,
        RightStart,
        RightEnd
    }

    /// <summary>
    /// 单个组件的持久化配置。保存到 CustomToolbarSettings.asset。
    /// </summary>
    [Serializable]
    public class ToolbarItemConfig
    {
        public string Id;
        public bool Enabled = true;
        public ToolbarSlot Slot = ToolbarSlot.LeftEnd;
        public int Order = 0;
        public float Width = 0f;        // <= 0 表示由组件自己决定宽度
        public float SpaceBefore = 0f;
        public float SpaceAfter = 0f;
        public string LabelOverride;
        public string TooltipOverride;
        public List<ToolbarItemParameter> Parameters = new();
    }

    /// <summary>
    /// 通用参数。Value 建议用字符串;复杂数据可存 JSON 由组件自行反序列化。
    /// </summary>
    [Serializable]
    public class ToolbarItemParameter
    {
        public string Key;
        public string Value;
    }

    /// <summary>
    /// 工具栏组件接口(UI Toolkit 版)。组件实现 Build 返回一个 VisualElement,由 Renderer 挂到工具栏。
    ///
    /// AI 新增组件协议:
    ///   1. 类用 [CustomToolbarItem("your.id", "Display Name")]
    ///   2. Build 返回一个横向的 VisualElement
    ///   3. 控件自己设 style.width / flexShrink,不要超过 18px 高
    ///   4. 不要在 Build 里持久存状态(每次 Rebuild 都会被重新调用)
    /// </summary>
    public interface ICustomToolbarItem
    {
        string Id { get; }
        string DisplayName { get; }
        ToolbarSlot DefaultSlot { get; }
        int DefaultOrder { get; }
        float DefaultWidth { get; }

        /// <summary>
        /// 构建组件的 VisualElement。Renderer 会把返回值挂到工具栏上。
        /// </summary>
        VisualElement Build(CustomToolbarContext context, ToolbarItemConfig config);
    }

    /// <summary>
    /// 标记类为自动注册的工具栏组件。Registry 启动时扫描 AppDomain。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CustomToolbarItemAttribute : Attribute
    {
        public string Id { get; }
        public string DisplayName { get; }

        public CustomToolbarItemAttribute(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// 组件构建上下文。
    /// </summary>
    public sealed class CustomToolbarContext
    {
        public CustomToolbarSettings Settings { get; internal set; }
        public bool IsPlaying => EditorApplication.isPlaying;
        public double EditorTime => EditorApplication.timeSinceStartup;
    }
}
