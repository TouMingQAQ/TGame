using System.Threading;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// TUI 模块示例:MonoBehaviour 启动器。
    /// 启动流程:PreloadPanelsAsync(按 label 预热 + 自动注册 Type→address)→ ShowPanelAsync(SamplePanel)。
    /// 面板注册不再需要逐个 RegisterPanelAsync —— 预热时从 prefab 反查 BaseUIPanel 子类自动写表。
    /// 注意:层级信息写在 prefab 上的 BaseUIPanel._layer 字段,不在此处配置。
    /// </summary>
    public class TUISample : MonoBehaviour
    {
        [Header("Bulk Preload")]
        [Tooltip("Addressables label for UI panels. Preload resolves addresses, warms the handle pool, and auto-registers Type→address from each prefab's root BaseUIPanel component.")]
        [SerializeField] private string _panelLabel = "ui_panels";

        private CancellationTokenSource _cts;

        private async void Awake()
        {
            _cts = new CancellationTokenSource();
            var uimgr = Game.Instance.GetManager<UIManager>();

            // 1. 按 label 预热 + 自动注册 Type→address
            await uimgr.PreloadPanelsAsync(_panelLabel, ct: _cts.Token);

            // 2. 异步显示第一个面板(预热已暖,命中句柄池)
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
