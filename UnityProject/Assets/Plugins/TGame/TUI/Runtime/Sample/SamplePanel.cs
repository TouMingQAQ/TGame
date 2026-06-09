using TGame.TCore.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace TGame.TUI
{
    public class SamplePanel : BaseUIPanel
    {
        
        public void OpenHelloPanel()
        {
            Game.Instance.GetManager<UIManager>().ShowPanel<HelloPanel>();
        }

        public void OpenAnimationCurvePanel()
        {
            Game.Instance.GetManager<UIManager>().ShowPanel<AnimationCurveTestPanel>();
        }

        public void OpenNumberPanel()
        {
            Game.Instance.GetManager<UIManager>().ShowPanel<NumberPanel>();
        }
    }
}
