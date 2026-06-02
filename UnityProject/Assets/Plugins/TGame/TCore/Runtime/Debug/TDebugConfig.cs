using System;

namespace TGame
{
    /// <summary>
    /// TDebug 内部配置状态
    /// </summary>
    internal static class TDebugConfig
    {
        // 全局开关
        internal static bool _enabled = true;
        internal static int _minLevel = 0;

        // 上下文（通过 SetTag / SetLevel 临时设置，为无参 Log 提供值）
        internal static string _contextTag;
        internal static int _contextLevel = -1; // -1 表示"未设置，使用默认值"

        // 默认值（来自 TDebugSettings SO）
        internal static string _defaultTag = "";
        internal static int _defaultLevel = 1;

        // 文件日志开关
        internal static bool _enableFileLogging = true;

        /// <summary>
        /// 从 ScriptableObject 加载配置
        /// </summary>
        internal static void ApplySettings(TDebugSettings settings)
        {
            if (settings == null) return;

            _enabled = settings.Enable;
            _minLevel = settings.MinLevel;
            _defaultTag = settings.DefaultTag ?? "";
            _defaultLevel = settings.DefaultLevel;
            _enableFileLogging = settings.EnableFileLogging;

            if (!string.IsNullOrEmpty(settings.LogDirectory))
                TDebugFileWriter.SetLogDirectory(settings.LogDirectory);
            TDebugFileWriter.SetMaxFileSizeKB(settings.MaxFileSizeKB);
        }

        /// <summary>
        /// Level 过滤（只比较 level，不再过滤 tag）
        /// </summary>
        internal static bool IsLogEnabled(int level)
        {
            return _enabled && level >= _minLevel;
        }

        /// <summary>
        /// 获取实际生效的 Tag：上下文 > 默认值
        /// </summary>
        internal static string GetEffectiveTag()
        {
            return _contextTag ?? _defaultTag;
        }

        /// <summary>
        /// 获取实际生效的 Level：上下文 > 默认值
        /// </summary>
        internal static int GetEffectiveLevel()
        {
            return _contextLevel >= 0 ? _contextLevel : _defaultLevel;
        }
    }
}
