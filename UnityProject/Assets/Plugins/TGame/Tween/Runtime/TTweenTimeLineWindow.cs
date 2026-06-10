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
            var entry = _entriesProp.GetArrayElementAtIndex(index);
            var aliasProp = entry.FindPropertyRelative("alias");
            string alias = aliasProp != null ? aliasProp.stringValue : null;
            if (!string.IsNullOrEmpty(alias)) return alias;

            var pp = entry.FindPropertyRelative("play");
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
            var aliasProp = entryProp.FindPropertyRelative("alias");
            var playObj = pp.objectReferenceValue;
            float x = startX;

            // Play 名（来源对象名）
            EditorGUI.LabelField(new Rect(x, r.y, 80, r.height), playObj != null ? playObj.name : "(none)");
            x += 84;

            // 别名输入框
            DrawAliasField(new Rect(x, r.y, 96, r.height), aliasProp, playObj != null ? playObj.name : "alias");
            x += 100;

            // startTime
            EditorGUI.PropertyField(new Rect(x, r.y, 55, r.height), startTimeProp, GUIContent.none);
            x += 59;

            // Play 引用
            EditorGUI.PropertyField(new Rect(x, r.y, r.xMax - x - 26, r.height), pp, GUIContent.none);
        }

        /// <summary>
        /// 绘制 alias 字段；为空时叠加灰色占位提示，编辑后触发时间轴重绘。
        /// </summary>
        private void DrawAliasField(Rect rect, SerializedProperty aliasProp, string placeholder)
        {
            if (aliasProp == null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.TextField(rect, string.Empty);
                return;
            }

            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUI.TextField(rect, aliasProp.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                aliasProp.stringValue = newValue;
                _needsRepaint = true;
            }

            if (string.IsNullOrEmpty(aliasProp.stringValue) && Event.current.type == EventType.Repaint)
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.4f);
                var pRect = new Rect(rect.x + 2, rect.y, rect.width - 4, rect.height);
                GUI.Label(pRect, placeholder, EditorStyles.miniLabel);
                GUI.color = prev;
            }
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
