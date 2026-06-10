using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.Tween
{
    /// <summary>
    /// Graphic 颜色构造器。
    /// 从 _fromValue 到 _targetValue 的颜色过渡。
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("TGame/Tween/Nodes/TTweenColor")]
    public class TTweenColor : TTweenNode
    {
        [Header("Target")]
        [SerializeField]
        [Tooltip("要动画的 Graphic，为空时从自身获取")]
        private Graphic _target;

        [Header("Animation")]
        [SerializeField]
        private Color _fromValue = Color.white;

        [SerializeField]
        private Color _targetValue = Color.white;
        

        [Header("Easing")]
        [SerializeField]
        private Ease _ease = Ease.Linear;

        [SerializeField]
        private AnimationCurve _customCurve;

        private Graphic Target
        {
            get
            {
                if (_target == null)
                    _target = GetComponent<Graphic>();
                return _target;
            }
        }

        public override DG.Tweening.Tween BuildTween()
        {
            if (Target == null) return null;

            var tween = Target.DOColor(_targetValue, Duration);
            tween.ChangeStartValue(_fromValue);

            var seq = DOTween.Sequence();
            seq.AppendCallback(() => { Target.color = _fromValue; });
            seq.Append(tween);

            if (_customCurve != null && _customCurve.length > 0)
                tween.SetEase(_customCurve);
            else
                tween.SetEase(_ease);

            return seq;
        }

        private void Reset()
        {
            _target = GetComponent<Graphic>();
        }
    }
}
