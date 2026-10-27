// ToolbarConfigStore.cs
// ToolbarConfig 的创建 / 加载 / 保存 + Build Settings 同步。
// 单独抽出来，让「工具栏」和「设置窗口」共用同一套逻辑，避免两处各写一遍。
//
// 路径策略：
//   优先 Assets/Resources/ToolbarConfig.asset（Resources.Load 能取到）；
//   同名的其它 Resources 目录（如 Assets/Plugins/TGame/.../Resources）也能被 Load 到，但
//   本工具只在 Assets/Resources 下创建，避免多处出现占位资源。

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TGame.EditorToolBar.BuiltIn
{
    public static class ToolbarConfigStore
    {
        /// <summary>默认创建位置。</summary>
        public const string DefaultFolder = "Assets/Resources";
        public const string DefaultAssetPath = DefaultFolder + "/" + ToolbarConfig.ResourceName + ".asset";

        /// <summary>
        /// 加载配置，不存在则自动创建（点「设置」时用）。
        /// </summary>
        public static ToolbarConfig LoadOrCreate()
        {
            var config = ToolbarConfig.Load();
            if (config != null)
                return config;

            config = CreateAsset();
            return config;
        }

        /// <summary>
        /// 只在已有配置时加载，不创建。
        /// </summary>
        public static ToolbarConfig LoadIfExists()
        {
            return ToolbarConfig.Load();
        }

        /// <summary>
        /// 在默认目录创建 ToolbarConfig 资源。
        /// </summary>
        public static ToolbarConfig CreateAsset()
        {
            EnsureFolder(DefaultFolder);

            // 已存在同名资源时直接返回，避免重复 CreateAsset
            var exist = AssetDatabase.LoadAssetAtPath<ToolbarConfig>(DefaultAssetPath);
            if (exist != null)
                return exist;

            var config = ScriptableObject.CreateInstance<ToolbarConfig>();
            config.initScenePath = GuessInitScene();
            config.targetScenePath = GuessTargetScene();

            AssetDatabase.CreateAsset(config, DefaultAssetPath);
            EditorUtility.SetDirty(config);

            // 先保存、再强制导入，确保资源真的落到磁盘上
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(DefaultAssetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log("<color=#66ccff>[ToolbarConfig]</color> 新建配置: " + DefaultAssetPath);
            return config;
        }

        /// <summary>
        /// 保存资源并刷新工具栏。
        /// </summary>
        public static void Save(ToolbarConfig config)
        {
            if (config == null)
                return;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 用配置里的初始化场景重写 Build Settings。
        /// 约定：整个工程只有初始化场景进 Build Settings，其余走 Addressables。
        /// </summary>
        /// <returns>成功返回 true，路径非法返回 false。</returns>
        public static bool SyncBuildSettings(ToolbarConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.initScenePath))
                return false;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(config.initScenePath) == null)
            {
                Debug.LogError("[ToolbarConfig] 初始化场景不存在: " + config.initScenePath);
                return false;
            }

            var current = EditorBuildSettings.scenes;
            if (current.Length == 1
                && current[0].path == config.initScenePath
                && current[0].enabled)
            {
                return true;
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(config.initScenePath, true)
            };
            Debug.Log("<color=#66ccff>[ToolbarConfig]</color> Build Settings 已同步为: " + config.initScenePath);
            return true;
        }

        /// <summary>
        /// Build Settings 里第一个场景，作为初始化场景的默认值。
        /// </summary>
        public static string GuessInitScene()
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled && !string.IsNullOrEmpty(scenes[i].path))
                    return scenes[i].path;
            }
            return "";
        }

        /// <summary>
        /// 目标场景默认值：优先 SceneNavigatorProfile 第一项，否则回退到 Build Settings 之外看到的场景。
        /// </summary>
        public static string GuessTargetScene()
        {
            var profile = Resources.Load<TGame.SceneNavigator.SceneNavigatorProfile>(
                SceneNavigatorProfileName);
            if (profile != null && profile.scenes != null)
            {
                foreach (var entry in profile.scenes)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.scenePath))
                        return entry.scenePath;
                }
            }
            return "";
        }

        private const string SceneNavigatorProfileName = "SceneNavigatorProfile";

        /// <summary>
        /// 工程内所有场景路径（供设置窗口的下拉使用）。
        /// </summary>
        public static List<string> GetAllScenePaths()
        {
            var result = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/"))
                    result.Add(path);
            }
            result.Sort(System.StringComparer.Ordinal);
            return result;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
