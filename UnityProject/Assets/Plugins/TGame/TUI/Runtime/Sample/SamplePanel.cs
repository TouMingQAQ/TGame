using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;

namespace TGame.TUI
{
    public class SamplePanel : BaseUIPanel
    {
        public void OpenHelloPanel() => OpenAsync<HelloPanel>().Forget();
        public void OpenAnimationCurvePanel() => OpenAsync<AnimationCurveTestPanel>().Forget();
        public void OpenNumberPanel() => OpenAsync<NumberPanel>().Forget();
        public void OpenButtonPanel() => OpenAsync<TButtonPanel>().Forget();
        public void OpenTweenPanel() => OpenAsync<TweenPanel>().Forget();
        public void OpenStackPanel() => Game.Instance.GetManager<UIManager>().ShowPanelStackAsync<StackSamplePanel>(destroyCancellationToken).Forget();

        private UniTask OpenAsync<T>() where T : BaseUIPanel
            => Game.Instance.GetManager<UIManager>().ShowPanelAsync<T>(destroyCancellationToken);
    }
}
