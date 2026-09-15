using System;
using UnityEngine;
using UnityEngine.UI;

namespace TFramework.Mobile
{
    /// <summary>
    /// 只向布局系统暴露一个值：屏幕安全区导致的水平内边距总和。
    /// preferredWidth = Screen.width - Screen.safeArea.width
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaAutoWidth : MonoBehaviour
    {
        [SerializeField]
        private RectTransform rectTransform;
        [SerializeField]
        private Rect safeArea;
        [SerializeField]
        private Rect screenArea;
        [SerializeField]
        private float width;

        private void LateUpdate()
        {
            if(rectTransform == null)
                return;
            safeArea = Screen.safeArea;
            screenArea = new Rect(0, 0, Screen.width, Screen.height);
            var w1 = safeArea.x;//左边宽
            var w2 = screenArea.xMax - safeArea.xMax;
            
            width =  w1 > w2 ? w1 : w2;
            rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);
        }

        private void Reset()
        {
            TryGetComponent(out rectTransform);
        }
    }
}