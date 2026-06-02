using UnityEngine;

namespace TGame
{
    /// <summary>
    /// TDebug 配置 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "TDebugSettings", menuName = "TGame/Debug Settings")]
    public class TDebugSettings : ScriptableObject
    {
        [Header("全局开关")]
        [Tooltip("是否启用日志输出")]
        public bool Enable = true;

        [Header("Level 过滤")]
        [Tooltip("低于此级别的日志将被跳过")]
        public int MinLevel = 0;

        [Header("默认上下文")]
        [Tooltip("未设置上下文 Tag 时使用的默认值")]
        public string DefaultTag = "";

        [Tooltip("未设置上下文 Level 时使用的默认值")]
        public int DefaultLevel = 1;

        [Header("文件日志")]
        [Tooltip("是否将日志写入文件")]
        public bool EnableFileLogging = true;

        [Tooltip("日志目录，为空时使用默认路径")]
        public string LogDirectory = "";

        [Tooltip("单文件大小上限（KB），默认 5120 KB = 5 MB")]
        public int MaxFileSizeKB = 5120;
    }
}
