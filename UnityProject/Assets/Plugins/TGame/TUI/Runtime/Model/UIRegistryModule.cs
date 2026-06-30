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

       

        public bool TryGetAddress<T>(out string address) where T : BaseUIPanel
            => _addresses.TryGetValue(typeof(T), out address);        
        /// <summary>某 Type 是否已注册(调试/校验用)</summary>
        public bool IsRegistered<T>(Type type) where T : BaseUIPanel 
            => _addresses.ContainsKey(typeof(T));

        /// <summary>已注册条目数(调试用)</summary>
        public int Count => _addresses.Count;
    }
}