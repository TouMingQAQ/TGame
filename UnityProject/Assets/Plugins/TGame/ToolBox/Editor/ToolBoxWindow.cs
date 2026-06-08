using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    public class ToolBoxWindow : EditorWindow
    {
        [SerializeField] private string _windowGroup;

        private List<BoxRegistration> _filteredBoxes = new();
        private int _selectedIndex = -1;

        private TwoPaneSplitView _splitView;
        private VisualElement _contentContainer;
        private List<Button> _sidebarButtons = new();

        #region Group Definitions

        private static readonly Dictionary<string, (string title, List<BoxRegistration> boxes)> _groups = new()
        {
            ["程序"] = ("程序工具", new List<BoxRegistration>
            {
                HelloBox.Registration,
                PathBox.Registration,
                DebugBox.Registration,
                SceneNavigator.SceneNavigatorBox.Registration,
            }),
            ["资源"] = ("资源工具", new List<BoxRegistration>
            {
                ColorBox.Registration,
                AnimationCurveBox.Registration,
            }),
            ["构建"] = ("构建工具", new List<BoxRegistration>
            {
                BuildBox.Registration,
            }),
        };

        #endregion

        #region Menu Items

        [MenuItem("Tools/ToolBox/程序")]
        private static void OpenProgram() => OpenGroup("程序");

        [MenuItem("Tools/ToolBox/资源")]
        private static void OpenAssets() => OpenGroup("资源");

        [MenuItem("Tools/ToolBox/构建")]
        private static void OpenBuild() => OpenGroup("构建");

        private static void OpenGroup(string group)
        {
            if (!_groups.TryGetValue(group, out var def)) return;

            var window = ScriptableObject.CreateInstance<ToolBoxWindow>();
            window.titleContent = new GUIContent(def.title);
            window._windowGroup = group;
            window._filteredBoxes = def.boxes;
            window.minSize = new Vector2(400, 300);
            window.position = new Rect(100, 100, 800, 600);
            window.Show();
        }

        #endregion

        private void OnEnable()
        {
            // Restore boxes after domain reload (script recompilation)
            if (!string.IsNullOrEmpty(_windowGroup) && _groups.TryGetValue(_windowGroup, out var def))
                _filteredBoxes = def.boxes;
        }

        private void OnDisable()
        {
            SaveSplitterPosition();
        }

        private void CreateGUI()
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Plugins/TGame/ToolBox/Editor/ToolBoxWindow.uss");
            if (sheet != null)
                rootVisualElement.styleSheets.Add(sheet);

            var savedWidth = EditorPrefs.GetFloat("ToolBox.SidebarWidth", 200f);

            _splitView = new TwoPaneSplitView(0, savedWidth, TwoPaneSplitViewOrientation.Horizontal);
            rootVisualElement.Add(_splitView);

            var sidebar = new VisualElement { name = "sidebar" };
            var scrollView = new ScrollView();
            sidebar.Add(scrollView);
            _splitView.Add(sidebar);

            _contentContainer = new VisualElement { name = "content-area" };
            _splitView.Add(_contentContainer);

            BuildSidebar(scrollView);

            if (_filteredBoxes.Count > 0)
            {
                _selectedIndex = 0;
                SelectBox(_selectedIndex);
            }
        }

        private void BuildSidebar(ScrollView scrollView)
        {
            _sidebarButtons.Clear();
            scrollView.Clear();

            for (int i = 0; i < _filteredBoxes.Count; i++)
            {
                var index = i;
                var reg = _filteredBoxes[i];

                var btn = new Button();
                btn.name = $"box-{i}";
                btn.AddToClassList("sidebar-tab-button");

                var icon = new Image();
                icon.image = GetIcon(reg.Icon);
                icon.style.width = 16;
                icon.style.height = 16;
                icon.style.marginRight = 6;
                btn.Add(icon);

                var label = new Label(reg.Name);
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.flexGrow = 1;
                btn.Add(label);

                btn.clicked += () => SelectBox(index);
                scrollView.Add(btn);
                _sidebarButtons.Add(btn);
            }

            UpdateSidebarSelection();
        }

        private static Texture2D GetIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;
            var content = EditorGUIUtility.IconContent(iconName);
            if (content?.image != null)
                return content.image as Texture2D;
            if (iconName.StartsWith("d_"))
            {
                content = EditorGUIUtility.IconContent(iconName[2..]);
                return content?.image as Texture2D;
            }
            return null;
        }

        private void UpdateSidebarSelection()
        {
            for (int i = 0; i < _sidebarButtons.Count; i++)
            {
                if (i == _selectedIndex)
                    _sidebarButtons[i].AddToClassList("sidebar-tab-button--selected");
                else
                    _sidebarButtons[i].RemoveFromClassList("sidebar-tab-button--selected");
            }
        }

        private void SelectBox(int index)
        {
            if (index < 0 || index >= _filteredBoxes.Count) return;

            _selectedIndex = index;
            _contentContainer.Clear();
            ShowContent(_filteredBoxes[_selectedIndex]);
            UpdateSidebarSelection();
        }

        private void ShowContent(BoxRegistration reg)
        {
            var wrapper = new VisualElement();
            wrapper.style.flexGrow = 1;

            var header = new VisualElement();
            header.AddToClassList("tbx-content-header");

            var title = new Label(reg.Name);
            title.AddToClassList("tbx-content-header-label");
            header.Add(title);
            wrapper.Add(header);

            var scrollView = new ScrollView();
            scrollView.Add(reg.Factory());
            wrapper.Add(scrollView);

            _contentContainer.Add(wrapper);
        }

        private void SaveSplitterPosition()
        {
            if (_splitView == null) return;
            var leftPane = _splitView.Children().FirstOrDefault();
            if (leftPane != null)
                EditorPrefs.SetFloat("ToolBox.SidebarWidth", leftPane.resolvedStyle.width);
        }
    }
}
