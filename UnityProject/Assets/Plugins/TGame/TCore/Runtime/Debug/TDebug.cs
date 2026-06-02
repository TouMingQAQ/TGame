using System;

namespace TGame
{
    /// <summary>
    /// TDebug — 轻量级调试日志工具
    ///
    /// 功能：
    ///   - 支持 Tag（string）和 Level（int）双重标注
    ///   - 可设置上下文，简化连续调用
    ///   - 同时输出到 Unity Console 和文件
    ///   - 通过 TDebugSettings SO 配置
    ///   - 提供 ToolBox 面板进行运行时控制
    /// </summary>
    public static class TDebug
    {
        // ==========================================
        //  初始化
        // ==========================================

        /// <summary>当前加载的配置 SO（可为 null）</summary>
        public static TDebugSettings CurrentSettings { get; private set; }

        /// <summary>
        /// 从 ScriptableObject 加载配置
        /// </summary>
        public static void Initialize(TDebugSettings settings)
        {
            CurrentSettings = settings;
            TDebugConfig.ApplySettings(settings);
        }

        // ==========================================
        //  上下文管理
        // ==========================================

        /// <summary>当前上下文 Tag（只读）</summary>
        public static string ContextTag => TDebugConfig._contextTag;

        /// <summary>当前上下文 Level（只读，-1 表示未设置）</summary>
        public static int ContextLevel => TDebugConfig._contextLevel;

        /// <summary>当前是否启用文件日志</summary>
        public static bool FileLoggingEnabled
        {
            get => TDebugConfig._enableFileLogging;
            set => TDebugConfig._enableFileLogging = value;
        }

        /// <summary>
        /// 设置上下文 Tag，之后无参 Log 会使用此 Tag
        /// </summary>
        public static void SetTag(string tag)
        {
            TDebugConfig._contextTag = tag;
        }

        /// <summary>
        /// 清空上下文 Tag，之后无参 Log 退回到 DefaultTag
        /// </summary>
        public static void ClearTag()
        {
            TDebugConfig._contextTag = null;
        }

        /// <summary>
        /// 设置上下文 Level，之后无参 Log 会使用此 Level
        /// </summary>
        public static void SetLevel(int level)
        {
            TDebugConfig._contextLevel = level;
        }

        /// <summary>
        /// 重置上下文 Level，之后无参 Log 退回到 DefaultLevel
        /// </summary>
        public static void ResetLevel()
        {
            TDebugConfig._contextLevel = -1;
        }

        // ==========================================
        //  日志 — 无参（使用上下文 Tag / Level）
        // ==========================================

        /// <summary>
        /// 使用上下文 Tag 和 Level 输出日志
        /// </summary>
        public static void Log(object message)
        {
            string tag = TDebugConfig.GetEffectiveTag();
            int level = TDebugConfig.GetEffectiveLevel();

            if (!TDebugConfig.IsLogEnabled(level))
                return;

            WriteLog(tag, level, message?.ToString() ?? "null");
        }

        /// <summary>
        /// 使用上下文 Tag 和 Level 输出格式化日志
        /// </summary>
        public static void LogFormat(string format, params object[] args)
        {
            string tag = TDebugConfig.GetEffectiveTag();
            int level = TDebugConfig.GetEffectiveLevel();

            if (!TDebugConfig.IsLogEnabled(level))
                return;

            string msg = args is { Length: > 0 } ? string.Format(format, args) : format;
            WriteLog(tag, level, msg);
        }

        // ==========================================
        //  日志 — 显式参数
        // ==========================================

        /// <summary>
        /// 输出一条带 Tag 和 Level 的日志
        /// </summary>
        public static void Log(string tag, int level, object message)
        {
            if (!TDebugConfig.IsLogEnabled(level))
                return;

            WriteLog(tag, level, message?.ToString() ?? "null");
        }

        /// <summary>
        /// 输出一条带 Tag 和 Level 的格式化日志
        /// </summary>
        public static void LogFormat(string tag, int level, string format, params object[] args)
        {
            if (!TDebugConfig.IsLogEnabled(level))
                return;

            string msg = args is { Length: > 0 } ? string.Format(format, args) : format;
            WriteLog(tag, level, msg);
        }

        // ==========================================
        //  内部写入
        // ==========================================

        private static void WriteLog(string tag, int level, string message)
        {
            string unityLog = $"<color=#FFA500>[{tag}]</color> <color=#888888>[Lv{level}]</color> {message}";
            UnityEngine.Debug.Log(unityLog);

            if (TDebugConfig._enableFileLogging)
            {
                string fileLog = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{tag}] [Lv{level}] {message}";
                TDebugFileWriter.WriteLog(fileLog);
            }
        }

        // ==========================================
        //  快捷配置
        // ==========================================

        public static void SetEnable(bool enable)
        {
            TDebugConfig._enabled = enable;
        }

        public static bool IsEnabled => TDebugConfig._enabled;

        public static void SetMinLevel(int minLevel)
        {
            TDebugConfig._minLevel = minLevel;
        }

        public static int GetMinLevel()
        {
            return TDebugConfig._minLevel;
        }

        public static void SetLogDirectory(string path)
        {
            TDebugFileWriter.SetLogDirectory(path);
        }

        public static void SetMaxFileSizeKB(int sizeKB)
        {
            TDebugFileWriter.SetMaxFileSizeKB(sizeKB);
        }
    }
}
