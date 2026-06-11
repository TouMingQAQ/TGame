using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    /// <summary>
    /// 栈演示面板 A（栈底入口）。点击按钮 PushPanel<StackSubPanel>()。
    /// </summary>
    public class StackSamplePanel : BaseUIPanel
    {
        [SerializeField] private Button _openSubButton;
        [SerializeField] private Text _depthLabel;

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
            var uimgr = Game.Instance.GetManager<UIManager>();
            if (uimgr != null && _depthLabel != null)
                _depthLabel.text = $"StackDepth = {uimgr.GetModule<StackPanelModel>().StackDepth}";
        }

        private void OnOpenSub()
        {
            Game.Instance.GetManager<UIManager>().GetModule<StackPanelModel>().Open<StackSubPanel>();
        }
    }
}