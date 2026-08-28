# AlphaTest — 深度遮挡透视效果实现记录（See-Through Occlusion）

## 目标

两个物体：建筑物体（Building）与目标物体（Target）。
当目标的渲染深度（view-Z，越远越负）大于建筑时，建筑在对应像素 alpha = 0，透出目标。

## 方案（最终定稿：双相机）

相机深度纹理含建筑本身，无法判断"背后有什么"。
**第一版方案（RG Renderer Feature）在 D3D12/URP17 下把 `DrawRendererList` 绘制到自定义
`CreateTexture` 纹理全部失败（清屏/格式/事件/全局桥接各种变体都试过）**，因此改用标准双相机：

```
AlphaTestOcclusionCam (Camera + OcclusionCameraProbe, [ExecuteAlways])
  ├ CullingMask = AlphaTestTarget；Clear = 纯色(0,0,0,0)
  ├ SetReplacementShader(OcclusionDepthWrite.shader, "RenderType")
  │    → 只把目标物体渲染成 View 空间 Z（负值=更远）到离屏 RT
  └ 每帧 buildingMaterial.SetTexture("_OcclusionDepthTex", rt)

OcclusionBuilding.shadergraph（URP Lit, Transparent）
  Alpha = saturate((targetZ - ownZ) / FadeWidth)   // targetZ 更负 → alpha=0
```

## 关键决策

| 问题 | 决策 |
|---|---|
| 深度语义 | View 空间 Z（负数，更远更负）；空像素=0 → alpha=1 保持不透明 |
| alpha 公式 | `Alpha = saturate((targetZ - ownZ) / FadeWidth)` 直接接 Alpha 块（**不要 OneMinus**） |
| 双相机 | 第二相机 + SetReplacementShader + 材质属性赋值，**不经过 RenderGraph 全局纹理** |
| Edit 模式 | `[ExecuteAlways]` + 非 Play 时手动 `Camera.Render()` 刷新 RT |
| ShaderGraph 材质绑定 | 必须 `{fileID: -6465566751694194690, type: 3}`；`{fileID:0,type:0}` 全品红 |
| 图层 | AlphaTestTarget（TagManager 第 3 层） |
| 预留 | `OcclusionDepthRendererFeature.cs`（RG 方案存档，未挂载） |

## 测试场景（Scenes/AlphaTest.unity）

- 相机 (0,2.2,-9) 朝向 +Z；建筑 Box (0,1.6,0) scale(6,3.2,2) + 屋顶；红色目标 ×2（AlphaTestTarget 图层）。
- 场景含 `AlphaTestOcclusionCam`（Camera + 探针，材质已绑定）。Unity 已重序列化场景（自带 fileID）。

## 测试步骤

1. Tools → AlphaTest → Setup All (Layer + Occlusion Camera)
2. 打开 Scenes/AlphaTest.unity → Play：红球在建筑后方时建筑像素透明（透视可见）、前方时正常遮挡
3. 调 `_FadeWidth`：设负值反向验证链路（墙透明 + 目标处白斑 = 深度纹理正常）

## 状态

- [x] 双相机探针 OcclusionCameraProbe 编译通过（ExecuteAlways + Play 双路径）
- [x] 两张 ShaderGraph 导入生成 Shader；材质绑定正确（负 fileID）
- [x] 建筑 Shader 暴露 `_OcclusionDepthTex`（GeneratePropertyBlock=true）
- [x] Setup 菜单：图层 + 透视线相机（幂等）
- [x] 场景 AlphaTest.unity：建筑层=0、含透视线相机、PC_Renderer 已移除旧 Feature、TransparentMask 已还原
- [~] 双相机链路需在 Play 模式最终目视确认（REST 离屏截图无法触发 Edit 循环 Update，故未能在会话内判定）