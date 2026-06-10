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

        private IMGUIContainer _canvasContainer;
        private IMGUIContainer _entriesContainer;
        private Label _zoomLabel;
        private Label _statusLabel;
        private Slider _zoomSlider;

        private float _zoom = 1f;
        private float _scrollTime = 0f;
        private bool _isPanning = false;
        private Vector2 _panStartMouse;
        private float _panStartScroll;

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

        public static void Open(TTweenPlay play)
        {
            var w = GetWindow<TTweenPlayWindow>("TTweenPlay Editor");
            w._play = play;
            w._serializedObject = new SerializedObject(play);
            w._entriesProp = w._serializedObject?.FindProperty("_nodeEntries");
            w.minSize = new Vector2(500, 400);
            w.Show();
        }

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
            var toolbar = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center,
                    paddingLeft = 4, paddingRight = 4, paddingTop = 2, paddingBottom = 2,
                    backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f)),
                    borderBottomWidth = 1, borderBottomColor = new Color(0.3f, 0.3f, 0.3f) }
            };
            toolbar.Add(new Button(() => { if (Application.isPlaying) _play?.Play(); else Debug.Log("TTweenPlay: Enter Play Mode."); }) { text = "▶ Play", style = { width = 70 } });
            toolbar.Add(new Button(() => _play?.Kill()) { text = "■ Stop", style = { width = 70 } });
            toolbar.Add(new VisualElement { style = { width = 16 } });
            toolbar.Add(new Label("Zoom:") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 2 } });
            _zoomSlider = new Slider(0.2f, 5f) { value = 1f, style = { width = 80 } };
            _zoomSlider.RegisterValueChangedCallback(e => { _zoom = e.newValue; _scrollTime = Mathf.Min(_scrollTime, Mathf.Max(0, GetMaxScroll())); _canvasContainer?.MarkDirtyRepaint(); });
            toolbar.Add(_zoomSlider);
            _zoomLabel = new Label("1.0x") { style = { width = 36, fontSize = 9 } };
            toolbar.Add(_zoomLabel);
            toolbar.Add(new Button(() => { _zoom = 1f; _zoomSlider.value = 1f; _scrollTime = 0; _canvasContainer?.MarkDirtyRepaint(); }) { text = "Fit", style = { fontSize = 9, paddingLeft = 6, paddingRight = 6 } });
            root.Add(toolbar);

            _statusLabel = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 8, paddingTop = 2, paddingBottom = 2 } };
            root.Add(_statusLabel);

            _canvasContainer = new IMGUIContainer(DrawTimelineCanvas)
            {
                style = { height = 130, minHeight = 100, flexGrow = 0, marginLeft = 4, marginRight = 4, marginTop = 4 }
            };
            root.Add(_canvasContainer);

            _entriesContainer = new IMGUIContainer(DrawEntriesList)
            {
                style = { flexGrow = 1, minHeight = 80, marginLeft = 4, marginRight = 4 },
                focusable = true
            };
            root.Add(_entriesContainer);

            var bottomBar = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 4, paddingRight = 4, paddingBottom = 4 } };
            bottomBar.Add(new Button(() => { if (_play == null) return; Undo.RecordObject(_play, "Add Node Entry"); _play.NodeEntries.Add(new TTweenPlay.NodeEntry()); EditorUtility.SetDirty(_play); _serializedObject?.Update(); Repaint(); }) { text = "+ Add Node" });
            bottomBar.Add(new Button(() => { if (_play == null) return; Undo.RecordObject(_play, "Collect Children"); _play.CollectChildren(); EditorUtility.SetDirty(_play); _serializedObject?.Update(); Repaint(); }) { text = "Collect Children" });
            root.Add(bottomBar);
            RefreshFromPlay();
        }

        private void Update() { if (_play != null) _canvasContainer?.MarkDirtyRepaint(); }

        private void RefreshFromPlay()
        {
            if (_play == null) { _statusLabel.text = "No play selected"; return; }
            _statusLabel.text = $"Editing: {_play.gameObject.name}  |  Nodes: {_play.NodeEntries.Count}";
            _serializedObject?.Update();
        }

        // ========== 时间轴绘制（纯 IMGUI，事件在末尾统一处理） ==========

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
            float dragTime = -1f; // 拖拽中的实时时间值
            if (_dragIndex >= 0)
            {
                float dx = (Event.current.mousePosition.x - _dragInitialMouse.x);
                float dt = (dx / rect.width) * (totalTime / _zoom) * 0.8f;
                dragTime = Mathf.Max(0, _dragInitialTime + dt);
                totalTime = Mathf.Max(totalTime, dragTime + 0.3f);
                // 跟手滚动：当拖拽位置超过可视区 75% 时向右推 scrollTime
                float followMargin = _scrollTime + (totalTime / _zoom) * 0.75f;
                if (dragTime > followMargin)
                {
                    _scrollTime += (dragTime - followMargin) * 1.2f;
                }
            }
            float visibleTime = totalTime / _zoom;
            _scrollTime = Mathf.Clamp(_scrollTime, 0f, Mathf.Max(0f, totalTime - visibleTime));

            var tlRect = new Rect(rect.x + leftPad, rect.y + topPad, rect.width - leftPad - rightPad, rect.height - topPad - bottomPad);

            // 背景
            EditorGUI.DrawRect(tlRect, new Color(0.16f, 0.16f, 0.16f, 1f));
            EditorGUI.DrawRect(new Rect(tlRect.x - 1, tlRect.y - 1, tlRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(tlRect.x - 1, tlRect.yMax, tlRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(tlRect.x - 1, tlRect.y, 1, tlRect.height), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(tlRect.xMax, tlRect.y, 1, tlRect.height), new Color(0.3f, 0.3f, 0.3f));

            // 时间刻度
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
            var totalLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.UpperRight, fontSize = 9, normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            GUI.Label(new Rect(tlRect.xMax - 80, rect.y + 2, 80, 14), $"Total: {totalTime:F2}s", totalLabelStyle);
            _zoomLabel.text = $"{_zoom:F1}x";

            // 收集所有块
            int count = _entriesProp?.arraySize ?? 0;
            int maxRows = Mathf.Max(1, (int)((tlRect.height - 4) / (rowHeight + rowGap)));
            var blocks = new System.Collections.Generic.List<(Rect rect, int index, string label, Color color, float startTime)>();

            for (int i = 0; i < count; i++)
            {
                var np = _entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("node");
                var tp = _entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("startTime");
                float st = tp.floatValue;
                if (st < _scrollTime - 0.05f || st > _scrollTime + visibleTime) continue;
                Color color = NodeColors[i % NodeColors.Length];
                float x = tlRect.x + ((st - _scrollTime) / visibleTime) * tlRect.width;
                int row = i % maxRows;
                float y = tlRect.y + 4 + row * (rowHeight + rowGap);
                var nodeObj = np.objectReferenceValue as TTweenNode;
                float duration = nodeObj != null ? nodeObj.Duration : 0.3f;
                float bw = Mathf.Max(16, (duration / visibleTime) * tlRect.width);
                var br = new Rect(x, y, bw, rowHeight);
                string label = np.objectReferenceValue != null ? np.objectReferenceValue.name : $"Node {i}";
                blocks.Add((br, i, label, color, st));
            }

            // 拖拽显示时间（用已计算的 dragTime）
            float dragDisplayTime = _dragIndex >= 0 ? Mathf.Max(0, Mathf.Round(dragTime / 0.01f) * 0.01f) : _dragInitialTime;

            // 绘制块
            foreach (var (br, idx, label, color, st) in blocks)
            {
                bool inDrag = idx == _dragIndex && Event.current.type == EventType.MouseDrag;
                bool hover = br.Contains(Event.current.mousePosition) && _dragIndex < 0;
                Color c = inDrag ? Color.white : hover ? Color.Lerp(color, Color.white, 0.3f) : color;
                EditorGUI.DrawRect(br, c);
                if (inDrag || hover) EditorGUI.DrawRect(new Rect(br.x - 2, br.y, 2, br.height), Color.white);

                // 拖拽中：在新位置画半透明预览 + 黄色指示线
                if (inDrag)
                {
                    float nx = tlRect.x + ((dragDisplayTime - _scrollTime) / visibleTime) * tlRect.width;
                    EditorGUI.DrawRect(new Rect(nx, tlRect.y, 2, tlRect.height), Color.yellow);
                    var pb = new Rect(nx, br.y, br.width, br.height);
                    Color pc = color; pc.a = 0.5f;
                    EditorGUI.DrawRect(pb, pc);
                    var dl = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10, normal = { textColor = Color.yellow }, fontStyle = FontStyle.Bold };
                    GUI.Label(new Rect(nx - 20, tlRect.yMax - 14, 50, 14), $"{dragDisplayTime:F2}s", dl);
                }

                var lStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9, normal = { textColor = Color.white }, clipping = TextClipping.Clip };
                GUI.Label(br, label, lStyle);
            }

            // 滚动条
            float maxScroll = Mathf.Max(0f, totalTime - visibleTime);
            if (maxScroll > 0.01f)
            {
                float sby = tlRect.yMax + 2;
                EditorGUI.DrawRect(new Rect(tlRect.x, sby, tlRect.width, 6), new Color(0.12f, 0.12f, 0.12f));
                EditorGUI.DrawRect(new Rect(tlRect.x + (_scrollTime / maxScroll) * (tlRect.width - Mathf.Max(20, tlRect.width * (visibleTime / totalTime))), sby, Mathf.Max(20, tlRect.width * (visibleTime / totalTime)), 6), new Color(0.5f, 0.5f, 0.5f));
            }

            // ========== IMGUI 事件处理 ==========

            // 滚轮缩放
            if (Event.current.type == EventType.ScrollWheel && rect.Contains(Event.current.mousePosition))
            {
                float ct = _scrollTime + (Event.current.mousePosition.x - tlRect.x) / tlRect.width * visibleTime;
                ct = Mathf.Clamp(ct, 0, totalTime);
                _zoom *= Event.current.delta.y > 0 ? 0.85f : 1.176f;
                _zoom = Mathf.Clamp(_zoom, 0.2f, 5f);
                _zoomSlider.SetValueWithoutNotify(_zoom);
                float nv = totalTime / _zoom;
                _scrollTime = Mathf.Clamp(ct - (Event.current.mousePosition.x - tlRect.x) / tlRect.width * nv, 0, Mathf.Max(0, totalTime - nv));
                Event.current.Use();
            }

            // 鼠标按下（左键：拖拽块 | 平移 | 右键：重置缩放）
            if (Event.current.type == EventType.MouseDown)
            {
                if (Event.current.button == 1)
                {
                    _zoom = 1f; _zoomSlider.value = 1f; _scrollTime = 0;
                    Event.current.Use();
                }
                else if (Event.current.button == 0 && tlRect.Contains(Event.current.mousePosition))
                {
                    bool hit = false;
                    foreach (var (br, i, _, _, st) in blocks)
                    {
                        var hr = new Rect(br.x - 4, br.y - 2, br.width + 8, br.height + 4);
                        if (hr.Contains(Event.current.mousePosition))
                        {
                            _dragIndex = i; _dragInitialTime = st; _dragInitialMouse = Event.current.mousePosition;
                            hit = true; Event.current.Use(); break;
                        }
                    }
                    if (!hit) { _isPanning = true; _panStartMouse = Event.current.mousePosition; _panStartScroll = _scrollTime; Event.current.Use(); }
                }
            }

            // 鼠标拖拽
            if (Event.current.type == EventType.MouseDrag)
            {
                if (_dragIndex >= 0 && _dragIndex < count)
                {
                    _entriesProp.GetArrayElementAtIndex(_dragIndex).FindPropertyRelative("startTime").floatValue = dragDisplayTime;
                    _serializedObject.ApplyModifiedProperties();
                    Event.current.Use();
                }
                else if (_isPanning)
                {
                    _scrollTime = Mathf.Clamp(_panStartScroll + (_panStartMouse.x - Event.current.mousePosition.x) / tlRect.width * visibleTime, 0, Mathf.Max(0, totalTime - visibleTime));
                    Event.current.Use();
                }
            }

            // 鼠标松开
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                if (_dragIndex >= 0 && _play != null) EditorUtility.SetDirty(_play);
                _dragIndex = -1; _isPanning = false;
                Event.current.Use();
            }

            _serializedObject.ApplyModifiedProperties();
        }

        // ========== 节点列表（可拖拽排序） ==========

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
                    float x = r.x + 24;
                    EditorGUI.DrawRect(new Rect(x, r.y + 1, 10, r.height - 2), c);
                    x += 14;
                    EditorGUI.LabelField(new Rect(x, r.y, 20, r.height), $"#{i}");
                    x += 22;
                    EditorGUI.LabelField(new Rect(x, r.y, 100, r.height), np.objectReferenceValue != null ? np.objectReferenceValue.name : "(none)");
                    x += 104;
                    EditorGUI.PropertyField(new Rect(x, r.y, 55, r.height), tp, GUIContent.none);
                    x += 59;
                    EditorGUI.PropertyField(new Rect(x, r.y, r.xMax - x - 26, r.height), np, GUIContent.none);
                    if (GUI.Button(new Rect(r.xMax - 22, r.y, 22, r.height), "×")) { _entriesProp.DeleteArrayElementAtIndex(i); _serializedObject.ApplyModifiedProperties(); }
                };
                _nodeList.elementHeight = EditorGUIUtility.singleLineHeight + 2;
                _nodeList.onReorderCallback = _ => { EditorUtility.SetDirty(_play); };
            }
            _nodeList.DoLayoutList();
            _serializedObject.ApplyModifiedProperties();
        }

        // ========== 辅助 ==========

        private float CalculateTotalDuration()
        {
            float maxTime = 0f;
            if (_play == null) return 1f;
            foreach (var e in _play.NodeEntries)
            {
                float dur = e.node != null ? e.node.Duration : 0.3f;
                float end = e.startTime + dur;
                if (end > maxTime) maxTime = end;
            }
            return maxTime > 0f ? maxTime : 1f;
        }

        private float GetMaxScroll() { return Mathf.Max(0, CalculateTotalDuration() - CalculateTotalDuration() / _zoom); }

        private static float GetTimeStep(float t) => t <= 1f ? 0.25f : t <= 3f ? 0.5f : t <= 10f ? 1f : t <= 30f ? 5f : 10f;
    }
}
#endif
