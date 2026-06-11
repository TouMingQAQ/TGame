// CustomToolbarSettings.cs
// 全局配置 ScriptableObject。保存到 Assets/Plugins/TGame/EditorToolBar/CustomToolbarSettings.asset。
// 用户编辑的"启用 / 槽位 / 顺序 / 宽度 / 参数"全部在这里。

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TGame.EditorToolBar
{
    public class CustomToolbarSettings : ScriptableObject
    {
        public List<ToolbarItemConfig> Items = new();

        public const string DefaultAssetPath =
            "Assets/Plugins/TGame/EditorToolBar/CustomToolbarSettings.asset";

        /// <summary>
        /// 加载或创建 Settings 资产。资产不存在时自动建一份空配置。
        /// </summary>
        public static CustomToolbarSettings GetOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CustomToolbarSettings>(DefaultAssetPath);
            if (existing != null) return existing;

            // 确保目录存在
            var dir = Path.GetDirectoryName(DefaultAssetPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var settings = CreateInstance<CustomToolbarSettings>();
            AssetDatabase.CreateAsset(settings, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EditorToolBar] Created default settings at {DefaultAssetPath}");
            return settings;
        }
    }

    /// <summary>
    /// 通用参数读取扩展。避免每个组件重复写解析代码。
    /// </summary>
    public static class ToolbarItemConfigExtensions
    {
        public static string GetString(this ToolbarItemConfig config, string key, string defaultValue = "")
        {
            if (config?.Parameters == null) return defaultValue;
            for (int i = 0; i < config.Parameters.Count; i++)
            {
                if (config.Parameters[i].Key == key)
                {
                    var v = config.Parameters[i].Value;
                    return string.IsNullOrEmpty(v) ? defaultValue : v;
                }
            }
            return defaultValue;
        }

        public static int GetInt(this ToolbarItemConfig config, string key, int defaultValue = 0)
        {
            return int.TryParse(config.GetString(key), out var v) ? v : defaultValue;
        }

        public static float GetFloat(this ToolbarItemConfig config, string key, float defaultValue = 0f)
        {
            return float.TryParse(config.GetString(key), out var v) ? v : defaultValue;
        }

        public static bool GetBool(this ToolbarItemConfig config, string key, bool defaultValue = false)
        {
            return bool.TryParse(config.GetString(key), out var v) ? v : defaultValue;
        }

        public static void SetParam(this ToolbarItemConfig config, string key, string value)
        {
            if (config.Parameters == null) config.Parameters = new List<ToolbarItemParameter>();
            for (int i = 0; i < config.Parameters.Count; i++)
            {
                if (config.Parameters[i].Key == key)
                {
                    config.Parameters[i].Value = value;
                    return;
                }
            }
            config.Parameters.Add(new ToolbarItemParameter { Key = key, Value = value });
        }
    }
}
