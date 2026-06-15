using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// 面板注册模式。Prefab = 旧路径,Inspector 直接拖 prefab 引用;
    /// Addressable = 新路径,提供 Addressables address 字符串,运行时由 UILoaderModule 异步加载。
    /// </summary>
    public enum RegisterMode
    {
        /// <summary>旧路径:Inspector 拖 prefab,UILoaderModule 同步 Instantiate</summary>
        Prefab,
        /// <summary>新路径:Addressables address,UILoaderModule 走 AddressableModel.LoadAsync</summary>
        Addressable,
    }

    /// <summary>
    /// 单个面板的注册配置。三种注册模式(Prefab / Addressable)的元数据共存于同一字典,
    /// 通过 <see cref="Mode"/> 字段区分,避免维护双份注册表。
    /// </summary>
    public readonly struct PanelConfig
    {
        /// <summary>注册模式</summary>
        public readonly RegisterMode Mode;
        /// <summary>旧路径 prefab(Mode == Prefab 时有效,Addressable 时为 null)</summary>
        public readonly GameObject Prefab;
        /// <summary>Addressables address(Mode == Addressable 时有效,Prefab 时为 null)</summary>
        public readonly string Address;
        /// <summary>UI 层级,影响 Instantiate 时的父节点和渲染顺序</summary>
        public readonly UILayer Layer;

        public PanelConfig(RegisterMode mode, GameObject prefab, string address, UILayer layer)
        {
            Mode = mode;
            Prefab = prefab;
            Address = address;
            Layer = layer;
        }
    }

    /// <summary>
    /// UI 面板注册表 Module。
    /// 职责:记录每个面板类型对应的 prefab/address 和所属 UILayer,供 UILoaderModule 实例化时读取。
    ///
    /// 状态:<c>Dictionary&lt;Type, PanelConfig&gt;</c>
    ///
    /// 依赖:无
    ///
    /// 调用链:
    ///   业务方 RegisterPanel(prefab) / RegisterPanelAsync(address) → UIManager 转发
    ///     → 本模块 Register
    ///   UILoaderModule.Load(type) / LoadAsync(type) → 本模块 TryGetConfig
    ///
    /// 生命周期:挂在 UIManager 下,由 BaseManager.GetModule&lt;T&gt;() 创建并 Init。
    /// 字典不持 Unity Object 强引用之外的资源,Destroy 时不需清理。
    /// </summary>
    public sealed class UIRegistryModule : BaseModule
    {
        private readonly Dictionary<Type, PanelConfig> _configs = new();

        /// <summary>
        /// 注册一个面板类型(旧 Prefab 路径,同步)。
        /// 已注册的同 Type 会被拒绝并 LogWarning,避免静默覆盖。
        /// </summary>
        /// <typeparam name="T">面板具体类型(继承自 BaseUIPanel)</typeparam>
        /// <param name="prefab">面板预制体引用(场景中或 Resources 里挂着的实例)</param>
        /// <param name="layer">所属 UI 层级,影响 Instantiate 时的父节点和渲染顺序</param>
        public void Register<T>(T prefab, UILayer layer = UILayer.Normal) where T : BaseUIPanel
        {
            var type = typeof(T);
            if (_configs.ContainsKey(type))
            {
                Debug.LogWarning($"[UIRegistryModule] Panel {type.Name} already registered");
                return;
            }
            _configs[type] = new PanelConfig(RegisterMode.Prefab, prefab.gameObject, null, layer);
        }

        /// <summary>
        /// 注册一个面板类型(Addressable 路径,提供 address 字符串)。
        /// 已注册的同 Type 会被拒绝并 LogWarning,避免静默覆盖。
        /// </summary>
        /// <typeparam name="T">面板具体类型(继承自 BaseUIPanel)</typeparam>
        /// <param name="address">Addressables address 字符串(在 Addressables Groups 窗口配置)</param>
        /// <param name="layer">所属 UI 层级</param>
        public void Register<T>(string address, UILayer layer = UILayer.Normal) where T : BaseUIPanel
        {
            var type = typeof(T);
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogError($"[UIRegistryModule] Register {type.Name}: address is null or empty");
                return;
            }
            if (_configs.ContainsKey(type))
            {
                Debug.LogWarning($"[UIRegistryModule] Panel {type.Name} already registered");
                return;
            }
            _configs[type] = new PanelConfig(RegisterMode.Addressable, null, address, layer);
        }

        /// <summary>
        /// 查询某 Type 的注册配置。
        /// </summary>
        /// <param name="type">面板类型</param>
        /// <param name="config">命中时返回 PanelConfig,未命中返回 default</param>
        /// <returns>true = 命中,false = 未注册</returns>
        public bool TryGetConfig(Type type, out PanelConfig config)
            => _configs.TryGetValue(type, out config);

        /// <summary>某 Type 是否已注册(调试/校验用)</summary>
        public bool IsRegistered(Type type) => _configs.ContainsKey(type);
    }
}
