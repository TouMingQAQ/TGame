// QuickLaunchToolbar.cs
// Unity 6000 官方 MainToolbarElement API:用 [MainToolbarElement] 静态方法向主工具栏注册 element。
// 本类暴露 4 个独立 element:InitScene 设置 / 目标场景选择 / 启动 / 运行。
//
//   InitScene  → "TGame/QuickLaunch/InitScene"  MainToolbarButton,点击弹 InitScenePickerWindow,
//                                              选完场景后按钮文字变为 "Init: <场景名>"
//   场景       → "TGame/QuickLaunch/Scene"      MainToolbarDropdown,列出 SceneNavigatorProfile.scenes
//   启动       → "TGame/QuickLaunch/Boot"       MainToolbarButton "▶ 启动",playModeStartScene=InitScene
//   运行       → "TGame/QuickLaunch/Run"        MainToolbarButton "▶ 运行",playModeStartScene=目标场景
//
// 启动语义:把当前 ActiveScene 路径存到 EditorPrefs[GameBootstrapper.TargetSceneKey],
// playModeStartScene=InitScene → Play,运行时 GameBootstrapper 读 TargetScene 跳回。
// 运行语义:playModeStartScene=目标场景 → Play(不跳回)。
//
// 退出 Play 时清理 playModeStartScene 和 TargetSceneKey(从 SceneNavigatorBox 抄来,保持一致)。

