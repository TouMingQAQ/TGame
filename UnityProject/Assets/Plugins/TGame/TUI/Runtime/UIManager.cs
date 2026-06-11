using System;
using System.Collections.Generic;
using UnityEngine;
using TGame.TCore.Runtime;

namespace TGame.TUI
{
    /// <summary>
    /// UI 管理器，负责面板的注册、加载、显示、隐藏和卸载。
    /// 通过层级 Transform 的父子关系和兄弟顺序控制渲染次序，不修改 Canvas.sortingOrder。
    /// ShowPanel 时将面板移至同层末尾，保证其渲染在最上层。
    /// </summary>
    public sealed class UIManager : BaseManager
    {
        [SerializeField] private Transform _backgroundRoot;
        [SerializeField] private Transform _sceneRoot;
        [SerializeField] private Transform _normalRoot;
        [SerializeField] private Transform _popupRoot;
        [SerializeField] private Transform _overlayRoot;
        [SerializeField] private Transform _topRoot;

        private Dictionary<UILayer, Transform> _layerRoots = new();
        private Dictionary<Type, UIPanelConfig> _configs = new();
        private Dictionary<Type, BaseUIPanel> _loadedPanels = new();
        // UI 栈：记录 PushPanel 打开顺序，PopPanel/BackTo 按此回退
        private readonly List<UIPanelStackEntry> _stack = new();

        private void Awake()
        {
            _layerRoots[UILayer.Background] = _backgroundRoot;
            _layerRoots[UILayer.Scene] = _sceneRoot;
            _layerRoots[UILayer.Normal] = _normalRoot;
            _layerRoots[UILayer.Popup] = _popupRoot;
            _layerRoots[UILayer.Overlay] = _overlayRoot;
            _layerRoots[UILayer.Top] = _topRoot;
        }

        private void Start()
        {
            game = Game.Instance;
            game.AddManager(this);
        }

        /// <summary>
        /// 注册面板类型，绑定预制体和层级
        /// </summary>
        public void RegisterPanel<T>(T prefab, UILayer layer = UILayer.Normal) where T : BaseUIPanel
        {
            var type = typeof(T);
            if (_configs.ContainsKey(type))
            {
                Debug.LogWarning($"[UIManager] Panel {type.Name} already registered");
                return;
            }
            _configs[type] = new UIPanelConfig { Prefab = prefab.gameObject, Layer = layer };
        }

        /// <summary>
        /// 加载面板到对应层级下，不显示。已加载的面板会直接返回
        /// </summary>
        public T LoadPanel<T>() where T : BaseUIPanel
        {
            return LoadPanel(typeof(T)) as T;
        }

        /// <summary>
        /// 按 Type 加载面板到对应层级下，不显示。已加载的面板会直接返回。
        /// 供 PushPanel(Type) 等无泛型上下文的内部调用使用。
        /// </summary>
        private BaseUIPanel LoadPanel(Type type)
        {
            if (_loadedPanels.TryGetValue(type, out var existing))
                return existing;

            if (!_configs.TryGetValue(type, out var config))
            {
                Debug.LogError($"[UIManager] Panel {type.Name} not registered");
                return null;
            }

            // 实例化到对应层级的 Transform 下
            var go = Instantiate(config.Prefab, _layerRoots[config.Layer]);
            var panel = go.GetComponent(type) as BaseUIPanel;
            if (panel == null)
            {
                Debug.LogError($"[UIManager] Prefab for {type.Name} missing component {type.Name}");
                Destroy(go);
                return null;
            }

            // 重置 RectTransform 为填充父节点
            var rt = go.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            panel.Init();
            go.SetActive(false);
            _loadedPanels[type] = panel;
            return panel;
        }

        /// <summary>
        /// 显示面板。移至同层末尾保证渲染在最上层，广播 PanelOpenedEvent
        /// </summary>
        public T ShowPanel<T>() where T : BaseUIPanel
        {
            var panel = LoadPanel<T>();
            if (panel == null) return null;
            if (panel.IsVisible) return panel;

            // 移至同层末尾 = 渲染在最上层
            panel.transform.SetAsLastSibling();
            panel.Show();
            Call(new PanelOpenedEvent(typeof(T).Name));
            return panel;
        }

