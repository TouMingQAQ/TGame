using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// 延迟构造器。
    /// 在 Sequence 中插入一段等待时间，用于控制动画先后顺序。
    /// </summary>
    [AddComponentMenu("TGame/Tween/Nodes/TTweenDelay")]
    public class TTweenDelay : TTweenNode
    {
        [Header("Animation")]
        [SerializeField]
        private float _duration = 0.5f;

        public float Duration
        {
            get => _duration;
            set => _duration = value;
        }

        public override DG.Tweening.Tween BuildTween()
        {
            return DOTween.Sequence().AppendInterval(_duration);
        }
    }
}
