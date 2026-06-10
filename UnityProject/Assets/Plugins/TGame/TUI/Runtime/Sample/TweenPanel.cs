using DG.Tweening;
using TGame.Tween;
using UnityEngine;

namespace TGame.TUI
{
    public class TweenPanel : BaseUIPanel
    {
        [SerializeField]
        private TTweenPlay tweenPlay;
        protected override Sequence BuildAnimation()
        {
            return DOTween.Sequence().Append(tweenPlay.BuildTween());
        }
    }
}
