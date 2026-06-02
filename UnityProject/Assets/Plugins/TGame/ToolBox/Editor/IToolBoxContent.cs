namespace TGame.ToolBox
{
    /// <summary>
    /// 普通类实现此接口以提供自定义 ToolBox 内容渲染。    /// 标记 [ToolBox] 的非 ScriptableObject 类必须实现此接口。    /// </summary>
    public interface IToolBoxContent
    {
        void DrawContent();
    }
}

