using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// Punch 震动构造器。
    /// 模拟碰撞或点击的弹性回弹效果，适用于按钮反馈、命中效果等。
    /// </summary>
    [AddComponentMenu("TGame/Tween/Nodes/TTweenPunch")]
    public class TTweenPunch : TTweenNode
    {
        [Header("Target")]
        [SerializeField]
        [Tooltip("要动画的目标 Transform，为空时使用自身")]
        private Transform _target;

        [Header("Animation")]
        [SerializeField]
        private Vector3 _strength = Vector3.one;
        
        [SerializeField]
        [Range(0, 20)]
        private int _vibrato = 10;

        [SerializeField]
        [Range(0, 1f)]
        private float _elasticity = 1f;

        private Transform Target => _target ? _target : transform;

        public override DG.Tweening.Tween BuildTween()
        {
            return Target.DOPunchPosition(_strength, Duration, _vibrato, _elasticity);
        }

        private void Reset()
        {
            _target = transform;
        }
    }
}
