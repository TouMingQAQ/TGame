using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.Addressable;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板加载/缓存 Module。
    /// 职责:从 UIRegistryModule 拿 PanelConfig(同步 Prefab 模式 或 异步 Addressable 模式) →
    /// Instantiate 到 UILayerRootModule 的根下 →
    /// 重置 RectTransform 填充父节点 → 写入运行时 Layer → Init → 缓存(单实例)。
    ///
    /// 状态:
    ///   - <c>Dictionary&lt;Type, BaseUIPanel&gt; _loaded</c>(单例缓存,Type → 已实例化面板)
    ///   - <c>Dictionary&lt;Type, string&gt; _loadedAddresses</c>(Addressable 模式下记录 address,Unload 时 release)
    ///
    /// 依赖:
    ///   - UIRegistryModule:读 PanelConfig
    ///   - UILayerRootModule:读 layer 根 Transform
    ///   - AddressableModel(挂在 UIManager 下):Addressable 模式下加载/释放 prefab
    ///
    /// 调用链:
    ///   业务方 ShowPanelAsync<T>() → UIManager 转发 → UIVisibilityModule.ShowAsync → 本模块 LoadAsync
    ///   业务方 LoadPanelAsync<T>() → UIManager 转发 → 本模块 LoadAsync
    ///   业务方 ShowPanel<T>() → UIManager 转发 → UIVisibilityModule.Show → 本模块 Load (仅 Prefab 模式可用)
    ///   业务方 GetPanel<T>() / IsPanelLoaded<T>() → UIManager 转发 → 本模块 Get / IsLoaded
    ///   业务方 UnloadPanel<T>() → UIManager 转发 → 本模块 Unload (Addressable 模式下同时 release)
    ///
    /// 关键不变量:
    ///   - Load / LoadAsync 幂等:二次调用直接返回缓存,不重复 Instantiate
    ///   - Prefab 缺组件 / LayerRoot 未配 / Addressable 加载失败:LogError + return null,不污染缓存
    ///   - Addressable 模式下,Unload 时同步 release address handle,RefCount 归零触发 Addressables.Release
    ///   - GO 销毁走三条路:Unload / UIManager.OnDestroy / 业务方外部 Destroy;
    ///     前两条由调用方清缓存,第三条由 Load/Get/IsLoaded 的假 null 守卫兜底清理。
    ///     Module.Destroy 只清字典(避免对已被 UIManager.OnDestroy 销毁的 GO 二次 Destroy 触发警告)
    /// </summary>
    public sealed class UILoaderModule : BaseModule
    {
        private UIManager _ui;
        private readonly Dictionary<Type, BaseUIPanel> _loaded = new();
        // Addressable 模式:Type → 当前持有引用的 address,Unload 时调 AddressableModel.Release
        private readonly Dictionary<Type, string> _loadedAddresses = new();

        /// <summary>由 UIManager.Start 注入自身引用</summary>
        public void SetUIManager(UIManager ui) => _ui = ui;

        // ===== 加载(同步,仅 Prefab 模式) =====

        /// <summary>同步加载(泛型),已加载则直接返回缓存。Addressable 模式请改用 <see cref="LoadAsync{T}"/></summary>
        public T Load<T>() where T : BaseUIPanel => Load(typeof(T)) as T;

        /// <summary>
        /// 同步按 Type 加载,已加载则直接返回缓存。
        /// 未注册 → LogError + return null;Prefab 缺组件 → LogError + Destroy(go) + return null。
        /// 缓存命中时若 GameObject 已被外部 Destroy(Unity 假 null),清理死引用并重新 Instantiate。
        /// <para>**Addressable 模式下面同步调用必返回 null**(无 prefab 引用可同步取),并 LogError。
        /// 异步场景请改用 <see cref="LoadAsync(Type, CancellationToken)"/>。</para>
        /// </summary>
        public BaseUIPanel Load(Type type)
        {
            if (_loaded.TryGetValue(type, out var existing))
            {
                // Unity 假 null:C# 引用非 null,但 Object 已被 Destroy
                if (existing != null && existing.gameObject != null)
                    return existing;
                _loaded.Remove(type);
            }

            if (!_ui.GetModule<UIRegistryModule>().TryGetConfig(type, out var config))
            {
                Debug.LogError($"[UILoaderModule] Panel {type.Name} not registered");
                return null;
            }

            if (config.Mode == RegisterMode.Addressable)
            {
                Debug.LogError($"[UILoaderModule] Panel {type.Name} is registered via Addressables, sync Load returns null; call LoadPanelAsync<{type.Name}>() instead");
                return null;
            }

            var root = _ui.GetModule<UILayerRootModule>().GetLayerRoot(config.Layer);
            if (root == null)
            {
                Debug.LogError($"[UILoaderModule] No layer root for {config.Layer}");
                return null;
            }

            // 实例化到对应层级的 Transform 下
            var go = UnityEngine.Object.Instantiate(config.Prefab, root);
            var panel = go.GetComponent(type) as BaseUIPanel;
            if (panel == null)
            {
                Debug.LogError($"[UILoaderModule] Prefab for {type.Name} missing component {type.Name}");
                UnityEngine.Object.Destroy(go);
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
            _loaded[type] = panel;
            return panel;
        }

        // ===== 加载(异步,Addressable 模式走 AddressableModel;Prefab 模式回退到同步 Load) =====

        /// <summary>异步加载(泛型),已加载则直接返回缓存。Prefab / Addressable 双模式统一入口</summary>
        public async UniTask<T> LoadAsync<T>(CancellationToken ct = default) where T : BaseUIPanel
            => (T)await LoadAsync(typeof(T), ct);

        /// <summary>
        /// 异步按 Type 加载。已加载则直接返回缓存。
        /// 未注册 → LogError + return null;Addressable 加载失败 → LogError + return null。
        /// 缓存命中时若 GameObject 已被外部 Destroy(Unity 假 null),清理死引用并重新加载。
        /// <para>Prefab 模式为方便起见,内部走同步 Load(无 IO 等待,UniTask 立刻完成)。</para>
        /// </summary>
        public async UniTask<BaseUIPanel> LoadAsync(Type type, CancellationToken ct = default)
        {
            if (_loaded.TryGetValue(type, out var existing))
            {
                if (existing != null && existing.gameObject != null)
                    return existing;
                _loaded.Remove(type);
            }

            if (!_ui.GetModule<UIRegistryModule>().TryGetConfig(type, out var config))
            {
                Debug.LogError($"[UILoaderModule] Panel {type.Name} not registered");
                return null;
            }

            GameObject prefab = config.Prefab;
            if (config.Mode == RegisterMode.Addressable)
            {
                var addrModel = _ui.GetModule<AddressableModel>();
                if (addrModel == null)
                {
                    Debug.LogError($"[UILoaderModule] AddressableModel not found on UIManager; ensure UIManager.Start creates it");
                    return null;
                }
                prefab = await addrModel.LoadAsync<GameObject>(config.Address, ct);
                if (prefab == null)
                {
                    Debug.LogError($"[UILoaderModule] Addressables load returned null for {type.Name} (address={config.Address})");
                    return null;
                }
            }

            var root = _ui.GetModule<UILayerRootModule>().GetLayerRoot(config.Layer);
            if (root == null)
            {
                Debug.LogError($"[UILoaderModule] No layer root for {config.Layer}");
                return null;
            }

            var go = UnityEngine.Object.Instantiate(prefab, root);
            var panel = go.GetComponent(type) as BaseUIPanel;
            if (panel == null)
            {
                Debug.LogError($"[UILoaderModule] Prefab for {type.Name} missing component {type.Name}");
                UnityEngine.Object.Destroy(go);
                return null;
            }

            panel.SetLayer(config.Layer);
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
            if (config.Mode == RegisterMode.Addressable)
            {
                _loadedAddresses[type] = config.Address;
            }
            return panel;
        }

        // ===== 查询 =====

        /// <summary>获取已加载的面板(泛型)</summary>
        public T Get<T>() where T : BaseUIPanel => Get(typeof(T)) as T;

        /// <summary>获取已加载的面板(Type),未加载返回 null。GameObject 已被外部 Destroy 时清理缓存并返回 null</summary>
        public BaseUIPanel Get(Type type)
        {
            if (_loaded.TryGetValue(type, out var p) && (p == null || p.gameObject == null))
            {
                _loaded.Remove(type);
                return null;
            }
            return p;
        }

        /// <summary>是否已加载(泛型)</summary>
        public bool IsLoaded<T>() where T : BaseUIPanel => IsLoaded(typeof(T));

        /// <summary>是否已加载(Type)。GameObject 已被外部 Destroy 时清理缓存并返回 false</summary>
        public bool IsLoaded(Type type)
        {
            if (!_loaded.TryGetValue(type, out var p)) return false;
            if (p == null || p.gameObject == null)
            {
                _loaded.Remove(type);
                return false;
            }
            return true;
        }

        // ===== 卸载 =====

        /// <summary>卸载面板(泛型),销毁 GO 并从缓存中移除。Addressable 模式下同时 release address handle</summary>
        public void Unload<T>() where T : BaseUIPanel => Unload(typeof(T));

        /// <summary>按 Type 卸载面板,销毁 GO 并从缓存中移除。Addressable 模式下同时 release address handle</summary>
        public void Unload(Type type)
        {
            if (!_loaded.TryGetValue(type, out var panel)) return;
            UnityEngine.Object.Destroy(panel.gameObject);
            _loaded.Remove(type);
            ReleaseAddressIfAny(type);
        }

        /// <summary>
        /// Addressable 模式专用:对当前 Type 持有的 address handle 做 Release。
        /// 找不到/未持有/已是 Prefab 模式 → 无副作用。
        /// </summary>
        private void ReleaseAddressIfAny(Type type)
        {
            if (!_loadedAddresses.TryGetValue(type, out var addr)) return;
            _loadedAddresses.Remove(type);
            var addrModel = _ui?.GetModule<AddressableModel>();
            if (addrModel != null)
            {
                try { addrModel.Release<GameObject>(addr); }
                catch (Exception e) { Debug.LogWarning($"[UILoaderModule] Release address {addr} failed: {e.Message}"); }
            }
        }

        /// <summary>
        /// 遍历所有已缓存的面板(供 UIManager.OnDestroy 统一销毁用)。
        /// 注意:返回的是字典 Values 引用,不要长期持有或修改。
        /// </summary>
        public IEnumerable<BaseUIPanel> GetAllLoaded() => _loaded.Values;

        /// <summary>
        /// 清空缓存并释放所有 address handle。
        /// **不**在这里 Destroy GameObject —— UIManager.OnDestroy 负责,避免双重 Destroy 触发警告。
        /// </summary>
        public override void Destroy()
        {
            var addrModel = _ui?.GetModule<AddressableModel>();
            if (addrModel != null)
            {
                foreach (var addr in _loadedAddresses.Values)
                {
                    try { addrModel.Release<GameObject>(addr); }
                    catch { /* ReleaseAll 由 AddressableModel.Destroy 兜底 */ }
                }
            }
            _loadedAddresses.Clear();
            _loaded.Clear();
        }
    }
}
