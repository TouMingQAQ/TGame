// InitScenePickerWindow.cs
// 独立 EditorWindow,专用于选 / 清「初始场景」(Toolbar 启动按钮所用)。
// 由 QuickLaunchToolbar 弹起,选完 SceneAsset 即通过 QuickLaunchToolbar.OnInitSceneChanged
// 把路径写回 EditorPrefs + 触发 MainToolbar.Refresh 重建按钮文字。
//
// 交互:ObjectField 拖入场景即生效并关闭,Clear 按钮清空。
// 复用:EditorWindow.GetWindow 单例(重复点击 Toolbar 按钮复用同一窗口)。

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.EditorToolBar.BuiltIn
{
    public class InitScenePickerWindow : EditorWindow
    {
        private const string PrefKey = "SceneNavigator.InitScenePath";

        private ObjectField _sceneField;

        [MenuItem("Tools/Editor ToolBar/Set Init Scene")]
        public static void ShowMenu()
        {
            ShowWindow();
        }

        public static void ShowWindow()
        {
            var win = GetWindow<InitScenePickerWindow>(utility: false, title: "Init Scene", focus: true);
            win.minSize = new Vector2(320, 80);
            win.maxSize = new Vector2(600, 120);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;

            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                }
            };

            var label = new Label("初始场景:");
            label.style.width = 60;
            row.Add(label);

            _sceneField = new ObjectField
            {
                objectType = typeof(SceneAsset),
                allowSceneObjects = false,
                value = LoadCurrentAsset(),
            };
            _sceneField.style.flexGrow = 1;
            _sceneField.RegisterValueChangedCallback(OnSceneChanged);
            row.Add(_sceneField);

            root.Add(row);

            // 提示行
            var help = new HelpBox(
                "从 Project 窗口拖入一个 SceneAsset,选完即生效并关闭本窗口。\n" +
                "清空 = 在 ObjectField 右侧 picker 里选 None,或点 Clear。",
                HelpBoxMessageType.Info);
            help.style.marginTop = 6;
            root.Add(help);

            // Clear 按钮
            var clearBtn = new Button(ClearAndClose) { text = "Clear" };
            clearBtn.style.marginTop = 4;
            clearBtn.style.height = 20;
            root.Add(clearBtn);
        }

        private void OnSceneChanged(ChangeEvent<Object> evt)
        {
            var asset = evt.newValue as SceneAsset;
            var path = asset != null ? AssetDatabase.GetAssetPath(asset) : "";
            QuickLaunchToolbar.OnInitSceneChanged(path);
            Close();
        }

        private void ClearAndClose()
        {
            if (_sceneField != null) _sceneField.SetValueWithoutNotify(null);
            QuickLaunchToolbar.OnInitSceneChanged(null);
            Close();
        }

        private static SceneAsset LoadCurrentAsset()
        {
            var path = EditorPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
    }
}
