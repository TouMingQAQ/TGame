using UnityEngine;

namespace TGame.SeeThrough
{
    /// <summary>
    /// 单目标控制器（兼容旧版命名），继承 OcclusionTarget 的全部功能。
    /// 旧场景/预制体若挂载过此组件，迁移后依然可用；新项目请直接使用 OcclusionTarget。
    /// </summary>
    [ExecuteAlways]
    public class TargetOcclusionController : OcclusionTarget
    {
    }
}
