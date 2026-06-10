#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.Tween
{
    /// <summary>
    /// TTweenTimeLine 的可视化时间轴编辑器窗口（UI Toolkit）。
    /// 从 Inspector 中 "Open Timeline Editor" 按钮打开。
    /// </summary>
    public class TTweenTimeLineWindow : EditorWindow
    {
        [System.NonSerialized] private TTweenTimeLine _timeline;
        [System.NonSerialized] private SerializedObject _serializedObject;
        [System.NonSerialized] private SerializedProperty _entriesProp;

        // ——— UI 元素 ———
        private IMGUIContainer _canvasContainer;
        private IMGUIContainer _entriesContainer;
        private Button _playBtn;
        private Button _stopBtn;
        private Toggle _loopToggle;
        private IntegerField _loopField;
        private Label _statusLabel;

        // ——— 拖拽状态 ———
        private int _dragIndex = -1;
        private float _dragInitialTime;
        private Vector2 _dragInitialMouse;

        // ——— 色板 ———
        private static readonly Color[] EntryColors = new[]
        {
            new Color(0.85f, 0.37f, 0.37f), // 红
            new Color(0.33f, 0.71f, 0.86f), // 蓝
            new Color(0.62f, 0.84f, 0.36f), // 绿
            new Color(0.96f, 0.65f, 0.14f), // 橙
            new Color(0.76f, 0.49f, 0.86f), // 紫
            new Color(0.96f, 0.76f, 0.24f), // 黄
            new Color(0.44f, 0.78f, 0.72f), // 青
            new Color(0.91f, 0.45f, 0.58f), // 粉
        };

        // ——— 公开方法 ———

        public static void Open(TTweenTimeLine timeline)
        {
            var window = GetWindow<TTweenTimeLineWindow>("TTweenTimeLine Editor");
            window._timeline = timeline;
            window._serializedObject = new SerializedObject(timeline);
            window._entriesProp = window._serializedObject?.FindProperty("_entries");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        // ——— UI Toolkit ———

        private void OnEnable()
        {
            if (_timeline != null)
            {
                _serializedObject = new SerializedObject(_timeline);
                _entriesProp = _serializedObject?.FindProperty("_entries");
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
                if (_timeline == null) return;
                if (Application.isPlaying)
                    _timeline.Play();
                else
                    Debug.Log("TTweenTimeLine: Enter Play Mode to preview.");
            })
            { text = "▶ Play", tooltip = "Play the timeline (Play Mode only)", style = { width = 70 } };
            toolbar.Add(_playBtn);

            _stopBtn = new Button(() => { _timeline?.Kill(); })
            { text = "■ Stop", tooltip = "Stop and kill all tweens", style = { width = 70 } };
            toolbar.Add(_stopBtn);

            toolbar.Add(new VisualElement { style = { width = 16 } }); // spacer

            toolbar.Add(new Label("Loops:")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 8, marginRight = 4 }
            });
            _loopField = new IntegerField { value = 1, isDelayed = true, style = { width = 50 } };
            _loopField.RegisterValueChangedCallback(evt =>
            {
                if (_timeline != null)
                {
                    Undo.RecordObject(_timeline, "Change Loops");
                    _timeline.Loops = evt.newValue;
                    EditorUtility.SetDirty(_timeline);
                }
            });
            toolbar.Add(_loopField);

            _loopToggle = new Toggle { text = "Loop", tooltip = "Enable/disable looping", style = { marginLeft = 8 } };
            _loopToggle.RegisterValueChangedCallback(evt =>
            {
                if (_timeline != null)
                {
                    Undo.RecordObject(_timeline, "Toggle Loop");
                    if (evt.newValue && _timeline.Loops <= 1)
                        _timeline.Loops = 0; // infinite
                    else if (!evt.newValue)
                        _timeline.Loops = 1;
                    _loopField.SetValueWithoutNotify(_timeline.Loops);
                    EditorUtility.SetDirty(_timeline);
                }
            });
            toolbar.Add(_loopToggle);

            var ignoreToggle = new Toggle
            {
                text = "Ignore TimeScale",
                value = true,
                style = { marginLeft = 8 }
            };
            ignoreToggle.RegisterValueChangedCallback(evt =>
            {
                if (_timeline != null)
                {
                    Undo.RecordObject(_timeline, "Toggle IgnoreTimeScale");
                    _timeline.IgnoreTimeScale = evt.newValue;
                    EditorUtility.SetDirty(_timeline);
                }
            });
            toolbar.Add(ignoreToggle);

            root.Add(toolbar);

            // ——— Status Bar ———
            _statusLabel = new Label("No timeline selected")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 8, paddingTop = 2, paddingBottom = 2 }
            };
            root.Add(_statusLabel);

            // ——— Timeline Canvas (IMGUI) ———
            _canvasContainer = new IMGUIContainer(DrawTimelineCanvas)
            {
                style =
                {
                    height = 120,
                    minHeight = 100,
                    flexGrow = 0,
                    marginLeft = 4,
                    marginRight = 4,
                    marginTop = 4,
                }
            };
            _canvasContainer.RegisterCallback<MouseDownEvent>(OnCanvasMouseDown);
            _canvasContainer.RegisterCallback<MouseMoveEvent>(OnCanvasMouseMove);
            _canvasContainer.RegisterCallback<MouseUpEvent>(OnCanvasMouseUp);
            root.Add(_canvasContainer);

            // ——— Entries List (IMGUI) ———
            _entriesContainer = new IMGUIContainer(DrawEntriesList)
            {
                style =
                {
                    flexGrow = 1,
                    minHeight = 80,
                    marginLeft = 4,
                    marginRight = 4,
                }
            };
            root.Add(_entriesContainer);

            // ——— 底部按钮栏 ———
            var bottomBar = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, paddingLeft = 4, paddingRight = 4, paddingBottom = 4 }
            };

            var addBtn = new Button(() =>
            {
                if (_timeline == null) return;
                Undo.RecordObject(_timeline, "Add Play Entry");
                _timeline.Entries.Add(new TTweenTimeLine.TimeLinePlayEntry());
                EditorUtility.SetDirty(_timeline);
                _serializedObject?.Update();
                Repaint();
            })
            { text = "+ Add Play", tooltip = "Add a new empty play entry" };
            bottomBar.Add(addBtn);

            var collectBtn = new Button(() =>
            {
                if (_timeline == null) return;
                Undo.RecordObject(_timeline, "Collect Plays");
                _timeline.CollectPlaysFromChildren();
                EditorUtility.SetDirty(_timeline);
                _serializedObject?.Update();
                Repaint();
            })
            { text = "Collect Plays From Children", tooltip = "Scan child GameObjects for TTweenPlay components" };
            bottomBar.Add(collectBtn);

            root.Add(bottomBar);

            // 初始刷新
            RefreshFromTimeline();
        }

        private void Update()
        {
            // 实时刷新（播放时更新时间指示）
            if (_timeline != null)
                _canvasContainer?.MarkDirtyRepaint();
        }

        // ——— 刷新数据 ———

        private void RefreshFromTimeline()
        {
            if (_timeline == null)
            {
                _statusLabel.text = "No timeline selected";
                return;
            }

            _statusLabel.text = $"Editing: {_timeline.gameObject.name}  |  Entries: {_timeline.Entries.Count}";
            _loopField.SetValueWithoutNotify(_timeline.Loops);
            _loopToggle.SetValueWithoutNotify(_timeline.Loops != 1);
            _serializedObject?.Update();
        }

        // ——— Timeline Canvas 绘制 ———

        private void DrawTimelineCanvas()
        {
            if (_timeline == null || _serializedObject == null)
            {
                EditorGUILayout.HelpBox("No timeline selected.", MessageType.Info);
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

            float totalTime = _timeline.CalculateTotalDuration();
            if (totalTime <= 0f) totalTime = 1f;

            var timelineRect = new Rect(
                rect.x + leftPad,
                rect.y + topPad,
                rect.width - leftPad - rightPad,
                rect.height - topPad - bottomPad
            );

            // ——— 背景 ———
            EditorGUI.DrawRect(timelineRect, new Color(0.16f, 0.16f, 0.16f, 1f));
            // 外边框
            EditorGUI.DrawRect(new Rect(timelineRect.x - 1, timelineRect.y - 1, timelineRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(timelineRect.x - 1, timelineRect.yMax, timelineRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(timelineRect.x - 1, timelineRect.y, 1, timelineRect.height), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(timelineRect.xMax, timelineRect.y, 1, timelineRect.height), new Color(0.3f, 0.3f, 0.3f));

            // ——— 时间刻度和网格线 ———
            float timeStep = GetTimeStep(totalTime);
            var timeLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 9,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            for (float t = 0; t <= totalTime + 0.001f; t += timeStep)
            {
                float x = timelineRect.x + (t / totalTime) * timelineRect.width;

                // 网格线
                EditorGUI.DrawRect(new Rect(x, timelineRect.y, 1, timelineRect.height),
                    new Color(0.25f, 0.25f, 0.25f));

                // 标签
                GUI.Label(new Rect(x - 20, rect.y + 2, 40, 18), $"{t:F1}s", timeLabelStyle);
            }

            // ——— 绘制 Entry 块 ———
            int entriesCount = _entriesProp?.arraySize ?? 0;
            float usableHeight = timelineRect.height - 4;
            int maxVisibleRows = Mathf.Max(1, (int)(usableHeight / (rowHeight + rowGap)));

            for (int i = 0; i < entriesCount; i++)
            {
                var entryProp = _entriesProp.GetArrayElementAtIndex(i);
                var playProp = entryProp.FindPropertyRelative("play");
                var startTimeProp = entryProp.FindPropertyRelative("startTime");

                float startTime = startTimeProp.floatValue;
                Color color = EntryColors[i % EntryColors.Length];

                // 块位置
                float x = timelineRect.x + (startTime / totalTime) * timelineRect.width;
                int row = i % maxVisibleRows;
                float y = timelineRect.y + 4 + row * (rowHeight + rowGap);
                float blockWidth = Mathf.Max(16, (0.4f / totalTime) * timelineRect.width);

                var blockRect = new Rect(x, y, blockWidth, rowHeight);

                // 高亮拖拽中的条目
                bool isDragging = i == _dragIndex;
                bool isHover = blockRect.Contains(Event.current.mousePosition);

                // 绘制块
                if (isDragging)
                {
                    EditorGUI.DrawRect(blockRect, Color.white);
                }
                else if (isHover)
                {
                    EditorGUI.DrawRect(blockRect, Color.Lerp(color, Color.white, 0.3f));
                }
                else
                {
                    EditorGUI.DrawRect(blockRect, color);
                }

                // 左侧色条（选中指示）
                if (isDragging || isHover)
                {
                    EditorGUI.DrawRect(new Rect(blockRect.x - 2, blockRect.y, 2, blockRect.height), Color.white);
                }

                // 标签
                string label = playProp.objectReferenceValue != null
                    ? playProp.objectReferenceValue.name
                    : $"Entry {i}";
                var blockLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9,
                    normal = { textColor = Color.white },
                    clipping = TextClipping.Clip
                };
                GUI.Label(blockRect, label, blockLabelStyle);

                // ——— 拖拽交互（在 IMGUIContainer 事件中处理） ———
                // 见 OnCanvasMouseDown/Move/Up
            }

            // ——— 总时长指示 ———
            var totalLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.LowerRight,
                fontSize = 9,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            GUI.Label(new Rect(timelineRect.xMax - 80, timelineRect.yMax - 16, 80, 14),
                $"▲ {totalTime:F2}s", totalLabelStyle);

            _serializedObject.ApplyModifiedProperties();
        }

        // ——— 时间轴鼠标交互 ———

        private void OnCanvasMouseDown(MouseDownEvent evt)
        {
            if (_timeline == null || _entriesProp == null) return;
            if (evt.button != 0) return; // left click only

            var rect = _canvasContainer.contentRect;
            (var timelineRect, float totalTime) = GetTimelineLayout(rect);
            if (totalTime <= 0) return;

            _serializedObject.Update();

            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                var entryProp = _entriesProp.GetArrayElementAtIndex(i);
                var startTimeProp = entryProp.FindPropertyRelative("startTime");
                float startTime = startTimeProp.floatValue;

                float blockWidth = Mathf.Max(16, (0.4f / totalTime) * timelineRect.width);
                float x = timelineRect.x + (startTime / totalTime) * timelineRect.width;

                float usableHeight = timelineRect.height - 4;
                int maxVisibleRows = Mathf.Max(1, (int)(usableHeight / 20));
                int row = i % maxVisibleRows;
                float y = timelineRect.y + 4 + row * 20;
                var blockRect = new Rect(x - 4, y - 2, blockWidth + 8, 22); // wider hit area

                if (blockRect.Contains(evt.mousePosition))
                {
                    _dragIndex = i;
                    _dragInitialTime = startTime;
                    _dragInitialMouse = evt.mousePosition;
                    evt.StopPropagation();
                    break;
                }
            }
        }

        private void OnCanvasMouseMove(MouseMoveEvent evt)
        {
            if (_dragIndex < 0 || _timeline == null || _entriesProp == null) return;

            var rect = _canvasContainer.contentRect;
            (var timelineRect, float totalTime) = GetTimelineLayout(rect);
            if (totalTime <= 0) return;

            _serializedObject.Update();

            if (_dragIndex < _entriesProp.arraySize)
            {
                var entryProp = _entriesProp.GetArrayElementAtIndex(_dragIndex);
                var startTimeProp = entryProp.FindPropertyRelative("startTime");

                float deltaX = evt.mousePosition.x - _dragInitialMouse.x;
                float deltaTime = (deltaX / timelineRect.width) * totalTime;
                float newTime = Mathf.Max(0, _dragInitialTime + deltaTime);
                newTime = Mathf.Round(newTime / 0.05f) * 0.05f; // snap to 0.05s

                startTimeProp.floatValue = newTime;
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
                if (_timeline != null)
                    EditorUtility.SetDirty(_timeline);
            }
        }

        // ——— Entry 列表绘制 ———

        private void DrawEntriesList()
        {
            if (_timeline == null || _serializedObject == null)
            {
                EditorGUILayout.HelpBox("No timeline selected.", MessageType.Info);
                return;
            }

            _serializedObject.Update();

            EditorGUILayout.LabelField("TimeLine Entries", EditorStyles.boldLabel);

            if (_entriesProp == null) return;

            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                var entryProp = _entriesProp.GetArrayElementAtIndex(i);
                var playProp = entryProp.FindPropertyRelative("play");
                var startTimeProp = entryProp.FindPropertyRelative("startTime");

                Color color = EntryColors[i % EntryColors.Length];

                EditorGUILayout.BeginHorizontal(GUI.skin.box);

                // 色标
                var colorRect = EditorGUILayout.GetControlRect(GUILayout.Width(12), GUILayout.Height(18));
                EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y, 12, 18), color);

                // 序号
                EditorGUILayout.LabelField($"#{i}", GUILayout.Width(24));

                // 名称（来自 play 引用）
                string playName = playProp.objectReferenceValue != null
                    ? playProp.objectReferenceValue.name
                    : "(none)";
                EditorGUILayout.LabelField(playName, GUILayout.Width(120));

                // startTime
                EditorGUILayout.PropertyField(startTimeProp, GUIContent.none, GUILayout.Width(60));

                // play 对象引用
                EditorGUILayout.PropertyField(playProp, GUIContent.none, GUILayout.MinWidth(100));

                // 删除按钮
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

        // ——— 辅助方法 ———

        private (Rect timelineRect, float totalTime) GetTimelineLayout(Rect containerRect)
        {
            const float leftPad = 50f;
            const float rightPad = 16f;
            const float topPad = 24f;
            const float bottomPad = 10f;

            float totalTime = _timeline != null ? _timeline.CalculateTotalDuration() : 1f;
            if (totalTime <= 0f) totalTime = 1f;

            var timelineRect = new Rect(
                containerRect.x + leftPad,
                containerRect.y + topPad,
                containerRect.width - leftPad - rightPad,
                containerRect.height - topPad - bottomPad
            );

            return (timelineRect, totalTime);
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
