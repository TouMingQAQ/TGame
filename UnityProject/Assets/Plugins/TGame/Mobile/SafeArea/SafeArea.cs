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

        // 是否已应用过一次；用于变化检测，不参与序列化。
        private bool _applied;

        private void Update()
        {
            UpdateArea();
        }

        /// <summary>
        /// 以实时 Screen.width/height 归一化 Screen.safeArea 并应用到 rect。
        /// 不再依赖序列化的旧 screenArea；屏幕尺寸或安全区变化时才刷新。
        /// </summary>
        void UpdateArea()
        {
            int width = Screen.width;
            int height = Screen.height;

            // 宽高 0 守卫：窗口最小化 / 尚未初始化的帧直接跳过，避免除零与 NaN 锚点。
            if (width <= 0 || height <= 0)
                return;

            var currentScreen = new Rect(0f, 0f, width, height);
            var currentSafe = Screen.safeArea;

            // 屏幕尺寸或安全区变化时才刷新（编辑器 Game 视图缩放同样生效）。
            if (_applied && currentScreen == screenArea && currentSafe == safeArea)
                return;

            screenArea = currentScreen;
            safeArea = currentSafe;
            _applied = true;

            // 安全区宽/高为 0（部分平台尚未初始化）时退化为整屏，避免 UI 塌缩。
            Rect applied = currentSafe;
            if (applied.width <= 0f || applied.height <= 0f)
                applied = currentScreen;

            // 将安全区裁剪到屏幕范围内，防止异常越界。
            float xMin = Mathf.Clamp(applied.xMin, 0f, width);
            float yMin = Mathf.Clamp(applied.yMin, 0f, height);
            float xMax = Mathf.Clamp(applied.xMax, 0f, width);
            float yMax = Mathf.Clamp(applied.yMax, 0f, height);
            resultSafeArea = Rect.MinMaxRect(xMin, yMin, xMax, yMax);

            ExecuteRect(width, height);
        }

        void ExecuteRect(float screenWidth, float screenHeight)
        {
            if (rect == null)
                return;

            // 以实时屏幕尺寸归一化到 0..1 Canvas 锚点并 clamp，保留刘海四边裁切语义。
            var anchorMin = new Vector2(
                Mathf.Clamp(resultSafeArea.xMin / screenWidth, 0f, 1f),
                Mathf.Clamp(resultSafeArea.yMin / screenHeight, 0f, 1f));
            var anchorMax = new Vector2(
                Mathf.Clamp(resultSafeArea.xMax / screenWidth, 0f, 1f),
                Mathf.Clamp(resultSafeArea.yMax / screenHeight, 0f, 1f));

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
