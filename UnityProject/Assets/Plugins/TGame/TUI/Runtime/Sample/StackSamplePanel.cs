using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    /// <summary>
    /// 栈演示面板 A（栈底入口）。点击按钮 ShowPanelStack<StackSubPanel>()。
    /// </summary>
    public class StackSamplePanel : BaseUIPanel
    {
        [SerializeField] private TButton _openSubButton;
        [SerializeField] private TMP_Text _depthLabel;

        protected override void Awake()
        {
            base.Awake();
            if (_openSubButton != null) _openSubButton.onClick.AddListener(OnOpenSub);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_openSubButton != null) _openSubButton.onClick.RemoveListener(OnOpenSub);
        }

        protected override void AfterShow()
        {
            base.AfterShow();
            if (_depthLabel != null)
                _depthLabel.text = $"StackDepth = {Root}";
        }

        private void OnOpenSub()
        {
            Root.ShowPanelAsync<StackSubPanel>(destroyCancellationToken).Forget();
        }
    }
}