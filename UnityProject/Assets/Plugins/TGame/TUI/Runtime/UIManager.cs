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
    /// 栈式 Panel 管理由 StackPanelModel 接管,本类只保留加载/单实例显隐注册表职责。
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
            // 注入自身引用给 StackPanelModel,供 Open/CloseTop 等栈操作使用
            GetModule<StackPanelModel>().SetUIManager(this);
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
        /// 供 StackPanelModel.Open 等无泛型上下文的调用方使用。
        /// </summary>
        public BaseUIPanel LoadPanel(Type type)
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

            // 写入运行时 Layer(取自注册配置),StackPanelModel 守门使用
            panel.SetLayer(config.Layer);

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
        /// </summary>
        public void UnloadPanel<T>() where T : BaseUIPanel
        {
            UnloadPanel(typeof(T));
        }

        /// <summary>
        /// 按 Type 卸载面板，销毁 GameObject 并从已加载列表中移除。
        /// </summary>
        public void UnloadPanel(Type type)
        {
            if (!_loadedPanels.TryGetValue(type, out var panel)) return;

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
        /// 按 Type 获取已加载的面板，未加载返回 null
        /// </summary>
        public BaseUIPanel GetPanel(Type type)
        {
            _loadedPanels.TryGetValue(type, out var panel);
            return panel;
        }

        /// <summary>
        /// 判断面板是否已加载
        /// </summary>
        public bool IsPanelLoaded<T>() where T : BaseUIPanel
        {
            return _loadedPanels.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 按 Type 判断面板是否已加载
        /// </summary>
        public bool IsPanelLoaded(Type type)
        {
            return _loadedPanels.ContainsKey(type);
        }

        /// <summary>
        /// 获取指定层级的根 Transform
        /// </summary>
        public Transform GetLayerRoot(UILayer layer)
        {
            _layerRoots.TryGetValue(layer, out var root);
            return root;
        }

        private void OnDestroy()
        {
            foreach (var panel in _loadedPanels.Values)
            {
                if (panel != null)
                    Destroy(panel.gameObject);
            }
            _loadedPanels.Clear();
            ClearModule();
        }

        private struct UIPanelConfig
        {
            public GameObject Prefab;
            public UILayer Layer;
        }
    }
}
