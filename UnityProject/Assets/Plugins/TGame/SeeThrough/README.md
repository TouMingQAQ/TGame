# TGame.SeeThrough — 多目标遮挡透视插件

> 由 `Assets/Beta/AlphaTest` 实验项目整理而成的正式插件模块。
> 运行时命名空间：`TGame.SeeThrough`；Editor 命名空间：`TGame.SeeThrough.Editor`。

## 功能概述

当目标物体（玩家、队友、NPC 等）被建筑/墙体遮挡时，将遮挡物在视线方向上"镂空透视"，
让目标始终可见，并在镂空边缘附加发光描边。支持**最多 16 个目标同时透视**，
多个目标的镂空区域采用 Metaball 式平滑并集融合，无接缝、无内部杂线。

## 目录结构

```
Assets/Plugins/TGame/SeeThrough/
├── Runtime/
│   ├── TGame.SeeThrough.asmdef        # 运行时程序集
│   ├── OcclusionManager.cs            # 全局管理器：收集目标 → 批量写入全局着色器数组
│   ├── OcclusionTarget.cs             # 目标组件：单个目标的镂空/羽化/发光参数 + 自动注册
│   ├── TargetOcclusionController.cs   # 旧版命名兼容类（继承 OcclusionTarget）
│   ├── Shader/
│   │   ├── BuildingSeeThrough.shadergraph      # 透视主 ShaderGraph（URP 半透明）
│   │   ├── BuildingSeeThroughLit.shadergraph   # 高保真 Lit 变体（不参与透视逻辑）
│   │   └── BuildingSeeThroughLogic.hlsl        # SDF 核心算法库（全局数组 + 自定义函数）
│   └── Material/
│       ├── Building_Mat.mat           # 建筑（透视 Shader，已调参）
│       ├── Ground_Mat.mat             # 地面
│       └── Target_*.mat               # 目标演示材质（橙/绿/紫）
├── Editor/
│   ├── TGame.SeeThrough.Editor.asmdef # Editor 程序集（引用 Runtime + URP Runtime）
│   └── SeeThroughSetup.cs             # 一键搭建演示场景
└── Demo/
    └── SeeThroughDemo.unity           # 三目标透视演示场景
```

## 使用方式

1. **一键演示**：菜单 `Tools → TGame/SeeThrough → Create Demo Scene and Assets`
   会生成/校准全部材质并重建 `Demo/SeeThroughDemo.unity`（摄像机 + 平行光 +
   OcclusionManager + 地面 + 遮挡宽墙 + 3 个自动移动的透视目标）。
2. **接入项目**：
   - 场景中放置一个空物体，挂 `OcclusionManager`（一个即可，全局唯一）。
   - 在这些需要被透视的物体（角色/NPC）上挂 `OcclusionTarget`，逐目标调节：
     `cutoutRadius` 镂空半径、`softness` 边缘羽化、`minAlpha` 镂空中心最小 Alpha
     （0 = 全透，0.2 = 半透明幽灵）、`edgeColor` 边缘发光颜色、`edgeWidth` 发光宽度。
   - 给建筑/墙体材质使用 `BuildingSeeThrough.shadergraph`（或复制一份）。
3. `OcclusionTarget` 上的"运动演示"参数组仅用于演示，接入正式玩法时可全部关闭/删除。

## 全局着色器属性（由 OcclusionManager 每帧更新）

| 属性 | 类型 | 含义 |
| --- | --- | --- |
| `_TargetPositions` | `float4[16]` | xyz = 世界坐标，w = 镂空半径 |
| `_TargetParams` | `float4[16]` | x = 半径, y = softness, z = minAlpha, w = edgeWidth |
| `_TargetColors` | `float4[16]` | RGBA 边缘发光颜色 |
| `_TargetCount` | `int` | 当前有效目标数量 |
| `_TargetPos` | `float4` | 单目标回退位（首个目标），兼容旧版单目标 Shader |

## 技术说明（BuildingSeeThroughLogic.hlsl）

- **视线锥体有向距离场**：逐像素计算"世界坐标 → 摄像机→目标视线"的最短距离，
  仅在像素位于摄像机与目标之间时生效（目标在建筑前方的场景自动排除）。
- **统一 SDF + 多项式平滑并集（SMinWithColor）**：多目标的距离场做 Metaball 融合，
  同时插值边缘颜色，彻底消除重叠区域内部的分割线与接缝。
- **Alpha 映射**：`alpha = lerp(minAlpha, 1, smoothstep(-softness, 0, d))`，
  由联合距离界定的羽化带过渡；发光边缘只在联合形状的最外层轮廓计算，内部不产生交叉线。
- 半透明渲染：ShaderGraph 面类型 Transparent、双面渲染（RenderFace = Both）。

## 兼容与迁移

- 由 `Assets/Beta/AlphaTest` 整体迁移而来；所有 `.meta` GUID 原样保留，
  旧场景/预制体对 `OcclusionTarget` / `OcclusionManager` 的引用仍然有效。
- 类名未变，仅命名空间从 `Beta.AlphaTest` 改为 `TGame.SeeThrough`。
- `TargetOcclusionController` 保留为旧命名兼容别名。
- 项目层 `AlphaTestTarget`(3) / `AlphaTestBuilding`(6) 为演示预留，可保留或删除。