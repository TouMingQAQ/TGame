#ifndef BUILDING_SEE_THROUGH_INCLUDED
#define BUILDING_SEE_THROUGH_INCLUDED

// 最大支持同时透视的目标数量
#define MAX_OCCLUSION_TARGETS 16

// 全局目标数组 (由 OcclusionManager 在 C# 中统一更新)
float4 _TargetPositions[MAX_OCCLUSION_TARGETS];
float4 _TargetParams[MAX_OCCLUSION_TARGETS]; // x: radius, y: softness, z: minAlpha, w: edgeWidth
float4 _TargetColors[MAX_OCCLUSION_TARGETS]; // x,y,z,w: RGBA 边缘发光颜色
int _TargetCount;

/// <summary>
/// 多目标平滑并集 (Polynomial Smooth Minimum)
/// 消除几何相交处的硬折线与接缝，实现水滴状 (Metaball) 完美融合。
/// </summary>
float SMinWithColor(float d1, float d2, float4 c1, float4 c2, float k, out float4 outColor)
{
    float h = saturate(0.5 + 0.5 * (d2 - d1) / max(0.0001, k));
    outColor = lerp(c2, c1, h);
    return lerp(d2, d1, h) - k * h * (1.0 - h);
}

/// <summary>
/// 计算建筑物当前像素到单个目标视线锥体的有向距离 (Signed Distance)
/// 返回值 < 0 表示在镂空孔内部，> 0 表示在镂空孔外部
/// </summary>
float GetTargetSignedDistance(
    float3 worldPos,
    float3 targetPos,
    float3 cameraPos,
    float radius)
{
    float distFragToCam = distance(worldPos, cameraPos);
    float distTargetToCam = distance(targetPos, cameraPos);
    
    // 深度判定：如果建筑物比目标离摄像机更远（目标在建筑前面），则该目标不遮挡建筑，返回极大值
    if (distFragToCam >= distTargetToCam)
    {
        return 1000.0;
    }
    
    // 视线射线：从摄像机连向目标物体
    float3 rayDir = targetPos - cameraPos;
    float rayLength = length(rayDir);
    if (rayLength <= 0.0001)
    {
        return 1000.0;
    }
    rayDir = rayDir / rayLength;
    
    // 投影像素点到视线上
    float3 toFrag = worldPos - cameraPos;
    float projDist = dot(toFrag, rayDir);
    
    // 仅当像素位于摄像机与目标之间时产生遮挡
    if (projDist < 0.0 || projDist > distTargetToCam)
    {
        return 1000.0;
    }
    
    float3 closestPoint = cameraPos + rayDir * projDist;
    float distToRay = distance(worldPos, closestPoint);
    
    // 有向距离：负值在镂空区域内，正值在区域外
    return distToRay - radius;
}

/// <summary>
/// 多目标透视主函数（ShaderGraph CustomFunctionNode 入口）
/// 基于统一符号距离场 (Unified SDF) 计算，彻底消除重叠区域内部的分割线与杂边。
/// </summary>
void CalculateOcclusionAlpha_float(
    float3 worldPos,
    float3 defaultTargetPos,
    float3 cameraPos,
    float defaultRadius,
    float defaultSoftness,
    float defaultMinAlpha,
    float defaultEdgeWidth,
    float4 defaultEdgeColor,
    out float outAlpha,
    out float4 outEmission)
{
    float combinedDist = 1000.0;
    float4 combinedColor = defaultEdgeColor;
    float globalSoftness = defaultSoftness;
    float globalMinAlpha = defaultMinAlpha;
    float globalEdgeWidth = defaultEdgeWidth;
    
    int activeCount = _TargetCount > 0 ? min(_TargetCount, MAX_OCCLUSION_TARGETS) : 1;
    
    // 融合平滑度 (Metaball 融合系数)
    const float blendK = 0.45;

    for (int i = 0; i < activeCount; i++)
    {
        float3 tPos = _TargetCount > 0 ? _TargetPositions[i].xyz : defaultTargetPos;
        float4 tParams = _TargetCount > 0 ? _TargetParams[i] : float4(defaultRadius, defaultSoftness, defaultMinAlpha, defaultEdgeWidth);
        float tRadius = tParams.x > 0.0 ? tParams.x : defaultRadius;
        float tSoftness = tParams.y > 0.0 ? tParams.y : defaultSoftness;
        float tMinAlpha = tParams.z >= 0.0 ? tParams.z : defaultMinAlpha;
        float tEdgeWidth = tParams.w > 0.0 ? tParams.w : defaultEdgeWidth;
        float4 tColor = (_TargetCount > 0 && dot(_TargetColors[i], _TargetColors[i]) > 0.001) ? _TargetColors[i] : defaultEdgeColor;
        
        float d = GetTargetSignedDistance(worldPos, tPos, cameraPos, tRadius);
        
        if (i == 0)
        {
            combinedDist = d;
            combinedColor = tColor;
            globalSoftness = tSoftness;
            globalMinAlpha = tMinAlpha;
            globalEdgeWidth = tEdgeWidth;
        }
        else
        {
            float4 blendedColor;
            combinedDist = SMinWithColor(combinedDist, d, combinedColor, tColor, blendK, blendedColor);
            combinedColor = blendedColor;
            globalSoftness = min(globalSoftness, tSoftness);
            globalMinAlpha = min(globalMinAlpha, tMinAlpha);
            globalEdgeWidth = max(globalEdgeWidth, tEdgeWidth);
        }
    }
    
    // 如果没有任何目标产生遮挡，完全不透明
    if (combinedDist >= 500.0)
    {
        outAlpha = 1.0;
        outEmission = float4(0.0, 0.0, 0.0, 0.0);
        return;
    }
    
    // 1. 在统一的联合距离场上计算 Alpha：
    // 当 combinedDist <= -globalSoftness 时，Alpha 达到最小值（完全透明 0）
    // 当 combinedDist >= 0 时，Alpha 恢复为 1.0（完全不透明）
    float holeFactor = smoothstep(-max(0.001, globalSoftness), 0.0, combinedDist);
    outAlpha = lerp(globalMinAlpha, 1.0, holeFactor);
    
    // 2. 在联合形状的【最外层轮廓】处统一计算发光边缘：
    // 内部区域 combinedDist << 0 不会产生任何内部交叉线，重叠区域完美平滑融合！
    float rimFactor = smoothstep(-max(0.001, globalSoftness), 0.0, combinedDist) * (1.0 - smoothstep(0.0, max(0.001, globalEdgeWidth), combinedDist));
    outEmission = combinedColor * rimFactor;
}

void CalculateOcclusionAlpha_half(
    half3 worldPos,
    half3 defaultTargetPos,
    half3 cameraPos,
    half defaultRadius,
    half defaultSoftness,
    half defaultMinAlpha,
    half defaultEdgeWidth,
    half4 defaultEdgeColor,
    out half outAlpha,
    out half4 outEmission)
{
    float a;
    float4 e;
    CalculateOcclusionAlpha_float(
        (float3)worldPos,
        (float3)defaultTargetPos,
        (float3)cameraPos,
        (float)defaultRadius,
        (float)defaultSoftness,
        (float)defaultMinAlpha,
        (float)defaultEdgeWidth,
        (float4)defaultEdgeColor,
        a,
        e
    );
    outAlpha = (half)a;
    outEmission = (half4)e;
}

#endif
