#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TGame.Tween
{
    // ——— TTweenPlay Editor ———

    [CustomEditor(typeof(TTweenPlay))]
    public class TTweenPlayEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var play = (TTweenPlay)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ Play"))
                {
                    if (Application.isPlaying)
                    {
                        play.Play();
                    }
                    else
                    {
                        Debug.Log("TTweenPlay: Enter Play Mode to preview animation.");
                    }
                }
                if (GUILayout.Button("▶ ReStart"))
                {
                    if (Application.isPlaying)
                    {
                        play.Resume();
                    }
                    else
                    {
                        Debug.Log("TTweenPlay: Enter Play Mode to preview animation.");
                    }
                }

                if (GUILayout.Button("■ Stop"))
                {
                    play.Kill();
                }
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Collect Children"))
            {
                Undo.RecordObject(play, "Collect Children");
                play.CollectChildren();
                EditorUtility.SetDirty(play);
            }

            if (GUILayout.Button("Open Play Editor", GUILayout.MinWidth(160)))
            {
                TTweenPlayWindow.Open(play);
            }
        }
    }

    // ——— TTweenTimeLine Editor ———

    [CustomEditor(typeof(TTweenTimeLine))]
    public class TTweenTimeLineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var timeline = (TTweenTimeLine)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ Play"))
                {
                    if (Application.isPlaying)
                        timeline.Play();
                    else
                        Debug.Log("TTweenTimeLine: Enter Play Mode to preview animation.");
                }

                if (GUILayout.Button("■ Stop"))
                {
                    timeline.Kill();
                }
            }

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Collect Plays From Children"))
                {
                    Undo.RecordObject(timeline, "Collect Plays");
                    timeline.CollectPlaysFromChildren();
                    EditorUtility.SetDirty(timeline);
                }

                if (GUILayout.Button("Open Timeline Editor", GUILayout.MinWidth(160)))
                {
                    TTweenTimeLineWindow.Open(timeline);
                }
            }
        }
    }
}
#endif
