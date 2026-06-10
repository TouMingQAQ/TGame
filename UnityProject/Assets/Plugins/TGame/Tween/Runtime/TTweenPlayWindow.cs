#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TGame.Tween
{
    public class TTweenPlayWindow : TTweenTimelineWindowBase
    {
        [System.NonSerialized] private TTweenPlay _play;

        // ——— 数据源实现 ———

        protected override UnityEngine.Object TargetObject => _play;
        protected override int EntryCount => _play != null ? _play.NodeEntries.Count : 0;
        protected override string EntriesPropertyName => "_nodeEntries";
        protected override string StartTimePropertyName => "startTime";
        protected override string WindowTitle => "TTweenPlay Editor";
        protected override string ListHeader => "Node Entries (drag to reorder)";
        protected override string AddButtonText => "Add Node";
        protected override string CollectButtonText => "Collect Children";

        protected override float CalculateTotalDuration() =>
            _play != null ? _play.CalculateTotalDuration() : 1f;

        protected override float GetEntryStartTime(int index)
        {
            if (_entriesProp == null || index >= _entriesProp.arraySize) return 0f;
            return _entriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("startTime").floatValue;
        }

        protected override float GetEntryDuration(int index)
        {
            if (_entriesProp == null || index >= _entriesProp.arraySize) return DefaultDuration;
            var np = _entriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("node");
            var nodeObj = np.objectReferenceValue as TTweenNode;
            return nodeObj != null ? nodeObj.Duration : TTweenPlay.DefaultEntryDuration;
        }

        protected override string GetEntryLabel(int index)
        {
            if (_entriesProp == null || index >= _entriesProp.arraySize) return $"Node {index}";
            var np = _entriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("node");
            return np.objectReferenceValue != null ? np.objectReferenceValue.name : $"Node {index}";
        }

        protected override void AddEntry()
        {
            _play.NodeEntries.Add(new TTweenPlay.NodeEntry());
        }

        protected override void CollectFromChildren()
        {
            _play.CollectChildren();
        }

        protected override void PlayTarget() => _play?.Play();
        protected override void KillTarget() => _play?.Kill();

        // ——— 列表行自定义 ———

        protected override void DrawEntryFields(Rect r, int index, SerializedProperty entryProp, SerializedProperty startTimeProp, float startX)
        {
            var np = entryProp.FindPropertyRelative("node");
            float x = startX;
            EditorGUI.LabelField(new Rect(x, r.y, 100, r.height), np.objectReferenceValue != null ? np.objectReferenceValue.name : "(none)");
            x += 104;
            EditorGUI.PropertyField(new Rect(x, r.y, 55, r.height), startTimeProp, GUIContent.none);
            x += 59;
            EditorGUI.PropertyField(new Rect(x, r.y, r.xMax - x - 26, r.height), np, GUIContent.none);
        }

        // ——— 打开窗口 ———

        public static void Open(TTweenPlay play)
        {
            var w = GetWindow<TTweenPlayWindow>("TTweenPlay Editor");
            w._play = play;
            w.InitSerializedObject();
            w.minSize = new Vector2(500, 400);
            w.Show();
        }

        private void OnEnable()
        {
            if (_play != null)
                InitSerializedObject();
        }
    }
}
#endif
