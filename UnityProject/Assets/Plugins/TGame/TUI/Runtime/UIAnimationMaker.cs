using UnityEngine;
using DG.Tweening;

namespace TGame.TUI
{
    /// <summary>
    /// 常用 UI 动画制作器，提供静态方法生成 Tween，
    /// 在 BaseUIPanel.BuildShowAnimation() / BuildHideAnimation() 中组合使用。
    /// </summary>
    public static class UIAnimationMaker
    {
        private static AnimationCurve DefaultCurve()
        {
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f, 0f, 0f, 2f));
            curve.AddKey(new Keyframe(1f, 1f, 0f, 0f));
            return curve;
        }

        /// <summary>
        /// 透明度渐入：alpha 0→1
        /// </summary>
        public static DG.Tweening.Tween FadeIn(CanvasGroup target, float duration = 0.3f, AnimationCurve curve = null)
        {
            target.alpha = 0f;
            return target.DOFade(1, duration);
        }

        /// <summary>
        /// 透明度渐出：alpha 1→0
        /// </summary>
        public static DG.Tweening.Tween FadeOut(CanvasGroup target, float duration = 0.3f, AnimationCurve curve = null)
        {
            target.alpha = 1f;
            return target.DOFade(0, duration);
        }

        /// <summary>
        /// 缩放弹出：scale 0→1
        /// </summary>
        public static DG.Tweening.Tween ScaleIn(Transform target, float duration = 0.3f, AnimationCurve curve = null)
        {
            target.localScale = Vector3.zero;
            return target.DOScale(1f, duration)
                .SetEase(curve != null && curve.length > 0 ? curve : DefaultCurve());
        }

        /// <summary>
        /// 缩放消失：scale 1→0
        /// </summary>
        public static DG.Tweening.Tween ScaleOut(Transform target, float duration = 0.3f, AnimationCurve curve = null)
        {
            target.localScale = Vector3.one;
            return target.DOScale(0f, duration)
                .SetEase(curve != null && curve.length > 0 ? curve : DefaultCurve());
        }

        /// <summary>
        /// 弹性弹出：scale 0.5→1 Overshoot
        /// </summary>
        public static DG.Tweening.Tween PopIn(Transform target, float duration = 0.4f)
        {
            target.localScale = Vector3.one * 0.5f;
            return target.DOScale(1f, duration).SetEase(Ease.OutBack);
        }

        /// <summary>
        /// 从上方滑入：anchoredPosition 从 offset 滑到 (0, 0)
        /// </summary>
        public static DG.Tweening.Tween SlideInFromTop(RectTransform target, float distance = 80f, float duration = 0.3f, AnimationCurve curve = null)
        {
            var original = target.anchoredPosition;
            target.anchoredPosition = original + new Vector2(0f, distance);
            return target.DOAnchorPos(original, duration)
                .SetEase(curve != null && curve.length > 0 ? curve : DefaultCurve());
        }
        
        /// <summary>
        /// 从下方滑入
        /// </summary>
        public static DG.Tweening.Tween SlideInFromBottom(RectTransform target, float distance = 80f, float duration = 0.3f, AnimationCurve curve = null)
        {
            var original = target.anchoredPosition;
            target.anchoredPosition = original + new Vector2(0f, -distance);
            return target.DOAnchorPos(original, duration)
                .SetEase(curve != null && curve.length > 0 ? curve : DefaultCurve());
        }
        
        /// <summary>
        /// 从左侧滑入
        /// </summary>
        public static DG.Tweening.Tween SlideInFromLeft(RectTransform target, float distance = 80f, float duration = 0.3f, AnimationCurve curve = null)
        {
            var original = target.anchoredPosition;
            target.anchoredPosition = original + new Vector2(-distance, 0f);
            return target.DOAnchorPos(original, duration)
                .SetEase(curve != null && curve.length > 0 ? curve : DefaultCurve());
        }
        
        /// <summary>
        /// 从右侧滑入
        /// </summary>
        public static DG.Tweening.Tween SlideInFromRight(RectTransform target, float distance = 80f, float duration = 0.3f, AnimationCurve curve = null)
        {
            var original = target.anchoredPosition;
            target.anchoredPosition = original + new Vector2(distance, 0f);
            return target.DOAnchorPos(original, duration)
                .SetEase(curve != null && curve.length > 0 ? curve : DefaultCurve());
        }

        /// <summary>
        /// 淡入 + 缩放入场的组合 Sequence
        /// </summary>
        public static Sequence FadeScaleIn(CanvasGroup cg, Transform tf, float duration = 0.3f, AnimationCurve curve = null)
        {
            cg.alpha = 0f;
            tf.localScale = Vector3.zero;
            var seq = DOTween.Sequence();
            seq.Join(FadeIn(cg, duration, curve));
            seq.Join(ScaleIn(tf, duration, curve));
            return seq;
        }

        // /// <summary>
        // /// 淡入 + 从下方滑入的组合 Sequence
        // /// </summary>
        // public static Sequence FadeSlideUp(CanvasGroup cg, RectTransform rt, float distance = 80f, float duration = 0.3f, AnimationCurve curve = null)
        // {
        //     cg.alpha = 0f;
        //     var seq = DOTween.Sequence();
        //     seq.Join(FadeIn(cg, duration, curve));
        //     seq.Join(SlideInFromBottom(rt, distance, duration, curve));
        //     return seq;
        // }
    }
}
