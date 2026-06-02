using System;
using System.IO;
using UnityEngine;

namespace TGame
{
    /// <summary>
    /// TDebug 文件日志写入器
    /// 默认路径：Application.persistentDataPath/Logs/TDebug.log
    /// 单文件写入，超过上限后自动轮转（重命名为 .1 并新建）
    /// </summary>
    internal static class TDebugFileWriter
    {
        private static StreamWriter _writer;
        private static string _logDirectory;
        private static string _logFilePath;
        private static long _maxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
        private static readonly object _lock = new();
        private static bool _initialized;

        internal static void SetLogDirectory(string path)
        {
            lock (_lock)
            {
                CloseWriter();
                _logDirectory = path;
                _logFilePath = null;
                _initialized = false;
            }
        }

        internal static void SetMaxFileSizeKB(int sizeKB)
        {
            lock (_lock)
            {
                _maxFileSizeBytes = sizeKB * 1024L;
            }
        }

        internal static void WriteLog(string formattedMessage)
        {
            lock (_lock)
            {
                EnsureInitialized();
                if (_writer == null)
                    return;

                try
                {
                    _writer.WriteLine(formattedMessage);
                    _writer.Flush();

                    if (_writer.BaseStream.Length >= _maxFileSizeBytes)
                        RotateLogFile();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[TDebug] Failed to write log file: {ex.Message}");
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            try
            {
                if (string.IsNullOrEmpty(_logDirectory))
                    _logDirectory = Path.Combine(Application.persistentDataPath, "Logs");

                Directory.CreateDirectory(_logDirectory);

                if (string.IsNullOrEmpty(_logFilePath))
                    _logFilePath = Path.Combine(_logDirectory, "TDebug.log");

                _writer = new StreamWriter(_logFilePath, append: true);
                _initialized = true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TDebug] Failed to initialize log file: {ex.Message}");
            }
        }

        private static void RotateLogFile()
        {
            CloseWriter();

            try
            {
                string backupPath = _logFilePath + ".1";
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Move(_logFilePath, backupPath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[TDebug] Failed to rotate log file: {ex.Message}");
            }

            _initialized = false;
            EnsureInitialized();
        }

        private static void CloseWriter()
        {
            if (_writer != null)
            {
                try
                {
                    _writer.Flush();
                    _writer.Close();
                    _writer.Dispose();
                }
                catch { }
                _writer = null;
            }
            _initialized = false;
        }
    }
}
