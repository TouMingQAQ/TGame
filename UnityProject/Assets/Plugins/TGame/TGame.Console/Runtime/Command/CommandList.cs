using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TGame.Console.Command;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TGame.Console.Command
{
    [Command]
    public static class Screen
    {
        [CommandMethod(note:"截屏")]
        public static void Capture()
        {
            var time = DateTime.Now.ToString("yyyy-MM-dd");
            var filePath = UnityEngine.Application.dataPath.Replace("/Assets","") + "/ScreenCapture";
            var directoryInfo = new DirectoryInfo(filePath);
            if(!directoryInfo.Exists)
                directoryInfo.Create();
            filePath += $"/Unity_{UnityEngine.Application.companyName}_{UnityEngine.Application.version}_{time}.png";
            ScreenCapture.CaptureScreenshot(filePath);
            Debug.Log($"Capture:{filePath}");
        }
    }
    [Command]
    public static class Time
    {
        [CommandMethod(note:"设置时间缩放")]
        public static void SetTimeScale(
            [CommandParameter("S")]float timeScale)
        {
            UnityEngine.Time.timeScale = timeScale;
            UnityEngine.Debug.Log($"Set TimeScale:{UnityEngine.Time.timeScale}");
        }
        [CommandMethod(note:"查询时间缩放")]
        public static void TimeScale()
        {
            UnityEngine.Debug.Log($"TimeScale:{UnityEngine.Time.timeScale}");
        }

    }
    [Command]
    public static class Application
    {
        private const long Kb = 1024;
        private const long Mb = 1024 * 1024;
        [CommandMethod(note:"查询系统信息")]
        public static void System()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("DeviceModel:");
            sb.Append(SystemInfo.deviceModel);
            sb.Append("\n");
            sb.Append("DeviceType:");
            sb.Append(SystemInfo.deviceType);
            sb.Append("\n");
            sb.Append("DeviceName:");
            sb.Append(SystemInfo.deviceName);
            sb.Append("\n");
            sb.Append("DeviceID:");
            sb.Append(SystemInfo.deviceUniqueIdentifier);
            sb.Append("\n");
            sb.Append("SystemName:");
            sb.Append(SystemInfo.operatingSystem);
            sb.Append("\n");
            sb.Append("GPU:");
            sb.Append(SystemInfo.graphicsDeviceName);
            sb.Append("\n");
            sb.Append("GPUMemorySize:");
            sb.Append(SystemInfo.graphicsMemorySize);
            sb.Append("\n");
            sb.Append("SystemMemorySize:");
            sb.Append(SystemInfo.systemMemorySize);
            sb.Append("\n");
            sb.Append("CPUType:");
            sb.Append(SystemInfo.processorType);
            sb.Append("\n");
            UnityEngine.Debug.Log(sb.ToString());
        }

        public static IEnumerable<string> GetTipFrameRate() => new[] { "-1", "30", "60", "144", "200", "240" };
        [CommandMethod(note:"设置帧率")]
        public static void SetFrameRate(
            [CommandParameter("F",nameof(GetTipFrameRate))]
            int frameRate)
        {
            UnityEngine.Application.targetFrameRate = frameRate;
            UnityEngine.Debug.Log($"Set FrameRate:{frameRate}");
        }
        [CommandMethod(note:"查询帧率")]
        public static void FrameRate()
        {
            UnityEngine.Debug.Log($"Setting FrameRate:{UnityEngine.Application.targetFrameRate}");
        }
        [CommandMethod(note:"查询FPS")]
        public static void CurrentFPS()
        {
            float fps = 1 / UnityEngine.Time.deltaTime;
            UnityEngine.Debug.Log($"CurrentFPS:{fps}");
        }
        [CommandMethod(note:"查询所有路径")]
        public static void AllPath()
        {
            PersistentDataPath();
            DataPath();
            StreamingAssetsPath();
        }
        [CommandMethod(note:"查询路径")]
        public static void PersistentDataPath()
        {
            UnityEngine.Debug.Log($"PersistentDataPath:{UnityEngine.Application.persistentDataPath}");
        }
        [CommandMethod(note:"查询路径")]
        public static void DataPath()
        {
            UnityEngine.Debug.Log($"DataPath:{UnityEngine.Application.dataPath}");
        }
        [CommandMethod(note:"查询路径")]
        public static void StreamingAssetsPath()
        {
            UnityEngine.Debug.Log($"StreamingAssetsPath:{UnityEngine.Application.streamingAssetsPath}");
        }
        [CommandMethod(note:"退出游戏")]
        public static void ExitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}