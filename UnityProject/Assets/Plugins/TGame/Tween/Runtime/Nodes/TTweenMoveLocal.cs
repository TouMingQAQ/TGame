using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// 局部坐标移动构造器。
    /// To 模式：从 _fromValue → _targetValue
    /// From 模式：从 _targetValue → _fromValue
    /// Additive：相对当前位移动画（不使用 _fromValue）
    /// </summary>
    [AddComponentMenu("TGame/Tween/Nodes/TTweenMoveLocal")]
    public class TTweenMoveLocal : TTweenNode
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
        

        [Header("Easing")]
        [SerializeField]
        private Ease _ease = Ease.OutQuad;

        [SerializeField]
        private AnimationCurve _customCurve;

        private Transform Target => _target ? _target : transform;

        public override DG.Tweening.Tween BuildTween()
        {
            if (_mode == MoveMode.Additive)
            {
                var addTween = Target.DOLocalMove(Target.localPosition + _targetValue, Duration);
                ApplyEase(addTween);
                return addTween;
            }

            Vector3 start = _fromValue;
            Vector3 end = _targetValue;
            if (_mode == MoveMode.From)
            {
                start = _targetValue;
                end = _fromValue;
            }

            var tween = Target.DOLocalMove(end, Duration);
            tween.ChangeStartValue(start);

            var seq = DOTween.Sequence();
            seq.AppendCallback(() => { Target.localPosition = start; });
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
