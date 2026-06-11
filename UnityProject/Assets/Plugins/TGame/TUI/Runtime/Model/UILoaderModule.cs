using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板加载/缓存 Module。
    /// 职责:从 UIRegistryModule 拿 prefab 配置 → Instantiate 到 UILayerRootModule 的根下 →
    /// 重置 RectTransform 填充父节点 → 写入运行时 Layer → Init → 缓存(单例)。
    ///
    /// 状态:<c>Dictionary&lt;Type, BaseUIPanel&gt; _loaded</c>(单例缓存)
    ///
    /// 依赖:
    ///   - UIRegistryModule:读 (prefab, layer) 配置
    ///   - UILayerRootModule:读 layer 根 Transform
    ///
    /// 调用链:
    ///   业务方 ShowPanel<T>() → UIManager.ShowPanel(转发)
    ///     → UIVisibilityModule.Show → 本模块 Load()
    ///   业务方 LoadPanel<T>() → UIManager.LoadPanel(转发) → 本模块 Load()
    ///   业务方 GetPanel<T>() / IsPanelLoaded<T>() → UIManager 转发 → 本模块 Get / IsLoaded
    ///   业务方 UnloadPanel<T>() → UIManager 转发 → 本模块 Unload
    ///
    /// 关键不变量:
    ///   - Load 幂等:二次调用直接返回缓存,不重复 Instantiate
    ///   - Prefab 缺组件 / LayerRoot 未配:LogError + return null,不污染缓存
    ///   - GO 销毁由 Unload 或 UIManager.OnDestroy 负责;Module.Destroy 只清字典
    ///     (避免对已被 UIManager.OnDestroy 销毁的 GO 二次 Destroy 触发警告)
    /// </summary>
    public sealed class UILoaderModule : BaseModule
    {
        private UIManager _ui;
        private readonly Dictionary<Type, BaseUIPanel> _loaded = new();

        /// <summary>由 UIManager.Start 注入自身引用</summary>
        public void SetUIManager(UIManager ui) => _ui = ui;

        // ===== 加载 =====

        /// <summary>加载(泛型版本),已加载则直接返回缓存</summary>
        public T Load<T>() where T : BaseUIPanel => Load(typeof(T)) as T;

        /// <summary>
        /// 按 Type 加载,已加载则直接返回缓存。
        /// 未注册 → LogError + return null;Prefab 缺组件 → LogError + Destroy(go) + return null。
        /// </summary>
        public BaseUIPanel Load(Type type)
        {
            if (_loaded.TryGetValue(type, out var existing))
                return existing;

            if (!_ui.GetModule<UIRegistryModule>().TryGetConfig(type, out var config))
            {
                Debug.LogError($"[UILoaderModule] Panel {type.Name} not registered");
                return null;
            }

            var root = _ui.GetModule<UILayerRootModule>().GetLayerRoot(config.layer);
            if (root == null)
            {
                Debug.LogError($"[UILoaderModule] No layer root for {config.layer}");
                return null;
            }

            // 实例化到对应层级的 Transform 下
            var go = UnityEngine.Object.Instantiate(config.prefab, root);
            var panel = go.GetComponent(type) as BaseUIPanel;
            if (panel == null)
            {
                Debug.LogError($"[UILoaderModule] Prefab for {type.Name} missing component {type.Name}");
                UnityEngine.Object.Destroy(go);
                return null;
            }

            // 写入运行时 Layer(取自注册配置),StackPanelModel 守门使用
            panel.SetLayer(config.layer);

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
            _loaded[type] = panel;
            return panel;
        }

        // ===== 查询 =====

        /// <summary>获取已加载的面板(泛型)</summary>
        public T Get<T>() where T : BaseUIPanel => Get(typeof(T)) as T;

        /// <summary>获取已加载的面板(Type),未加载返回 null</summary>
        public BaseUIPanel Get(Type type)
        {
            _loaded.TryGetValue(type, out var p);
            return p;
        }

        /// <summary>是否已加载(泛型)</summary>
        public bool IsLoaded<T>() where T : BaseUIPanel => IsLoaded(typeof(T));

        /// <summary>是否已加载(Type)</summary>
        public bool IsLoaded(Type type) => _loaded.ContainsKey(type);

        // ===== 卸载 =====

        /// <summary>卸载面板(泛型),销毁 GO 并从缓存中移除</summary>
        public void Unload<T>() where T : BaseUIPanel => Unload(typeof(T));

        /// <summary>按 Type 卸载面板,销毁 GO 并从缓存中移除</summary>
        public void Unload(Type type)
        {
            if (!_loaded.TryGetValue(type, out var panel)) return;
            UnityEngine.Object.Destroy(panel.gameObject);
            _loaded.Remove(type);
        }

        /// <summary>
        /// 遍历所有已缓存的面板(供 UIManager.OnDestroy 统一销毁用)。
        /// 注意:返回的是字典 Values 引用,不要长期持有或修改。
        /// </summary>
        public IEnumerable<BaseUIPanel> GetAllLoaded() => _loaded.Values;

        /// <summary>
        /// 清空缓存。
        /// **不**在这里 Destroy GameObject —— UIManager.OnDestroy 负责,避免双重 Destroy 触发警告。
        /// </summary>
        public override void Destroy() => _loaded.Clear();
    }
}
