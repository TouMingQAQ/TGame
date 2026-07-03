using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    /// <summary>
    /// LoadingPanel 示例实现：Slider + 文本双展示。
    /// 在 Prefab 上挂本组件并把 Slider / TextMeshProUGUI 拖入对应字段即可使用。
    /// </summary>
    public class LoadingSamplePanel : LoadingPanel
    {
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _progressText;

        public override void SetProgress(float progress)
        {
            float clamped = Mathf.Clamp01(progress);

            if (_progressSlider != null)
                _progressSlider.value = clamped;

            if (_progressText != null)
                _progressText.text = $"{clamped * 100f:F0}%";
        }
    }
}
