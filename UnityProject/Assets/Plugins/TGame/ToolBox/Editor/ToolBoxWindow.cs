using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TGame.ToolBox
{
    public class ToolBoxWindow : EditorWindow
    {
        private const float MinSidebarWidth = 120f;
        private const float MaxSidebarWidth = 400f;
        private const float DividerWidth = 4f;

        private List<TabEntry> _tabs = new();
        private int _selectedIndex = -1;
        private float _sidebarWidth;
        private bool _isDraggingDivider;
        private Vector2 _sidebarScrollPos;
        private Vector2 _contentScrollPos;

        private GUIStyle _tabButtonStyle;
        private GUIStyle _selectedTabStyle;

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
            _sidebarWidth = EditorPrefs.GetFloat("ToolBox.SidebarWidth", 200f);
            _sidebarWidth = Mathf.Clamp(_sidebarWidth, MinSidebarWidth, MaxSidebarWidth);

            RefreshTabs();
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat("ToolBox.SidebarWidth", _sidebarWidth);
            DestroyEditors();
        }

        private void EnsureStyles()
        {
            if (_tabButtonStyle != null) return;

            _tabButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                fixedHeight = 0,
                stretchHeight = true,
                normal = { textColor = Color.white }
            };

            _selectedTabStyle = new GUIStyle(_tabButtonStyle)
            {
                normal =
                {
                    background = MakeColorTex(new Color(0.24f, 0.48f, 0.90f)),
                    textColor = Color.white
                },
                fontStyle = FontStyle.Bold
            };
        }

        private static Texture2D MakeColorTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
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
            _contentScrollPos = Vector2.zero;
            Repaint();
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

        private void OnGUI()
        {
            var totalWidth = position.width;
            var totalHeight = position.height;

            var sidebarRect = new Rect(0, 0, _sidebarWidth, totalHeight);
            var dividerRect = new Rect(_sidebarWidth, 0, DividerWidth, totalHeight);
            var contentRect = new Rect(_sidebarWidth + DividerWidth, 0,
                totalWidth - _sidebarWidth - DividerWidth, totalHeight);

            DrawSidebar(sidebarRect);
            HandleDivider(dividerRect);
            DrawContent(contentRect);
        }

        private void DrawSidebar(Rect rect)
        {
            EnsureStyles();
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);

            var innerRect = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);

            GUILayout.BeginArea(innerRect);
            _sidebarScrollPos = EditorGUILayout.BeginScrollView(_sidebarScrollPos);

            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                var style = i == _selectedIndex ? _selectedTabStyle : _tabButtonStyle;

                if (GUILayout.Button(tab.Attribute.Name, style, GUILayout.Height(30)))
                    SelectTab(i);
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void HandleDivider(Rect rect)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            var color = _isDraggingDivider
                ? new Color(0.3f, 0.6f, 0.9f, 1f)
                : new Color(0.3f, 0.3f, 0.3f, 1f);
            EditorGUI.DrawRect(rect, color);

            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (rect.Contains(e.mousePosition))
                    {
                        _isDraggingDivider = true;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (_isDraggingDivider)
                    {
                        _isDraggingDivider = false;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (_isDraggingDivider)
                    {
                        _sidebarWidth += e.delta.x;
                        _sidebarWidth = Mathf.Clamp(_sidebarWidth, MinSidebarWidth, Mathf.Min(MaxSidebarWidth, position.width - 80));
                        Repaint();
                        e.Use();
                    }
                    break;
            }
        }

        private void DrawContent(Rect rect)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _tabs.Count)
            {
                GUILayout.BeginArea(rect);
                EditorGUILayout.HelpBox(
                    "没有可用的 ToolBox 标签。\n\n为类添加 [ToolBox(\"名称\")] 特性即可在此显示。",
                    MessageType.Info);
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(rect);
            _contentScrollPos = EditorGUILayout.BeginScrollView(_contentScrollPos);

            var entry = _tabs[_selectedIndex];
            DrawEntryContent(entry);

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
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
            else if (entry.Instance is IToolBoxContent content)
            {
                content.DrawContent();
            }
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
