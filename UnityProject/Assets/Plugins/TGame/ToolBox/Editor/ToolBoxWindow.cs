using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    public class ToolBoxWindow : EditorWindow
    {
        private List<TabEntry> _tabs = new();
        private int _selectedIndex = -1;
        private Vector2 _imguiContentScrollPos;

        private TwoPaneSplitView _splitView;
        private VisualElement _contentContainer;
        private List<Button> _sidebarButtons = new();

        [MenuItem("Tools/ToolBox")]
        private static void Open()
        {
            var window = GetWindow<ToolBoxWindow>("ToolBox");
            window.minSize = new Vector2(400, 300);
            var pos = window.position;
            pos.width = 800;
            pos.height = 600;
            window.position = pos;
            window.Show();
        }

        private void OnEnable()
        {
            RefreshTabs();
        }

        private void OnDisable()
        {
            SaveSplitterPosition();
            DestroyEditors();
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

            if (_tabs.Count > 0)
            {
                if (_selectedIndex < 0)
                    _selectedIndex = 0;
                SelectTab(_selectedIndex);
            }
            else
            {
                ShowEmptyState();
            }
        }

        private void BuildSidebar(ScrollView scrollView)
        {
            _sidebarButtons.Clear();
            scrollView.Clear();

            for (int i = 0; i < _tabs.Count; i++)
            {
                var index = i;
                var button = new Button(() => SelectTab(index))
                {
                    text = _tabs[i].Attribute.Name,
                    name = $"tab-{i}"
                };
                button.AddToClassList("sidebar-tab-button");
                scrollView.Add(button);
                _sidebarButtons.Add(button);
            }

            UpdateSidebarSelection();
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

        private void RefreshTabs()
        {
            DestroyEditors();
            _tabs.Clear();

            var types = TypeCache.GetTypesWithAttribute<ToolBoxAttribute>();
            foreach (var type in types)
            {
                var attr = type.GetCustomAttributes(typeof(ToolBoxAttribute), false)
                    .Cast<ToolBoxAttribute>().First();

                var entry = new TabEntry
                {
                    Type = type,
                    Attribute = attr
                };

                CreateInstance(entry);
                _tabs.Add(entry);
            }

            _tabs = _tabs.OrderBy(t => t.Attribute.Order).ThenBy(t => t.Attribute.Name).ToList();

            if (_tabs.Count > 0 && _selectedIndex < 0)
                _selectedIndex = 0;
        }

        private void CreateInstance(TabEntry entry)
        {
            if (typeof(ScriptableObject).IsAssignableFrom(entry.Type))
            {
                if (!string.IsNullOrEmpty(entry.Attribute.Path))
                {
                    var so = AssetDatabase.LoadAssetAtPath(entry.Attribute.Path, entry.Type) as ScriptableObject;
                    if (so != null)
                    {
                        entry.Instance = so;
                        entry.IsTemporary = false;
                    }
                    else
                    {
                        entry.Instance = ScriptableObject.CreateInstance(entry.Type);
                        entry.IsTemporary = true;
                    }
                }
                else
                {
                    entry.Instance = ScriptableObject.CreateInstance(entry.Type);
                    entry.IsTemporary = true;
                }
            }
            else
            {
                entry.Instance = Activator.CreateInstance(entry.Type);
            }
        }

        private void SelectTab(int index)
        {
            if (_selectedIndex == index) return;

            if (_selectedIndex >= 0 && _selectedIndex < _tabs.Count)
            {
                var oldEntry = _tabs[_selectedIndex];
                if (oldEntry.Editor != null)
                {
                    DestroyImmediate(oldEntry.Editor);
                    oldEntry.Editor = null;
                }
            }

            _selectedIndex = index;
            _imguiContentScrollPos = Vector2.zero;

            _contentContainer.Clear();
            ShowContent(_tabs[_selectedIndex]);
            UpdateSidebarSelection();
        }

        private void ShowContent(TabEntry entry)
        {
            if (entry.Instance is IToolBoxContentVisualElement veContent)
            {
                var scrollView = new ScrollView();
                scrollView.Add(veContent.CreateContent());
                _contentContainer.Add(scrollView);
            }
            else if (entry.Instance is ScriptableObject)
            {
                var container = new IMGUIContainer(() =>
                {
                    _imguiContentScrollPos = EditorGUILayout.BeginScrollView(_imguiContentScrollPos);
                    DrawEntryContent(entry);
                    EditorGUILayout.EndScrollView();
                });
                _contentContainer.Add(container);
            }
            else if (entry.Instance is IToolBoxContent content)
            {
                var container = new IMGUIContainer(() =>
                {
                    _imguiContentScrollPos = EditorGUILayout.BeginScrollView(_imguiContentScrollPos);
                    content.DrawContent();
                    EditorGUILayout.EndScrollView();
                });
                _contentContainer.Add(container);
            }
        }

        private void DrawEntryContent(TabEntry entry)
        {
            if (entry.Instance is ScriptableObject so)
            {
                if (entry.Editor == null)
                    entry.Editor = Editor.CreateEditor(so);

                if (entry.IsTemporary)
                {
                    EditorGUILayout.HelpBox("此 SO 为临时实例（未找到资产文件），修改不会保存。",
                        MessageType.Warning);
                }

                entry.Editor.OnInspectorGUI();
            }
        }

        private void DestroyEditors()
        {
            foreach (var entry in _tabs)
            {
                if (entry.Editor != null)
                {
                    DestroyImmediate(entry.Editor);
                    entry.Editor = null;
                }
            }
        }

        private void ShowEmptyState()
        {
            var label = new Label("没有可用的 ToolBox 标签。\n\n为类添加 [ToolBox(\"名称\")] 特性即可在此显示。");
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = 10;
            label.style.marginTop = 10;
            label.style.color = Color.gray;
            _contentContainer.Add(label);
        }

        private void SaveSplitterPosition()
        {
            if (_splitView == null) return;
            var leftPane = _splitView.Children().FirstOrDefault();
            if (leftPane != null)
                EditorPrefs.SetFloat("ToolBox.SidebarWidth", leftPane.resolvedStyle.width);
        }

        private class TabEntry
        {
            public Type Type;
            public ToolBoxAttribute Attribute;
            public object Instance;
            public bool IsTemporary;
            public Editor Editor;
        }
    }
}
