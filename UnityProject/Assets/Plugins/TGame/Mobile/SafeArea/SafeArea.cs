using System;
using UnityEngine;

namespace TFramework.Mobile
{
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeArea : MonoBehaviour
    {
        [SerializeField]
        private RectTransform rect;
        [SerializeField]
        private Rect screenArea;
        [SerializeField]
        private Rect safeArea;
        [SerializeField]
        private Rect resultSafeArea;

        
        private void Update()
        {
#if UNITY_EDITOR
            UpdateArea();
#else
            if (Screen.safeArea != safeArea || !Mathf.Approximately(Screen.width, screenArea.width) || !Mathf.Approximately(Screen.height, screenArea.height))
            {
                UpdateArea();
            }
#endif
            ExecuteRect();
        }

        void UpdateArea()
        {
            screenArea = new Rect(0, 0, Screen.width, Screen.height);
            safeArea = Screen.safeArea;
            var dis = new Vector2(screenArea.width - safeArea.width, screenArea.height - safeArea.height);
            resultSafeArea = safeArea;
            resultSafeArea.width -= dis.x;
            resultSafeArea.height -= dis.y;
            if (resultSafeArea.x <= 0)
                resultSafeArea.x += dis.x;
            if (resultSafeArea.y <= 0)
                resultSafeArea.y += dis.y;
            
        }

        void ExecuteRect()
        {
            if(rect == null)
                return;
            // 2. 计算安全区的锚点和尺寸（基于Canvas的归一化坐标，脱离像素依赖）
            Vector2 anchorMin = resultSafeArea.position;
            Vector2 anchorMax = resultSafeArea.position + resultSafeArea.size;
            // 将像素坐标转换为**Canvas的归一化坐标（0-1）**，适配任意分辨率
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(rect);
#endif
        }
    }
}

