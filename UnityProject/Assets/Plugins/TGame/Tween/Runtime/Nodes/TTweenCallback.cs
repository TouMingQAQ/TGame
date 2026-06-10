using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace TGame.Tween
{
    /// <summary>
    /// 回调构造器。
    /// 在 Sequence 中插入一个回调点，执行 UnityEvent。
    /// 用于在动画特定时刻触发逻辑，如播放音效、激活物体等。
    /// </summary>
    [AddComponentMenu("TGame/Tween/Nodes/TTweenCallback")]
    public class TTweenCallback : TTweenNode
    {
        [Header("Event")]
        [SerializeField]
        private UnityEvent _onInvoke = new UnityEvent();

        public UnityEvent OnInvoke
        {
            get => _onInvoke;
            set => _onInvoke = value;
        }

        public override DG.Tweening.Tween BuildTween()
        {
            return DOTween.Sequence().AppendCallback(() => _onInvoke?.Invoke());
        }
    }
}
