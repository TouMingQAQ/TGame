using DG.Tweening;
using TGame.Tween;
using TMPro;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// 框架自带的默认浮窗:只显示一行文本。
    /// 演示"用户零成本使用"路径 — 业务方注册 DefaultPopup prefab 后,
    /// <c>ShowPopup&lt;DefaultPopup&gt;(pos, p =&gt; p.SetText("..."))</c> 即可。
    /// </summary>
    public class DefaultToolTip : BaseUIPopup
    {
        [Header("Default")]
        [SerializeField] protected TMP_Text _label;
        [SerializeField]
        private TTweenPlay _showTween;

        /// <summary>直接设置显示文本(setup 回调里调用最方便)</summary>
        public virtual void SetText(string text)
        {
            if (_label != null) 
                _label.text = text ?? string.Empty;
        }

        public override void SetData<TData>(TData data)
        {
            if (data is string s)
            {
                SetText(s);
            }
            else if (data != null)
            {
                SetText(data.ToString());
            }
            else
            {
                SetText(string.Empty);
            }
        }

        protected override Sequence BuildShowAnimation()
        {
            if (_showTween == null)
                return base.BuildShowAnimation();

            var tween = _showTween.BuildTween();
            if (tween == null)
                return base.BuildShowAnimation();

            return DOTween.Sequence().Append(tween);
        }
    }
}
