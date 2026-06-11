using System;
using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UI 面板注册表 Module。
    /// 职责:记录每个面板类型对应的 prefab 和所属 UILayer,供 UILoaderModule 实例化时读取。
    ///
    /// 状态:<c>Dictionary&lt;Type, (GameObject prefab, UILayer layer)&gt;</c>
    ///
    /// 依赖:无
    ///
    /// 调用链:
    ///   业务方 RegisterPanel(prefab, layer) → UIManager.RegisterPanel(转发)
    ///     → 本模块 Register()
    ///   UILoaderModule.Load(type) → 本模块 TryGetConfig()
    ///
    /// 生命周期:挂在 UIManager 下,由 BaseManager.AddModule<T>() 创建并 Init。
    /// 字典不持 Unity Object 强引用之外的资源,Destroy 时不需清理。
    /// </summary>
    public sealed class UIRegistryModule : BaseModule
    {
        private readonly Dictionary<Type, (GameObject prefab, UILayer layer)> _configs = new();

        /// <summary>
        /// 注册一个面板类型。
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
            _configs[type] = (prefab.gameObject, layer);
        }

        /// <summary>
        /// 查询某 Type 的注册配置。
        /// </summary>
        /// <param name="type">面板类型</param>
        /// <param name="config">命中时返回 (prefab, layer),未命中返回 default</param>
        /// <returns>true = 命中,false = 未注册</returns>
        public bool TryGetConfig(Type type, out (GameObject prefab, UILayer layer) config)
            => _configs.TryGetValue(type, out config);

        /// <summary>某 Type 是否已注册(调试/校验用)</summary>
        public bool IsRegistered(Type type) => _configs.ContainsKey(type);
    }
}
