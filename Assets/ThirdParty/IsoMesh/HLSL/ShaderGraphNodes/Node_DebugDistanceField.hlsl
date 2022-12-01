#ifndef SDF_DEBUG
#define SDF_DEBUG

#include "../MapSignedDistanceField.hlsl"

void DebugMap_float(in float3 Position, out float Distance)
{
    Distance = Map(Position);
}

#endif // SDF_DEBUG