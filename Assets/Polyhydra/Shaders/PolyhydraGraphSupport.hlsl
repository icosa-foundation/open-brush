#ifndef POLYHYDRA_GRAPH_SUPPORT_INCLUDED
#define POLYHYDRA_GRAPH_SUPPORT_INCLUDED

#include "Packages/com.icosa.open-brush-unity-tools/Runtime/Shaders/Include/MobileSelection.cginc"
// The legacy ODS include uses PI as a local variable; URP defines PI as a macro.
#undef PI
#include "Packages/com.icosa.open-brush-unity-tools/Runtime/Shaders/Include/Ods.cginc"
#define PI 3.14159265358979323846

void PolyhydraSelection_float(out float3 Emission)
{
    Emission = 0;
#if defined(SELECTION_ON) || defined(HIGHLIGHT_ON)
    Emission = AddSelectColor(0).rgb;
#endif
}

void PolyhydraPosition_float(float3 Position, out float3 Result)
{
    float4 vertex = float4(Position, 1);
    PrepForOds(vertex);
    Result = vertex.xyz;
}

#endif
