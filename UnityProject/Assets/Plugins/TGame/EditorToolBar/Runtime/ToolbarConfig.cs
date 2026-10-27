// ToolbarConfig.cs
// 顶部导航栏（MainToolbar）的配置资源。
//
// 默认位置：Assets/Resources/ToolbarConfig.asset
// 由 QuickLaunchToolbar 加载；不存在时点「设置」按钮会自动创建。

using UnityEngine;

namespace TGame.EditorToolBar
{
    [CreateAssetMenu(fileName = "ToolbarConfig", menuName = "TGame/Toolbar Config")]
    public class ToolbarConfig : ScriptableObject
    {
        public const string ResourceName = "ToolbarConfig";

        [Header("初始化场景")]
        [Tooltip("框架启动场景，Build Settings 里只放它。「测试」按钮以它为 playModeStartScene。")]
        public string initScenePath = "";

        [Header("目标场景")]
        [Tooltip("「运行」按钮直接运行的场景。")]
        public string targetScenePath = "";

        [Header("按钮说明")]
        [Tooltip("设置窗口中展示的说明文本。留空则显示内置说明。")]
        [TextArea(6, 20)]
        public string helpText = "";

        public static ToolbarConfig Load()
        {
            return Resources.Load<ToolbarConfig>(ResourceName);
        }
    }
}