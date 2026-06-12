using System;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    public class TUISample : MonoBehaviour
    {
        [SerializeField]
        private HelloPanel _helloPanel;
        [SerializeField]
        private SamplePanel _samplePanel;
        [SerializeField]
        private AnimationCurveTestPanel _animationCurveTestPanel;
        [SerializeField]
        private NumberPanel _numberPanel;
        [SerializeField]
        private TButtonPanel _buttonPanel;
        [SerializeField]
        private TweenPanel _tweenPanel;
        [SerializeField]
        private StackSamplePanel _stackSamplePanel;
        [SerializeField]
        private StackSubPanel _stackSubPanel;
        private void Awake()
        {
            var uimgr = Game.Instance.GetManager<UIManager>();
            uimgr.RegisterPanel(_helloPanel);
            uimgr.RegisterPanel(_samplePanel);
            uimgr.RegisterPanel(_animationCurveTestPanel);
            uimgr.RegisterPanel(_numberPanel);
            uimgr.RegisterPanel(_buttonPanel);
            uimgr.RegisterPanel(_tweenPanel);
            // 栈演示：StackSamplePanel 在 Normal 层，StackSubPanel 在 Popup 层
            uimgr.RegisterPanel(_stackSamplePanel, UILayer.Normal);
            uimgr.RegisterPanel(_stackSubPanel, UILayer.Popup);
            uimgr.ShowPanel<SamplePanel>();
        }
    }
}
