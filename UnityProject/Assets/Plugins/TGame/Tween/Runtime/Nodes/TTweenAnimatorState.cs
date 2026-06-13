using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// Animator State 构造器。
    /// 用 DOTween 驱动 Animator 的 normalizedTime,把 Animator Controller 中的一段 State 接入 TTweenPlay 时间轴。
    /// </summary>
    [AddComponentMenu("TGame/Tween/Nodes/TTweenAnimatorState")]
    public class TTweenAnimatorState : TTweenNode
    {
        private enum DurationMode
        {
            AnimatorStateLength,
            NodeDuration,
        }

        [Header("Target")]
        [SerializeField]
        [Tooltip("要采样的 Animator,为空时使用自身或父级 Animator")]
        private Animator _target;

        [Header("State")]
        [SerializeField]
        [Tooltip("Animator Controller 中的 State 名称")]
        private string _stateName;

        [SerializeField]
        [Tooltip("Animator Layer 索引")]
        private int _layer = 0;

        [Header("Range")]
        [SerializeField]
        [Tooltip("起始 normalizedTime。0 = 动画开头,1 = 第一轮结尾")]
        private float _fromNormalizedTime = 0f;

        [SerializeField]
        [Tooltip("结束 normalizedTime。可大于 1 播放多轮循环 State")]
        private float _toNormalizedTime = 1f;

        [Header("Duration")]
        [SerializeField]
        [Tooltip("AnimatorStateLength = 尽量按 Animator 原生速度播放;NodeDuration = 使用 TTweenNode.Duration 手动控制时长")]
        private DurationMode _durationMode = DurationMode.AnimatorStateLength;

        [SerializeField]
        [Tooltip("可选。指定后优先用该 Clip.length 计算原生时长;为空时尝试按 StateName 在 Animator Controller 中查找同名 Clip")]
        private AnimationClip _clip;

        [SerializeField]
        [Tooltip("按原生时长播放时,是否把 Animator.speed 计入速度计算")]
        private bool _respectAnimatorSpeed = true;

        [Header("Animator Control")]
        [SerializeField]
        [Tooltip("播放 Tween 期间冻结 Animator 自身时间推进,只由 Tween 采样")]
        private bool _freezeAnimatorSpeed = true;

        [SerializeField]
        [Tooltip("Tween 完成、回退或 Kill 时恢复 Animator.speed")]
        private bool _restoreSpeed = true;

        [Header("Easing")]
        [SerializeField]
        private Ease _ease = Ease.Linear;

        [SerializeField]
        private AnimationCurve _customCurve;

        private float _savedSpeed = 1f;
        private bool _speedCaptured;
        private int _stateHash;

        public Animator Target
        {
            get => _target;
            set => _target = value;
        }

        public string StateName
        {
            get => _stateName;
            set => _stateName = value;
        }

        public int Layer
        {
            get => _layer;
            set => _layer = value;
        }

        public override DG.Tweening.Tween BuildTween()
        {
            var target = ResolveTarget();
            if (target == null)
            {
                Debug.LogWarning($"[TTweenAnimatorState] {name} missing Animator target", this);
                return null;
            }

            if (string.IsNullOrEmpty(_stateName))
            {
                Debug.LogWarning($"[TTweenAnimatorState] {name} missing state name", this);
                return null;
            }

            if (!TryResolveStateHash(target, out _stateHash))
            {
                Debug.LogWarning($"[TTweenAnimatorState] Animator does not have state '{_stateName}' on layer {_layer}", this);
                return null;
            }

            float normalizedTime = _fromNormalizedTime;
            float duration = ResolveDuration(target);

            var tween = DOTween.To(
                () => normalizedTime,
                value =>
                {
                    normalizedTime = value;
                    Sample(target, normalizedTime);
                },
                _toNormalizedTime,
                duration);

            ApplyEase(tween);

            var seq = DOTween.Sequence();
            seq.Append(tween);
            seq.OnPlay(() =>
            {
                CaptureAnimatorSpeed(target);
                Sample(target, normalizedTime);
            });
            seq.OnComplete(() => RestoreAnimatorSpeed(target));
            seq.OnRewind(() => RestoreAnimatorSpeed(target));
            seq.OnKill(() => RestoreAnimatorSpeed(target));

            return seq;
        }

        private Animator ResolveTarget()
        {
            if (_target != null)
                return _target;

            _target = GetComponent<Animator>();
            if (_target == null)
                _target = GetComponentInParent<Animator>();
            return _target;
        }

        private bool TryResolveStateHash(Animator target, out int stateHash)
        {
            stateHash = 0;
            if (target == null)
                return false;

            if (_layer < 0 || _layer >= target.layerCount)
                return false;

            int directHash = Animator.StringToHash(_stateName);
            if (target.HasState(_layer, directHash))
            {
                stateHash = directHash;
                return true;
            }

            string layerName = target.GetLayerName(_layer);
            int fullPathHash = Animator.StringToHash($"{layerName}.{_stateName}");
            if (target.HasState(_layer, fullPathHash))
            {
                stateHash = fullPathHash;
                return true;
            }

            return false;
        }

        private float ResolveDuration(Animator target)
        {
            if (_durationMode == DurationMode.NodeDuration)
                return Mathf.Max(0.0001f, Duration);

            float range = Mathf.Abs(_toNormalizedTime - _fromNormalizedTime);
            if (range <= 0f)
                return 0.0001f;

            if (TryGetClipLength(target, out var clipLength))
                return Mathf.Max(0.0001f, clipLength * range / GetAnimatorSpeedFactor(target));

            if (TryGetStateLength(target, out var stateLength))
                return Mathf.Max(0.0001f, stateLength * range / GetAnimatorSpeedFactor(target));

            Debug.LogWarning($"[TTweenAnimatorState] Cannot resolve duration for '{_stateName}', fallback to node Duration", this);
            return Mathf.Max(0.0001f, Duration);
        }

        private bool TryGetClipLength(Animator target, out float clipLength)
        {
            clipLength = 0f;
            if (_clip != null)
            {
                clipLength = _clip.length;
                return clipLength > 0f;
            }

            var controller = target != null ? target.runtimeAnimatorController : null;
            if (controller == null)
                return false;

            string shortStateName = GetShortStateName(_stateName);
            var clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null) continue;
                if (clip.name == _stateName || clip.name == shortStateName)
                {
                    clipLength = clip.length;
                    return clipLength > 0f;
                }
            }

            return false;
        }

        private bool TryGetStateLength(Animator target, out float stateLength)
        {
            stateLength = 0f;
            if (target == null || _layer < 0 || _layer >= target.layerCount)
                return false;

            var currentState = target.GetCurrentAnimatorStateInfo(_layer);
            int currentHash = currentState.fullPathHash;
            float currentNormalizedTime = currentState.normalizedTime;
            float currentSpeed = target.speed;

            target.Play(_stateHash, _layer, _fromNormalizedTime);
            target.Update(0f);

            var sampledState = target.GetCurrentAnimatorStateInfo(_layer);
            stateLength = sampledState.length;

            if (currentHash != 0)
            {
                target.Play(currentHash, _layer, currentNormalizedTime);
                target.Update(0f);
            }
            target.speed = currentSpeed;

            return stateLength > 0f;
        }

        private float GetAnimatorSpeedFactor(Animator target)
        {
            if (!_respectAnimatorSpeed || target == null)
                return 1f;

            float speed = Mathf.Abs(target.speed);
            return speed > 0.0001f ? speed : 1f;
        }

        private static string GetShortStateName(string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
                return string.Empty;

            int dotIndex = stateName.LastIndexOf('.');
            return dotIndex >= 0 && dotIndex + 1 < stateName.Length
                ? stateName[(dotIndex + 1)..]
                : stateName;
        }

        private void CaptureAnimatorSpeed(Animator target)
        {
            if (!_freezeAnimatorSpeed || target == null)
                return;

            if (!_speedCaptured)
            {
                _savedSpeed = target.speed;
                _speedCaptured = true;
            }
            target.speed = 0f;
        }

        private void RestoreAnimatorSpeed(Animator target)
        {
            if (!_restoreSpeed || !_speedCaptured || target == null)
                return;

            target.speed = _savedSpeed;
            _speedCaptured = false;
        }

        private void Sample(Animator target, float normalizedTime)
        {
            if (target == null)
                return;

            target.Play(_stateHash, _layer, normalizedTime);
            target.Update(0f);
        }

        private void ApplyEase(DG.Tweening.Tween tween)
        {
            if (_customCurve != null && _customCurve.length > 0)
                tween.SetEase(_customCurve);
            else
                tween.SetEase(_ease);
        }

        private void Reset()
        {
            _target = GetComponent<Animator>();
            if (_target == null)
                _target = GetComponentInParent<Animator>();
        }
    }
}
