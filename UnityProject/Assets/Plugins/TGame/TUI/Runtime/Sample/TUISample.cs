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
        private void Awake()
        {
            var uimgr = Game.Instance.GetManager<UIManager>();
            uimgr.RegisterPanel(_helloPanel);
            uimgr.RegisterPanel(_samplePanel);
            uimgr.RegisterPanel(_animationCurveTestPanel);
            uimgr.RegisterPanel(_numberPanel);
            uimgr.ShowPanel<SamplePanel>();

        }
    }
}
