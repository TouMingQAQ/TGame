using TGame.TCore.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    /// <summary>
    /// 栈演示面板 B（子面板）。点击返回按钮 PopPanel() 回到上一级。
    /// 同时提供 BackTo / PopToRoot 按钮演示其他栈操作。
    /// </summary>
    public class StackSubPanel : BaseUIPanel
    {
        [SerializeField] private TButton _backButton;
        [SerializeField] private TButton _backToRootButton;
        [SerializeField] private TMP_Text _depthLabel;
        [SerializeField] private TMP_Text _topLabel;

        protected override void Awake()
        {
            base.Awake();
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
            if (_backToRootButton != null) _backToRootButton.onClick.AddListener(OnBackToRoot);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
            if (_backToRootButton != null) _backToRootButton.onClick.RemoveListener(OnBackToRoot);
        }

        protected override void AfterShow()
        {
            base.AfterShow();
            var uimgr = Game.Instance.GetManager<UIManager>();
            if (uimgr == null) return;
            var model = uimgr.GetModule<StackPanelModel>();
            if (_depthLabel != null) _depthLabel.text = $"StackDepth = {model.StackDepth}";
            if (_topLabel != null) _topLabel.text = $"Top = {model.GetStackTop()?.Name}";
        }

        private void OnBack()
        {
            Game.Instance.GetManager<UIManager>().GetModule<StackPanelModel>().CloseTop();
        }

        private void OnBackToRoot()
        {
            // 弹到只剩栈底(StackSamplePanel)
            Game.Instance.GetManager<UIManager>().GetModule<StackPanelModel>().PopToRoot();
        }
    }
}