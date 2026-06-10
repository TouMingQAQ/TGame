using DG.Tweening;
using UnityEngine;

namespace TGame.Tween
{
    /// <summary>
    /// Tween 构造器节点接口。
    /// 所有子构造器（TTweenMove、TTweenScale 等）实现此接口，
    /// 父构造器 TTweenBuilder 通过扫描子 GameObject 收集 ITweenNode 并构建动画 Sequence。
    /// </summary>
    public abstract class TTweenNode : MonoBehaviour
    {
        /// <summary>
        /// 构建单个 Tween 对象。
        /// 此方法不自动播放，由父构造器统一编排。
        /// </summary>
        public abstract DG.Tweening.Tween BuildTween();

        [SerializeField]
        private float _duration = 0.3f;

        /// <summary>
        /// 节点的动画时长（秒）。用于可视化编辑器中块的宽度计算。
        /// </summary>
        public float Duration => _duration;
    }
}
