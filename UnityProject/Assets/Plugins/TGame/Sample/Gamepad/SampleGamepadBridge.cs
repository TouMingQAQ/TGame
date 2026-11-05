using System.Collections.Generic;
using TGame.Gamepad;
using TGame.TCore.Runtime;
using TGame.TUI;
using UnityEngine;

namespace TGame.Sample
{
    /// <summary>
    /// 示例桥接：每个面板一个 GamepadFocus，由面板打开/关闭事件显式驱动聚焦与取消聚焦。
    /// 不依赖 GameObject 的 active 状态；TButtonPanel 等面板各自拥有独立聚焦区，互不干扰。
    /// 面板关闭既监听 PanelClosedEvent，也监听 BaseUIPanel.Hidden（覆盖直接调用 panel.Hide 的情况）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SampleGamepadBridge : MonoBehaviour
    {
        [SerializeField] private UIRoot uiRoot;
        [SerializeField] private bool verboseLog = true;

        private GamepadUI ui;
        private EventModule events;
        private readonly Dictionary<BaseUIPanel, GamepadPanel> panels = new Dictionary<BaseUIPanel, GamepadPanel>();

        private void OnEnable()
        {
            EnsureUI();
            Subscribe();
            ApplyVisiblePanels();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private UIRoot Root => uiRoot ? uiRoot : FindAnyObjectByType<UIRoot>();

        private void EnsureUI()
        {
            if (ui) return;
            ui = GamepadUI.Instance;
            if (ui) return;
            var go = new GameObject("GamepadUI");
            go.transform.SetParent(transform, false);
            ui = go.AddComponent<GamepadUI>();
        }

        private void Subscribe()
        {
            var root = Root;
            if (!root) return;
            var entity = root.GetComponent<ModuleEntity>();
            if (!entity)
            {
                if (verboseLog) Debug.LogWarning("[SampleGamepadBridge] UIRoot 上找不到 ModuleEntity，无法监听面板事件。", this);
                return;
            }
            events = entity.GetModule<EventModule>();
            events.Register<PanelOpenedEvent>(OnPanelOpened);
            events.Register<PanelClosedEvent>(OnPanelClosed);
        }

        private void Unsubscribe()
        {
            if (events == null) return;
            events.UnRegister<PanelOpenedEvent>(OnPanelOpened);
            events.UnRegister<PanelClosedEvent>(OnPanelClosed);
            events = null;
        }

        private void OnPanelOpened(PanelOpenedEvent evt)
        {
            var panel = Find(evt.PanelName);
            if (panel) Focus(panel);
        }

        private void OnPanelClosed(PanelClosedEvent evt)
        {
            var panel = Find(evt.PanelName);
            if (panel) Unfocus(panel);
        }

        private void OnPanelHidden(BaseUIPanel panel)
        {
            Unfocus(panel);
        }

        private BaseUIPanel Find(string panelName)
        {
            var root = Root;
            if (!root || string.IsNullOrEmpty(panelName)) return null;
            foreach (var panel in root.GetComponentsInChildren<BaseUIPanel>(true))
                if (panel && panel.GetType().Name == panelName) return panel;
            return null;
        }

        private void ApplyVisiblePanels()
        {
            var root = Root;
            if (!root) return;
            foreach (var panel in root.GetComponentsInChildren<BaseUIPanel>(true))
                if (panel && panel.IsVisible && !panel.IsHiding) Focus(panel);
        }

        private void Focus(BaseUIPanel panel)
        {
            var gp = GetPanel(panel);
            if (!gp) return;
            gp.ApplyFocus();
            if (verboseLog) Debug.Log($"[SampleGamepadBridge] 聚焦面板: {panel.GetType().Name}", this);
        }

        private void Unfocus(BaseUIPanel panel)
        {
            if (!panel) return;
            if (panels.TryGetValue(panel, out var gp) && gp) gp.RemoveFocus();
            if (verboseLog) Debug.Log($"[SampleGamepadBridge] 取消聚焦: {panel.GetType().Name}", this);
        }

        private GamepadPanel GetPanel(BaseUIPanel panel)
        {
            if (!panel) return null;
            if (panels.TryGetValue(panel, out var gp) && gp) return gp;
            gp = panel.GetComponent<GamepadPanel>();
            if (!gp) gp = panel.gameObject.AddComponent<GamepadPanel>();
            if (!panel.GetComponent<GamepadTooltipHint>())
                panel.gameObject.AddComponent<GamepadTooltipHint>();
            panels[panel] = gp;
            panel.Hidden -= OnPanelHidden;
            panel.Hidden += OnPanelHidden;
            return gp;
        }

        private void OnGUI()
        {
            if (!verboseLog) return;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            var selected = ui ? ui.SelectedItem : null;
            var current = ui ? ui.CurrentFocus : null;
            var text = $"[Gamepad] pad={(pad != null ? pad.displayName : "none")} focus={(current ? current.name : "none")} sel={(selected ? selected.name : "none")}";
            GUI.Box(new Rect(8f, 8f, 760f, 24f), text);
        }
    }
}
