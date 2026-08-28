# TGame.SeeThrough 多目标遮挡透视插件 — 功能总结

> 来源：`Assets/Beta/AlphaTest` 实验项目 → 已整理为正式插件 `Assets/Plugins/TGame/SeeThrough/`
> 文档日期：本次整理时生成

## 一句话概述

建筑遮挡透视（X-Ray / See-Through）：被墙挡住的角色在墙面上形成"视线镂空"，边缘带发光描边；
多目标同时生效，镂空区域 Metaball 平滑融合。最大支持 16 个目标。

## 资产清单与职责

| 资产 | 类型 | 职责 |
| --- | --- | --- |
| `OcclusionTarget.cs` | Runtime 组件 | 每个透视目标挂一个；配置半径/羽化/最小Alpha/边缘颜色/边缘宽度；OnEnable 自动注册、OnDisable 注销；带演示用正弦巡逻移动 + 选中 Gizmo 线框 |
| `OcclusionManager.cs` | Runtime 组件（全局） | `[ExecuteAlways]`；收集所有激活目标，LateUpdate/OnValidate 批量写入 `_TargetPositions/_TargetParams/_TargetColors/_TargetCount/_TargetPos` 全局属性；自动清理销毁/失活目标 |
| `TargetOcclusionController.cs` | 兼容类 | 继承 OcclusionTarget，旧命名桥接 |
| `BuildingSeeThroughLogic.hlsl` | HLSL 库 | 全局数组声明 + `GetTargetSignedDistance`（视线锥体 SDF）+ `SMinWithColor`（颜色插值的多项式平滑并集）+ `CalculateOcclusionAlpha_float/_half`（统一 SDF → outAlpha/outEmission） |
| `BuildingSeeThrough.shadergraph` | URP ShaderGraph | 半透明/双面；BaseColor = `_BaseMap × _BaseColor`；Alpha、Emission 接 Custom Function 输出；属性 `_TargetPos/_Radius/_Softness/_MinAlpha/_EdgeColor/_EdgeWidth` 作单目标回退默认值 |
| `BuildingSeeThroughLit.shadergraph` | URP ShaderGraph | Lit 高保真变体（Base/Normal/Mask/Smoothness），不参与透视逻辑 |
| `Building_Mat.mat` | 材质 | 已调参（Softness 0.04、Radius 1.87、EdgeWidth 0.09、绿色边缘、Queue 3000） |
| `Ground_Mat / Target_*_Mat` | 材质 | 演示用 URP/Lit 纯色材质 |
| `SeeThroughSetup.cs` | Editor 工具 | 菜单 `Tools/TGame/SeeThrough/Create Demo Scene and Assets`：建目录、校准材质、重建演示场景 |
| `SeeThroughDemo.unity` | 场景 | 三目标（橙/绿/紫）+ 宽墙 + OcclusionManager 演示 |

## 关键设计决策

1. **C# 零依赖**：运行时只依赖 UnityEngine.CoreModule，无 URP 程序集引用；
   与 Shader 的耦合完全通过全局着色器属性完成。
2. **SDF 而非模板/深度方案**：逐像素在片元阶段计算与"摄像机→目标"视线的距离，
   得到的是世界空间平滑过渡（连续软边），无像素化锯齿；配合 `smoothstep` 羽化。
3. **统一距离场消除内部接缝**：多目标不做逐目标 Alpha 叠加，而是先把所有目标的
   SDF 用 `SMinWithColor` 并成一个联合距离场再映射 Alpha/发光——重叠区只有一条外轮廓，
   从根本上消除分割线与杂边。
4. **全局数组批量上传 vs 逐材质 SetVector**：OcclusionManager 每帧一次 `SetGlobalVectorArray`
   驱动全部使用该 Shader 的材质，Draw Call 无关、目标数量只受数组长度限制（16）。
5. **回退机制**：`_TargetCount == 0` 时 Shader 使用材质自身的 `_TargetPos` 等属性，
   保证没有 OcclusionManager 时材质仍可用（旧单目标兼容路径）。

## 迁移记录

- 命名空间 `Beta.AlphaTest` → `TGame.SeeThrough`／`TGame.SeeThrough.Editor`；类名不变。
- 新增 `TGame.SeeThrough.asmdef`（Runtime）与 `TGame.SeeThrough.Editor.asmdef`
  （Editor，引用 Runtime + `Unity.RenderPipelines.Universal.Runtime`
  `GUID:15fc0a57446b3144c949da3e2b9737a9`）。
- 全部资产 `.meta` GUID 原样搬运，旧场景/预制体引用不受影响。
- 原 `Assets/Beta/AlphaTest` 目录已删除。
- 项目 TagManager 保留 `AlphaTestTarget` / `AlphaTestBuilding` 两个自定义 Layer（可选删除）。