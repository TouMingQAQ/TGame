#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.Tween
{
    public class TTweenPlayWindow : EditorWindow
    {
        [System.NonSerialized] private TTweenPlay _play;
        [System.NonSerialized] private SerializedObject _serializedObject;
        [System.NonSerialized] private SerializedProperty _entriesProp;

        // ——— UI 元素 ———
        private IMGUIContainer _canvasContainer;
        private IMGUIContainer _entriesContainer;
        private Label _zoomLabel;
        private Label _statusLabel;
        private Slider _zoomSlider;

        // ——— 视口状态 ———
        private float _zoom = 1f;
        private float _scrollTime = 0f;
        private bool _isPanning = false;
        private Vector2 _panStartMouse;
        private float _panStartScroll;

        // ——— 拖拽状态 ———
        private int _dragIndex = -1;
        private float _dragInitialTime;
        private Vector2 _dragInitialMouse;

        private static readonly Color[] NodeColors = new[]
        {
            new Color(0.85f, 0.37f, 0.37f), new Color(0.33f, 0.71f, 0.86f),
            new Color(0.62f, 0.84f, 0.36f), new Color(0.96f, 0.65f, 0.14f),
            new Color(0.76f, 0.49f, 0.86f), new Color(0.96f, 0.76f, 0.24f),
            new Color(0.44f, 0.78f, 0.72f), new Color(0.91f, 0.45f, 0.58f),
        };

        // ——— 公开方法 ———

        public static void Open(TTweenPlay play)
        {
            var w = GetWindow<TTweenPlayWindow>("TTweenPlay Editor");
            w._play = play;
            w._serializedObject = new SerializedObject(play);
            w._entriesProp = w._serializedObject?.FindProperty("_nodeEntries");
            w.minSize = new Vector2(500, 400);
            w.Show();
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
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center,
                    paddingLeft = 4, paddingRight = 4, paddingTop = 2, paddingBottom = 2,
                    backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f)),
                    borderBottomWidth = 1, borderBottomColor = new Color(0.3f, 0.3f, 0.3f) }
            };

            toolbar.Add(new Button(() => { if (Application.isPlaying) _play?.Play(); else Debug.Log("TTweenPlay: Enter Play Mode."); })
            { text = "▶ Play", style = { width = 70 } });

            toolbar.Add(new Button(() => _play?.Kill()) { text = "■ Stop", style = { width = 70 } });

            toolbar.Add(new VisualElement { style = { width = 16 } });

            toolbar.Add(new Label("Zoom:") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 2 } });
            _zoomSlider = new Slider(0.2f, 5f) { value = 1f, style = { width = 80 } };
            _zoomSlider.RegisterValueChangedCallback(e => { _zoom = e.newValue; _scrollTime = Mathf.Min(_scrollTime, Mathf.Max(0, GetMaxScroll())); _canvasContainer?.MarkDirtyRepaint(); });
            toolbar.Add(_zoomSlider);
            _zoomLabel = new Label("1.0x") { style = { width = 36, fontSize = 9 } };
            toolbar.Add(_zoomLabel);

            toolbar.Add(new Button(() => { _zoom = 1f; _zoomSlider.value = 1f; _scrollTime = 0; _canvasContainer?.MarkDirtyRepaint(); })
            { text = "Fit", style = { fontSize = 9, paddingLeft = 6, paddingRight = 6 } });

            root.Add(toolbar);

            // ——— Status ———
            _statusLabel = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 8, paddingTop = 2, paddingBottom = 2 } };
            root.Add(_statusLabel);

            // ——— 时间轴画布 ———
            _canvasContainer = new IMGUIContainer(DrawTimelineCanvas)
            {
                style = { height = 130, minHeight = 100, flexGrow = 0, marginLeft = 4, marginRight = 4, marginTop = 4 }
            };
            _canvasContainer.RegisterCallback<MouseDownEvent>(OnCanvasMouseDown);
            _canvasContainer.RegisterCallback<MouseMoveEvent>(OnCanvasMouseMove);
            _canvasContainer.RegisterCallback<MouseUpEvent>(OnCanvasMouseUp);
            _canvasContainer.RegisterCallback<WheelEvent>(OnCanvasWheel);
            root.Add(_canvasContainer);

            // ——— 节点列表（可拖拽排序） ———
            _entriesContainer = new IMGUIContainer(DrawEntriesList)
            {
                style = { flexGrow = 1, minHeight = 80, marginLeft = 4, marginRight = 4 },
                focusable = true
            };
            root.Add(_entriesContainer);

            // ——— 底部按钮 ———
            var bottomBar = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 4, paddingRight = 4, paddingBottom = 4 } };
            bottomBar.Add(new Button(() => { if (_play == null) return; Undo.RecordObject(_play, "Add Node Entry"); _play.NodeEntries.Add(new TTweenPlay.NodeEntry()); EditorUtility.SetDirty(_play); _serializedObject?.Update(); Repaint(); }) { text = "+ Add Node" });
            bottomBar.Add(new Button(() => { if (_play == null) return; Undo.RecordObject(_play, "Collect Children"); _play.CollectChildren(); EditorUtility.SetDirty(_play); _serializedObject?.Update(); Repaint(); }) { text = "Collect Children" });
            root.Add(bottomBar);

            RefreshFromPlay();
        }

        private void Update()
        {
            if (_play != null) _canvasContainer?.MarkDirtyRepaint();
        }

        private void RefreshFromPlay()
        {
            if (_play == null) { _statusLabel.text = "No play selected"; return; }
            _statusLabel.text = $"Editing: {_play.gameObject.name}  |  Nodes: {_play.NodeEntries.Count}";
            _serializedObject?.Update();
        }

        // ——— 时间轴绘制 ———

        private void DrawTimelineCanvas()
        {
            if (_play == null || _serializedObject == null) { EditorGUILayout.HelpBox("No play selected.", MessageType.Info); return; }
            _serializedObject.Update();
            var rect = _canvasContainer.contentRect;
            if (rect.width < 20 || rect.height < 20) return;

            const float leftPad = 50f, rightPad = 16f, topPad = 24f, bottomPad = 16f;
            const float rowHeight = 18f, rowGap = 2f;

            float totalTime = CalculateTotalDuration();
            if (totalTime <= 0f) totalTime = 1f;
            float visibleTime = totalTime / _zoom;
            _scrollTime = Mathf.Clamp(_scrollTime, 0f, Mathf.Max(0f, totalTime - visibleTime));

            var tlRect = new Rect(rect.x + leftPad, rect.y + topPad, rect.width - leftPad - rightPad, rect.height - topPad - bottomPad);
            // 背景
            EditorGUI.DrawRect(tlRect, new Color(0.16f, 0.16f, 0.16f, 1f));
            EditorGUI.DrawRect(new Rect(tlRect.x - 1, tlRect.y - 1, tlRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(tlRect.x - 1, tlRect.yMax, tlRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(tlRect.x - 1, tlRect.y, 1, tlRect.height), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(tlRect.xMax, tlRect.y, 1, tlRect.height), new Color(0.3f, 0.3f, 0.3f));

            // 时间刻度 + 网格线
            float timeStep = GetTimeStep(visibleTime);
            var tsStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperCenter, fontSize = 9, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
            for (float t = 0; t <= visibleTime + 0.001f; t += timeStep)
            {
                float absT = _scrollTime + t;
                if (absT > totalTime + 0.001f) break;
                float x = tlRect.x + (t / visibleTime) * tlRect.width;
                EditorGUI.DrawRect(new Rect(x, tlRect.y, 1, tlRect.height), new Color(0.25f, 0.25f, 0.25f));
                GUI.Label(new Rect(x - 20, rect.y + 2, 40, 18), $"{absT:F1}s", tsStyle);
            }

            // 显示总时长标签（右上）
            var totalLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperRight, fontSize = 9, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            GUI.Label(new Rect(tlRect.xMax - 80, rect.y + 2, 80, 14), $"Total: {totalTime:F2}s", totalLabelStyle);

            // 节点块
            _zoomLabel.text = $"{_zoom:F1}x";
            int count = _entriesProp?.arraySize ?? 0;
            float usableHeight = tlRect.height - 4;
            int maxRows = Mathf.Max(1, (int)(usableHeight / (rowHeight + rowGap)));

            for (int i = 0; i < count; i++)
            {
                var ep = _entriesProp.GetArrayElementAtIndex(i);
                var np = ep.FindPropertyRelative("node");
                var tp = ep.FindPropertyRelative("startTime");
                float st = tp.floatValue;

                // 不在可见范围内 → 跳过（场景外裁剪）
                if (st < _scrollTime - 0.05f || st > _scrollTime + visibleTime) continue;

                Color color = NodeColors[i % NodeColors.Length];
                float localT = st - _scrollTime;
                float x = tlRect.x + (localT / visibleTime) * tlRect.width;
                int row = i % maxRows;
                float y = tlRect.y + 4 + row * (rowHeight + rowGap);
                float bw = Mathf.Max(16, (0.3f / visibleTime) * tlRect.width);
                var br = new Rect(x, y, bw, rowHeight);

                bool isDragging = i == _dragIndex;
                bool isHover = br.Contains(Event.current.mousePosition);

                EditorGUI.DrawRect(br, isDragging ? Color.white : isHover ? Color.Lerp(color, Color.white, 0.3f) : color);
                if (isDragging || isHover) EditorGUI.DrawRect(new Rect(br.x - 2, br.y, 2, br.height), Color.white);

                string label = np.objectReferenceValue != null ? np.objectReferenceValue.name : $"Node {i}";
                var lStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9, normal = { textColor = Color.white }, clipping = TextClipping.Clip };
                GUI.Label(br, label, lStyle);
            }

            // 滚动条
            float maxScroll = Mathf.Max(0f, totalTime - visibleTime);
            if (maxScroll > 0.01f)
            {
                float scrollBarY = tlRect.yMax + 2;
                float scrollBarW = tlRect.width;
                var scrollBarRect = new Rect(tlRect.x, scrollBarY, scrollBarW, 6);
                EditorGUI.DrawRect(scrollBarRect, new Color(0.12f, 0.12f, 0.12f));
                float thumbW = Mathf.Max(20, scrollBarW * (visibleTime / totalTime));
                float thumbX = tlRect.x + (_scrollTime / maxScroll) * (scrollBarW - thumbW);
                EditorGUI.DrawRect(new Rect(thumbX, scrollBarY, thumbW, 6), new Color(0.5f, 0.5f, 0.5f));
            }

            _serializedObject.ApplyModifiedProperties();
        }

        // ——— 鼠标交互 ———

        private void OnCanvasMouseDown(MouseDownEvent evt)
        {
            if (_play == null || _entriesProp == null) return;
            var rect = _canvasContainer.contentRect;
            (var tlRect, float visibleTime) = GetLayout(rect);
            if (visibleTime <= 0) return;

            if (evt.button == 0)
            {
                _serializedObject.Update();
                for (int i = 0; i < _entriesProp.arraySize; i++)
                {
                    var ep = _entriesProp.GetArrayElementAtIndex(i);
                    float st = ep.FindPropertyRelative("startTime").floatValue;
                    if (st < _scrollTime || st > _scrollTime + visibleTime) continue;
                    float bw = Mathf.Max(16, (0.3f / visibleTime) * tlRect.width);
                    float localT = st - _scrollTime;
                    float x = tlRect.x + (localT / visibleTime) * tlRect.width;
                    float uh = tlRect.height - 4;
                    int mr = Mathf.Max(1, (int)(uh / 20));
                    int row = i % mr;
                    float y = tlRect.y + 4 + row * 20;
                    var br = new Rect(x - 4, y - 2, bw + 8, 22);
                    if (br.Contains(evt.mousePosition))
                    {
                        _dragIndex = i; _dragInitialTime = st; _dragInitialMouse = evt.mousePosition;
                        evt.StopPropagation(); return;
                    }
                }
                // 空白处点击 → 开始平移
                _isPanning = true; _panStartMouse = evt.mousePosition; _panStartScroll = _scrollTime;
                evt.StopPropagation();
            }
            else if (evt.button == 1)
            {
                _zoom = 1f; _zoomSlider.value = 1f; _scrollTime = 0; _canvasContainer?.MarkDirtyRepaint(); evt.StopPropagation();
            }
        }

        private void OnCanvasMouseMove(MouseMoveEvent evt)
        {
            if (_dragIndex >= 0 && _play != null && _entriesProp != null)
            {
                var rect = _canvasContainer.contentRect;
                (var tlRect, float visibleTime) = GetLayout(rect);
                if (visibleTime > 0 && _dragIndex < _entriesProp.arraySize)
                {
                    var p = _entriesProp.GetArrayElementAtIndex(_dragIndex).FindPropertyRelative("startTime");
                    float dx = evt.mousePosition.x - _dragInitialMouse.x;
                    float dt = (dx / tlRect.width) * visibleTime;
                    p.floatValue = Mathf.Max(0, Mathf.Round((_dragInitialTime + dt) / 0.05f) * 0.05f);
                    _serializedObject.ApplyModifiedProperties();
                }
                evt.StopPropagation();
                return;
            }
            if (_isPanning)
            {
                float totalTime = CalculateTotalDuration();
                float visibleTime = totalTime / _zoom;
                float dx = (_panStartMouse.x - evt.mousePosition.x);
                var rect = _canvasContainer.contentRect;
                (var tlRect, _) = GetLayout(rect);
                float dt = (dx / tlRect.width) * visibleTime;
                _scrollTime = Mathf.Clamp(_panStartScroll + dt, 0, Mathf.Max(0, totalTime - visibleTime));
                evt.StopPropagation();
            }
        }

        private void OnCanvasMouseUp(MouseUpEvent evt)
        {
            if (_dragIndex >= 0) { _dragIndex = -1; if (_play != null) EditorUtility.SetDirty(_play); evt.StopPropagation(); }
            _isPanning = false;
        }

        private void OnCanvasWheel(WheelEvent evt)
        {
            if (_play == null) return;
            float totalTime = CalculateTotalDuration();
            float oldVisible = totalTime / _zoom;
            float cursorTime = _scrollTime + (evt.mousePosition.x - 50) / (_canvasContainer.contentRect.width - 66) * oldVisible;
            cursorTime = Mathf.Clamp(cursorTime, 0, totalTime);

            _zoom *= evt.delta.y > 0 ? 0.85f : 1.176f;
            _zoom = Mathf.Clamp(_zoom, 0.2f, 5f);
            _zoomSlider.SetValueWithoutNotify(_zoom);

            float newVisible = totalTime / _zoom;
            _scrollTime = Mathf.Clamp(cursorTime - (evt.mousePosition.x - 50) / (_canvasContainer.contentRect.width - 66) * newVisible, 0, Mathf.Max(0, totalTime - newVisible));
            _canvasContainer.MarkDirtyRepaint();
            evt.StopPropagation();
        }

        // ——— 节点列表（可拖拽排序） ———

        private UnityEditorInternal.ReorderableList _nodeList;

        private void DrawEntriesList()
        {
            if (_play == null || _serializedObject == null) { EditorGUILayout.HelpBox("No play selected.", MessageType.Info); return; }
            _serializedObject.Update();
            if (_entriesProp == null) return;

            if (_nodeList == null || _entriesProp == null || _nodeList.count != _entriesProp.arraySize)
            {
                _entriesProp = _serializedObject.FindProperty("_nodeEntries");
                _nodeList = new UnityEditorInternal.ReorderableList(_serializedObject, _entriesProp, true, true, false, false);
                _nodeList.drawHeaderCallback = r => EditorGUI.LabelField(r, "Node Entries (drag to reorder)", EditorStyles.boldLabel);
                _nodeList.drawElementCallback = (r, i, _, _) =>
                {
                    if (i >= _entriesProp.arraySize) return;
                    var ep = _entriesProp.GetArrayElementAtIndex(i);
                    var np = ep.FindPropertyRelative("node");
                    var tp = ep.FindPropertyRelative("startTime");
                    Color c = NodeColors[i % NodeColors.Length];

                    float x = r.x; float w = r.width;
                    EditorGUI.DrawRect(new Rect(x, r.y + 1, 10, r.height - 2), c);
                    x += 14; w -= 14;
                    EditorGUI.LabelField(new Rect(x, r.y, 24, r.height), $"#{i}");
                    x += 26; w -= 26;
                    string name = np.objectReferenceValue != null ? np.objectReferenceValue.name : "(none)";
                    EditorGUI.LabelField(new Rect(x, r.y, 100, r.height), name);
                    x += 104; w -= 104;
                    EditorGUI.PropertyField(new Rect(x, r.y, 55, r.height), tp, GUIContent.none);
                    x += 59; w -= 59;
                    EditorGUI.PropertyField(new Rect(x, r.y, w - 26, r.height), np, GUIContent.none);
                    if (GUI.Button(new Rect(r.xMax - 22, r.y, 22, r.height), "×"))
                    {
                        _entriesProp.DeleteArrayElementAtIndex(i);
                        _serializedObject.ApplyModifiedProperties();
                    }
                };
                _nodeList.elementHeight = EditorGUIUtility.singleLineHeight + 2;
                _nodeList.onReorderCallback = _ => { EditorUtility.SetDirty(_play); };
            }

            _nodeList.DoLayoutList();
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

        private float GetMaxScroll()
        {
            return Mathf.Max(0, CalculateTotalDuration() - CalculateTotalDuration() / _zoom);
        }

        private (Rect tlRect, float visibleTime) GetLayout(Rect containerRect)
        {
            const float lp = 50f, rp = 16f, tp = 24f, bp = 16f;
            float totalTime = CalculateTotalDuration();
            if (totalTime <= 0f) totalTime = 1f;
            float visibleTime = totalTime / _zoom;
            return (new Rect(containerRect.x + lp, containerRect.y + tp, containerRect.width - lp - rp, containerRect.height - tp - bp), visibleTime);
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
