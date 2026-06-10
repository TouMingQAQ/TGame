using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// CanvasGroup 透明度构造器。
    /// 从 _fromValue 到 _targetValue 的透明度动画。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [AddComponentMenu("TGame/Tween/Nodes/TTweenFade")]
    public class TTweenFade : TTweenNode
    {
        [Header("Target")]
        [SerializeField]
        [Tooltip("要动画的 CanvasGroup，为空时从自身获取")]
        private CanvasGroup _target;

        [Header("Animation")]
        [SerializeField]
        private float _fromValue = 0f;

        [SerializeField]
        private float _targetValue = 1f;

        [SerializeField]
        private float _duration = 0.3f;

        [Header("Easing")]
        [SerializeField]
        private Ease _ease = Ease.Linear;

        [SerializeField]
        private AnimationCurve _customCurve;

        private CanvasGroup Target
        {
            get
            {
                if (_target == null)
                    _target = GetComponent<CanvasGroup>();
                return _target;
            }
        }

        public override DG.Tweening.Tween BuildTween()
        {
            if (Target == null) return null;

            var tween = Target.DOFade(_targetValue, _duration);
            tween.ChangeStartValue(_fromValue);

            var seq = DOTween.Sequence();
            seq.AppendCallback(() => { Target.alpha = _fromValue; });
            seq.Append(tween);

            if (_customCurve != null && _customCurve.length > 0)
                tween.SetEase(_customCurve);
            else
                tween.SetEase(_ease);

            return seq;
        }

        private void Reset()
        {
            _target = GetComponent<CanvasGroup>();
        }
    }
}
