// PS1VertexSnap.hlsl
// Подключается в Shader Graph через Custom Function Node (File mode).
//
// Как использовать в Shader Graph:
//   1. Создать HDRP/Lit Shader Graph
//   2. В Graph Settings: Target = HDRP, Material Type = Lit
//   3. Добавить Custom Function Node:
//      - Type: File
//      - Source: этот файл
//      - Function: PS1VertexSnap
//      - Inputs:  PositionOS (Vector3), LightProbeIntensity (Float),
//                 MinGrid (Float), MaxGrid (Float), Smoothness (Float)
//      - Outputs: SnappedPositionOS (Vector3)
//   4. Подключить:
//      - Position Node (Object Space) → PositionOS
//      - Ambient или Baked GI → LightProbeIntensity
//      - Output SnappedPositionOS → Position (Vertex stage)
//
// HDRP 17.4: Custom Function работает в Vertex stage через
// позицию в Object Space. Результат идёт в Position блок.

#ifndef PS1_VERTEX_SNAP_INCLUDED
#define PS1_VERTEX_SNAP_INCLUDED

/// <summary>
/// Снаппит вершину к сетке размером зависящим от освещённости.
/// Тёмные вершины → крупная сетка (сильная деградация).
/// Светлые вершины → мелкая сетка (точная геометрия).
/// </summary>
void PS1VertexSnap_float(
    float3 PositionOS,          // позиция вершины в Object Space
    float  LightProbeIntensity, // освещённость [0, 1] из light probe SH
    float  MinGrid,             // размер сетки при полном свете (например 0.01)
    float  MaxGrid,             // размер сетки в тени (например 0.15)
    float  Smoothness,          // плавность перехода [0, 1]
    out float3 SnappedPositionOS)
{
    // Luminance из light probe — определяет степень деградации
    float lum = saturate(LightProbeIntensity);

    // Размер сетки: чем темнее — тем грубее снаппинг
    // PERF: lerp с smoothstep для плавного перехода без ветвления
    float t    = 1.0 - smoothstep(Smoothness * 0.5, Smoothness, lum);
    float grid = lerp(MinGrid, MaxGrid, t);

    // Снаппинг: округление к ближайшему шагу сетки
    // floor(x / grid + 0.5) * grid — стандартный snap
    SnappedPositionOS = floor(PositionOS / grid + 0.5) * grid;
}

// Half-precision вариант для совместимости
void PS1VertexSnap_half(
    half3 PositionOS,
    half  LightProbeIntensity,
    half  MinGrid,
    half  MaxGrid,
    half  Smoothness,
    out half3 SnappedPositionOS)
{
    half lum   = saturate(LightProbeIntensity);
    half t     = 1.0h - smoothstep(Smoothness * 0.5h, Smoothness, lum);
    half grid  = lerp(MinGrid, MaxGrid, t);
    SnappedPositionOS = floor(PositionOS / grid + 0.5h) * grid;
}

#endif // PS1_VERTEX_SNAP_INCLUDED
