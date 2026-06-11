// QuickLaunchToolbarItem.cs
// 合并在 Toolbar 右侧的 4 个 UI：快速启动场景 ObjectField + 启动按钮 + 运行场景下拉 + 运行按钮。
// 所有元素内联到一个 row container，避免宽度溢出 ToolbarZoneRightAlign。
//
// 行为：
//   "启动" → 快速启动场景（ObjectField）→ playModeStartScene + TGame_InitBoot_TargetScene → Play
//   "运行" → 下拉选中场景 → playModeStartScene → Play（不跳回）
//
// 反射读 SceneNavigatorProfile.scenes 列表填下拉选项。

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TGame.EditorToolBar.BuiltIn
{
    [CustomToolbarItem("toolbox.quicklaunch", "快速启动")]
    public class QuickLaunchToolbarItem : ICustomToolbarItem
    {
        private const string InitScenePrefKey = "SceneNavigator.InitScenePath";
        private const string TargetSceneKey = "TGame_InitBoot_TargetScene";

        // 下拉场景列表缓存
        private List<string> _sceneDisplayNames = new();
        private List<string> _scenePaths = new();
        private string _selectedPath = "";

        public string Id => "toolbox.quicklaunch";
        public string DisplayName => "快速启动";
        public ToolbarSlot DefaultSlot => ToolbarSlot.Center;
        public int DefaultOrder => 100;
        public float DefaultWidth => 0f;

        public VisualElement Build(CustomToolbarContext context, ToolbarItemConfig config)
        {
            RefreshSceneList();

            var row = new VisualElement
            {
                pickingMode = PickingMode.Position,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 18,
                    flexShrink = 0,
                }
            };

            // ── 1. 快速启动场景 ObjectField ──
            var objField = new ObjectField
            {
                objectType = typeof(SceneAsset),
                tooltip = "快速启动场景（需含 GameBootstrapper）",
                style = { minWidth = 100, maxWidth = 140, flexGrow = 0, flexShrink = 1, marginRight = 2 }
            };
            // 隐藏 label 节省空间
            objField.label = "";
            var savedPath = EditorPrefs.GetString(InitScenePrefKey, "");
            if (!string.IsNullOrEmpty(savedPath))
                objField.value = AssetDatabase.LoadAssetAtPath<SceneAsset>(savedPath);
            objField.RegisterValueChangedCallback(evt =>
            {
                var asset = evt.newValue as SceneAsset;
                if (asset != null)
                    EditorPrefs.SetString(InitScenePrefKey, AssetDatabase.GetAssetPath(asset));
                else
                    EditorPrefs.DeleteKey(InitScenePrefKey);
            });
            row.Add(objField);

            // ── 2. 启动按钮 ──
            var launchBtn = new Button(() => RunQuickLaunch(objField.value as SceneAsset))
            {
                tooltip = "启动快速启动场景；GameBootstrapper 跑完后跳回当前工作场景",
                style = { height = 18, flexShrink = 0, marginRight = 6 }
            };
            var launchIcon = new Image
            {
                image = EditorGUIUtility.IconContent("d_Play")?.image,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 14, height = 14, marginRight = 2 }
            };
            launchBtn.Add(launchIcon);
            launchBtn.Add(new Label("启动"));
            row.Add(launchBtn);

            // ── 3. 运行场景下拉 ──
            var choices = _sceneDisplayNames.Count > 0
                ? _sceneDisplayNames
                : new List<string> { "<未配置>" };
            var dropdown = new DropdownField(choices, 0)
            {
                tooltip = "选择要运行的场景",
                style = { minWidth = 80, maxWidth = 140, flexGrow = 0, flexShrink = 1, marginRight = 2 }
            };
            dropdown.label = "";
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int idx = dropdown.index;
                if (idx >= 0 && idx < _scenePaths.Count)
                    _selectedPath = _scenePaths[idx];
            });
            if (_scenePaths.Count > 0)
                _selectedPath = _scenePaths[0];
            row.Add(dropdown);

            // ── 4. 运行按钮 ──
            var runBtn = new Button(() => RunSelectedScene())
            {
                tooltip = "运行下拉选中的场景（不跳回）",
                style = { height = 18, flexShrink = 0 }
            };
            var runIcon = new Image
            {
                image = EditorGUIUtility.IconContent("d_Play")?.image,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 14, height = 14, marginRight = 2 }
            };
            runBtn.Add(runIcon);
            runBtn.Add(new Label("运行"));
            row.Add(runBtn);

            return row;
        }

        // ── 场景列表 ──
        private void RefreshSceneList()
        {
            _sceneDisplayNames.Clear();
            _scenePaths.Clear();
            var profile = SceneNavigatorReflection.LoadProfile();
            var scenes = SceneNavigatorReflection.ReadScenes(profile);
            foreach (var s in scenes)
            {
                _scenePaths.Add(s.ScenePath);
                _sceneDisplayNames.Add(s.DisplayName);
            }
        }

        // ── 快速启动 ──
        private static void RunQuickLaunch(SceneAsset initScene)
        {
            if (initScene == null)
            {
                Debug.LogWarning("[EditorToolBar] 请先在工具栏选择快速启动场景。");
                return;
            }
            var initPath = AssetDatabase.GetAssetPath(initScene);
            if (string.IsNullOrEmpty(initPath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(initPath) == null)
            {
                Debug.LogError($"[EditorToolBar] 快速启动场景不存在: {initPath}");
                return;
            }

            var currentScenePath = SceneManager.GetActiveScene().path;
            EditorPrefs.SetString(TargetSceneKey, currentScenePath);
            EditorSceneManager.playModeStartScene = initScene;
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        // ── 运行选中场景 ──
        private void RunSelectedScene()
        {
            if (_scenePaths.Count == 0 || string.IsNullOrEmpty(_selectedPath))
            {
                Debug.LogWarning("[EditorToolBar] 运行场景下拉未选择有效场景");
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(_selectedPath);
            if (asset == null)
            {
                Debug.LogError($"[EditorToolBar] 场景文件不存在: {_selectedPath}");
                return;
            }
            EditorSceneManager.playModeStartScene = asset;
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }
    }
}
