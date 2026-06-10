#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TGame.Tween
{
    public class TTweenTimeLineWindow : TTweenTimelineWindowBase
    {
        [System.NonSerialized] private TTweenTimeLine _timeline;

        // ——— 数据源实现 ———

        protected override UnityEngine.Object TargetObject => _timeline;
        protected override int EntryCount => _timeline != null ? _timeline.Entries.Count : 0;
        protected override string EntriesPropertyName => "_entries";
        protected override string StartTimePropertyName => "startTime";
        protected override string WindowTitle => "TTweenTimeLine Editor";
        protected override string ListHeader => "TimeLine Entries (drag to reorder)";
        protected override string AddButtonText => "Add Play";
        protected override string CollectButtonText => "Collect Plays";

        protected override float CalculateTotalDuration() =>
            _timeline != null ? _timeline.CalculateTotalDuration() : 1f;

        protected override float GetEntryStartTime(int index)
        {
            if (_entriesProp == null || index >= _entriesProp.arraySize) return 0f;
            return _entriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("startTime").floatValue;
        }

        protected override float GetEntryDuration(int index)
        {
            if (_entriesProp == null || index >= _entriesProp.arraySize) return DefaultDuration;
            var pp = _entriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("play");
            var playObj = pp.objectReferenceValue as TTweenPlay;
            return playObj != null ? TTweenTimeLine.EstimatePlayDuration(playObj) : DefaultDuration;
        }

        protected override string GetEntryLabel(int index)
        {
            if (_entriesProp == null || index >= _entriesProp.arraySize) return $"Entry {index}";
            var pp = _entriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("play");
            return pp.objectReferenceValue != null ? pp.objectReferenceValue.name : $"Entry {index}";
        }

        protected override void AddEntry()
        {
            _timeline.Entries.Add(new TTweenTimeLine.TimeLinePlayEntry());
        }

        protected override void CollectFromChildren()
        {
            _timeline.CollectPlaysFromChildren();
        }

        protected override void PlayTarget() => _timeline?.Play();
        protected override void KillTarget() => _timeline?.Kill();

        // ——— 列表行自定义 ———

        protected override void DrawEntryFields(Rect r, int index, SerializedProperty entryProp, SerializedProperty startTimeProp, float startX)
        {
            var pp = entryProp.FindPropertyRelative("play");
            float x = startX;
            EditorGUI.LabelField(new Rect(x, r.y, 100, r.height), pp.objectReferenceValue != null ? pp.objectReferenceValue.name : "(none)");
            x += 104;
            EditorGUI.PropertyField(new Rect(x, r.y, 55, r.height), startTimeProp, GUIContent.none);
            x += 59;
            EditorGUI.PropertyField(new Rect(x, r.y, r.xMax - x - 26, r.height), pp, GUIContent.none);
        }

        // ——— 打开窗口 ———

        public static void Open(TTweenTimeLine timeline)
        {
            var w = GetWindow<TTweenTimeLineWindow>("TTweenTimeLine Editor");
            w._timeline = timeline;
            w.InitSerializedObject();
            w.minSize = new Vector2(500, 400);
            w.Show();
        }

        private void OnEnable()
        {
            if (_timeline != null)
                InitSerializedObject();
        }
    }
}
#endif
