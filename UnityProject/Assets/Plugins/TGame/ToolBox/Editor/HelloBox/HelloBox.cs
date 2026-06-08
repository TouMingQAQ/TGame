using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    public class HelloBox : IToolBoxContentVisualElement
    {
        public static BoxRegistration Registration => new()
        {
            Name = "欢迎使用ToolBox",
            Group = "程序",
            Icon = "d_Help",
            Factory = () => new HelloBox().CreateContent()
        };

        private TextAsset _readme;
        private List<MdBlock> _blocks;

        private static readonly Color BgColor = new Color(0.18f, 0.20f, 0.22f);
        private static readonly Color CodeBg = new Color(0.12f, 0.13f, 0.15f);
        private static readonly Color H1Color = Color.white;
        private static readonly Color H2Color = new Color(0.7f, 0.85f, 1f);
        private static readonly Color BodyColor = new Color(0.82f, 0.82f, 0.82f);
        private static readonly Color CodeColor = new Color(0.6f, 0.85f, 0.6f);
        private static readonly Color SeparatorColor = new Color(0.3f, 0.3f, 0.3f);

        public VisualElement CreateContent()
        {
            LoadAssets();

            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 10;

            DrawBlocks(root);
            return root;
        }

        private void LoadAssets()
        {
            if (_readme == null)
            {
                _readme = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/Plugins/TGame/ToolBox/Editor/HelloBox/README.md");
                if (_readme != null)
                    ParseMarkdown(_readme.text);
            }
        }

        private void DrawBlocks(VisualElement content)
        {
            if (_blocks == null) return;

            foreach (var block in _blocks)
            {
                switch (block.Type)
                {
                    case MdType.H1:
                        content.Add(new VisualElement { style = { height = 6 } });
                        content.Add(MakeLabel(block.Text, 18, FontStyle.Bold, H1Color));
                        content.Add(MakeSeparator());
                        content.Add(new VisualElement { style = { height = 4 } });
                        break;

                    case MdType.H2:
                        content.Add(new VisualElement { style = { height = 8 } });
                        content.Add(MakeLabel(block.Text, 14, FontStyle.Bold, H2Color));
                        content.Add(new VisualElement { style = { height = 2 } });
                        break;

                    case MdType.Body:
                        content.Add(MakeLabel(block.Text, 12, FontStyle.Normal, BodyColor));
                        break;

                    case MdType.ListItem:
                        content.Add(MakeListItem(block));
                        break;

                    case MdType.Table:
                        content.Add(MakeTableRow(block.Text));
                        break;

                    case MdType.CodeBlock:
                        content.Add(MakeCodeBlock(block.CodeLines));
                        break;

                    case MdType.Separator:
                        content.Add(MakeSeparator());
                        content.Add(new VisualElement { style = { height = 4 } });
                        break;

                    case MdType.Space:
                        content.Add(new VisualElement { style = { height = 4 } });
                        break;
                }
            }
        }

        private static Label MakeLabel(string text, int fontSize, FontStyle fontStyle, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = fontStyle;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static VisualElement MakeListItem(MdBlock block)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.Add(new VisualElement { style = { width = block.Indent + 20 } });

            var bullet = new Label("•");
            bullet.style.width = 12;
            bullet.style.fontSize = 12;
            bullet.style.color = BodyColor;
            row.Add(bullet);

            row.Add(MakeLabel(block.Text, 12, FontStyle.Normal, BodyColor));
            return row;
        }

        private static VisualElement MakeTableRow(string line)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var cells = line.Split('|');
            foreach (var cell in cells)
            {
                var trimmed = cell.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (Regex.IsMatch(trimmed, @"^[\-\s:]+$")) continue;

                var label = new Label(trimmed);
                label.style.width = 80;
                label.style.fontSize = 12;
                label.style.color = BodyColor;
                row.Add(label);
            }

            return row;
        }

        private static VisualElement MakeCodeBlock(List<string> codeLines)
        {
            var container = new VisualElement();
            container.AddToClassList("tbx-code-block");

            var sb = new StringBuilder();
            foreach (var line in codeLines)
                sb.AppendLine(line);

            var label = new Label(sb.ToString().TrimEnd());
            label.style.fontSize = 12;
            label.style.color = CodeColor;
            label.style.whiteSpace = WhiteSpace.Normal;
            container.Add(label);

            return container;
        }

        private static VisualElement MakeSeparator()
        {
            var sep = new VisualElement();
            sep.AddToClassList("tbx-separator");
            return sep;
        }

        #region Markdown Parser

        private void ParseMarkdown(string text)
        {
            _blocks = new List<MdBlock>();
            var lines = text.Split('\n');
            bool inCode = false;
            var codeLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd();

                if (line.StartsWith("```"))
                {
                    if (inCode)
                    {
                        _blocks.Add(new MdBlock { Type = MdType.CodeBlock, CodeLines = codeLines });
                        codeLines = new List<string>();
                    }
                    inCode = !inCode;
                    continue;
                }

                if (inCode)
                {
                    codeLines.Add(line);
                    continue;
                }

                if (string.IsNullOrEmpty(line))
                {
                    _blocks.Add(new MdBlock { Type = MdType.Space });
                    continue;
                }

                if (line.StartsWith("# "))
                {
                    _blocks.Add(new MdBlock { Type = MdType.H1, Text = InlineToRich(line.Substring(2)) });
                    continue;
                }
                if (line.StartsWith("## "))
                {
                    _blocks.Add(new MdBlock { Type = MdType.H2, Text = InlineToRich(line.Substring(3)) });
                    continue;
                }

                if (line.Trim() == "---" || line.Trim() == "***")
                {
                    _blocks.Add(new MdBlock { Type = MdType.Separator });
                    continue;
                }

                if (line.StartsWith("|"))
                {
                    _blocks.Add(new MdBlock { Type = MdType.Table, Text = InlineToRich(line) });
                    continue;
                }

                if (Regex.IsMatch(line, @"^\s*[\-\*]\s"))
                {
                    var indent = line.Length - line.TrimStart().Length;
                    var content = Regex.Replace(line.TrimStart(), @"^[\-\*]\s+", "");
                    _blocks.Add(new MdBlock { Type = MdType.ListItem, Text = InlineToRich(content), Indent = indent });
                    continue;
                }

                _blocks.Add(new MdBlock { Type = MdType.Body, Text = InlineToRich(line) });
            }
        }

        private string InlineToRich(string text)
        {
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<b>$1</b>");
            text = Regex.Replace(text, @"`([^`]+)`",
                m => $"<color=#{ColorUtility.ToHtmlStringRGB(CodeColor)}>{m.Groups[1].Value}</color>");
            text = Regex.Replace(text, @"\*(.+?)\*", "<i>$1</i>");
            return text;
        }

        #endregion

        private enum MdType { H1, H2, Body, ListItem, Table, CodeBlock, Separator, Space }

        private struct MdBlock
        {
            public MdType Type;
            public string Text;
            public List<string> CodeLines;
            public int Indent;
        }
    }
}

