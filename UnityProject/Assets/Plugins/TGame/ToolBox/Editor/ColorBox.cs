using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    [Serializable]
    public class ColorEntry
    {
        public string Name;
        public Color Color;
        public string Hex => ColorUtility.ToHtmlStringRGB(Color);

        public ColorEntry(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }

    public class ColorBox : IToolBoxContentVisualElement
    {
        public static BoxRegistration Registration => new()
        {
            Name = "颜色工具箱",
            Group = "资源",
            Icon = "ColorPicker",
            Factory = () => new ColorBox().CreateContent()
        };

        private const string LibPath = "Assets/Resources/ColorLibrary.asset";

        private List<ColorEntry> _allColors;
        private List<ColorEntry> _filtered;
        private ColorLibrary _library;
        private VisualElement _grid;
        private VisualElement _diyGrid;
        private Color _pendingColor = Color.white;

        public VisualElement CreateContent()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingTop = 4;

            var title = new Label("颜色工具箱");
            title.AddToClassList("tbx-section-title");
            root.Add(title);

            if (_allColors == null)
                InitColors();
            if (_library == null)
                LoadLibrary();

            var searchField = new TextField();
            searchField.style.marginBottom = 6;
            searchField.RegisterValueChangedCallback(OnSearch);
            searchField.AddToClassList("tbx-form-field");
            root.Add(searchField);

            var instructions = new Label("点击色块复制 HEX 代码");
            instructions.AddToClassList("tbx-help-text");
            instructions.style.marginBottom = 6;
            root.Add(instructions);

            BuildDIYSection(root);
            BuildClassicSection(root);

            _filtered = new List<ColorEntry>(_allColors);
            RebuildGrid();

            return root;
        }

        private void OnSearch(ChangeEvent<string> evt)
        {
            var query = evt.newValue?.Trim().ToLower();
            if (string.IsNullOrEmpty(query))
            {
                _filtered = new List<ColorEntry>(_allColors);
            }
            else
            {
                _filtered = _allColors.Where(c =>
                    c.Name.ToLower().Contains(query) ||
                    c.Hex.ToLower().Contains(query)).ToList();
            }
            RebuildGrid();
        }

        private void RebuildGrid()
        {
            _grid.Clear();
            if (_filtered.Count == 0)
            {
                _grid.Add(new Label("无匹配颜色") { name = "empty-hint" });
                return;
            }
            foreach (var entry in _filtered)
                _grid.Add(BuildColorCard(entry, false, -1));
        }

        // --- DIY section ---

        private void BuildDIYSection(VisualElement root)
        {
            var foldout = new Foldout();
            foldout.text = "DIY 颜色";
            foldout.value = true;
            root.Add(foldout);

            var addRow = new VisualElement();
            addRow.style.flexDirection = FlexDirection.Row;
            addRow.style.marginBottom = 6;
            addRow.style.alignItems = Align.Center;
            foldout.Add(addRow);

            var colorPicker = new IMGUIContainer(() =>
            {
                _pendingColor = EditorGUILayout.ColorField(_pendingColor, GUILayout.Width(60));
            });
            colorPicker.style.width = 60;
            colorPicker.style.height = 20;
            colorPicker.style.marginRight = 4;
            addRow.Add(colorPicker);

            var nameField = new TextField();
            nameField.value = "";
            nameField.style.flexGrow = 1;
            nameField.style.marginRight = 4;
            addRow.Add(nameField);

            var addBtn = new Button(() =>
            {
                var name = nameField.value;
                if (string.IsNullOrWhiteSpace(name))
                    name = $"Color #{_library.Entries.Count + 1}";
                _library.Entries.Add(new ColorEntry(name, _pendingColor));
                nameField.value = "";
                SaveLibrary();
                RefreshDIYGrid();
            });
            addBtn.text = "添加";
            addBtn.AddToClassList("tbx-btn-secondary");
            addRow.Add(addBtn);

            _diyGrid = new VisualElement();
            _diyGrid.style.flexWrap = Wrap.Wrap;
            _diyGrid.style.flexDirection = FlexDirection.Row;
            _diyGrid.style.alignItems = Align.FlexStart;
            foldout.Add(_diyGrid);

            RefreshDIYGrid();
        }

        private void RefreshDIYGrid()
        {
            _diyGrid.Clear();

            if (_library == null || _library.Entries.Count == 0)
            {
                _diyGrid.Add(new Label("暂无自定义颜色，在上方添加") { name = "empty-hint" });
                return;
            }

            for (int i = 0; i < _library.Entries.Count; i++)
            {
                var index = i;
                _diyGrid.Add(BuildColorCard(_library.Entries[i], true, index));
            }
        }

        private void DeleteDIYColor(int index)
        {
            if (_library != null && index >= 0 && index < _library.Entries.Count)
            {
                _library.Entries.RemoveAt(index);
                SaveLibrary();
                RefreshDIYGrid();
            }
        }

        // --- classic section ---

        private void BuildClassicSection(VisualElement root)
        {
            _grid = new VisualElement();
            _grid.style.flexWrap = Wrap.Wrap;
            _grid.style.flexDirection = FlexDirection.Row;
            _grid.style.alignItems = Align.FlexStart;
            root.Add(_grid);
        }

        // --- color card ---

        private VisualElement BuildColorCard(ColorEntry entry, bool editable, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("tbx-color-card");

            var swatch = new VisualElement();
            swatch.AddToClassList("tbx-color-swatch");
            swatch.style.backgroundColor = entry.Color;
            card.Add(swatch);

           var nameLabel = new Label(entry.Name);
           nameLabel.AddToClassList("tbx-color-name");
           card.Add(nameLabel);

           var hexLabel = new Label($"#{entry.Hex}");
           hexLabel.AddToClassList("tbx-color-hex");
           hexLabel.style.color = entry.Color;
           card.Add(hexLabel);

           card.RegisterCallback<ClickEvent>(_ =>
            {
                EditorGUIUtility.systemCopyBuffer = $"#{entry.Hex}";
                ShowCopyFeedback(card, $"已复制 #{entry.Hex}");
            });

            return card;
        }

        private void ShowCopyFeedback(VisualElement target, string message)
        {
            var feedback = new Label(message);
            feedback.AddToClassList("tbx-copy-overlay");
            target.Add(feedback);

            target.schedule.Execute(() =>
            {
                if (feedback.parent != null)
                    feedback.parent.Remove(feedback);
            }).StartingIn(800);
        }

        // --- persistence ---

        private void LoadLibrary()
        {
            _library = AssetDatabase.LoadAssetAtPath<ColorLibrary>(LibPath);
            if (_library == null)
            {
                var dir = Path.GetDirectoryName(LibPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                _library = ScriptableObject.CreateInstance<ColorLibrary>();
                _library.Entries.Add(new ColorEntry("My Red", Color.red));
                _library.Entries.Add(new ColorEntry("My Cyan", Color.cyan));
                _library.Entries.Add(new ColorEntry("My Green", Color.green));
                AssetDatabase.CreateAsset(_library, LibPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=#66ccff>[Color]</color> 已创建 ColorLibrary 资产: {LibPath}");
            }
        }

        private void SaveLibrary()
        {
            if (_library != null)
            {
                EditorUtility.SetDirty(_library);
                AssetDatabase.SaveAssetIfDirty(_library);
            }
        }

        // --- built-in color palette ---

        private void InitColors()
        {
            _allColors = new List<ColorEntry>
            {
                // Red / Pink
                new("Indian Red", new Color(0.80f, 0.36f, 0.36f)),
                new("Light Coral", new Color(0.94f, 0.50f, 0.50f)),
                new("Salmon", new Color(0.98f, 0.50f, 0.45f)),
                new("Dark Salmon", new Color(0.91f, 0.59f, 0.48f)),
                new("Light Salmon", new Color(1.00f, 0.63f, 0.48f)),
                new("Crimson", new Color(0.86f, 0.08f, 0.24f)),
                new("Red", new Color(1.00f, 0.00f, 0.00f)),
                new("Fire Brick", new Color(0.70f, 0.13f, 0.13f)),
                new("Dark Red", new Color(0.55f, 0.00f, 0.00f)),
                new("Pink", new Color(1.00f, 0.75f, 0.80f)),
                new("Light Pink", new Color(1.00f, 0.71f, 0.76f)),
                new("Hot Pink", new Color(1.00f, 0.41f, 0.71f)),
                new("Deep Pink", new Color(1.00f, 0.08f, 0.58f)),
                new("Medium Violet Red", new Color(0.78f, 0.08f, 0.52f)),
                new("Pale Violet Red", new Color(0.86f, 0.44f, 0.58f)),

                // Orange
                new("Coral", new Color(1.00f, 0.50f, 0.31f)),
                new("Tomato", new Color(1.00f, 0.39f, 0.28f)),
                new("Orange Red", new Color(1.00f, 0.27f, 0.00f)),
                new("Dark Orange", new Color(1.00f, 0.55f, 0.00f)),
                new("Orange", new Color(1.00f, 0.65f, 0.00f)),
                new("Gold", new Color(1.00f, 0.84f, 0.00f)),

                // Yellow
                new("Yellow", new Color(1.00f, 1.00f, 0.00f)),
                new("Light Yellow", new Color(1.00f, 1.00f, 0.88f)),
                new("Lemon Chiffon", new Color(1.00f, 0.98f, 0.80f)),
                new("Light Goldenrod", new Color(0.93f, 0.87f, 0.51f)),
                new("Papaya Whip", new Color(1.00f, 0.94f, 0.84f)),
                new("Moccasin", new Color(1.00f, 0.89f, 0.71f)),
                new("Peach Puff", new Color(1.00f, 0.85f, 0.73f)),
                new("Pale Goldenrod", new Color(0.93f, 0.91f, 0.67f)),
                new("Khaki", new Color(0.94f, 0.90f, 0.55f)),
                new("Dark Khaki", new Color(0.74f, 0.72f, 0.42f)),

                // Green
                new("Green Yellow", new Color(0.68f, 1.00f, 0.18f)),
                new("Chartreuse", new Color(0.50f, 1.00f, 0.00f)),
                new("Lawn Green", new Color(0.49f, 0.99f, 0.00f)),
                new("Lime", new Color(0.00f, 1.00f, 0.00f)),
                new("Lime Green", new Color(0.20f, 0.80f, 0.20f)),
                new("Pale Green", new Color(0.60f, 0.98f, 0.60f)),
                new("Light Green", new Color(0.56f, 0.93f, 0.56f)),
                new("Medium Spring Green", new Color(0.00f, 0.98f, 0.60f)),
                new("Spring Green", new Color(0.00f, 1.00f, 0.50f)),
                new("Medium Sea Green", new Color(0.24f, 0.70f, 0.44f)),
                new("Sea Green", new Color(0.18f, 0.55f, 0.34f)),
                new("Forest Green", new Color(0.13f, 0.55f, 0.13f)),
                new("Green", new Color(0.00f, 0.50f, 0.00f)),
                new("Dark Green", new Color(0.00f, 0.39f, 0.00f)),
                new("Yellow Green", new Color(0.60f, 0.80f, 0.20f)),
                new("Olive Drab", new Color(0.42f, 0.56f, 0.14f)),
                new("Olive", new Color(0.50f, 0.50f, 0.00f)),
                new("Dark Olive Green", new Color(0.33f, 0.42f, 0.18f)),
                new("Medium Aquamarine", new Color(0.40f, 0.80f, 0.67f)),
                new("Aquamarine", new Color(0.50f, 1.00f, 0.83f)),

                // Cyan / Teal
                new("Aqua / Cyan", new Color(0.00f, 1.00f, 1.00f)),
                new("Light Cyan", new Color(0.88f, 1.00f, 1.00f)),
                new("Pale Turquoise", new Color(0.69f, 0.93f, 0.93f)),
                new("Turquoise", new Color(0.25f, 0.88f, 0.82f)),
                new("Medium Turquoise", new Color(0.28f, 0.82f, 0.80f)),
                new("Dark Turquoise", new Color(0.00f, 0.81f, 0.82f)),
                new("Teal", new Color(0.00f, 0.50f, 0.50f)),
                new("Dark Cyan", new Color(0.00f, 0.55f, 0.55f)),
                new("Light Sea Green", new Color(0.13f, 0.70f, 0.67f)),
                new("Cadet Blue", new Color(0.37f, 0.62f, 0.63f)),

                // Blue
                new("Powder Blue", new Color(0.69f, 0.88f, 0.90f)),
                new("Light Blue", new Color(0.68f, 0.85f, 0.90f)),
                new("Light Steel Blue", new Color(0.69f, 0.77f, 0.87f)),
                new("Steel Blue", new Color(0.27f, 0.51f, 0.71f)),
                new("Cornflower Blue", new Color(0.39f, 0.58f, 0.93f)),
                new("Deep Sky Blue", new Color(0.00f, 0.75f, 1.00f)),
                new("Dodger Blue", new Color(0.12f, 0.56f, 1.00f)),
                new("Royal Blue", new Color(0.25f, 0.41f, 0.88f)),
                new("Blue", new Color(0.00f, 0.00f, 1.00f)),
                new("Medium Blue", new Color(0.00f, 0.00f, 0.80f)),
                new("Dark Blue", new Color(0.00f, 0.00f, 0.55f)),
                new("Navy", new Color(0.00f, 0.00f, 0.50f)),
                new("Midnight Blue", new Color(0.10f, 0.10f, 0.44f)),

                // Purple / Violet
                new("Lavender", new Color(0.90f, 0.90f, 0.98f)),
                new("Thistle", new Color(0.85f, 0.75f, 0.85f)),
                new("Plum", new Color(0.87f, 0.63f, 0.87f)),
                new("Violet", new Color(0.93f, 0.51f, 0.93f)),
                new("Orchid", new Color(0.85f, 0.44f, 0.84f)),
                new("Magenta / Fuchsia", new Color(1.00f, 0.00f, 1.00f)),
                new("Medium Orchid", new Color(0.73f, 0.33f, 0.83f)),
                new("Medium Purple", new Color(0.58f, 0.44f, 0.86f)),
                new("Blue Violet", new Color(0.54f, 0.17f, 0.89f)),
                new("Dark Violet", new Color(0.58f, 0.00f, 0.83f)),
                new("Dark Orchid", new Color(0.60f, 0.20f, 0.80f)),
                new("Medium Slate Blue", new Color(0.48f, 0.41f, 0.93f)),
                new("Slate Blue", new Color(0.42f, 0.35f, 0.80f)),
                new("Dark Slate Blue", new Color(0.28f, 0.24f, 0.55f)),
                new("Rebecca Purple", new Color(0.40f, 0.20f, 0.60f)),
                new("Purple", new Color(0.50f, 0.00f, 0.50f)),
                new("Dark Magenta", new Color(0.55f, 0.00f, 0.55f)),
                new("Indigo", new Color(0.29f, 0.00f, 0.51f)),

                // Brown / Beige
                new("Cornsilk", new Color(1.00f, 0.97f, 0.86f)),
                new("Blanched Almond", new Color(1.00f, 0.92f, 0.80f)),
                new("Bisque", new Color(1.00f, 0.89f, 0.77f)),
                new("Navajo White", new Color(1.00f, 0.87f, 0.68f)),
                new("Wheat", new Color(0.96f, 0.87f, 0.70f)),
                new("Burly Wood", new Color(0.87f, 0.72f, 0.53f)),
                new("Tan", new Color(0.82f, 0.71f, 0.55f)),
                new("Rosy Brown", new Color(0.74f, 0.56f, 0.56f)),
                new("Sandy Brown", new Color(0.96f, 0.64f, 0.38f)),
                new("Goldenrod", new Color(0.85f, 0.65f, 0.13f)),
                new("Dark Goldenrod", new Color(0.72f, 0.53f, 0.04f)),
                new("Peru", new Color(0.80f, 0.52f, 0.25f)),
                new("Chocolate", new Color(0.82f, 0.41f, 0.12f)),
                new("Saddle Brown", new Color(0.55f, 0.27f, 0.07f)),
                new("Sienna", new Color(0.63f, 0.32f, 0.18f)),
                new("Brown", new Color(0.65f, 0.16f, 0.16f)),
                new("Maroon", new Color(0.50f, 0.00f, 0.00f)),

                // White / Gray
                new("White", new Color(1.00f, 1.00f, 1.00f)),
                new("Snow", new Color(1.00f, 0.98f, 0.98f)),
                new("Honeydew", new Color(0.94f, 1.00f, 0.94f)),
                new("Mint Cream", new Color(0.96f, 1.00f, 0.98f)),
                new("Azure", new Color(0.94f, 1.00f, 1.00f)),
                new("Alice Blue", new Color(0.94f, 0.97f, 1.00f)),
                new("Ghost White", new Color(0.97f, 0.97f, 1.00f)),
                new("White Smoke", new Color(0.96f, 0.96f, 0.96f)),
                new("Sea Shell", new Color(1.00f, 0.96f, 0.93f)),
                new("Beige", new Color(0.96f, 0.96f, 0.86f)),
                new("Old Lace", new Color(0.99f, 0.96f, 0.90f)),
                new("Floral White", new Color(1.00f, 0.98f, 0.94f)),
                new("Ivory", new Color(1.00f, 1.00f, 0.94f)),
                new("Light Gray", new Color(0.83f, 0.83f, 0.83f)),
                new("Silver", new Color(0.75f, 0.75f, 0.75f)),
                new("Dark Gray", new Color(0.66f, 0.66f, 0.66f)),
                new("Dim Gray", new Color(0.41f, 0.41f, 0.41f)),
                new("Gray", new Color(0.50f, 0.50f, 0.50f)),
                new("Slate Gray", new Color(0.44f, 0.50f, 0.56f)),
                new("Dark Slate Gray", new Color(0.18f, 0.31f, 0.31f)),
                new("Black", new Color(0.00f, 0.00f, 0.00f)),
            };
        }
    }
}




