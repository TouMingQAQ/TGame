using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// 栈式 Panel 管理 Model。
    /// Open/CloseTop/BackTo/PopToRoot 一组栈式 API,挂在 UIManager 下。
    /// 监听每个栈项 Panel 的 Hidden 事件,顶层被外部关掉时自动 Show 上一层。
    /// Open 时校验新 Panel.Layer >= 当前栈顶.Layer,不满足则 LogError 并拒绝打开。
    /// </summary>
    public sealed class StackPanelModel : BaseModule
    {
        private UIManager _ui;
        private readonly List<StackPanelEntry> _stack = new();
        // 标记"我正在主动 CloseTop",CloseTop 内 Hide 完成后别触发自动恢复
        private bool _suppressHiddenHandler;
        // 缓存订阅,Destroy 时统一反注册
        private readonly Dictionary<BaseUIPanel, Action<BaseUIPanel>> _subs = new();

        /// <summary>由 UIManager 在 Start 时注入自身引用</summary>
        public void SetUIManager(UIManager ui) => _ui = ui;

        // ===== 查询 =====

        /// <summary>当前栈深度</summary>
        public int StackDepth => _stack.Count;

        /// <summary>栈顶面板类型,栈空返回 null</summary>
        public Type GetStackTop() => _stack.Count == 0 ? null : _stack[^1].PanelType;

        /// <summary>类型是否在栈中(任意位置)</summary>
        public bool IsInStack<T>() where T : BaseUIPanel => IsInStack(typeof(T));

        /// <summary>类型是否在栈中(任意位置)</summary>
        public bool IsInStack(Type type)
        {
            for (int i = 0; i < _stack.Count; i++)
                if (_stack[i].PanelType == type) return true;
            return false;
        }

        /// <summary>类型是否在栈顶</summary>
        public bool IsStackTop<T>() where T : BaseUIPanel => IsStackTop(typeof(T));

        /// <summary>类型是否在栈顶</summary>
        public bool IsStackTop(Type type)
        {
            return _stack.Count > 0 && _stack[^1].PanelType == type;
        }

        /// <summary>返回栈快照(仅 Type 列表,调试用)</summary>
        public IReadOnlyList<Type> GetStackSnapshot()
        {
            var list = new List<Type>(_stack.Count);
            foreach (var e in _stack) list.Add(e.PanelType);
            return list;
        }

        // ===== 主操作 =====

        /// <summary>
        /// 将面板压入栈顶。已加载则复用,不重复 Instantiate。
        /// 同面板已在栈中时拒绝重复 Open。
        /// 校验新 Panel.Layer >= 当前栈顶.Layer,不满足则 LogError 并拒绝。
        /// </summary>
        public T Open<T>() where T : BaseUIPanel => Open(typeof(T)) as T;

        /// <summary>按 Type 压入栈顶</summary>
        public BaseUIPanel Open(Type panelType)
        {
            if (_ui == null)
            {
                Debug.LogError("[StackPanelModel] UIManager not set; call SetUIManager first");
                return null;
            }
            if (IsInStack(panelType))
            {
                Debug.LogWarning($"[StackPanelModel] Panel {panelType.Name} is already in stack, refusing to open");
                return _ui.GetPanel(panelType);
            }

            var panel = _ui.LoadPanel(panelType);
            if (panel == null) return null;

            // 层级守门:新 Panel.Layer 必须 >= 当前栈顶.Layer(空栈放行)
            if (_stack.Count > 0)
            {
                var topPanel = _stack[^1].Instance;
                if (topPanel != null && (int)panel.Layer < (int)topPanel.Layer)
                {
                    Debug.LogError($"[StackPanelModel] Refuse open {panelType.Name}(Layer={panel.Layer}) on top of {topPanel.GetType().Name}(Layer={topPanel.Layer}): new layer must be >= current top layer");
                    return null;
                }
            }

            // 订阅 Hidden 事件,用于外部关闭兜底
            Action<BaseUIPanel> handler = OnPanelHidden;
            panel.Hidden += handler;
            _subs[panel] = handler;

            var entry = new StackPanelEntry(panelType, panel, _stack.Count + 1);
            panel.OnPushed(entry);
            panel.transform.SetAsLastSibling();
            panel.Show();
            _stack.Add(entry);

            _ui.Call(new PanelOpenedEvent(panelType.Name));
            _ui.Call(new PanelPushedEvent(panelType.Name, _stack.Count));
            return panel;
        }

        /// <summary>
        /// 异步将面板压入栈顶。Addressable 模式下走 UILoaderModule.LoadAsync,Prefab 模式下回退到同步 Load。
        /// 已加载则复用,不重复 Instantiate。
        /// 同面板已在栈中时拒绝重复 Open。
        /// 校验新 Panel.Layer >= 当前栈顶.Layer,不满足则 LogError 并拒绝。
        /// </summary>
        public async UniTask<T> OpenAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => (T)await OpenAsync(typeof(T), ct);

        /// <summary>异步按 Type 压入栈顶</summary>
        public async UniTask<BaseUIPanel> OpenAsync(Type panelType, CancellationToken ct = default)
        {
            if (_ui == null)
            {
                Debug.LogError("[StackPanelModel] UIManager not set; call SetUIManager first");
                return null;
            }
            if (IsInStack(panelType))
            {
                Debug.LogWarning($"[StackPanelModel] Panel {panelType.Name} is already in stack, refusing to open");
                return _ui.GetPanel(panelType);
            }

            var panel = await _ui.GetModule<UILoaderModule>().LoadAsync(panelType, ct);
            if (panel == null) return null;

            // 层级守门:新 Panel.Layer 必须 >= 当前栈顶.Layer(空栈放行)
            if (_stack.Count > 0)
            {
                var topPanel = _stack[^1].Instance;
                if (topPanel != null && (int)panel.Layer < (int)topPanel.Layer)
                {
                    Debug.LogError($"[StackPanelModel] Refuse open {panelType.Name}(Layer={panel.Layer}) on top of {topPanel.GetType().Name}(Layer={topPanel.Layer}): new layer must be >= current top layer");
                    return null;
                }
            }

            // 订阅 Hidden 事件,用于外部关闭兜底
            Action<BaseUIPanel> handler = OnPanelHidden;
            panel.Hidden += handler;
            _subs[panel] = handler;

            var entry = new StackPanelEntry(panelType, panel, _stack.Count + 1);
            panel.OnPushed(entry);
            panel.transform.SetAsLastSibling();
            panel.Show();
            _stack.Add(entry);

            _ui.Call(new PanelOpenedEvent(panelType.Name));
            _ui.Call(new PanelPushedEvent(panelType.Name, _stack.Count));
            return panel;
        }

        /// <summary>
        /// 弹出栈顶面板。栈空返回 false。
        /// 弹栈后若新栈顶当前不可见,会自动 Show 以维持"栈顶可见"不变量。
        /// </summary>
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
                    _ui.Call(new PanelClosedEvent(top.PanelType.Name));
                }
            }
            finally
            {
                _suppressHiddenHandler = false;
            }

            RestoreStackTop();
            _ui.Call(new PanelPoppedEvent(top.PanelType.Name, _stack.Count));
            return true;
        }

        /// <summary>
        /// 弹栈直至指定类型出现在栈顶(保留该类型在栈中的最后一次出现)。
        /// 找不到该类型返回 false。
        /// </summary>
        public bool BackTo<T>() where T : BaseUIPanel => BackTo(typeof(T));

        /// <summary>弹栈直至指定类型出现在栈顶</summary>
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

        /// <summary>
        /// 弹到只剩栈底。栈空或仅一项时返回 false。
        /// </summary>
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

        /// <summary>
        /// 仅清空栈,不主动 Hide 栈内面板。供业务方在切场景/重置时自行控制收尾。
        /// </summary>
        public void ClearStack() => _stack.Clear();

        /// <summary>
        /// 清空栈并 Hide 所有栈内可见面板。
        /// </summary>
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

        /// <summary>
        /// Panel Hidden 事件回调。用于外部代码调用 HidePanel 时自动恢复上一层。
        /// </summary>
        private void OnPanelHidden(BaseUIPanel panel)
        {
            if (_suppressHiddenHandler) return;          // 我自己 CloseTop 触发的,不管
            if (_stack.Count == 0) return;
            if (_stack[^1].Instance != panel) return;  // 不是顶层,不管

            // 顶层被外部关掉,移除栈项并恢复上一层
            _stack.RemoveAt(_stack.Count - 1);
            RestoreStackTop();
            _ui.Call(new PanelPoppedEvent(panel.GetType().Name, _stack.Count));
        }

        /// <summary>
        /// 弹栈后若新栈顶当前不可见,自动 Show 出来。
        /// 若栈条目残留但实例已被销毁,递归清理。
        /// </summary>
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

        /// <summary>
        /// 若指定 Type 已加载但不可见,则 Show 出来。BackTo/PopToRoot 收尾用。
        /// </summary>
        private void EnsureVisible(Type panelType)
        {
            if (_ui.IsPanelLoaded(panelType))
            {
                var p = _ui.GetPanel(panelType);
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
