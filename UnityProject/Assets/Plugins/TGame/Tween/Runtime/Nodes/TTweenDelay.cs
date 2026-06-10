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
        public override DG.Tweening.Tween BuildTween()
        {
            return DOTween.Sequence().AppendInterval(Duration);
        }
    }
}