using System.IO;
using TGame.SceneNavigator;
using TGame.TCore.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TGame.EditorToolBar.BuiltIn
{
    public static class QuickLaunchToolbar
    {
        // ── 路径常量 ──
        private const string InitSceneElementPath  = "TGame/QuickLaunch/InitScene";
        private const string SceneElementPath      = "TGame/QuickLaunch/Scene";
        private const string BootElementPath       = "TGame/QuickLaunch/Boot";
        private const string RunElementPath        = "TGame/QuickLaunch/Run";

        private const string InitScenePrefKey      = "SceneNavigator.InitScenePath";
        private const string TargetScenePathKey    = "TGame/QuickLaunch.TargetScenePath";

        private const string ProfileResName        = "SceneNavigatorProfile";
        private const string ProfileResFolder      = "Assets/Plugins/TGame/SceneNavigator/Resources";
        private const string ProfileFullPath       = ProfileResFolder + "/" + ProfileResName + ".asset";

        // ── 状态(静态 — 因为 MainToolbarElement 是静态方法,无法持有实例字段) ──
        private static string _initScenePath = LoadInitScenePath();
        private static string _targetScenePath = EditorPrefs.GetString(TargetScenePathKey, "");

        // ── Profile 加载 ──
        private static SceneNavigatorProfile LoadProfile()
        {
            var profile = Resources.Load<SceneNavigatorProfile>(ProfileResName);
            if (profile != null) return profile;
            EnsureProfileExists();
            return Resources.Load<SceneNavigatorProfile>(ProfileResName);
        }

        private static void EnsureProfileExists()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneNavigatorProfile>(ProfileFullPath) != null) return;
            if (!AssetDatabase.IsValidFolder(ProfileResFolder))
            {
                const string parent = "Assets/Plugins/TGame/SceneNavigator";
                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets/Plugins/TGame", "SceneNavigator");
                AssetDatabase.CreateFolder(parent, "Resources");
            }
            var profile = ScriptableObject.CreateInstance<SceneNavigatorProfile>();
            AssetDatabase.CreateAsset(profile, ProfileFullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ══════════════════════════════════════════════════════════════
        //  1. InitScene 按钮 — 点击弹 InitScenePickerWindow
        // ══════════════════════════════════════════════════════════════
        [MainToolbarElement(InitSceneElementPath, defaultDockPosition = MainToolbarDockPosition.Middle,defaultDockIndex = 2)]
        public static MainToolbarElement CreateInitSceneButton()
        {
            var content = new MainToolbarContent(
                GetInitSceneButtonText(),
                EditorGUIUtility.IconContent("SceneAsset Icon")?.image as Texture2D,
                "点击设置初始场景(用于「启动」按钮)");
            return new MainToolbarButton(content, OpenInitScenePicker);
        }

        private static void OpenInitScenePicker()
        {
            InitScenePickerWindow.ShowWindow();
        }

        /// <summary>外部(InitScenePickerWindow)选完场景后回调:更新状态 + 持久化 + 通知 Unity 重建按钮。</summary>
        internal static void OnInitSceneChanged(string newPath)
        {
            _initScenePath = newPath ?? "";
            if (string.IsNullOrEmpty(_initScenePath))
            {
                EditorPrefs.DeleteKey(InitScenePrefKey);
            }
            else
            {
                EditorPrefs.SetString(InitScenePrefKey, _initScenePath);
            }
            MainToolbar.Refresh(InitSceneElementPath);
        }

        // ══════════════════════════════════════════════════════════════
        //  2. 目标场景 下拉
        // ══════════════════════════════════════════════════════════════
        [MainToolbarElement(SceneElementPath, defaultDockPosition = MainToolbarDockPosition.Middle,defaultDockIndex = 3)]
        public static MainToolbarElement CreateSceneDropdown()
        {
            var content = new MainToolbarContent(
                GetTargetSceneButtonText(),
                null,
                "目标场景:用于「运行」按钮");
            return new MainToolbarDropdown(content, ShowTargetSceneMenu);
        }

        private static void ShowTargetSceneMenu(Rect dropDownRect)
        {
            var menu = new GenericMenu();
            var profile = LoadProfile();
            if (profile == null || profile.scenes == null || profile.scenes.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("<Profile 为空,请先添加场景>"));
            }
            else
            {
                for (int i = 0; i < profile.scenes.Count; i++)
                {
                    var entry = profile.scenes[i];
                    if (entry == null || string.IsNullOrEmpty(entry.scenePath)) continue;
                    var displayName = string.IsNullOrEmpty(entry.alias)
                        ? Path.GetFileNameWithoutExtension(entry.scenePath)
                        : entry.alias;
                    var isCurrent = entry.scenePath == _targetScenePath;
                    var capturedPath = entry.scenePath;
                    menu.AddItem(new GUIContent(displayName), isCurrent, () =>
                    {
                        _targetScenePath = capturedPath;
                        EditorPrefs.SetString(TargetScenePathKey, capturedPath);
                        MainToolbar.Refresh(SceneElementPath);
                    });
                }
            }
            menu.DropDown(dropDownRect);
        }

        // ══════════════════════════════════════════════════════════════
        //  3. 启动 按钮
        // ══════════════════════════════════════════════════════════════
        [MainToolbarElement(BootElementPath, defaultDockPosition = MainToolbarDockPosition.Middle,defaultDockIndex = 1)]
        public static MainToolbarElement CreateBootButton()
        {
            var content = new MainToolbarContent(
                "测试",
                EditorGUIUtility.IconContent("d_Play")?.image as Texture2D,
                "用 InitScene 作为 playModeStartScene 启动,启动后由业务代码跳回原场景，主要用于测试需要框架的场景");
            return new MainToolbarButton(content, RunBoot);
        }

        private static void RunBoot()
        {
            if (string.IsNullOrEmpty(_initScenePath))
            {
                Debug.LogWarning("[EditorToolBar] 请先在「InitScene」按钮中设置初始场景。");
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(_initScenePath);
            if (asset == null)
            {
                Debug.LogError($"[EditorToolBar] 初始场景文件不存在: {_initScenePath}");
                return;
            }

            // 保存当前场景路径,运行时 GameBootstrapper 读它跳回
            var currentScenePath = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(currentScenePath))
            {
                EditorPrefs.SetString(GameBootstrapper.TargetSceneKey, currentScenePath);
            }
            EditorSceneManager.playModeStartScene = asset;
            RegisterPlayModeCleanup();
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        // ══════════════════════════════════════════════════════════════
        //  4. 运行 按钮
        // ══════════════════════════════════════════════════════════════
        [MainToolbarElement(RunElementPath, defaultDockPosition = MainToolbarDockPosition.Middle,defaultDockIndex = 3)]
        public static MainToolbarElement CreateRunButton()
        {
            var content = new MainToolbarContent(
                "运行",
                EditorGUIUtility.IconContent("d_Play")?.image as Texture2D,
                "直接运行目标场景(不跳回)");
            return new MainToolbarButton(content, RunSelected);
        }

        private static void RunSelected()
        {
            if (string.IsNullOrEmpty(_targetScenePath))
            {
                Debug.LogWarning("[EditorToolBar] 请先在「场景」下拉中选择目标场景。");
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(_targetScenePath);
            if (asset == null)
            {
                Debug.LogError($"[EditorToolBar] 场景文件不存在: {_targetScenePath}");
                return;
            }
            EditorSceneManager.playModeStartScene = asset;
            RegisterPlayModeCleanup();
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        // ── PlayMode 退出清理(从 SceneNavigatorBox.OnPlayModeStateChanged 抄来,保持一致) ──
        private static void RegisterPlayModeCleanup()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorPrefs.DeleteKey(GameBootstrapper.TargetSceneKey);
                EditorSceneManager.playModeStartScene = null;
            }
        }

        // ── 按钮文字辅助 ──
        private static string GetInitSceneButtonText()
        {
            if (string.IsNullOrEmpty(_initScenePath)) return "Init: <未设置>";
            return "Init: " + Path.GetFileNameWithoutExtension(_initScenePath);
        }

        private static string GetTargetSceneButtonText()
        {
            if (string.IsNullOrEmpty(_targetScenePath)) return "场景: <未选择>";
            return "场景: " + Path.GetFileNameWithoutExtension(_targetScenePath);
        }

        private static string LoadInitScenePath()
        {
            return EditorPrefs.GetString(InitScenePrefKey, "");
        }
    }
}
