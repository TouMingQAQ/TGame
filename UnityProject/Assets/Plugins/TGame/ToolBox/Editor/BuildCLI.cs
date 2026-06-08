using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TGame.ToolBox
{
    /// <summary>
    /// 命令行打包入口。
    /// 仅在 Unity -batchmode -quit 模式下调用，不包含任何 UI。
    ///
    /// 用法:  Unity.exe -batchmode -quit -executeMethod TGame.ToolBox.BuildCLI.Build
    ///            -profile "&lt;BuildProfileGUID|BuildProfileName&gt;"  必需
    ///            -outputPath "&lt;path&gt;"                 必需，输出路径
    ///            -buildOptions "Dev,Profiler,..."              可选，逗号分隔
    ///            -sceneList "&lt;a.unity,b.unity&gt;"        可选，逗号分隔场景路径
    ///            -autoIncrementBN 0|1                    可选，是否自增版本号
    /// </summary>
    public static class BuildCLI
    {
        public static void Build()
        {
            var args = ParseCommandLineArgs();

            // ---- 1. 校验必需参数 ----
            var profileRef = args.GetValueOrDefault("profile");
            if (string.IsNullOrEmpty(profileRef))
            {
                Debug.LogError("[BuildCLI] 缺少必需参数: -profile <GUID|Name>");
                EditorApplication.Exit(1);
                return;
            }

            // ---- 2. 查找 BuildProfile ----
            var profile = FindBuildProfile(profileRef);
            if (profile == null)
            {
                Debug.LogError($"[BuildCLI] 未找到 BuildProfile: \"{profileRef}\"");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[BuildCLI] 使用 BuildProfile: {profile.name}");

            // ---- 3. 填充场景 ----
            var scenes = GetScenes(args.GetValueOrDefault("scenelist"));
            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildCLI] 没有可用的场景 (sceneList 为空且 BuildSettings 中无启用场景)");
                EditorApplication.Exit(1);
                return;
            }
            profile.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();

            // ---- 4. 输出路径（必需） ----
            var outputPath = args.GetValueOrDefault("outputpath");
            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.LogError("[BuildCLI] 必须指定输出路径: -outputPath <path>");
                EditorApplication.Exit(1);
                return;
            }

            // ---- 5. 解析构建选项 ----
            var rawOptions = args.GetValueOrDefault("buildoptions");
            var buildOptionsFlags = ParseBuildOptions(rawOptions);

            // ---- 6. 自动递增版本号（可选） ----
            if (args.GetValueOrDefault("autoincrementbn") == "1")
            {
                var config = AssetDatabase.LoadAssetAtPath<BuildConfig>("Assets/Resources/BuildConfig.asset");
                var target = EditorUserBuildSettings.activeBuildTarget;
                AutoIncrementBuildNumber(target, config);
            }

            // ---- 7. CleanBuild: BuildOptions.CleanBuild 在 Unity 6 中已移除，需手动删除 ----
            if (!string.IsNullOrEmpty(rawOptions) && rawOptions.IndexOf("CleanBuild", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                else if (Directory.Exists(outputPath))
                    Directory.Delete(outputPath, true);
                Debug.Log("[BuildCLI] CleanBuild: 已删除旧输出");
            }

            // ---- 8. 执行构建 ----
            var buildOptions = new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = outputPath,
                options = buildOptionsFlags.options,
            };

            Debug.Log($"[BuildCLI] 开始构建...\n" +
                      $"  Profile: {profile.name}\n" +
                      $"  输出: {outputPath}\n" +
                      $"  场景: {string.Join(", ", scenes)}\n" +
                      $"  选项: {buildOptions.options}");

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(buildOptions);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildCLI] 构建异常: {ex.Message}\n{ex.StackTrace}");
                EditorApplication.Exit(1);
                return;
            }

            var result = report.summary.result;
            Debug.Log($"[BuildCLI] 构建完成: {result}");
            Debug.Log($"[BuildCLI] 输出路径: {outputPath}");
            Debug.Log($"[BuildCLI] 耗时: {report.summary.totalTime.TotalSeconds:F1}s");

            EditorApplication.Exit(result == BuildResult.Succeeded ? 0 : 1);
        }

        // ------------------------------------------------------------
        // 内部辅助
        // ------------------------------------------------------------

        private static BuildProfile FindBuildProfile(string nameOrGuid)
        {
            // 1) 按 GUID 查找
            if (nameOrGuid.Length == 32 && nameOrGuid.All(c => "0123456789abcdef".Contains(c)))
            {
                var path = AssetDatabase.GUIDToAssetPath(nameOrGuid);
                if (!string.IsNullOrEmpty(path))
                    return AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
            }

            // 2) 按资产路径查找 (含 .asset 扩展名)
            var assetPath = nameOrGuid;
            if (!assetPath.EndsWith(".asset"))
                assetPath += ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(assetPath);
            if (profile != null) return profile;

            // 3) 按名称查找 (遍历所有 BuildProfile)
            var guids = AssetDatabase.FindAssets($"t:{nameof(BuildProfile)}");
            foreach (var guid in guids)
            {
                var p = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null && p.name == nameOrGuid)
                    return p;
            }

            return null;
        }

        private static string[] GetScenes(string sceneListArg)
        {
            if (!string.IsNullOrEmpty(sceneListArg))
                return sceneListArg.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();

            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }

        private static BuildPlayerOptions ParseBuildOptions(string raw)
        {
            var options = new BuildPlayerOptions();
            if (string.IsNullOrEmpty(raw)) return options;

            foreach (var opt in raw.Split(','))
            {
                var trimmed = opt.Trim();
                switch (trimmed)
                {
                    case "Development":    options.options |= BuildOptions.Development;            break;
                    case "Profiler":            options.options |= BuildOptions.ConnectWithProfiler;    break;
                    case "DeepProfiling":  options.options |= BuildOptions.EnableDeepProfilingSupport; break;
                    case "ScriptsOnly":    options.options |= BuildOptions.BuildScriptsOnly;       break;
                    case "CleanBuild":     /* 在 Build 方法中手动处理 */                          break;
                    default:
                        Debug.LogWarning($"[BuildCLI] 未知构建选项: {trimmed}");
                        break;
                }
            }

            return options;
        }

        private static Dictionary<string, string> ParseCommandLineArgs()
        {
            var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var raw = Environment.GetCommandLineArgs();

            for (int i = 0; i < raw.Length; i++)
            {
                if (!raw[i].StartsWith("-")) continue;

                var key = raw[i].TrimStart('-').ToLowerInvariant();

                // 只提取我们关心的参数
                switch (key)
                {
                    case "profile":
                    case "outputpath":
                    case "buildoptions":
                    case "scenelist":
                    case "autoincrementbn":
                        if (i + 1 < raw.Length)
                        {
                            args[key] = raw[i + 1];
                            i++; // 跳过值
                        }
                        break;
                    // 忽略 -batchmode, -quit, -executeMethod, -nographics 等 Unity 标准参数
                }
            }

            return args;
        }

        private static void AutoIncrementBuildNumber(BuildTarget target, BuildConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[BuildCLI] 未找到 BuildConfig，跳过版本号自增");
                return;
            }

            if (target == BuildTarget.Android)
            {
                PlayerSettings.Android.bundleVersionCode++;
                Debug.Log($"[BuildCLI] Android bundleVersionCode -> {PlayerSettings.Android.bundleVersionCode}");
            }
            else if (target == BuildTarget.iOS)
            {
                if (int.TryParse(PlayerSettings.iOS.buildNumber, out var n))
                    PlayerSettings.iOS.buildNumber = (n + 1).ToString();
                else
                    PlayerSettings.iOS.buildNumber = "1";
                Debug.Log($"[BuildCLI] iOS buildNumber -> {PlayerSettings.iOS.buildNumber}");
            }
            else
            {
                var platformName = target switch
                {
                    BuildTarget.StandaloneWindows64 => "Windows",
                    BuildTarget.StandaloneOSX       => "macOS",
                    BuildTarget.StandaloneLinux64   => "Linux",
                    BuildTarget.WebGL               => "WebGL",
                    _ => target.ToString()
                };

                var entry = config.platformBuildNumbers.FirstOrDefault(p => p.platformName == platformName);
                if (entry == null)
                {
                    entry = new PlatformBuildNumber { platformName = platformName, buildNumber = 0 };
                    config.platformBuildNumbers.Add(entry);
                }
                entry.buildNumber++;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
                Debug.Log($"[BuildCLI] {platformName} buildNumber -> {entry.buildNumber}");
            }
        }
    }
}
