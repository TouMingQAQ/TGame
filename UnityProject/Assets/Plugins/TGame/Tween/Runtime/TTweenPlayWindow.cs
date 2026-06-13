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
            return nodeObj != null ? nodeObj.GetPlaybackDuration() : TTweenPlay.DefaultEntryDuration;
        }

        protected override string GetEntryLabel(int index)
        {
            if (_entriesProp == null || index >= _entriesProp.arraySize) return $"Node {index}";
            var entry = _entriesProp.GetArrayElementAtIndex(index);
            var aliasProp = entry.FindPropertyRelative("alias");
            string alias = aliasProp != null ? aliasProp.stringValue : null;
            if (!string.IsNullOrEmpty(alias)) return alias;

            var np = entry.FindPropertyRelative("node");
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
            var aliasProp = entryProp.FindPropertyRelative("alias");
            var nodeObj = np.objectReferenceValue as TTweenNode;
            float x = startX;

            // 节点名（来源对象名）
            EditorGUI.LabelField(new Rect(x, r.y, 56, r.height), nodeObj != null ? nodeObj.name : "(none)");
            x += 60;

            // 别名输入框（空时显示灰色占位）
            DrawAliasField(new Rect(x, r.y, 96, r.height), aliasProp, nodeObj != null ? nodeObj.name : "alias");
            x += 100;

            // startTime
            EditorGUI.PropertyField(new Rect(x, r.y, 44, r.height), startTimeProp, GUIContent.none);
            x += 48;

            // Duration — 通过 SerializedObject 编辑节点自身的 _duration
            var durRect = new Rect(x, r.y, 44, r.height);
            if (nodeObj != null)
            {
                if (nodeObj is TTweenAnimatorState)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUI.FloatField(durRect, nodeObj.GetPlaybackDuration());
                    }
                }
                else
                {
                    var nodeSO = new SerializedObject(nodeObj);
                    var durProp = nodeSO.FindProperty("_duration");
                    float before = durProp.floatValue;
                    EditorGUI.PropertyField(durRect, durProp, GUIContent.none);
                    if (nodeSO.ApplyModifiedProperties() && !Mathf.Approximately(before, durProp.floatValue))
                        _needsRepaint = true;
                    nodeSO.Dispose();
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.FloatField(durRect, DefaultDuration);
                }
            }
            x += 48;

            // 节点引用
            EditorGUI.PropertyField(new Rect(x, r.y, r.xMax - x - 26, r.height), np, GUIContent.none);
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
