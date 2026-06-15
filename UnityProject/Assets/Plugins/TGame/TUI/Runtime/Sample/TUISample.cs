using System.Threading;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// TUI 模块示例:MonoBehaviour 启动器。
    /// 9 个面板现在通过 Addressables address 字符串注册,运行时由 UIManager 走 AddressableModel 异步加载。
    /// 启动流程:PreloadPanelsByLabelAsync(按 label 批量预热)→ RegisterPanelAsync × 9 → ShowPanelAsync(SamplePanel)。
    /// </summary>
    public class TUISample : MonoBehaviour
    {
        [Header("Panel Addressables Addresses")]
        [SerializeField] private string _helloPanelAddress = "HelloPanel";
        [SerializeField] private string _samplePanelAddress = "SamplePanel";
        [SerializeField] private string _animationCurveTestAddress = "AnimationCurveTestPanel";
        [SerializeField] private string _numberPanelAddress = "NumberPanel";
        [SerializeField] private string _buttonPanelAddress = "TButtonPanel";
        [SerializeField] private string _tweenPanelAddress = "TweenPanel";
        [SerializeField] private string _stackSampleAddress = "StackSamplePanel";
        [SerializeField] private string _stackSubAddress = "StackSubPanel";
        [SerializeField] private string _loadingSampleAddress = "LoadingSamplePanel";

        [Header("Bulk Preload (Optional)")]
        [Tooltip("Addressables label for bulk preload. Leave empty to skip preload (first Show will load on demand).")]
        [SerializeField] private string _panelLabel = "ui_panels";

        private CancellationTokenSource _cts;

        private async void Awake()
        {
            _cts = new CancellationTokenSource();
            var uimgr = Game.Instance.GetManager<UIManager>();

            // 1. (可选)按 label 批量预热所有 UI 面板 prefab 到 AddressableModel 句柄池
            if (!string.IsNullOrEmpty(_panelLabel))
            {
                await uimgr.PreloadPanelsByLabelAsync<GameObject>(_panelLabel, ct: _cts.Token);
            }

            // 2. 注册所有面板(异步 API,提供 address 字符串)
            uimgr.RegisterPanelAsync<HelloPanel>(_helloPanelAddress);
            uimgr.RegisterPanelAsync<SamplePanel>(_samplePanelAddress);
            uimgr.RegisterPanelAsync<AnimationCurveTestPanel>(_animationCurveTestAddress);
            uimgr.RegisterPanelAsync<NumberPanel>(_numberPanelAddress);
            uimgr.RegisterPanelAsync<TButtonPanel>(_buttonPanelAddress);
            uimgr.RegisterPanelAsync<TweenPanel>(_tweenPanelAddress);
            // 栈演示:StackSamplePanel 在 Normal 层,StackSubPanel 在 Popup 层
            uimgr.RegisterPanelAsync<StackSamplePanel>(_stackSampleAddress, UILayer.Normal);
            uimgr.RegisterPanelAsync<StackSubPanel>(_stackSubAddress, UILayer.Popup);
            // LoadingPanel 示例:默认 Popup 层
            uimgr.RegisterLoadingPanelAsync<LoadingSamplePanel>(_loadingSampleAddress, UILayer.Popup);

            // 3. 异步显示第一个面板(DefaultPopup 由 UIConfig 注入并自动注册,零样板)
            await uimgr.ShowPanelAsync<SamplePanel>(_cts.Token);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
