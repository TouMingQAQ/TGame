// ToolbarSettingsWindow.cs
// 顶部导航栏的配置窗口。
//
// 打开方式：点工具栏右侧的「设置」按钮。
// 功能：
//   1. 选择「初始化场景」（同时可一键同步到 Build Settings，工程约定 Build Settings 只放它）；
//   2. 选择「目标场景」（「运行」按钮用）；
//   3. 展示四个按钮的作用说明（文案来自 ToolbarConfig.helpText，可在 Inspector 里改）。
//
// 配置资源不存在时会自动创建到 Assets/Resources/ToolbarConfig.asset。

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.EditorToolBar.BuiltIn
{
    public class ToolbarSettingsWindow : EditorWindow
    {
        private const int LabelWidth = 110;

        private ToolbarConfig _config;
        private ObjectField _initSceneField;
        private ObjectField _targetSceneField;
        private Label _buildSettingsLabel;
        private Label _configPathLabel;

        [MenuItem("TGame/Toolbar Settings", false, 100)]
        public static void ShowWindow()
        {
            var win = GetWindow<ToolbarSettingsWindow>(false, "Toolbar Settings", true);
            win.minSize = new Vector2(460, 420);
            win.RebuildGUI();
        }

        /// <summary>
        /// 重新构建界面。EditorWindow 是单例，重复 ShowWindow 不会触发 CreateGUI，
        /// 所以配置被外部改动（或被删除重建）后需要手动重建一次。
        /// </summary>
        public void RebuildGUI()
        {
            rootVisualElement.Clear();
            CreateGUI();
        }

        private void CreateGUI()
        {
            // 每次打开都重新取配置；不存在则自动创建
            _config = ToolbarConfigStore.LoadOrCreate();
            if (_config == null)
            {
                rootVisualElement.Add(new HelpBox("无法创建 ToolbarConfig，请检查 Assets/Resources 是否可写。",
                    HelpBoxMessageType.Error));
                return;
            }

            var root = rootVisualElement;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;

            // ── 配置资源位置 ──
            _configPathLabel = new Label();
            _configPathLabel.style.marginBottom = 8;
            _configPathLabel.style.color = new Color(0.65f, 0.65f, 0.65f);
            root.Add(_configPathLabel);
            RefreshConfigPathLabel();

            var locateBtn = new Button(() =>
            {
                EditorGUIUtility.PingObject(_config);
                Selection.activeObject = _config;
            })
            { text = "定位配置文件" };
            locateBtn.style.alignSelf = Align.FlexStart;
            locateBtn.style.marginBottom = 10;
            root.Add(locateBtn);

            // ── 初始化场景 ──
            root.Add(BuildSectionTitle("初始化场景（Build Settings 启动场景）"));

            _initSceneField = new ObjectField
            {
                objectType = typeof(SceneAsset),
                allowSceneObjects = false,
                value = LoadSceneAsset(_config.initScenePath)
            };
            _initSceneField.RegisterValueChangedCallback(evt =>
            {
                var asset = evt.newValue as SceneAsset;
                _config.initScenePath = asset != null ? AssetDatabase.GetAssetPath(asset) : "";
                ToolbarConfigStore.Save(_config);
                QuickLaunchToolbar.RefreshAll();
                RefreshBuildSettingsLabel();
            });
            root.Add(BuildRow("Scene", _initSceneField));

            var syncBtn = new Button(() =>
            {
                if (!ToolbarConfigStore.SyncBuildSettings(_config))
                    EditorUtility.DisplayDialog("Toolbar Settings",
                        "请先选择一个有效的初始化场景。", "OK");
                else
                    RefreshBuildSettingsLabel();
            })
            { text = "同步到 Build Settings（只保留此场景）" };
            syncBtn.style.marginTop = 4;
            syncBtn.style.alignSelf = Align.FlexStart;
            root.Add(syncBtn);

            _buildSettingsLabel = new Label();
            _buildSettingsLabel.style.marginTop = 4;
            _buildSettingsLabel.style.marginBottom = 12;
            _buildSettingsLabel.style.color = new Color(0.65f, 0.65f, 0.65f);
            root.Add(_buildSettingsLabel);
            RefreshBuildSettingsLabel();

            // ── 目标场景 ──
            root.Add(BuildSectionTitle("目标场景（「运行」按钮使用）"));

            _targetSceneField = new ObjectField
            {
                objectType = typeof(SceneAsset),
                allowSceneObjects = false,
                value = LoadSceneAsset(_config.targetScenePath)
            };
            _targetSceneField.RegisterValueChangedCallback(evt =>
            {
                var asset = evt.newValue as SceneAsset;
                _config.targetScenePath = asset != null ? AssetDatabase.GetAssetPath(asset) : "";
                ToolbarConfigStore.Save(_config);
                QuickLaunchToolbar.RefreshAll();
            });
            root.Add(BuildRow("Scene", _targetSceneField));

            // ── 按钮说明（只读展示，不可编辑）──
            root.Add(BuildSectionTitle("按钮说明"));

            var helpText = string.IsNullOrEmpty(_config.helpText)
                ? BuiltinHelpText
                : _config.helpText;

            var helpBox = new HelpBox(helpText, HelpBoxMessageType.Info);
            helpBox.style.whiteSpace = WhiteSpace.Normal;
            helpBox.style.marginTop = 2;
            helpBox.style.marginBottom = 8;
            root.Add(helpBox);
        }

        /// <summary>
        /// 内置按钮说明。放在这里而不是 ScriptableObject 里，
        /// 避免资源序列化时把换行与特殊字符写坏。
        /// </summary>
        private const string BuiltinHelpText =
            "[测试] 以「初始化场景」作为启动场景进入 Play。\n" +
            "用于测试依赖框架的环境：Game 单例是否就绪、各 Manager 是否注册完成。\n" +
            "进入前会记住当前打开的场景，启动后由 GameBootstrapper 跳回。\n" +
            "\n" +
            "[场景: xxx] 选择「目标场景」。列表来自 SceneNavigatorProfile，只影响「运行」。\n" +
            "\n" +
            "[运行] 直接运行「目标场景」，不做跳转。\n" +
            "适合单独调试某一个白盒场景，不需要框架启动流程。\n" +
            "\n" +
            "[设置] 打开本配置：修改初始化场景、同步 Build Settings、查看说明。";

        #region UI 辅助

        private static VisualElement BuildSectionTitle(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 6;
            label.style.marginBottom = 4;
            return label;
        }

        private static VisualElement BuildRow(string labelText, VisualElement field)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var label = new Label(labelText);
            label.style.width = LabelWidth;
            row.Add(label);

            field.style.flexGrow = 1;
            row.Add(field);
            return row;
        }

        private static SceneAsset LoadSceneAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }

        private void RefreshConfigPathLabel()
        {
            var path = AssetDatabase.GetAssetPath(_config);
            _configPathLabel.text = "配置文件: " + (string.IsNullOrEmpty(path) ? ToolbarConfigStore.DefaultAssetPath : path);
        }

        private void RefreshBuildSettingsLabel()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                _buildSettingsLabel.text = "Build Settings 当前为空。";
                return;
            }

            var names = new List<string>();
            foreach (var s in scenes)
            {
                if (s.enabled)
                    names.Add(Path.GetFileNameWithoutExtension(s.path));
            }
            _buildSettingsLabel.text = "Build Settings 当前: " + string.Join(", ", names);
        }

        #endregion
    }
}
