using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// 旋转构造器。
    /// To 模式：从 _fromValue → _targetValue
    /// From 模式：从 _targetValue → _fromValue
    ///
    /// 使用 ChangeStartValue 避免 BuildTween() 时修改 Transform。
    /// </summary>
    [AddComponentMenu("TGame/Tween/Nodes/TTweenRotate")]
    public class TTweenRotate : TTweenNode
    {
        public enum RotateMode { To, From }

        [Header("Target")]
        [SerializeField]
        [Tooltip("要动画的目标 Transform，为空时使用自身")]
        private Transform _target;

        [Header("Animation")]
        [SerializeField]
        private RotateMode _mode = RotateMode.To;

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

        private Transform Target => _target ? _target : transform;

        public override DG.Tweening.Tween BuildTween()
        {
            Vector3 start = _fromValue;
            Vector3 end = _targetValue;
            if (_mode == RotateMode.From)
            {
                start = _targetValue;
                end = _fromValue;
            }

            var tween = Target.DORotate(end, _duration);
            tween.ChangeStartValue(start);

            var seq = DOTween.Sequence();
            seq.AppendCallback(() => { Target.eulerAngles = start; });
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
