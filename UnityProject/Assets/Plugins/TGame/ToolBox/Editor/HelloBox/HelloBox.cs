using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TGame.ToolBox
{
    [ToolBox("欢迎使用ToolBox")]
    public class HelloBox : IToolBoxContent
    {
        private Texture2D _bgImage;
        private TextAsset _readme;
        private List<MdBlock> _blocks;
        private Vector2 _scrollPos;

        private static readonly Color BgColor = new Color(0.18f, 0.20f, 0.22f, 1f);
        private static readonly Color CodeBg = new Color(0.12f, 0.13f, 0.15f, 1f);
        private static readonly Color H1Color = Color.white;
        private static readonly Color H2Color = new Color(0.7f, 0.85f, 1f);
        private static readonly Color BodyColor = new Color(0.82f, 0.82f, 0.82f);
        private static readonly Color CodeColor = new Color(0.6f, 0.85f, 0.6f);
        private static readonly Color SeparatorColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        public void DrawContent()
        {
            LoadAssets();

            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(rect, BgColor);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            GUILayout.Space(10);
            DrawBlocks();
            GUILayout.FlexibleSpace();
            DrawFooter();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

        private void LoadAssets()
        {
            if (_bgImage == null)
                _bgImage = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/Plugins/TGame/ToolBox/Editor/HelloBox/躲在墙后偷看.png");

            if (_readme == null)
            {
                _readme = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/Plugins/TGame/ToolBox/Editor/HelloBox/README.md");
                if (_readme != null)
                    ParseMarkdown(_readme.text);
            }
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

                // 代码块边界
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

                // 空行
                if (string.IsNullOrEmpty(line))
                {
                    _blocks.Add(new MdBlock { Type = MdType.Space });
                    continue;
                }

                // 标题
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

                // 分割线
                if (line.Trim() == "---" || line.Trim() == "***")
                {
                    _blocks.Add(new MdBlock { Type = MdType.Separator });
                    continue;
                }

                // 表格
                if (line.StartsWith("|"))
                {
                    _blocks.Add(new MdBlock { Type = MdType.Table, Text = InlineToRich(line) });
                    continue;
                }

                // 列表
                if (Regex.IsMatch(line, @"^\s*[\-\*]\s"))
                {
                    var indent = line.Length - line.TrimStart().Length;
                    var content = Regex.Replace(line.TrimStart(), @"^[\-\*]\s+", "");
                    _blocks.Add(new MdBlock { Type = MdType.ListItem, Text = InlineToRich(content), Indent = indent });
                    continue;
                }

                // 普通段落
                _blocks.Add(new MdBlock { Type = MdType.Body, Text = InlineToRich(line) });
            }
        }

        /// <summary>
        /// 转换行内 Markdown 为 Unity Rich Text
        /// </summary>
        private string InlineToRich(string text)
        {
            // 粗体 **text**
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<b>$1</b>");
            // 行内代码 `code`
            text = Regex.Replace(text, @"`([^`]+)`",
                m => $"<color=#{ColorUtility.ToHtmlStringRGB(CodeColor)}>{m.Groups[1].Value}</color>");
            // 斜体 *text*
            text = Regex.Replace(text, @"\*(.+?)\*", "<i>$1</i>");
            return text;
        }

        #endregion

        #region Renderer

        private void DrawBlocks()
        {
            if (_blocks == null) return;

            var h1Style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                richText = true,
                normal = { textColor = H1Color }
            };
            var h2Style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                richText = true,
                normal = { textColor = H2Color }
            };
            var bodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                richText = true,
                wordWrap = true,
                normal = { textColor = BodyColor }
            };
            var codeStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                richText = true,
                wordWrap = true,
                normal = { textColor = CodeColor }
            };

            foreach (var block in _blocks)
            {
                switch (block.Type)
                {
                    case MdType.H1:
                        GUILayout.Space(6);
                        GUILayout.Label(block.Text, h1Style);
                        DrawSeparator();
                        GUILayout.Space(4);
                        break;

                    case MdType.H2:
                        GUILayout.Space(8);
                        GUILayout.Label(block.Text, h2Style);
                        GUILayout.Space(2);
                        break;

                    case MdType.Body:
                        GUILayout.Label(block.Text, bodyStyle);
                        break;

                    case MdType.ListItem:
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(block.Indent + 20);
                        bodyStyle.richText = false;
                        GUILayout.Label("•", bodyStyle, GUILayout.Width(12));
                        bodyStyle.richText = true;
                        GUILayout.Label(block.Text, bodyStyle);
                        GUILayout.EndHorizontal();
                        break;

                    case MdType.Table:
                        DrawTableRow(block.Text, bodyStyle);
                        break;

                    case MdType.CodeBlock:
                        DrawCodeBlock(block.CodeLines, codeStyle);
                        break;

                    case MdType.Separator:
                        DrawSeparator();
                        GUILayout.Space(4);
                        break;

                    case MdType.Space:
                        GUILayout.Space(4);
                        break;
                }
            }
        }

        private void DrawTableRow(string line, GUIStyle style)
        {
            var cells = line.Split('|');
            if (cells.Length < 2) return;

            GUILayout.BeginHorizontal();
            foreach (var cell in cells)
            {
                var trimmed = cell.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // 跳过分隔行（只包含 - 和空格）
                if (Regex.IsMatch(trimmed, @"^[\-\s:]+$"))
                {
                    GUILayout.Label("", style, GUILayout.Width(80));
                    continue;
                }

                GUILayout.Label(trimmed, style, GUILayout.Width(80));
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCodeBlock(List<string> lines, GUIStyle style)
        {
            GUILayout.Space(4);
            var sb = new StringBuilder();
            foreach (var l in lines)
                sb.AppendLine(l);

            var content = new GUIContent(sb.ToString().TrimEnd());
            var height = style.CalcHeight(content, EditorGUIUtility.currentViewWidth - 60);
            var rect = GUILayoutUtility.GetRect(content, style, GUILayout.Height(height + 16));

            // 代码块背景
            EditorGUI.DrawRect(
                new Rect(rect.x + 10, rect.y, rect.width - 20, rect.height),
                CodeBg);

            // 代码文本
            var textRect = new Rect(rect.x + 20, rect.y + 8,
                rect.width - 40, rect.height - 16);
            EditorGUI.LabelField(textRect, content, style);

            GUILayout.Space(4);
        }

        private void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
            rect.height = 1;
            EditorGUI.DrawRect(rect, SeparatorColor);
        }

        private void DrawFooter()
        {
            if (_bgImage == null) return;

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            var prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUILayout.Label(_bgImage, GUILayout.Width(200), GUILayout.Height(200));
            GUI.color = prevColor;
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
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
