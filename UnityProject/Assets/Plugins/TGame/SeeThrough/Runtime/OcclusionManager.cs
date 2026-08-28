using System.Collections.Generic;
using UnityEngine;

namespace TGame.SeeThrough
{
    /// <summary>
    /// 全局多目标遮挡透视管理器。
    /// 收集场景中所有激活的 OcclusionTarget，并将其世界坐标、半径、颜色等参数
    /// 以数组形式批量传递给全局着色器属性，驱动建筑 ShaderGraph 实现多目标同时透视。
    /// </summary>
    [ExecuteAlways]
    public class OcclusionManager : MonoBehaviour
    {
        public const int MaxTargets = 16;

        private static readonly List<OcclusionTarget> ActiveTargets = new List<OcclusionTarget>();

        private static readonly int TargetPositionsId = Shader.PropertyToID("_TargetPositions");
        private static readonly int TargetParamsId = Shader.PropertyToID("_TargetParams");
        private static readonly int TargetColorsId = Shader.PropertyToID("_TargetColors");
        private static readonly int TargetCountId = Shader.PropertyToID("_TargetCount");
        private static readonly int FallbackTargetPosId = Shader.PropertyToID("_TargetPos");

        private readonly Vector4[] _positionsArray = new Vector4[MaxTargets];
        private readonly Vector4[] _paramsArray = new Vector4[MaxTargets];
        private readonly Vector4[] _colorsArray = new Vector4[MaxTargets];

        public static void Register(OcclusionTarget target)
        {
            if (target != null && !ActiveTargets.Contains(target))
            {
                ActiveTargets.Add(target);
            }
        }

        public static void Unregister(OcclusionTarget target)
        {
            if (target != null)
            {
                ActiveTargets.Remove(target);
            }
        }

        private void LateUpdate()
        {
            UpdateShaderGlobals();
        }

        private void OnValidate()
        {
            UpdateShaderGlobals();
        }

        public void UpdateShaderGlobals()
        {
            // 清理已销毁对象
            ActiveTargets.RemoveAll(t => t == null || !t.isActiveAndEnabled);

            int count = Mathf.Min(ActiveTargets.Count, MaxTargets);

            for (int i = 0; i < count; i++)
            {
                OcclusionTarget target = ActiveTargets[i];
                Vector3 wPos = target.transform.position;

                _positionsArray[i] = new Vector4(wPos.x, wPos.y, wPos.z, target.cutoutRadius);
                _paramsArray[i] = new Vector4(target.cutoutRadius, target.softness, target.minAlpha, target.edgeWidth);
                _colorsArray[i] = new Vector4(target.edgeColor.r, target.edgeColor.g, target.edgeColor.b, target.edgeColor.a);
            }

            // 清零剩余槽位
            for (int i = count; i < MaxTargets; i++)
            {
                _positionsArray[i] = Vector4.zero;
                _paramsArray[i] = Vector4.zero;
                _colorsArray[i] = Vector4.zero;
            }

            Shader.SetGlobalVectorArray(TargetPositionsId, _positionsArray);
            Shader.SetGlobalVectorArray(TargetParamsId, _paramsArray);
            Shader.SetGlobalVectorArray(TargetColorsId, _colorsArray);
            Shader.SetGlobalInt(TargetCountId, count);

            if (count > 0)
            {
                Shader.SetGlobalVector(FallbackTargetPosId, _positionsArray[0]);
            }
        }
    }
}
