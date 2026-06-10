using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// Shake 抖动构造器。
    /// 模拟抖动效果，适用于受击震动、相机晃动等。
    /// </summary>
    [AddComponentMenu("TGame/Tween/Nodes/TTweenShake")]
    public class TTweenShake : TTweenNode
    {
        [Header("Target")]
        [SerializeField]
        [Tooltip("要动画的目标 Transform，为空时使用自身")]
        private Transform _target;

        [Header("Animation")]
        [SerializeField]
        private Vector3 _strength = Vector3.one;

        [SerializeField]
        private float _duration = 0.5f;

        [SerializeField]
        [Range(0, 20)]
        private int _vibrato = 10;

        [SerializeField]
        [Range(0, 90f)]
        private float _randomness = 90f;

        [SerializeField]
        private bool _snapping = false;

        [SerializeField]
        private bool _fadeOut = true;

        private Transform Target => _target ? _target : transform;

        public override DG.Tweening.Tween BuildTween()
        {
            return Target.DOShakePosition(_duration, _strength, _vibrato, _randomness, _snapping, _fadeOut);
        }

        private void Reset()
        {
            _target = transform;
        }
    }
}
