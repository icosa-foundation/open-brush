// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.
#ifndef OPEN_BRUSH_SHAPE_MATERIAL_INCLUDED
#define OPEN_BRUSH_SHAPE_MATERIAL_INCLUDED
#include "Packages/com.icosa.open-brush-unity-tools/Runtime/Shaders/Include/MobileSelection.cginc"
#undef PI
#include "Packages/com.icosa.open-brush-unity-tools/Runtime/Shaders/Include/Ods.cginc"
#define PI 3.14159265358979323846
void ShapeSelection_float(out float3 Emission)
{
    Emission = 0;
#if defined(SELECTION_ON) || defined(HIGHLIGHT_ON)
    Emission = AddSelectColor(0).rgb;
#endif
}
void ShapeColor_float(float4 Tint, float4 Texture, float4 VertexColor, float Saturation,
    out float3 Albedo, out float3 Specular, out float Alpha)
{
    float gray = dot(VertexColor.rgb, float3(0.299, 0.587, 0.114));
    Albedo = Texture.rgb * Tint.rgb * lerp(gray.xxx, VertexColor.rgb, Saturation);
    Specular = VertexColor.rgb;
    Alpha = Texture.a * Tint.a;
}
void ShapeUnlitColor_float(float4 Tint, float4 Texture, float4 VertexColor, float Saturation,
    out float3 Albedo, out float3 Specular, out float Alpha)
{
    ShapeColor_float(Tint, Texture, VertexColor, Saturation, Albedo, Specular, Alpha);
    float3 selection;
    ShapeSelection_float(selection);
    Albedo += selection;
}
void ShapePosition_float(float3 Position, out float3 Result)
{
    float4 vertex = float4(Position, 1);
    PrepForOds(vertex);
    Result = vertex.xyz;
}
#endif
