using DG.Tweening;
using TGame.Tween;
using UnityEngine;

namespace TGame.TUI
{
    public class TweenPanel : BaseUIPanel
    {
        [SerializeField]
        private TTweenPlay showTweenPlay;
        [SerializeField]
        private TTweenPlay hideTweenPlay;
        protected override Sequence BuildShowAnimation()
        {
            return DOTween.Sequence().Append(showTweenPlay.BuildTween());
        }

        protected override Sequence BuildHideAnimation()
        {
            return DOTween.Sequence().Append(hideTweenPlay.BuildTween());
        }
    }
}
