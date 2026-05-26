using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TGame.ToolBox
{
    [ToolBox("PathBox", Order = 0)]
    public class PathBox : IToolBoxContent
    {
        private List<PathEntry> _paths;

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

        public void DrawContent()
        {
            EditorGUILayout.LabelField("Unity 路径速查", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            foreach (var entry in _paths)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.Label, GUILayout.Width(160));
                EditorGUILayout.SelectableLabel(entry.Path, EditorStyles.textField, GUILayout.Height(18));
                if (GUILayout.Button("复制", GUILayout.Width(40)))
                    GUIUtility.systemCopyBuffer = entry.Path;
                if (entry.ShowInExplorer && GUILayout.Button("打开", GUILayout.Width(40)))
                    EditorUtility.RevealInFinder(entry.Path);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("刷新"))
                _paths.ForEach(p => p.Refresh());
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
