using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// Tween 播放器。
    /// 收集子 <see cref="TTweenNode"/> 组件并按时间轴编排动画。
    /// 可作为独立预制体设计，由 <see cref="TTweenTimeLine"/> 统一编排。
    /// </summary>
    [AddComponentMenu("TGame/Tween/TTweenPlay")]
    public class TTweenPlay : MonoBehaviour
    {
        [System.Serializable]
        public struct NodeEntry
        {
            [Tooltip("子 TTweenNode 组件")]
            public TTweenNode node;

            [Tooltip("在当前 Play 内的时间轴起始时间（秒）")]
            public float startTime;

            [Tooltip("条目别名，用于在编辑器时间轴与列表中区分相同类型的多个节点。为空则回退到节点名。")]
            public string alias;
        }

        [Header("Play Settings")]
        [SerializeField]
        [Tooltip("Start 时自动播放动画")]
        private bool _autoPlayOnStart = false;

        [SerializeField]
        [Tooltip("动画循环次数，0 = 无限循环")]
        private int _loops = 1;

        [SerializeField]
        private LoopType _loopType = LoopType.Restart;

        [Header("Time")]
        [SerializeField]
        [Tooltip("是否忽略 Time.timeScale")]
        private bool _ignoreTimeScale = true;

        [Header("Node List")]
        [SerializeField]
        private List<NodeEntry> _nodeEntries = new List<NodeEntry>();

        private Sequence _runtimeSequence;

        // ——— Public Properties ———

        public bool AutoPlayOnStart
        {
            get => _autoPlayOnStart;
            set => _autoPlayOnStart = value;
        }

        public int Loops
        {
            get => _loops;
            set => _loops = value;
        }

        public LoopType LoopType
        {
            get => _loopType;
            set => _loopType = value;
        }

        public bool IgnoreTimeScale
        {
            get => _ignoreTimeScale;
            set => _ignoreTimeScale = value;
        }

        public List<NodeEntry> NodeEntries => _nodeEntries;

        public bool IsPlaying => _runtimeSequence != null && _runtimeSequence.active;

        // ——— Unity Lifecycle ———

        private void Start()
        {
            if (_autoPlayOnStart)
                Play();
        }

        private void OnDestroy()
        {
            Kill();
        }

        // ——— Public API ———

        public void Play()
        {
            Kill();
            _runtimeSequence = BuildSequenceFromEntries();
            _runtimeSequence?.Play();
        }

        public void Stop()
        {
            _runtimeSequence?.Rewind();
        }

        public void Pause()
        {
            _runtimeSequence?.Pause();
        }

        public void Resume()
        {
            if (_runtimeSequence != null && !_runtimeSequence.IsPlaying())
                _runtimeSequence.Play();
        }

        public void Kill()
        {
            _runtimeSequence?.Kill();
            _runtimeSequence = null;
        }

        /// <summary>
        /// 构建当前 Play 的动画 Sequence。
        /// 供 <see cref="TTweenTimeLine"/> 调用以实现时间线编排。
        /// 注意：返回类型必须使用完全限定名 DG.Tweening.Tween，
        /// 因为在 namespace TGame.Tween 内部裸写 Tween 可能导致编译器解析歧义。
        /// </summary>
        public DG.Tweening.Tween BuildTween()
        {
            return BuildSequenceFromEntries();
        }

        // ——— 编辑器辅助 ———

        /// <summary>
        /// 计算当前 Play 的动画总时长（所有 entry 中最大 startTime + duration）。
        /// 用于 Editor 可视化时间轴的比例计算。
        /// </summary>
        internal float CalculateTotalDuration()
        {
            float maxTime = 0f;
            foreach (var e in _nodeEntries)
            {
                float dur = e.node != null ? e.node.GetPlaybackDuration() : DefaultEntryDuration;
                float end = e.startTime + dur;
                if (end > maxTime) maxTime = end;
            }
            return maxTime > 0f ? maxTime : 1f;
        }

        /// <summary>
        /// 当 entry.node 为 null 时使用的默认时长，用于可视化编辑器。
        /// </summary>
        internal const float DefaultEntryDuration = 0.3f;

        [ContextMenu("Collect Children")]
        public void CollectChildren()
        {
            _nodeEntries.Clear();
            var nodes = GetComponentsInChildren<TTweenNode>();
            foreach (var node in nodes)
            {
                _nodeEntries.Add(new NodeEntry
                {
                    node = node,
                    startTime = 0f
                });
            }
        }

        [ContextMenu("Preview Play")]
        public void PreviewPlay()
        {
            Play();
        }

        [ContextMenu("Preview Stop")]
        public void PreviewStop()
        {
            Kill();
        }

        // ——— Internal ———

        private Sequence BuildSequenceFromEntries()
        {
            if (_nodeEntries.Count == 0)
                return null;

            var seq = DOTween.Sequence();
            seq.SetAutoKill(false);
            seq.SetUpdate(_ignoreTimeScale);
            seq.SetLink(gameObject);

            if (_loops != 1)
                seq.SetLoops(_loops, _loopType);

            foreach (var entry in _nodeEntries)
            {
                if (entry.node == null) continue;

                var tween = entry.node.BuildTween();
                if (tween == null) continue;

                seq.Insert(entry.startTime, tween);
            }

            return seq;
        }
    }
}
