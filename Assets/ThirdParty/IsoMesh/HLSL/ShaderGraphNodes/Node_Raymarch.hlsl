#ifndef SHADERGRAPH_SDF_FUNCTIONS_INCLUDED
#define SHADERGRAPH_SDF_FUNCTIONS_INCLUDED

struct Data
{
    float Time;
    float3 RayOrigin;
    float3 RayDirection;
    float3 ObjectPosition;
    float Smoothing;
};

#include "../MapSignedDistanceField.hlsl"

void RayMarch_float
(
in float Time,
in float3 RayOrigin,
in float3 RayDirection,
in float3 ObjectPosition,
in float Smoothing,
in float SubsurfaceFalloff,
in float SubsurfaceMaxDistance,
in float SceneDistance,

out int Iterations, 
out float Hit,
out float HitDistance, 
out float3 HitPoint,
out float3 HitNormal,
out float Thickness,
out float2 UV)
{
    Data data;
    data.Time = Time;
    data.RayOrigin = RayOrigin;
    data.RayDirection = RayDirection;
    data.ObjectPosition = ObjectPosition;
    data.Smoothing = Smoothing;
    
    float h = 0.0;
    HitDistance = 0.0;
    Iterations = 0;
    
    float3 closestPoint = float3(0, 0, 0);
    float closestDist = 10000000.0;
    
    Thickness = 0.0;
    float maxDistance = min(SceneDistance, MAX_DISTANCE);
    // March the distance field until a surface is hit.
    for (Iterations = 0; Iterations < MAX_ITERATIONS; Iterations++)
    {
        float3 p = RayOrigin + RayDirection * HitDistance;
        
        h = Map(p);
        HitDistance += h * STEP_SIZE_SCALAR;
        
        if (h < closestDist)
        {
            closestDist = h;
            closestPoint = p;
        }
        
        if (h < SURFACE_DISTANCE || HitDistance > maxDistance)
            break;
    }
    
    if (h < SURFACE_DISTANCE)
    {
        HitPoint = RayOrigin + RayDirection * HitDistance;
        UV = MapUV(HitPoint/*, data*/)/*.UV*/;
        //UV = float2(1, 0);
        HitNormal = MapNormal(HitPoint, -1.0);
        Hit = 1.0;
        
#ifdef SUBSURFACE_SCATTERING_ON
        Thickness = MapThickness(HitPoint, HitNormal, SubsurfaceMaxDistance, SubsurfaceFalloff);
#endif
    }
    else
    {
        UV = MapUV(closestPoint);
        HitPoint = float3(0, 0, 0);
        HitNormal = float3(0, 0, 0);
        Hit = 0.0;
    }
}

#endif // SHADERGRAPH_SDF_FUNCTIONS_INCLUDED