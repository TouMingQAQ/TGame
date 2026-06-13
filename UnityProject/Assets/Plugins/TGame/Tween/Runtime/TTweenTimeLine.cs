using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// Tween 时间线编排器。
    /// 引用多个 <see cref="TTweenPlay"/> 实例，在一条时间线上用 <c>Insert(startTime, tween)</c> 统一编排播放。
    /// 每个 TTweenPlay 可以是独立的预制体，通过 Inspector 列表或 "Collect Plays" 按钮收集。
    /// </summary>
    [AddComponentMenu("TGame/Tween/TTweenTimeLine")]
    public class TTweenTimeLine : MonoBehaviour
    {
        [System.Serializable]
        public struct TimeLinePlayEntry
        {
            [Tooltip("要播放的 TTweenPlay 组件")]
            public TTweenPlay play;

            [Tooltip("在时间线上的起始时间（秒）")]
            public float startTime;

            [Tooltip("条目别名，用于在编辑器时间轴与列表中区分相同 Play。为空则回退到 Play 名称。")]
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

        [Header("TimeLine")]
        [SerializeField]
        private List<TimeLinePlayEntry> _entries = new List<TimeLinePlayEntry>();

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

        public List<TimeLinePlayEntry> Entries => _entries;

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

        /// <summary>
        /// 构建时间线并播放。
        /// 遍历所有 Entry，调用每个 play.BuildTween()，
        /// 用 Insert(startTime, tween) 组合成主 Sequence 播放。
        /// </summary>
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

        // ——— 编辑器辅助 ———

        [ContextMenu("Collect Plays")]
        public void CollectPlaysFromChildren()
        {
            _entries.Clear();
            var plays = GetComponentsInChildren<TTweenPlay>();
            foreach (var play in plays)
            {
                _entries.Add(new TimeLinePlayEntry
                {
                    play = play,
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

        /// <summary>
        /// 计算时间线总时长（所有 entry 的最大 endTime）。
        /// 用于 Editor 可视化时间轴的比例计算。
        /// </summary>
        internal float CalculateTotalDuration()
        {
            float maxTime = 0f;
            foreach (var entry in _entries)
            {
                if (entry.play == null) continue;
                float endTime = entry.startTime + EstimatePlayDuration(entry.play);
                if (endTime > maxTime)
                    maxTime = endTime;
            }
            return maxTime > 0f ? maxTime : 1f;
        }

        /// <summary>
        /// 估算一个 TTweenPlay 的动画时长（取 NodeEntries 中最大 startTime + duration）。
        /// 用于 Editor 可视化。
        /// </summary>
        internal static float EstimatePlayDuration(TTweenPlay play)
        {
            if (play == null) return 0.3f;
            float maxEnd = 0f;
            var entries = play.NodeEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.node == null) continue;
                maxEnd = Mathf.Max(maxEnd, entry.startTime + entry.node.GetPlaybackDuration());
            }
            return maxEnd > 0f ? maxEnd : 0.3f;
        }

        private Sequence BuildSequenceFromEntries()
        {
            if (_entries.Count == 0)
                return null;

            var seq = DOTween.Sequence();
            seq.SetAutoKill(false);
            seq.SetUpdate(_ignoreTimeScale);
            seq.SetLink(gameObject);

            if (_loops != 1)
                seq.SetLoops(_loops, _loopType);

            foreach (var entry in _entries)
            {
                if (entry.play == null) continue;

                var tween = entry.play.BuildTween();
                if (tween == null) continue;

                seq.Insert(entry.startTime, tween);
            }

            return seq;
        }
    }
}
