using System.Collections.Generic;
using TGame.TCore.Runtime;
using UnityEngine;

namespace TGame.TUI
{
    /// <summary>
    /// UI 层级根 Transform Module。
    /// 职责:管理 6 个 UILayer 枚举值对应的根 Transform,供 UILoaderModule 实例化时挂载用。
    ///
    /// 状态:<c>Dictionary&lt;UILayer, Transform&gt;</c>
    ///
    /// 依赖:无
    ///
    /// 数据来源:UIManager.Awake 持 6 个 SerializeField,在 Awake 里逐个 SetLayerRoot 写进来。
    /// 保持 SerializeField 写在 UIManager(MonoBehaviour)而非 Module 上,避免污染 BaseModule 纯 C# 体系。
    ///
    /// 生命周期:挂在 UIManager 下,Awake 时第一波填表;后续 GetLayerRoot 即可。
    /// Transform 引用由 UIManager(场景组件)生命周期管理,本模块无需 Destroy。
    /// </summary>
    public sealed class UILayerRootModule : BaseModule
    {
        private readonly Dictionary<UILayer, Transform> _roots = new();

        /// <summary>
        /// 设置某 UILayer 的根 Transform。由 UIManager.Awake 调用。
        /// </summary>
        public void SetLayerRoot(UILayer layer, Transform root) => _roots[layer] = root;

        /// <summary>
        /// 查询某 UILayer 的根 Transform,未设置返回 null。
        /// </summary>
        public Transform GetLayerRoot(UILayer layer)
        {
            _roots.TryGetValue(layer, out var root);
            return root;
        }

        /// <summary>遍历所有已配置的 (Layer, Root) 对(调试/校验用)</summary>
        public IEnumerable<KeyValuePair<UILayer, Transform>> GetAllLayerRoots() => _roots;
    }
}