        /// <summary>
        /// 隐藏面板，广播 PanelClosedEvent
        /// </summary>
        public void HidePanel<T>() where T : BaseUIPanel
        {
            var type = typeof(T);
            if (!_loadedPanels.TryGetValue(type, out var panel)) return;
            if (!panel.IsVisible) return;

            panel.Hide();
            Call(new PanelClosedEvent(type.Name));
        }

        /// <summary>
        /// 卸载面板，销毁 GameObject 并从已加载列表中移除。
        /// 同步清理该类型在 UI 栈中的所有条目。
        /// </summary>
        public void UnloadPanel<T>() where T : BaseUIPanel
        {
            UnloadPanel(typeof(T));
        }

        /// <summary>
        /// 按 Type 卸载面板，销毁 GameObject 并从已加载列表中移除。
        /// 同步清理该 Type 在 UI 栈中的所有条目。
        /// </summary>
        public void UnloadPanel(Type type)
        {
            if (!_loadedPanels.TryGetValue(type, out var panel)) return;

            // 清理栈中所有该 Type 的条目；
            // 若被清理的是栈顶且清后栈非空，新栈顶若不可见则 Show 出来。
            if (_stack.Count > 0 && _stack[_stack.Count - 1].PanelType == type)
            {
                _stack.RemoveAt(_stack.Count - 1);
                RestoreStackTop();
            }
            else
            {
                for (int i = _stack.Count - 1; i >= 0; i--)
                    if (_stack[i].PanelType == type) _stack.RemoveAt(i);
            }

            if (panel.IsVisible)
                Call(new PanelClosedEvent(type.Name));

            Destroy(panel.gameObject);
            _loadedPanels.Remove(type);
        }

        /// <summary>
        /// 获取已加载的面板，未加载返回 null
        /// </summary>
        public T GetPanel<T>() where T : BaseUIPanel
        {
            _loadedPanels.TryGetValue(typeof(T), out var panel);
            return panel as T;
        }

        /// <summary>
        /// 判断面板是否已加载
        /// </summary>
        public bool IsPanelLoaded<T>() where T : BaseUIPanel
        {
            return _loadedPanels.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 获取指定层级的根 Transform
        /// </summary>
        public Transform GetLayerRoot(UILayer layer)
        {
            _layerRoots.TryGetValue(layer, out var root);
            return root;
        }

        // ========== UI 栈 API ==========

        /// <summary>当前 UI 栈深度</summary>
        public int StackDepth => _stack.Count;

        /// <summary>栈顶面板类型，栈空返回 null</summary>
        public Type GetStackTop() => _stack.Count == 0 ? null : _stack[_stack.Count - 1].PanelType;

        /// <summary>类型是否在栈中（任意位置）</summary>
        public bool IsInStack<T>() where T : BaseUIPanel => IsInStack(typeof(T));

        /// <summary>类型是否在栈中（任意位置）</summary>
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
            return _stack.Count > 0 && _stack[_stack.Count - 1].PanelType == type;
        }

        /// <summary>返回栈快照（仅 Type 列表，调试用）</summary>
        public IReadOnlyList<Type> GetStackSnapshot()
        {
            var list = new List<Type>(_stack.Count);
            foreach (var e in _stack) list.Add(e.PanelType);
            return list;
        }

        /// <summary>
        /// 将面板压入 UI 栈顶。已加载则复用，不重复 Instantiate。
        /// 不自动隐藏栈中已有面板（与浏览器/移动端前进-后退语义一致）。
        /// 同面板已在栈中时拒绝重复 Push 并发出警告。
        /// </summary>
        public T PushPanel<T>() where T : BaseUIPanel
        {
            return PushPanel(typeof(T)) as T;
        }

