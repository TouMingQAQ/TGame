using TMPro;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// 滚动 TMP_Text。
    /// 通过每帧基于原始 <see cref="TMP_TextInfo"/> 顶点写入偏移实现,不走 DOTween。
    ///
    /// 注意:
    /// 1. 持有本组件时,**不能启用** TMP 的 autoSizeTextContainer(本类会强制设 false),
    ///    否则 TMP 重建 mesh 后会丢弃我们修改的顶点。
    /// 2. _scrollSpeed 单位为像素/秒,正值向 <see cref="ScrollDirection"/> 方向滚,负值反向。
    /// 3. Loop 模式会整段文本滚到末端后回到起点并等待 <see cref="_loopInterval"/> 秒,
    ///    不做字符级回卷,避免 "Hello" 显示成 "elloH"。
    /// 4. 文本宽/高小于容器宽/高时,不会滚动(无内容可滚)。
    /// 5. 默认 <see cref="_enforceNoWrap"/> = true,会在 OnEnable 强制把
    ///    <see cref="TMP_Text.textWrappingMode"/> 设为 <see cref="TextWrappingModes.NoWrap"/>。
    ///    原因:换行会让 preferredWidth 不等于单行文本宽度,loop 回卷偏差。
    ///    如果业务方需要 Wrapping,关闭本开关。
    /// </summary>
    [ExecuteAlways]
    public class TextMeshProScroller : MonoBehaviour
    {
        /// <summary>滚动方向</summary>
        public enum ScrollDirection { Left, Right, Up, Down }

        [Header("Scroll Settings")]
        [Tooltip("滚动速度(像素/秒)。正值向 ScrollDirection 方向滚,负值反向。0 = 静止。")]
        [SerializeField]
        protected float _scrollSpeed = 50f;

        [Tooltip("是否在 OnEnable 时自动启动滚动")]
        [SerializeField]
        protected bool _autoPlay = true;

        [Tooltip("滚动方向(Left/Right 为水平,Up/Down 为垂直)")]
        [SerializeField]
        protected ScrollDirection _direction = ScrollDirection.Left;

        [Tooltip("滚到末端后是否回到起点并等待下一轮。关闭则停在末端。")]
        [SerializeField]
        protected bool _loop = true;

        [Tooltip("Loop 模式下每轮滚动结束后,回到起点等待的时间(秒)。")]
        [SerializeField]
        protected float _loopInterval = 1f;

        [Tooltip("OnEnable 时重置累计偏移到 0")]
        [SerializeField]
        protected bool _resetOnEnable = true;

        [Tooltip("不可见时暂停滚动")]
        [SerializeField]
        protected bool _pauseWhenNotVisible = true;

        [Tooltip("OnEnable 时强制把 _text.textWrappingMode 设为 NoWrap。" +
                 "换行会让 preferredWidth 失真,loop 回卷计算偏差;关闭本开关则保留 TMP 自身的 wrapping 设置。")]
        [SerializeField]
        protected bool _enforceNoWrap = true;

        [Header("References")]
        [SerializeField]
        protected TMP_Text _text;

        private float _offset;
        private bool _isPlaying;
        private bool _isVisible = true;
        private bool _cacheDirty = true;
        private float _cachedTextWidth;
        private float _cachedTextHeight;
        private float _cachedViewportWidth;
        private float _cachedViewportHeight;
        private TMP_MeshInfo[] _cachedMeshInfo;
        private bool _isRefreshingMesh;
        private float _loopIntervalTimer;
        private bool _isWaitingLoopInterval;
        private Vector3 _initialOffset;

        private bool IsHorizontal => _direction is ScrollDirection.Left or ScrollDirection.Right;
        private float ViewportSize => IsHorizontal ? _cachedViewportWidth : _cachedViewportHeight;
        private float TextSize => IsHorizontal ? _cachedTextWidth : _cachedTextHeight;
        private float OverflowRange => TextSize - ViewportSize;
        protected virtual void Awake()
        {
            EnsureTextReference();
        }

        protected virtual void OnEnable()
        {
            EnsureTextReference();
            if (_text == null) return;

            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

            if (_enforceNoWrap && _text.textWrappingMode != TextWrappingModes.NoWrap)
                _text.textWrappingMode = TextWrappingModes.NoWrap;

            if (_text.autoSizeTextContainer)
                _text.autoSizeTextContainer = false;

            if (_resetOnEnable)
                _offset = 0f;

            _isVisible = true;
            _cacheDirty = true;
            _cachedMeshInfo = null;
            _loopIntervalTimer = 0f;
            _isWaitingLoopInterval = false;

            if (_autoPlay)
                Play();
            else
                Stop();
        }

        protected virtual void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            Stop();
            RestoreOriginalVertices();
        }

        protected virtual void OnBecameVisible()
        {
            _isVisible = true;
        }

        protected virtual void OnBecameInvisible()
        {
            _isVisible = false;
        }

        private void Update()
        {
            if (_text == null && !EnsureTextReference()) return;

            if (_text.autoSizeTextContainer)
                _text.autoSizeTextContainer = false;

            if (!_isPlaying) return;
            if (_pauseWhenNotVisible && !IsVisibleForScroll()) return;

            TickScroll(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            if (_text == null && !EnsureTextReference()) return;

            if (_text.autoSizeTextContainer)
                _text.autoSizeTextContainer = false;

            if (_isPlaying || _offset != 0f || _cacheDirty)
                ApplyOffset();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            EnsureTextReference();
            _loopInterval = Mathf.Max(0f, _loopInterval);
            _cacheDirty = true;
            _cachedMeshInfo = null;
        }
#endif

        private void Reset()
        {
            EnsureTextReference();
        }

        /// <summary>
        /// 从头开始播放:把累计偏移归零,启动滚动。
        /// 与 <see cref="Stop"/> 配对使用。
        /// </summary>
        public virtual void Play()
        {
            _offset = 0f;
            _cacheDirty = true;
            _cachedMeshInfo = null;
            _loopIntervalTimer = 0f;
            _isWaitingLoopInterval = false;
            _isPlaying = true;
        }

        /// <summary>
        /// 暂停并归位:停止滚动,累计偏移清零。
        /// 与 <see cref="Play"/> 配对使用。
        /// </summary>
        public virtual void Stop()
        {
            _isPlaying = false;
            _offset = 0f;
            _cacheDirty = true;
            _loopIntervalTimer = 0f;
            _isWaitingLoopInterval = false;
        }

        /// <summary>当前是否在播放(返回 _isPlaying,不是 _isVisible)</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>当前累计偏移(像素)</summary>
        public float CurrentOffset => _offset;

        private void TickScroll(float dt)
        {
            EnsureCache();

            float overflowRange = OverflowRange;
            if (overflowRange <= 0f || Mathf.Approximately(_scrollSpeed, 0f))
            {
                _offset = 0f;
                _loopIntervalTimer = 0f;
                _isWaitingLoopInterval = false;
                return;
            }

            if (_loop && _isWaitingLoopInterval)
            {
                _loopIntervalTimer -= dt;
                if (_loopIntervalTimer > 0f) return;

                _offset = 0f;
                _loopIntervalTimer = 0f;
                _isWaitingLoopInterval = false;
            }

            _offset += _scrollSpeed * dt;

            if (_loop)
            {
                if (_offset >= overflowRange || _offset <= -overflowRange)
                {
                    _offset = _offset >= 0f ? overflowRange : -overflowRange;
                    _loopIntervalTimer = Mathf.Max(0f, _loopInterval);
                    _isWaitingLoopInterval = _loopIntervalTimer > 0f;

                    if (!_isWaitingLoopInterval)
                        _offset = 0f;
                }

                return;
            }

            if (_offset >= overflowRange)
            {
                _offset = overflowRange;
                _isPlaying = false;
            }
            else if (_offset <= -overflowRange)
            {
                _offset = -overflowRange;
                _isPlaying = false;
            }
        }

        /// <summary>
        /// 把累计偏移应用到所有顶点。每次都从 _cachedMeshInfo 的原始顶点重算,
        /// 避免把偏移反复叠加到已经修改过的顶点上。
        /// </summary>
        private void ApplyOffset()
        {
            if (!EnsureMeshCache()) return;

            float overflowRange = OverflowRange;
            float effectiveOffset = overflowRange > 0f ? _offset : 0f;
            var offsetVec = _initialOffset + GetOffsetVector(effectiveOffset);
            bool anyModified = false;

            var textInfo = _text.textInfo;
            int materialCount = Mathf.Min(textInfo.meshInfo.Length, _cachedMeshInfo.Length);

            for (int i = 0; i < materialCount; i++)
            {
                var src = _cachedMeshInfo[i].vertices;
                var dst = textInfo.meshInfo[i].vertices;
                if (src == null || dst == null) continue;

                int count = Mathf.Min(src.Length, dst.Length);
                for (int v = 0; v < count; v++)
                    dst[v] = src[v] + offsetVec;

                anyModified |= count > 0;
            }

            if (anyModified)
                _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private void RestoreOriginalVertices()
        {
            if (_text == null || _cachedMeshInfo == null) return;

            var textInfo = _text.textInfo;
            if (textInfo == null || textInfo.meshInfo == null) return;

            bool anyModified = false;
            int materialCount = Mathf.Min(textInfo.meshInfo.Length, _cachedMeshInfo.Length);

            for (int i = 0; i < materialCount; i++)
            {
                var src = _cachedMeshInfo[i].vertices;
                var dst = textInfo.meshInfo[i].vertices;
                if (src == null || dst == null) continue;

                int count = Mathf.Min(src.Length, dst.Length);
                for (int v = 0; v < count; v++)
                    dst[v] = src[v];

                anyModified |= count > 0;
            }

            if (anyModified)
                _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        private bool EnsureTextReference()
        {
            if (_text != null) return true;
            return TryGetComponent(out _text);
        }

        private void EnsureCache()
        {
            if (!_cacheDirty) return;

            if (_text == null)
            {
                _cacheDirty = false;
                return;
            }

            if (_text.autoSizeTextContainer)
                _text.autoSizeTextContainer = false;

            try
            {
                _isRefreshingMesh = true;
                _text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            }
            finally
            {
                _isRefreshingMesh = false;
            }

            _cachedTextWidth = _text.preferredWidth;
            _cachedTextHeight = _text.preferredHeight;

            var rect = _text.rectTransform.rect;
            _cachedViewportWidth = Mathf.Max(0f, rect.width);
            _cachedViewportHeight = Mathf.Max(0f, rect.height);
            _cachedMeshInfo = _text.textInfo.CopyMeshInfoVertexData();
            UpdateInitialOffset();
            _cacheDirty = false;
        }

        private void UpdateInitialOffset()
        {
            _initialOffset = Vector3.zero;
            if (OverflowRange <= 0f) return;

            var bounds = _text.textBounds;
            if (bounds.size == Vector3.zero) return;

            var rect = _text.rectTransform.rect;
            _initialOffset = _direction switch
            {
                ScrollDirection.Left => new Vector3(rect.xMin - bounds.min.x, 0f, 0f),
                ScrollDirection.Right => new Vector3(rect.xMax - bounds.max.x, 0f, 0f),
                ScrollDirection.Up => new Vector3(0f, rect.yMax - bounds.max.y, 0f),
                ScrollDirection.Down => new Vector3(0f, rect.yMin - bounds.min.y, 0f),
                _ => Vector3.zero,
            };
        }

        private bool EnsureMeshCache()
        {
            EnsureCache();
            return _text != null && _cachedMeshInfo != null;
        }

        private bool IsVisibleForScroll()
        {
            if (!_isVisible) return false;
            if (!_text.isActiveAndEnabled) return false;
            if (_text.canvasRenderer != null && _text.canvasRenderer.cull) return false;
            return true;
        }

        private void OnTextChanged(Object obj)
        {
            if (obj != _text) return;
            if (_isRefreshingMesh) return;

            _cacheDirty = true;
            _cachedMeshInfo = null;
            _offset = 0f;
            _loopIntervalTimer = 0f;
            _isWaitingLoopInterval = false;
        }

        private Vector3 GetOffsetVector(float offset)
        {
            return _direction switch
            {
                ScrollDirection.Left => new Vector3(-offset, 0f, 0f),
                ScrollDirection.Right => new Vector3(offset, 0f, 0f),
                ScrollDirection.Up => new Vector3(0f, offset, 0f),
                ScrollDirection.Down => new Vector3(0f, -offset, 0f),
                _ => Vector3.zero,
            };
        }

    }
}
