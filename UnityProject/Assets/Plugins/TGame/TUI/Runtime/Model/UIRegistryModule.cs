using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TGame.Addressable;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板注册表 Module,挂载在 UIManager 上(全局单例:全 UIRoot 共享)。
    /// 职责:记录每个面板类型对应的 Addressables address,供 UILoaderModule 异步加载时读取。
    ///
    /// 划界:本表只管"资产身份"(Type → address),属于资产侧,挂在 UIManager 上与 AddressableModule 同 host。
    /// 同一 Type 的 address 在所有 UIRoot 下一致 —— 渲染隔离(不同 UIRoot 各自的实例)由 UILoaderModule 的 per-UIRoot 实例缓存负责。
    ///
    /// 状态:<c>Dictionary&lt;Type, string&gt;</c>
    /// 依赖:无(预热时传入 AddressableModule 走 label 自动注册)
    /// </summary>
    public sealed class UIRegistryModule : BaseModule
    {
        private readonly Dictionary<Type, string> _addresses = new();

        /// <summary>
        /// 注册面板(Addressable 路径,泛型入口)。已注册的同 Type 会被拒绝并 LogWarning,避免静默覆盖。
        /// </summary>
        public void Register<T>(string address) where T : BaseUIPanel
            => Register(typeof(T), address);

        /// <summary>
        /// 注册面板(Addressable 路径,运行时 Type 入口 —— 预热反查到的 panel 用)。
        /// 已注册的同 Type 会被拒绝并 LogWarning,避免静默覆盖。
        /// </summary>
        public void Register(Type panelType, string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError($"[UIRegistryModule] Register {panelType.Name}: address is null or empty");
                return;
            }
            if (_addresses.ContainsKey(panelType))
            {
                Debug.LogWarning($"[UIRegistryModule] Panel {panelType.Name} already registered, skipping");
                return;
            }
            _addresses[panelType] = address;
        }

        /// <summary>
        /// 按 Addressables label 预热:解析 label → address 列表,
        /// 对每个 address 调 <paramref name="addr"/>.LoadAsync&lt;GameObject&gt; 暖热句柄池,
        /// 并从 prefab 反查 BaseUIPanel 具体子类,把 (panelType, address) 自动写入本表。
        /// 不 Instantiate —— 仅暖热句柄池,后续 LoadPanelAsync&lt;T&gt; 命中池直接 Instantiate。
        ///
        /// 已注册的同 Type 会被 Register 跳过(LogWarning);Addressable 加载失败的 address 会被记录并跳过,不影响其他 address。
        /// </summary>
        public async UniTask PreloadByLabelAsync(
            string label, AddressableModule addr,
            IProgress<float> progress = null, CancellationToken ct = default)
        {
            if (addr == null)
            {
                Debug.LogError("[UIRegistryModule] PreloadByLabelAsync: AddressableModule is null");
                return;
            }
            if (string.IsNullOrEmpty(label))
            {
                Debug.LogError("[UIRegistryModule] PreloadByLabelAsync: label is null or empty");
                return;
            }

            var addresses = await addr.ResolveAddressesByLabelAsync(label, ct);
            int total = addresses.Count;
            if (total == 0)
            {
                Debug.LogWarning($"[UIRegistryModule] PreloadByLabelAsync: no addresses under label '{label}'");
                return;
            }

            int done = 0;
            var tasks = addresses.Select(address => LoadOneAsync(address, addr, ct, () =>
            {
                var n = Interlocked.Increment(ref done);
                progress?.Report((float)n / total);
            }));
            await UniTask.WhenAll(tasks);

            async UniTask LoadOneAsync(string a, AddressableModule m, CancellationToken c, Action onDone)
            {
                try
                {
                    var prefab = await m.LoadAsync<GameObject>(a, c);
                    if (prefab != null && prefab.GetComponent<BaseUIPanel>() is BaseUIPanel panel)
                        Register(panel.GetType(), a);
                    else if (prefab != null)
                        Debug.LogWarning($"[UIRegistryModule] Preload: prefab '{a}' has no BaseUIPanel on root, skipped");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UIRegistryModule] Preload '{a}' failed: {e.Message}");
                }
                finally
                {
                    onDone?.Invoke();
                }
            }
        }

        /// <summary>
        /// 查询某 Type 的注册 address。
        /// </summary>
        /// <param name="type">面板类型</param>
        /// <param name="address">命中时返回 address,未命中返回 null</param>
        /// <returns>true = 命中,false = 未注册</returns>
        public bool TryGetAddress(Type type, out string address)
            => _addresses.TryGetValue(type, out address);

        /// <summary>某 Type 是否已注册(调试/校验用)</summary>
        public bool IsRegistered(Type type) => _addresses.ContainsKey(type);

        /// <summary>已注册条目数(调试用)</summary>
        public int Count => _addresses.Count;
    }
}