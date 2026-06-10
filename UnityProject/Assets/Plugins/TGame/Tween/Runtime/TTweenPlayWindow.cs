#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.Tween
{
    /// <summary>
    /// TTweenPlay 的可视化节点时间轴编辑器窗口（UI Toolkit）。
    /// 从 Inspector 中 "Open Play Editor" 按钮打开。
    /// </summary>
    public class TTweenPlayWindow : EditorWindow
    {
        [System.NonSerialized] private TTweenPlay _play;
        [System.NonSerialized] private SerializedObject _serializedObject;
        [System.NonSerialized] private SerializedProperty _entriesProp;

        // ——— UI 元素 ———
        private IMGUIContainer _canvasContainer;
        private IMGUIContainer _entriesContainer;
        private Button _playBtn;
        private Button _stopBtn;
        private Label _statusLabel;

        // ——— 拖拽状态 ———
        private int _dragIndex = -1;
        private float _dragInitialTime;
        private Vector2 _dragInitialMouse;

        // ——— 色板 ———
        private static readonly Color[] NodeColors = new[]
        {
            new Color(0.85f, 0.37f, 0.37f),
            new Color(0.33f, 0.71f, 0.86f),
            new Color(0.62f, 0.84f, 0.36f),
            new Color(0.96f, 0.65f, 0.14f),
            new Color(0.76f, 0.49f, 0.86f),
            new Color(0.96f, 0.76f, 0.24f),
            new Color(0.44f, 0.78f, 0.72f),
            new Color(0.91f, 0.45f, 0.58f),
        };

        // ——— 公开方法 ———

        public static void Open(TTweenPlay play)
        {
            var window = GetWindow<TTweenPlayWindow>("TTweenPlay Editor");
            window._play = play;
            window._serializedObject = new SerializedObject(play);
            window._entriesProp = window._serializedObject?.FindProperty("_nodeEntries");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        // ——— UI Toolkit ———

        private void OnEnable()
        {
            if (_play != null)
            {
                _serializedObject = new SerializedObject(_play);
                _entriesProp = _serializedObject?.FindProperty("_nodeEntries");
            }
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            // ——— Toolbar ———
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 2,
                    paddingBottom = 2,
                    backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f)),
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };

            _playBtn = new Button(() =>
            {
                if (_play == null) return;
                if (Application.isPlaying)
                    _play.Play();
                else
                    Debug.Log("TTweenPlay: Enter Play Mode to preview.");
            })
            { text = "▶ Play", tooltip = "Play the timeline (Play Mode only)", style = { width = 70 } };
            toolbar.Add(_playBtn);

            _stopBtn = new Button(() => { _play?.Kill(); })
            { text = "■ Stop", tooltip = "Stop and kill all tweens", style = { width = 70 } };
            toolbar.Add(_stopBtn);

            root.Add(toolbar);

            // ——— Status ———
            _statusLabel = new Label("No play selected")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 8, paddingTop = 2, paddingBottom = 2 }
            };
            root.Add(_statusLabel);

            // ——— 时间轴画布 ———
            _canvasContainer = new IMGUIContainer(DrawTimelineCanvas)
            {
                style = { height = 120, minHeight = 100, flexGrow = 0, marginLeft = 4, marginRight = 4, marginTop = 4 }
            };
            _canvasContainer.RegisterCallback<MouseDownEvent>(OnCanvasMouseDown);
            _canvasContainer.RegisterCallback<MouseMoveEvent>(OnCanvasMouseMove);
            _canvasContainer.RegisterCallback<MouseUpEvent>(OnCanvasMouseUp);
            root.Add(_canvasContainer);

            // ——— 节点列表 ———
            _entriesContainer = new IMGUIContainer(DrawEntriesList)
            {
                style = { flexGrow = 1, minHeight = 80, marginLeft = 4, marginRight = 4 }
            };
            root.Add(_entriesContainer);

            // ——— 底部按钮 ———
            var bottomBar = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, paddingLeft = 4, paddingRight = 4, paddingBottom = 4 }
            };

            var addBtn = new Button(() =>
            {
                if (_play == null) return;
                Undo.RecordObject(_play, "Add Node Entry");
                _play.NodeEntries.Add(new TTweenPlay.NodeEntry());
                EditorUtility.SetDirty(_play);
                _serializedObject?.Update();
                Repaint();
            })
            { text = "+ Add Node" };
            bottomBar.Add(addBtn);

            var collectBtn = new Button(() =>
            {
                if (_play == null) return;
                Undo.RecordObject(_play, "Collect Children");
                _play.CollectChildren();
                EditorUtility.SetDirty(_play);
                _serializedObject?.Update();
                Repaint();
            })
            { text = "Collect Children" };
            bottomBar.Add(collectBtn);

            root.Add(bottomBar);

            RefreshFromPlay();
        }

        private void Update()
        {
            if (_play != null)
                _canvasContainer?.MarkDirtyRepaint();
        }

        // ——— 刷新 ———

        private void RefreshFromPlay()
        {
            if (_play == null)
            {
                _statusLabel.text = "No play selected";
                return;
            }
            _statusLabel.text = $"Editing: {_play.gameObject.name}  |  Nodes: {_play.NodeEntries.Count}";
            _serializedObject?.Update();
        }

        // ——— 时间轴绘制 ———

        private void DrawTimelineCanvas()
        {
            if (_play == null || _serializedObject == null)
            {
                EditorGUILayout.HelpBox("No play selected.", MessageType.Info);
                return;
            }

            _serializedObject.Update();

            var rect = _canvasContainer.contentRect;
            if (rect.width < 20 || rect.height < 20) return;

            const float leftPad = 50f;
            const float rightPad = 16f;
            const float topPad = 24f;
            const float bottomPad = 10f;
            const float rowHeight = 18f;
            const float rowGap = 2f;

            float totalTime = CalculateTotalDuration();
            if (totalTime <= 0f) totalTime = 1f;

            var timelineRect = new Rect(
                rect.x + leftPad, rect.y + topPad,
                rect.width - leftPad - rightPad, rect.height - topPad - bottomPad
            );

            // 背景
            EditorGUI.DrawRect(timelineRect, new Color(0.16f, 0.16f, 0.16f, 1f));
            EditorGUI.DrawRect(new Rect(timelineRect.x - 1, timelineRect.y - 1, timelineRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(timelineRect.x - 1, timelineRect.yMax, timelineRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(timelineRect.x - 1, timelineRect.y, 1, timelineRect.height), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(timelineRect.xMax, timelineRect.y, 1, timelineRect.height), new Color(0.3f, 0.3f, 0.3f));

            // 时间刻度
            float timeStep = GetTimeStep(totalTime);
            var timeLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter, fontSize = 9,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            for (float t = 0; t <= totalTime + 0.001f; t += timeStep)
            {
                float x = timelineRect.x + (t / totalTime) * timelineRect.width;
                EditorGUI.DrawRect(new Rect(x, timelineRect.y, 1, timelineRect.height), new Color(0.25f, 0.25f, 0.25f));
                GUI.Label(new Rect(x - 20, rect.y + 2, 40, 18), $"{t:F1}s", timeLabelStyle);
            }

            // 节点块
            int count = _entriesProp?.arraySize ?? 0;
            float usableHeight = timelineRect.height - 4;
            int maxRows = Mathf.Max(1, (int)(usableHeight / (rowHeight + rowGap)));

            for (int i = 0; i < count; i++)
            {
                var entryProp = _entriesProp.GetArrayElementAtIndex(i);
                var nodeProp = entryProp.FindPropertyRelative("node");
                var timeProp = entryProp.FindPropertyRelative("startTime");

                float startTime = timeProp.floatValue;
                Color color = NodeColors[i % NodeColors.Length];

                float x = timelineRect.x + (startTime / totalTime) * timelineRect.width;
                int row = i % maxRows;
                float y = timelineRect.y + 4 + row * (rowHeight + rowGap);
                float blockWidth = Mathf.Max(16, (0.4f / totalTime) * timelineRect.width);
                var blockRect = new Rect(x, y, blockWidth, rowHeight);

                bool isDragging = i == _dragIndex;
                bool isHover = blockRect.Contains(Event.current.mousePosition);

                if (isDragging)
                    EditorGUI.DrawRect(blockRect, Color.white);
                else if (isHover)
                    EditorGUI.DrawRect(blockRect, Color.Lerp(color, Color.white, 0.3f));
                else
                    EditorGUI.DrawRect(blockRect, color);

                if (isDragging || isHover)
                    EditorGUI.DrawRect(new Rect(blockRect.x - 2, blockRect.y, 2, blockRect.height), Color.white);

                string label = nodeProp.objectReferenceValue != null
                    ? nodeProp.objectReferenceValue.name
                    : $"Node {i}";
                var blockLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter, fontSize = 9,
                    normal = { textColor = Color.white }, clipping = TextClipping.Clip
                };
                GUI.Label(blockRect, label, blockLabelStyle);
            }

            // 总时长
            var totalLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.LowerRight, fontSize = 9,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            GUI.Label(new Rect(timelineRect.xMax - 80, timelineRect.yMax - 16, 80, 14),
                $"▲ {totalTime:F2}s", totalLabelStyle);

            _serializedObject.ApplyModifiedProperties();
        }

        // ——— 鼠标交互 ———

        private void OnCanvasMouseDown(MouseDownEvent evt)
        {
            if (_play == null || _entriesProp == null || evt.button != 0) return;

            var rect = _canvasContainer.contentRect;
            (var tlRect, float totalTime) = GetLayout(rect);
            if (totalTime <= 0) return;

            _serializedObject.Update();
            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                var entryProp = _entriesProp.GetArrayElementAtIndex(i);
                float t = entryProp.FindPropertyRelative("startTime").floatValue;
                float bw = Mathf.Max(16, (0.4f / totalTime) * tlRect.width);
                float x = tlRect.x + (t / totalTime) * tlRect.width;
                float uh = tlRect.height - 4;
                int mr = Mathf.Max(1, (int)(uh / 20));
                int r = i % mr;
                float y = tlRect.y + 4 + r * 20;
                var br = new Rect(x - 4, y - 2, bw + 8, 22);
                if (br.Contains(evt.mousePosition))
                {
                    _dragIndex = i;
                    _dragInitialTime = t;
                    _dragInitialMouse = evt.mousePosition;
                    evt.StopPropagation();
                    break;
                }
            }
        }

        private void OnCanvasMouseMove(MouseMoveEvent evt)
        {
            if (_dragIndex < 0 || _play == null || _entriesProp == null) return;
            var rect = _canvasContainer.contentRect;
            (var tlRect, float totalTime) = GetLayout(rect);
            if (totalTime <= 0) return;
            _serializedObject.Update();
            if (_dragIndex < _entriesProp.arraySize)
            {
                var p = _entriesProp.GetArrayElementAtIndex(_dragIndex).FindPropertyRelative("startTime");
                float dx = evt.mousePosition.x - _dragInitialMouse.x;
                float dt = (dx / tlRect.width) * totalTime;
                p.floatValue = Mathf.Max(0, Mathf.Round((_dragInitialTime + dt) / 0.05f) * 0.05f);
                _serializedObject.ApplyModifiedProperties();
            }
            evt.StopPropagation();
        }

        private void OnCanvasMouseUp(MouseUpEvent evt)
        {
            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                evt.StopPropagation();
                if (_play != null) EditorUtility.SetDirty(_play);
            }
        }

        // ——— 节点列表 ———

        private void DrawEntriesList()
        {
            if (_play == null || _serializedObject == null)
            {
                EditorGUILayout.HelpBox("No play selected.", MessageType.Info);
                return;
            }
            _serializedObject.Update();
            EditorGUILayout.LabelField("Node Entries", EditorStyles.boldLabel);
            if (_entriesProp == null) return;

            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                var ep = _entriesProp.GetArrayElementAtIndex(i);
                var np = ep.FindPropertyRelative("node");
                var tp = ep.FindPropertyRelative("startTime");
                Color c = NodeColors[i % NodeColors.Length];

                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                var cr = EditorGUILayout.GetControlRect(GUILayout.Width(12), GUILayout.Height(18));
                EditorGUI.DrawRect(new Rect(cr.x, cr.y, 12, 18), c);
                EditorGUILayout.LabelField($"#{i}", GUILayout.Width(24));
                string name = np.objectReferenceValue != null ? np.objectReferenceValue.name : "(none)";
                EditorGUILayout.LabelField(name, GUILayout.Width(120));
                EditorGUILayout.PropertyField(tp, GUIContent.none, GUILayout.Width(60));
                EditorGUILayout.PropertyField(np, GUIContent.none, GUILayout.MinWidth(100));
                if (GUILayout.Button("×", GUILayout.Width(22)))
                {
                    _entriesProp.DeleteArrayElementAtIndex(i);
                    _serializedObject.ApplyModifiedProperties();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            _serializedObject.ApplyModifiedProperties();
        }

        // ——— 辅助 ———

        private float CalculateTotalDuration()
        {
            float maxTime = 0f;
            if (_play == null) return 1f;
            var entries = _play.NodeEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                float end = entries[i].startTime + 0.3f;
                if (end > maxTime) maxTime = end;
            }
            return maxTime > 0f ? maxTime : 1f;
        }

        private (Rect, float) GetLayout(Rect containerRect)
        {
            const float lp = 50f, rp = 16f, tp = 24f, bp = 10f;
            float t = CalculateTotalDuration();
            if (t <= 0f) t = 1f;
            return (new Rect(containerRect.x + lp, containerRect.y + tp,
                containerRect.width - lp - rp, containerRect.height - tp - bp), t);
        }

        private static float GetTimeStep(float totalTime)
        {
            if (totalTime <= 1f) return 0.25f;
            if (totalTime <= 3f) return 0.5f;
            if (totalTime <= 10f) return 1f;
            if (totalTime <= 30f) return 5f;
            return 10f;
        }
    }
}
#endif
