#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace TGame.Tween
{
    /// <summary>
    /// TTweenPlayWindow 和 TTweenTimeLineWindow 的公共基类。
    /// 包含时间轴画布绘制、IMGUI 事件处理、拖拽排序列表等公共逻辑。
    /// 子类只需实现数据源抽象方法即可。
    /// </summary>
    public abstract class TTweenTimelineWindowBase : EditorWindow
    {
        // ——— 子类必须实现的数据源抽象 ———

        /// <summary>获取当前编辑的目标 Object（用于 SerializedObject 和 Undo）</summary>
        protected abstract UnityEngine.Object TargetObject { get; }

        /// <summary>获取条目数量</summary>
        protected abstract int EntryCount { get; }

        /// <summary>计算时间轴总时长</summary>
        protected abstract float CalculateTotalDuration();

        /// <summary>获取第 i 个条目的起始时间</summary>
        protected abstract float GetEntryStartTime(int index);

        /// <summary>获取第 i 个条目的时长</summary>
        protected abstract float GetEntryDuration(int index);

        /// <summary>获取第 i 个条目的显示名称</summary>
        protected abstract string GetEntryLabel(int index);

        /// <summary>获取条目列表的 SerializedProperty 名称</summary>
        protected abstract string EntriesPropertyName { get; }

        /// <summary>获取条目中 "startTime" 字段的属性名</summary>
        protected virtual string StartTimePropertyName => "startTime";

        /// <summary>窗口标题</summary>
        protected abstract string WindowTitle { get; }

        /// <summary>条目列表标题</summary>
        protected abstract string ListHeader { get; }

        /// <summary>"添加"按钮文字</summary>
        protected abstract string AddButtonText { get; }

        /// <summary>"收集"按钮文字</summary>
        protected abstract string CollectButtonText { get; }

        /// <summary>执行添加条目操作</summary>
        protected abstract void AddEntry();

        /// <summary>执行收集子对象操作</summary>
        protected abstract void CollectFromChildren();

        /// <summary>播放</summary>
        protected abstract void PlayTarget();

        /// <summary>停止</summary>
        protected abstract void KillTarget();

        // ——— 常量（原魔数） ———

        protected const float DragSensitivity = 0.8f;
        protected const float DefaultDuration = 0.3f;
        protected const float BlockMinWidth = 16f;
        protected const float FollowMarginRatio = 0.75f;
        protected const float FollowSpeed = 1.2f;
        protected const float ZoomMin = 0.2f;
        protected const float ZoomMax = 5f;
        protected const float ZoomInFactor = 0.85f;
        protected const float ZoomOutFactor = 1.176f;
        protected const float CullingMargin = 0.05f;

        // ——— 共享状态 ———

        protected SerializedObject _serializedObject;
        protected SerializedProperty _entriesProp;

        private IMGUIContainer _canvasContainer;
        private IMGUIContainer _entriesContainer;
        private Label _zoomLabel;
        private Label _statusLabel;
        private Slider _zoomSlider;

        private float _zoom = 1f;
        private float _scrollTime = 0f;
        private bool _isPanning;
        private Vector2 _panStartMouse;
        private float _panStartScroll;

        private int _dragIndex = -1;
        private float _dragInitialTime;
        private Vector2 _dragInitialMouse;
        private bool _needsRepaint;

        private ReorderableList _reorderableList;

        // 缓存 GUIStyle
        private GUIStyle _timeScaleStyle;
        private GUIStyle _totalLabelStyle;
        private GUIStyle _dragLabelStyle;
        private GUIStyle _blockLabelStyle;

        /// <summary>条目颜色盘</summary>
        protected static readonly Color[] EntryColors = new[]
        {
            new Color(0.85f, 0.37f, 0.37f), new Color(0.33f, 0.71f, 0.86f),
            new Color(0.62f, 0.84f, 0.36f), new Color(0.96f, 0.65f, 0.14f),
            new Color(0.76f, 0.49f, 0.86f), new Color(0.96f, 0.76f, 0.24f),
            new Color(0.44f, 0.78f, 0.72f), new Color(0.91f, 0.45f, 0.58f),
        };

        // ——— 生命周期 ———

        protected void InitSerializedObject()
        {
            if (TargetObject != null)
            {
                _serializedObject = new SerializedObject(TargetObject);
                _entriesProp = _serializedObject.FindProperty(EntriesPropertyName);
            }
        }

        private void OnDisable()
        {
            _serializedObject?.Dispose();
            _serializedObject = null;
            _entriesProp = null;
            _reorderableList = null;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            // 工具栏
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center,
                    paddingLeft = 4, paddingRight = 4, paddingTop = 2, paddingBottom = 2,
                    backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.22f)),
                    borderBottomWidth = 1, borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };
            toolbar.Add(new Button(() =>
            {
                if (Application.isPlaying) PlayTarget();
                else Debug.Log($"{WindowTitle}: Enter Play Mode to preview.");
            })
            { text = "▶ Play", style = { width = 70 } });

            toolbar.Add(new Button(() =>
            {
                if (Application.isPlaying) PlayTarget(); // Restart = 从头播放
                else Debug.Log($"{WindowTitle}: Enter Play Mode to preview.");
            })
            { text = "▶ Restart", style = { width = 80 } });

            toolbar.Add(new Button(() => KillTarget())
            { text = "■ Stop", style = { width = 70 } });

            toolbar.Add(new VisualElement { style = { width = 16 } });
            toolbar.Add(new Label("Zoom:")
            { style = { unityFontStyleAndWeight = FontStyle.Bold, marginRight = 2 } });

            _zoomSlider = new Slider(ZoomMin, ZoomMax) { value = 1f, style = { width = 80 } };
            _zoomSlider.RegisterValueChangedCallback(e =>
            {
                _zoom = e.newValue;
                _scrollTime = Mathf.Min(_scrollTime, Mathf.Max(0, GetMaxScroll()));
                _needsRepaint = true;
            });
            toolbar.Add(_zoomSlider);

            _zoomLabel = new Label("1.0x") { style = { width = 36, fontSize = 9 } };
            toolbar.Add(_zoomLabel);

            toolbar.Add(new Button(() =>
            {
                _zoom = 1f; _zoomSlider.value = 1f; _scrollTime = 0; _needsRepaint = true;
            })
            { text = "Fit", style = { fontSize = 9, paddingLeft = 6, paddingRight = 6 } });

            root.Add(toolbar);

            _statusLabel = new Label
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 8, paddingTop = 2, paddingBottom = 2 }
            };
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

            // 底部按钮栏
            var bottomBar = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, paddingLeft = 4, paddingRight = 4, paddingBottom = 4 }
            };
            bottomBar.Add(new Button(() =>
            {
                if (TargetObject == null) return;
                Undo.RecordObject(TargetObject, $"Add {AddButtonText}");
                AddEntry();
                EditorUtility.SetDirty(TargetObject);
                _serializedObject?.Update();
                RebuildReorderableList();
                Repaint();
            })
            { text = $"+ {AddButtonText}" });

            bottomBar.Add(new Button(() =>
            {
                if (TargetObject == null) return;
                Undo.RecordObject(TargetObject, CollectButtonText);
                CollectFromChildren();
                EditorUtility.SetDirty(TargetObject);
                _serializedObject?.Update();
                RebuildReorderableList();
                Repaint();
            })
            { text = CollectButtonText });

            root.Add(bottomBar);
            RefreshStatus();
        }

        private void Update()
        {
            if (_needsRepaint)
            {
                _canvasContainer?.MarkDirtyRepaint();
                _needsRepaint = false;
            }
        }

        protected void RefreshStatus()
        {
            if (TargetObject == null) { _statusLabel.text = "No target selected"; return; }
            _statusLabel.text = $"Editing: {TargetObject.name}  |  Entries: {EntryCount}";
            _serializedObject?.Update();
        }

        // ——— 子类刷新入口 ———

        /// <summary>子类在数据变化后调用以刷新窗口</summary>
        protected void MarkDirty()
        {
            _needsRepaint = true;
            RefreshStatus();
        }

        // ========== 时间轴绘制 ==========

        private void EnsureStyles()
        {
            if (_timeScaleStyle != null) return;
            _timeScaleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter, fontSize = 9,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            _totalLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperRight, fontSize = 9,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            _dragLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow }
            };
            _blockLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 9,
                normal = { textColor = Color.white }, clipping = TextClipping.Clip
            };
        }

        private void DrawTimelineCanvas()
        {
            if (TargetObject == null || _serializedObject == null)
            {
                EditorGUILayout.HelpBox("No target selected.", MessageType.Info);
                return;
            }
            _serializedObject.Update();
            EnsureStyles();

            var rect = _canvasContainer.contentRect;
            if (rect.width < 20 || rect.height < 20) return;

            const float leftPad = 50f, rightPad = 16f, topPad = 24f, bottomPad = 16f;
            const float rowHeight = 18f, rowGap = 2f;

            float totalTime = CalculateTotalDuration();
            if (totalTime <= 0f) totalTime = 1f;

            // 拖拽中实时计算
            float dragTime = -1f;
            if (_dragIndex >= 0)
            {
                float dx = Event.current.mousePosition.x - _dragInitialMouse.x;
                float dt = (dx / rect.width) * (totalTime / _zoom) * DragSensitivity;
                dragTime = Mathf.Max(0, _dragInitialTime + dt);
                totalTime = Mathf.Max(totalTime, dragTime + DefaultDuration);

                // 跟手滚动
                float followMargin = _scrollTime + (totalTime / _zoom) * FollowMarginRatio;
                if (dragTime > followMargin)
                    _scrollTime += (dragTime - followMargin) * FollowSpeed;
            }

            float visibleTime = totalTime / _zoom;
            _scrollTime = Mathf.Clamp(_scrollTime, 0f, Mathf.Max(0f, totalTime - visibleTime));

            var tlRect = new Rect(rect.x + leftPad, rect.y + topPad,
                rect.width - leftPad - rightPad, rect.height - topPad - bottomPad);

            // 背景
            EditorGUI.DrawRect(tlRect, new Color(0.16f, 0.16f, 0.16f, 1f));
            EditorGUI.DrawRect(new Rect(tlRect.x - 1, tlRect.y - 1, tlRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(tlRect.x - 1, tlRect.yMax, tlRect.width + 2, 1), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(tlRect.x - 1, tlRect.y, 1, tlRect.height), new Color(0.3f, 0.3f, 0.3f));
            EditorGUI.DrawRect(new Rect(tlRect.xMax, tlRect.y, 1, tlRect.height), new Color(0.3f, 0.3f, 0.3f));

            // 时间刻度
            float timeStep = GetTimeStep(visibleTime);
            for (float t = 0; t <= visibleTime + 0.001f; t += timeStep)
            {
                float absT = _scrollTime + t;
                if (absT > totalTime + 0.001f) break;
                float x = tlRect.x + (t / visibleTime) * tlRect.width;
                EditorGUI.DrawRect(new Rect(x, tlRect.y, 1, tlRect.height), new Color(0.25f, 0.25f, 0.25f));
                GUI.Label(new Rect(x - 20, rect.y + 2, 40, 18), $"{absT:F1}s", _timeScaleStyle);
            }

            GUI.Label(new Rect(tlRect.xMax - 80, rect.y + 2, 80, 14), $"Total: {totalTime:F2}s", _totalLabelStyle);
            _zoomLabel.text = $"{_zoom:F1}x";

            // 收集所有块（修复：越界裁剪检查块结束时间，防止长块被错误跳过）
            int count = _entriesProp?.arraySize ?? 0;
            int maxRows = Mathf.Max(1, (int)((tlRect.height - 4) / (rowHeight + rowGap)));
            var blocks = new List<(Rect rect, int index, string label, Color color, float startTime)>();

            for (int i = 0; i < count; i++)
            {
                float st = GetEntryStartTime(i);
                float dur = GetEntryDuration(i);
                float endTime = st + dur;

                // 裁剪：块的结束时间在可视区左边界之前，或起始时间在可视区右边界之后，才跳过
                if (endTime < _scrollTime - CullingMargin || st > _scrollTime + visibleTime + CullingMargin)
                    continue;

                Color color = EntryColors[i % EntryColors.Length];
                float x = tlRect.x + ((st - _scrollTime) / visibleTime) * tlRect.width;
                int row = i % maxRows;
                float y = tlRect.y + 4 + row * (rowHeight + rowGap);
                float bw = Mathf.Max(BlockMinWidth, (dur / visibleTime) * tlRect.width);
                var br = new Rect(x, y, bw, rowHeight);
                string label = GetEntryLabel(i);
                blocks.Add((br, i, label, color, st));
            }

            // 拖拽显示时间
            float dragDisplayTime = _dragIndex >= 0
                ? Mathf.Max(0, Mathf.Round(dragTime / 0.01f) * 0.01f)
                : _dragInitialTime;

            // 绘制块（裁剪到时间轴区域内）
            foreach (var (br, idx, label, color, st) in blocks)
            {
                // 裁剪块 rect 到时间轴区域内
                var clipBr = ClipRect(br, tlRect);
                if (clipBr.width <= 0 || clipBr.height <= 0) continue;

                bool inDrag = idx == _dragIndex && Event.current.type == EventType.MouseDrag;
                bool hover = br.Contains(Event.current.mousePosition) && _dragIndex < 0;
                Color c = inDrag ? Color.white : hover ? Color.Lerp(color, Color.white, 0.3f) : color;
                EditorGUI.DrawRect(clipBr, c);

                if (inDrag || hover)
                {
                    var hoverLine = new Rect(br.x - 2, br.y, 2, br.height);
                    var clipHover = ClipRect(hoverLine, tlRect);
                    if (clipHover.width > 0) EditorGUI.DrawRect(clipHover, Color.white);
                }

                if (inDrag)
                {
                    float nx = tlRect.x + ((dragDisplayTime - _scrollTime) / visibleTime) * tlRect.width;
                    EditorGUI.DrawRect(new Rect(nx, tlRect.y, 2, tlRect.height), Color.yellow);
                    var pb = new Rect(nx, br.y, br.width, br.height);
                    var clipPb = ClipRect(pb, tlRect);
                    if (clipPb.width > 0)
                    {
                        Color pc = color; pc.a = 0.5f;
                        EditorGUI.DrawRect(clipPb, pc);
                    }
                    GUI.Label(new Rect(nx - 20, tlRect.yMax - 14, 50, 14), $"{dragDisplayTime:F2}s", _dragLabelStyle);
                }

                GUI.Label(clipBr, label, _blockLabelStyle);
            }

            // 滚动条
            float maxScroll = Mathf.Max(0f, totalTime - visibleTime);
            if (maxScroll > 0.01f)
            {
                float sby = tlRect.yMax + 2;
                EditorGUI.DrawRect(new Rect(tlRect.x, sby, tlRect.width, 6), new Color(0.12f, 0.12f, 0.12f));
                float thumbWidth = Mathf.Max(20, tlRect.width * (visibleTime / totalTime));
                float thumbX = tlRect.x + (_scrollTime / maxScroll) * (tlRect.width - thumbWidth);
                EditorGUI.DrawRect(new Rect(thumbX, sby, thumbWidth, 6), new Color(0.5f, 0.5f, 0.5f));
            }

            // ========== IMGUI 事件处理 ==========

            // 滚轮缩放
            if (Event.current.type == EventType.ScrollWheel && rect.Contains(Event.current.mousePosition))
            {
                float ct = _scrollTime + (Event.current.mousePosition.x - tlRect.x) / tlRect.width * visibleTime;
                ct = Mathf.Clamp(ct, 0, totalTime);
                _zoom *= Event.current.delta.y > 0 ? ZoomInFactor : ZoomOutFactor;
                _zoom = Mathf.Clamp(_zoom, ZoomMin, ZoomMax);
                _zoomSlider.SetValueWithoutNotify(_zoom);
                float nv = totalTime / _zoom;
                _scrollTime = Mathf.Clamp(
                    ct - (Event.current.mousePosition.x - tlRect.x) / tlRect.width * nv,
                    0, Mathf.Max(0, totalTime - nv));
                Event.current.Use();
            }

            // 鼠标按下
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
                            // P1 修复：拖拽开始时记录 Undo
                            if (TargetObject != null)
                                Undo.RecordObject(TargetObject, "Drag Entry Start Time");
                            _dragIndex = i;
                            _dragInitialTime = st;
                            _dragInitialMouse = Event.current.mousePosition;
                            hit = true;
                            Event.current.Use();
                            break;
                        }
                    }
                    if (!hit)
                    {
                        _isPanning = true;
                        _panStartMouse = Event.current.mousePosition;
                        _panStartScroll = _scrollTime;
                        Event.current.Use();
                    }
                }
            }

            // 鼠标拖拽
            if (Event.current.type == EventType.MouseDrag)
            {
                if (_dragIndex >= 0 && _dragIndex < count && _entriesProp != null)
                {
                    var ep = _entriesProp.GetArrayElementAtIndex(_dragIndex);
                    ep.FindPropertyRelative(StartTimePropertyName).floatValue = dragDisplayTime;
                    _serializedObject.ApplyModifiedProperties();
                    Event.current.Use();
                }
                else if (_isPanning)
                {
                    _scrollTime = Mathf.Clamp(
                        _panStartScroll + (_panStartMouse.x - Event.current.mousePosition.x) / tlRect.width * visibleTime,
                        0, Mathf.Max(0, totalTime - visibleTime));
                    Event.current.Use();
                }
            }

            // 鼠标松开
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                if (_dragIndex >= 0 && TargetObject != null)
                    EditorUtility.SetDirty(TargetObject);
                _dragIndex = -1;
                _isPanning = false;
                Event.current.Use();
            }

            _serializedObject.ApplyModifiedProperties();
        }

        // ========== 条目列表（可拖拽排序） ==========

        private void RebuildReorderableList()
        {
            if (_serializedObject == null) return;
            _entriesProp = _serializedObject.FindProperty(EntriesPropertyName);
            if (_entriesProp == null) return;

            _reorderableList = new ReorderableList(_serializedObject, _entriesProp, true, true, false, false);
            _reorderableList.drawHeaderCallback = r =>
                EditorGUI.LabelField(r, ListHeader, EditorStyles.boldLabel);
            _reorderableList.drawElementCallback = DrawListElement;
            _reorderableList.elementHeight = EditorGUIUtility.singleLineHeight + 2;
            _reorderableList.onReorderCallback = _ =>
            {
                if (TargetObject != null) EditorUtility.SetDirty(TargetObject);
            };
        }

        private void DrawEntriesList()
        {
            if (TargetObject == null || _serializedObject == null)
            {
                EditorGUILayout.HelpBox("No target selected.", MessageType.Info);
                return;
            }
            _serializedObject.Update();
            if (_entriesProp == null) return;

            // 安全重建：count 不匹配时重建
            if (_reorderableList == null || _reorderableList.count != _entriesProp.arraySize)
                RebuildReorderableList();

            if (_reorderableList == null) return;
            _reorderableList.DoLayoutList();
            _serializedObject.ApplyModifiedProperties();
        }

        /// <summary>子类可重写以自定义列表行绘制，默认实现提供基础布局</summary>
        protected virtual void DrawListElement(Rect r, int index, bool isActive, bool isFocused)
        {
            if (index >= _entriesProp.arraySize) return;
            var ep = _entriesProp.GetArrayElementAtIndex(index);
            var tp = ep.FindPropertyRelative(StartTimePropertyName);
            Color c = EntryColors[index % EntryColors.Length];
            float x = r.x + 24;
            EditorGUI.DrawRect(new Rect(x, r.y + 1, 10, r.height - 2), c);
            x += 14;
            EditorGUI.LabelField(new Rect(x, r.y, 24, r.height), $"#{index}");
            x += 26;

            // 子类负责绘制具体字段
            DrawEntryFields(r, index, ep, tp, x);

            if (GUI.Button(new Rect(r.xMax - 22, r.y, 22, r.height), "x"))
            {
                _entriesProp.DeleteArrayElementAtIndex(index);
                _serializedObject.ApplyModifiedProperties();
                RebuildReorderableList();
            }
        }

        /// <summary>子类重写以绘制条目具体字段（名称、引用等）</summary>
        protected abstract void DrawEntryFields(Rect r, int index, SerializedProperty entryProp, SerializedProperty startTimeProp, float startX);

        // ========== 辅助 ==========

        /// <summary>将 rect 裁剪到 clip 区域内</summary>
        private static Rect ClipRect(Rect r, Rect clip)
        {
            float x = Mathf.Max(r.x, clip.x);
            float y = Mathf.Max(r.y, clip.y);
            float xMax = Mathf.Min(r.x + r.width, clip.x + clip.width);
            float yMax = Mathf.Min(r.y + r.height, clip.y + clip.height);
            float w = Mathf.Max(0, xMax - x);
            float h = Mathf.Max(0, yMax - y);
            return new Rect(x, y, w, h);
        }

        private float GetMaxScroll()
        {
            float total = CalculateTotalDuration();
            return Mathf.Max(0, total - total / _zoom);
        }

        protected static float GetTimeStep(float t) =>
            t <= 1f ? 0.25f : t <= 3f ? 0.5f : t <= 10f ? 1f : t <= 30f ? 5f : 10f;
    }
}
#endif
