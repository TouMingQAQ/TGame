// QuickLaunchToolbar.cs
// Unity 6000 官方 MainToolbarElement API：用 [MainToolbarElement] 静态方法向主工具栏注册 element。
//
// 现暴露 5 个 element：
//   InitScene  → "TGame/QuickLaunch/InitScene"   显示当前初始化场景，点击弹 InitScenePickerWindow
//   测试        → "TGame/QuickLaunch/Boot"        playModeStartScene = 初始化场景
//   场景        → "TGame/QuickLaunch/Scene"       下拉选目标场景（来源 SceneNavigatorProfile）
//   运行        → "TGame/QuickLaunch/Run"         playModeStartScene = 目标场景
//   设置        → "TGame/QuickLaunch/Settings"    打开 ToolbarSettingsWindow
//
// 配置来源：Assets/Resources/ToolbarConfig.asset（ScriptableObject）。
// 早先版本把状态放在 EditorPrefs 里，现在统一走配置文件，便于随工程提交与版本管理；
// 读取时若配置文件里没有值，会回退读一次旧的 EditorPrefs 键，做无痛迁移。

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TGame.EditorToolBar.BuiltIn
{
    public static class QuickLaunchToolbar
    {
        // ── Element 路径 ──
        private const string InitSceneElementPath = "TGame/QuickLaunch/InitScene";
        private const string SceneElementPath = "TGame/QuickLaunch/Scene";
        private const string BootElementPath = "TGame/QuickLaunch/Boot";
        private const string RunElementPath = "TGame/QuickLaunch/Run";
        private const string SettingsElementPath = "TGame/QuickLaunch/Settings";

        // ── 旧版 EditorPrefs 键，仅用于迁移 ──
        private const string LegacyInitScenePrefKey = "SceneNavigator.InitScenePath";
        private const string LegacyTargetScenePrefKey = "TGame/QuickLaunch.TargetScenePath";

        private const string ProfileResName = "SceneNavigatorProfile";

        private static ToolbarConfig Config => ToolbarConfig.Load();

        /// <summary>
        /// 读取初始化场景：配置文件优先，否则回退旧 EditorPrefs 并迁移进配置。
        /// </summary>
        private static string GetInitScenePath()
        {
            var config = Config;
            if (config != null && !string.IsNullOrEmpty(config.initScenePath))
                return config.initScenePath;

            // 迁移：旧数据还在 EditorPrefs 里
            string legacy = EditorPrefs.GetString(LegacyInitScenePrefKey, "");
            if (!string.IsNullOrEmpty(legacy) && config != null)
            {
                config.initScenePath = legacy;
                ToolbarConfigStore.Save(config);
                EditorPrefs.DeleteKey(LegacyInitScenePrefKey);
            }
            return config != null ? config.initScenePath : legacy;
        }

        /// <summary>
        /// 读取目标场景：配置文件优先，否则回退旧 EditorPrefs。
        /// </summary>
        private static string GetTargetScenePath()
        {
            var config = Config;
            if (config != null && !string.IsNullOrEmpty(config.targetScenePath))
                return config.targetScenePath;

            string legacy = EditorPrefs.GetString(LegacyTargetScenePrefKey, "");
            if (!string.IsNullOrEmpty(legacy) && config != null)
            {
                config.targetScenePath = legacy;
                ToolbarConfigStore.Save(config);
                EditorPrefs.DeleteKey(LegacyTargetScenePrefKey);
            }
            return config != null ? config.targetScenePath : legacy;
        }

        private static void SetInitScenePath(string path)
        {
            var config = ToolbarConfigStore.LoadOrCreate();
            if (config == null)
                return;
            config.initScenePath = path ?? "";
            ToolbarConfigStore.Save(config);
        }

        private static void SetTargetScenePath(string path)
        {
            var config = ToolbarConfigStore.LoadOrCreate();
            if (config == null)
                return;
            config.targetScenePath = path ?? "";
            ToolbarConfigStore.Save(config);
        }

        /// <summary>
        /// 外部（设置窗口 / 场景选择器）改完配置后刷新所有按钮。
        /// </summary>
        public static void RefreshAll()
        {
            MainToolbar.Refresh(InitSceneElementPath);
            MainToolbar.Refresh(SceneElementPath);
        }

        // ══════════════════════════════════════════════════════════════
        //  1. 初始化场景按钮
        // ══════════════════════════════════════════════════════════════
        [MainToolbarElement(InitSceneElementPath, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 0)]
        public static MainToolbarElement CreateInitSceneButton()
        {
            var content = new MainToolbarContent(
                GetInitSceneButtonText(),
                EditorGUIUtility.IconContent("SceneAsset Icon")?.image as Texture2D,
                "初始化场景：Build Settings 的启动场景，「测试」按钮用它进入 Play");
            return new MainToolbarButton(content, OpenInitScenePicker);
        }

        private static void OpenInitScenePicker()
        {
            InitScenePickerWindow.ShowWindow();
        }

        /// <summary>场景选择器回调。</summary>
        internal static void OnInitSceneChanged(string newPath)
        {
            SetInitScenePath(newPath);
            MainToolbar.Refresh(InitSceneElementPath);
        }

        // ══════════════════════════════════════════════════════════════
        //  2. 测试按钮
        // ══════════════════════════════════════════════════════════════
        [MainToolbarElement(BootElementPath, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 1)]
        public static MainToolbarElement CreateBootButton()
        {
            var content = new MainToolbarContent(
                "测试",
                EditorGUIUtility.IconContent("d_Play")?.image as Texture2D,
                "以初始化场景启动，启动后由业务代码跳回原场景，用于测试需要框架的场景");
            return new MainToolbarButton(content, RunBoot);
        }

        private static void RunBoot()
        {
            string initPath = GetInitScenePath();
            if (string.IsNullOrEmpty(initPath))
            {
                Debug.LogWarning("[EditorToolBar] 请先在「InitScene」按钮或「设置」里指定初始化场景。");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(initPath);
            if (asset == null)
            {
                Debug.LogError($"[EditorToolBar] 初始化场景文件不存在: {initPath}");
                return;
            }

            // 记住当前打开的场景，运行时由 GameBootstrapper 跳回
            var currentScenePath = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(currentScenePath))
                EditorPrefs.SetString(TGame.TCore.Runtime.GameBootstrapper.TargetSceneKey, currentScenePath);

            EditorSceneManager.playModeStartScene = asset;
            RegisterPlayModeCleanup();
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        // ══════════════════════════════════════════════════════════════
        //  3. 目标场景下拉
        // ══════════════════════════════════════════════════════════════
        [MainToolbarElement(SceneElementPath, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 2)]
        public static MainToolbarElement CreateSceneDropdown()
        {
            var content = new MainToolbarContent(
                GetTargetSceneButtonText(),
                null,
                "目标场景：「运行」按钮使用");
            return new MainToolbarDropdown(content, ShowTargetSceneMenu);
        }

        private static void ShowTargetSceneMenu(Rect dropDownRect)
        {
            var menu = new GenericMenu();
            string current = GetTargetScenePath();
            var profile = Resources.Load<TGame.SceneNavigator.SceneNavigatorProfile>(ProfileResName);

            bool any = false;
            if (profile != null && profile.scenes != null)
            {
                foreach (var entry in profile.scenes)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.scenePath))
                        continue;

                    any = true;
                    string displayName = string.IsNullOrEmpty(entry.alias)
                        ? Path.GetFileNameWithoutExtension(entry.scenePath)
                        : entry.alias;
                    string capturedPath = entry.scenePath;
                    menu.AddItem(new GUIContent(displayName), capturedPath == current, () =>
                    {
                        SetTargetScenePath(capturedPath);
                        MainToolbar.Refresh(SceneElementPath);
                    });
                }
            }

            if (!any)
            {
                menu.AddDisabledItem(new GUIContent("<SceneNavigatorProfile 为空>"));
                menu.AddSeparator("");
                string openPath = OpenScenePath();
                if (!string.IsNullOrEmpty(openPath))
                {
                    menu.AddItem(new GUIContent("使用当前打开的场景"), false, () =>
                    {
                        SetTargetScenePath(openPath);
                        MainToolbar.Refresh(SceneElementPath);
                    });
                }
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("打开设置..."), false, ToolbarSettingsWindow.ShowWindow);
            menu.DropDown(dropDownRect);
        }

        private static string OpenScenePath()
        {
            var active = SceneManager.GetActiveScene();
            return active.IsValid() ? active.path : "";
        }

        // ══════════════════════════════════════════════════════════════
        //  4. 运行按钮
        // ══════════════════════════════════════════════════════════════
        [MainToolbarElement(RunElementPath, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 3)]
        public static MainToolbarElement CreateRunButton()
        {
            var content = new MainToolbarContent(
                "运行",
                EditorGUIUtility.IconContent("d_Play")?.image as Texture2D,
                "直接运行目标场景（不做跳转）");
            return new MainToolbarButton(content, RunSelected);
        }

        private static void RunSelected()
        {
            string targetPath = GetTargetScenePath();
            if (string.IsNullOrEmpty(targetPath))
            {
                Debug.LogWarning("[EditorToolBar] 请先在「场景」下拉或「设置」里选择目标场景。");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath);
            if (asset == null)
            {
                Debug.LogError($"[EditorToolBar] 场景文件不存在: {targetPath}");
                return;
            }

            EditorSceneManager.playModeStartScene = asset;
            RegisterPlayModeCleanup();
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        // ══════════════════════════════════════════════════════════════
        //  5. 设置按钮
        // ══════════════════════════════════════════════════════════════
        [MainToolbarElement(SettingsElementPath, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 4)]
        public static MainToolbarElement CreateSettingsButton()
        {
            var content = new MainToolbarContent(
                "",
                EditorGUIUtility.IconContent("Settings")?.image as Texture2D,
                "导航栏设置：初始化场景 / 目标场景 / 按钮说明");
            return new MainToolbarButton(content, ToolbarSettingsWindow.ShowWindow);
        }

        // ── PlayMode 退出清理 ──
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
                EditorPrefs.DeleteKey(TGame.TCore.Runtime.GameBootstrapper.TargetSceneKey);
                EditorSceneManager.playModeStartScene = null;
            }
        }

        // ── 按钮文字 ──
        private static string GetInitSceneButtonText()
        {
            string path = GetInitScenePath();
            if (string.IsNullOrEmpty(path))
                return "Init: <未设置>";
            return "Init: " + Path.GetFileNameWithoutExtension(path);
        }

        private static string GetTargetSceneButtonText()
        {
            string path = GetTargetScenePath();
            if (string.IsNullOrEmpty(path))
                return "场景: <未选择>";
            return "场景: " + Path.GetFileNameWithoutExtension(path);
        }
    }
}
