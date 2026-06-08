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
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;

            var title = new Label("Unity 路径速查");
            title.AddToClassList("tbx-section-title");
            root.Add(title);

            _pathFields.Clear();

            for (int i = 0; i < _paths.Count; i++)
            {
                var entry = _paths[i];
                var row = new VisualElement();
                row.AddToClassList("tbx-path-row");

                var label = new Label(entry.Label);
                label.AddToClassList("tbx-path-label");
                row.Add(label);

                var pathField = new TextField();
                pathField.value = entry.Path;
                pathField.isReadOnly = true;
                pathField.AddToClassList("tbx-path-field");
                row.Add(pathField);
                _pathFields.Add(pathField);

                Button copyBtn = null;
                copyBtn = new Button(() =>
                {
                    EditorGUIUtility.systemCopyBuffer = entry.Path;
                    ShowCopyFeedback(copyBtn, "已复制");
                })
                { text = "复制" };
                copyBtn.AddToClassList("tbx-btn-secondary");
                row.Add(copyBtn);

                if (entry.ShowInExplorer)
                {
                    var openBtn = new Button(() => EditorUtility.RevealInFinder(entry.Path))
                    { text = "打开" };
                    openBtn.AddToClassList("tbx-btn-secondary");
                    row.Add(openBtn);
                }

                root.Add(row);
            }

            // Refresh button
            var refreshBtn = new Button(RefreshAll) { text = "刷新" };
            refreshBtn.AddToClassList("tbx-btn-secondary");
            refreshBtn.style.marginTop = 8;
            root.Add(refreshBtn);

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

