using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// 栈式 Panel 管理 Module,挂载在 UIRoot 上(per-UIRoot 单实例,每个 UIRoot 拥有独立 Module 实例和栈)。
    /// Open/CloseTop/BackTo/PopToRoot 一组栈式 API。
    /// 监听每个栈项 Panel 的 Hidden 事件,顶层被外部关掉时自动 Show 上一层。
    /// Open 时校验新 Panel.Layer >= 当前栈顶.Layer,不满足则 LogError 并拒绝打开。
    /// </summary>
    public sealed class StackPanelModule : BaseModule
    {
        // 单一栈 — Module 本身就是 per-UIRoot 挂载的,不需要 UIRoot→Stack 字典
        private readonly List<StackPanelEntry> _stack = new();
        // 标记"我正在主动 CloseTop",CloseTop 内 Hide 完成后别触发自动恢复
        private bool _suppressHiddenHandler;
        // 缓存订阅,Destroy 时统一反注册
        private readonly Dictionary<BaseUIPanel, Action<BaseUIPanel>> _subs = new();

        // ===== 查询 =====

        public int StackDepth => _stack.Count;

        public Type GetStackTop()
            => _stack.Count > 0 ? _stack[^1].PanelType : null;

        public bool IsInStack<T>() where T : BaseUIPanel => IsInStack(typeof(T));

        public bool IsInStack(Type type)
        {
            for (int i = 0; i < _stack.Count; i++)
                if (_stack[i].PanelType == type) return true;
            return false;
        }

        public bool IsStackTop<T>() where T : BaseUIPanel => IsStackTop(typeof(T));

        public bool IsStackTop(Type type)
            => _stack.Count > 0 && _stack[^1].PanelType == type;

        public IReadOnlyList<StackPanelEntry> GetStackSnapshot()
            => _stack.AsReadOnly();

        // ===== 主操作 =====

        /// <summary>
        /// 将面板异步压入栈顶。已加载则复用,不重复 Instantiate。
        /// 同面板已在栈中时拒绝重复 Open。
        /// 校验新 Panel.Layer >= 当前栈顶.Layer,不满足则 LogError 并拒绝。
        /// </summary>
        public async UniTask<T> OpenAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => (T)await OpenAsync(typeof(T), ct);

        public async UniTask<BaseUIPanel> OpenAsync(Type panelType, CancellationToken ct = default)
        {
            if (IsInStack(panelType))
            {
                Debug.LogWarning($"[StackPanelModel] Panel {panelType.Name} is already in stack, refusing to open");
                return Host.GetModule<UILoaderModule>().GetPanel(panelType);
            }

            var panel = await Host.GetModule<UILoaderModule>().LoadAsync(panelType, ct);
            if (panel == null) return null;

            // 层级守门
            if (_stack.Count > 0)
            {
                var topPanel = _stack[^1].Instance;
                if (topPanel != null && (int)panel.Layer < (int)topPanel.Layer)
                {
                    Debug.LogError($"[StackPanelModel] Refuse open {panelType.Name}(Layer={panel.Layer}) on top of {topPanel.GetType().Name}(Layer={topPanel.Layer}): new layer must be >= current top layer");
                    return null;
                }
            }

            Action<BaseUIPanel> handler = OnPanelHidden;
            panel.Hidden += handler;
            _subs[panel] = handler;

            var entry = new StackPanelEntry(panelType, panel, _stack.Count + 1);
            panel.OnPushed(entry);
            panel.transform.SetAsLastSibling();
            panel.Show();
            _stack.Add(entry);

            var eventModule = Host.GetModule<EventModule>();
            eventModule?.Call(new PanelOpenedEvent(panelType.Name));
            eventModule?.Call(new PanelPushedEvent(panelType.Name, _stack.Count));
            return panel;
        }

        public bool Back()
        {
            if (_stack.Count == 0) return false;
            var top = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);

            _suppressHiddenHandler = true;
            try
            {
                if (top.Instance != null && top.Instance.IsVisible)
                {
                    top.Instance.OnPopped(new StackPanelEntry(top.PanelType, top.Instance, _stack.Count + 1));
                    top.Instance.Hide();
                    Host.GetModule<EventModule>()?.Call(new PanelClosedEvent(top.PanelType.Name));
                }
            }
            finally
            {
                _suppressHiddenHandler = false;
            }

            RestoreStackTop();
            Host.GetModule<EventModule>()?.Call(new PanelPoppedEvent(top.PanelType.Name, _stack.Count));
            return true;
        }

        public bool BackTo<T>() where T : BaseUIPanel => BackTo(typeof(T));

        public bool BackTo(Type panelType)
        {
            int targetIndex = -1;
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].PanelType == panelType) { targetIndex = i; break; }
            }
            if (targetIndex < 0) return false;

            while (_stack.Count > targetIndex + 1)
            {
                if (!Back()) break;
            }
            EnsureVisible(panelType);
            return true;
        }

        public bool PopToRoot()
        {
            if (_stack.Count <= 1) return false;
            while (_stack.Count > 1)
            {
                if (!Back()) break;
            }
            EnsureVisible(_stack[0].PanelType);
            return true;
        }

        public void ClearStack() => _stack.Clear();

        public void ClearStackAndHide()
        {
            foreach (var entry in _stack)
            {
                if (entry.Instance != null && entry.Instance.IsVisible)
                    entry.Instance.Hide();
            }
            _stack.Clear();
        }

        // ===== 内部 =====

        private void OnPanelHidden(BaseUIPanel panel)
        {
            if (_suppressHiddenHandler) return;
            if (panel == null) return;
            if (_stack.Count == 0) return;

            int index = -1;
            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i].Instance == panel) { index = i; break; }
            }
            if (index < 0 || index != _stack.Count - 1) return;

            _stack.RemoveAt(_stack.Count - 1);
            RestoreStackTop();
            Host.GetModule<EventModule>()?.Call(new PanelPoppedEvent(panel.GetType().Name, _stack.Count));
        }

        private void RestoreStackTop()
        {
            while (_stack.Count > 0)
            {
                var top = _stack[^1];
                if (top.Instance == null || top.Instance.gameObject == null)
                {
                    Debug.LogWarning($"[StackPanelModel] Stale stack entry {top.PanelType.Name}, removing");
                    _stack.RemoveAt(_stack.Count - 1);
                    continue;
                }
                if (!top.Instance.IsVisible || top.Instance.IsHiding) top.Instance.Show();
                return;
            }
        }

        private void EnsureVisible(Type panelType)
        {
            var loader = Host.GetModule<UILoaderModule>();
            if (loader.IsPanelLoaded(panelType))
            {
                var p = loader.GetPanel(panelType);
                if (p != null && (!p.IsVisible || p.IsHiding)) p.Show();
            }
        }

        public override void Destroy()
        {
            foreach (var kv in _subs)
            {
                if (kv.Key != null) kv.Key.Hidden -= kv.Value;
            }
            _subs.Clear();
            _stack.Clear();
        }
    }
}
