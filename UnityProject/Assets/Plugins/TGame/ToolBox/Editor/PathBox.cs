using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.ToolBox
{
    [ToolBox("常用路径", Order = 0)]
    public class PathBox : IToolBoxContentVisualElement
    {
        private List<PathEntry> _paths;
        private List<TextField> _pathFields = new();

        public PathBox()
        {
            _paths = new List<PathEntry>
            {
                new PathEntry("dataPath", Application.dataPath),
                new PathEntry("persistentDataPath", Application.persistentDataPath),
                new PathEntry("streamingAssetsPath", Application.streamingAssetsPath),
                new PathEntry("temporaryCachePath", Application.temporaryCachePath),
                new PathEntry("consoleLogPath", Application.consoleLogPath),
                new PathEntry("Editor Application.dataPath", Application.dataPath, true),
            };
        }

        public VisualElement CreateContent()
        {
            var root = new VisualElement();
            root.style.padding = 12;

            var title = new Label("Unity 路径速查");
            title.AddToClassList("tbx-section-title");
            root.Add(title);

            _pathFields.Clear();

            for (int i = 0; i < _paths.Count; i++)
            {
                var entry = _paths[i];
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6;

                var label = new Label(entry.Label);
                label.style.width = 200;
                label.style.fontSize = 11;
                label.style.color = new Color(0.6f, 0.6f, 0.6f);
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                label.style.flexShrink = 0;
                row.Add(label);

                var pathField = new TextField();
                pathField.value = entry.Path;
                pathField.isReadOnly = true;
                pathField.style.flexGrow = 1;
                pathField.style.marginRight = 4;
                row.Add(pathField);
                _pathFields.Add(pathField);

                var copyBtn = new Button(() =>
                {
                    EditorGUIUtility.systemCopyBuffer = entry.Path;
                    ShowCopyFeedback(copyBtn, "已复制");
                })
                { text = "复制" };
                copyBtn.style.width = 50;
                row.Add(copyBtn);

                if (entry.ShowInExplorer)
                {
                    var openBtn = new Button(() => EditorUtility.RevealInFinder(entry.Path))
                    { text = "打开" };
                    openBtn.style.width = 50;
                    row.Add(openBtn);
                }

                root.Add(row);
            }

            // Refresh button
            var refreshRow = new VisualElement();
            refreshRow.style.marginTop = 8;
            var refreshBtn = new Button(RefreshAll)
            { text = "刷新" };
            refreshBtn.style.width = 100;
            refreshRow.Add(refreshBtn);
            root.Add(refreshRow);

            return root;
        }

        private void RefreshAll()
        {
            foreach (var e in _paths)
                e.Refresh();
            for (int i = 0; i < _paths.Count && i < _pathFields.Count; i++)
                _pathFields[i].value = _paths[i].Path;
        }

        private static void ShowCopyFeedback(VisualElement target, string message)
        {
            if (!(target is Button btn)) return;
            var originalText = btn.text;
            btn.text = message;
            btn.schedule.Execute(() => btn.text = originalText).StartingIn(800);
        }

        private class PathEntry
        {
            public string Label;
            public string Path;
            public bool ShowInExplorer;

            public PathEntry(string label, string path, bool showInExplorer = false)
            {
                Label = label;
                Path = path;
                ShowInExplorer = showInExplorer;
            }

            public void Refresh()
            {
                if (Label == "Editor Application.dataPath")
                    Path = Application.dataPath;
            }
        }
    }
}