        /// <summary>
        /// 按 Type 压入 UI 栈顶。
        /// </summary>
        public BaseUIPanel PushPanel(Type panelType)
        {
            if (IsInStack(panelType))
            {
                Debug.LogWarning($"[UIManager] Panel {panelType.Name} is already in stack, refusing to push");
                return _loadedPanels.TryGetValue(panelType, out var p) ? p : null;
            }

            var panel = LoadPanel(panelType);
            if (panel == null) return null;
            if (panel.IsVisible) return panel;

            // 记录入栈上下文（在 Show 之前）供 OnPushed 钩子使用
            var entry = new UIPanelStackEntry(panelType, panel, _stack.Count + 1);

            panel.OnPushed(entry);
            panel.transform.SetAsLastSibling();
            panel.Show();
            _stack.Add(entry);
            Call(new PanelOpenedEvent(panelType.Name));
            Call(new PanelPushedEvent(panelType.Name, _stack.Count));
            return panel;
        }

        /// <summary>
        /// 弹出栈顶面板。栈空返回 false。
        /// 弹栈后若新栈顶当前不可见，会自动 Show 以维持"栈顶可见"不变量。
        /// </summary>
        public bool PopPanel()
        {
            if (_stack.Count == 0) return false;
            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);

            if (top.Instance != null && top.Instance.IsVisible)
            {
                top.Instance.OnPopped(new UIPanelStackEntry(top.PanelType, top.Instance, _stack.Count + 1));
                top.Instance.Hide();
                Call(new PanelClosedEvent(top.PanelType.Name));
            }
            else if (top.Instance == null)
            {
                // 实例已被外部销毁但栈条目残留：跳过 Hide，仅记日志
                Debug.LogWarning($"[UIManager] Stack top {top.PanelType.Name} instance is null on pop, skipping hide");
            }

            RestoreStackTop();

            Call(new PanelPoppedEvent(top.PanelType.Name, _stack.Count));
            return true;
        }

        /// <summary>
        /// 弹栈直至指定类型出现在栈顶（保留该类型在栈中的最后一次出现）。
        /// 找不到该类型返回 false。
        /// </summary>
        public bool BackTo<T>() where T : BaseUIPanel => BackTo(typeof(T));

        /// <summary>
        /// 弹栈直至指定类型出现在栈顶。
        /// </summary>
        public bool BackTo(Type panelType)
        {
            int targetIndex = -1;
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].PanelType == panelType) { targetIndex = i; break; }
            }
            if (targetIndex < 0) return false;

            // 弹出 [targetIndex + 1, count) 之间的所有条目
            while (_stack.Count > targetIndex + 1)
            {
                if (!PopPanel()) break;
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
            // 弹出 [1, count) —— 保留栈底
            while (_stack.Count > 1)
            {
                if (!PopPanel()) break;
            }
            EnsureVisible(_stack[0].PanelType);
            return true;
        }

        /// <summary>
        /// 仅清空栈，不主动 Hide 栈内面板。供业务方在切场景/重置时自行控制收尾。
        /// </summary>
        public void ClearStack()
        {
            _stack.Clear();
        }

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

        /// <summary>
        /// 内部辅助：弹栈后若新栈顶当前不可见，自动 Show 出来。
        /// 若栈条目残留但实例已被销毁，递归清理。
        /// </summary>
        private void RestoreStackTop()
        {
            if (_stack.Count == 0) return;
            var top = _stack[_stack.Count - 1];
            // 实例已被外部 Destroy 残留栈条目：清理该条目，递归继续
            if (top.Instance == null || !_loadedPanels.ContainsKey(top.PanelType))
            {
                Debug.LogWarning($"[UIManager] Stack entry {top.PanelType.Name} is stale, removing");
                _stack.RemoveAt(_stack.Count - 1);
                RestoreStackTop();
                return;
            }
            if (!top.Instance.IsVisible)
            {
                top.Instance.Show();
            }
        }

        /// <summary>
        /// 若指定 Type 已加载但不可见，则 Show 出来。BackTo/PopToRoot 收尾用。
        /// </summary>
        private void EnsureVisible(Type panelType)
        {
            if (_loadedPanels.TryGetValue(panelType, out var panel) && !panel.IsVisible)
                panel.Show();
        }

        private void OnDestroy()
        {
            _stack.Clear();
            foreach (var panel in _loadedPanels.Values)
            {
                if (panel != null)
                    Destroy(panel.gameObject);
            }
            _loadedPanels.Clear();
        }

        private struct UIPanelConfig
        {
            public GameObject Prefab;
            public UILayer Layer;
        }
    }
}
