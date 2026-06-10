using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// 位置移动构造器。
    /// To 模式：从 _fromValue → _targetValue
    /// From 模式：从 _targetValue → _fromValue
    /// Additive：相对当前位移动画（不使用 _fromValue）
    ///
    /// 使用 ChangeStartValue 避免 BuildTween() 时修改 Transform，
    /// 包装 Sequence 确保播放时正确设值起点，多节点共享同一 Transform 时互不干扰。
    /// </summary>
    [AddComponentMenu("TGame/Tween/Nodes/TTweenMove")]
    public class TTweenMove : TTweenNode
    {
        public enum MoveMode { To, From, Additive }

        [Header("Target")]
        [SerializeField]
        [Tooltip("要动画的目标 Transform，为空时使用自身")]
        private Transform _target;

        [Header("Animation")]
        [SerializeField]
        private MoveMode _mode = MoveMode.To;

        [SerializeField]
        private Vector3 _fromValue = Vector3.zero;

        [SerializeField]
        private Vector3 _targetValue = Vector3.zero;

        [SerializeField]
        private float _duration = 0.3f;

        [Header("Easing")]
        [SerializeField]
        private Ease _ease = Ease.OutQuad;

        [SerializeField]
        private AnimationCurve _customCurve;

        [Header("Options")]
        [SerializeField]
        private bool _snapping = false;

        private Transform Target => _target ? _target : transform;

        public override DG.Tweening.Tween BuildTween()
        {
            // Additive：相对偏移，不使用 FromValue 和 ChangeStartValue
            if (_mode == MoveMode.Additive)
            {
                var addTween = Target.DOMove(Target.position + _targetValue, _duration, _snapping);
                ApplyEase(addTween);
                return addTween;
            }

            // 确定起点和终点
            Vector3 start = _fromValue;
            Vector3 end = _targetValue;
            if (_mode == MoveMode.From)
            {
                // From：反过来，从 targetValue→fromValue（类似 .From() 的语义）
                start = _targetValue;
                end = _fromValue;
            }

            var tween = Target.DOMove(end, _duration, _snapping);
            tween.ChangeStartValue(start);

            var seq = DOTween.Sequence();
            seq.AppendCallback(() => { Target.position = start; });
            seq.Append(tween);

            ApplyEase(tween);
            return seq;
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
            _target = transform;
        }
    }
}
