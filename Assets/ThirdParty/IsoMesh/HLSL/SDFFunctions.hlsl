#ifndef SDF_FUNCTIONS_INCLUDED
#define SDF_FUNCTIONS_INCLUDED

#include "Common.hlsl"
#include "./Compute_IsoSurfaceExtraction_Structs.hlsl"

float3 sdf_op_translate(float3 p, float3 translation)
{
    return p - translation;
}

float3 sdf_op_rotate(float3 p, float3 axis, float degrees)
{
    return mul(AngleAxis3x3(degrees * DEGREES_TO_RADIANS, axis), p);
}

float3 sdf_op_rotate(float3 p, float3 eulerAngles)
{
    return mul(p, Euler3x3(eulerAngles));
}

float sdf_cylinder(float3 p, float h, float r)
{
    float2 d = abs(float2(length(p.xz), p.y)) - float2(h, r);
    return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
}

float sdf_box(float3 p, float3 b)
{
    float3 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float sdf_roundedBox(float3 p, float3 b, float r)
{
    float3 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0) - r;
}

float3 sdf_box_normal(float3 p, float3 b)
{
    float3 d = abs(p) - b;
    float3 s = sign(p);
    float g = max(d.x, max(d.y, d.z));
    return s * ((g > 0.0) ? normalize(max(d, 0.0)) : step(d.yzx, d.xyz) * step(d.zxy, d.xyz));
}

float sdf_boxFrame(float3 p, float3 b, float e)
{
    p = abs(p) - b;
    float3 q = abs(p + e) - e;
    return min(min(
      length(max(float3(p.x, q.y, q.z), 0.0)) + min(max(p.x, max(q.y, q.z)), 0.0),
      length(max(float3(q.x, p.y, q.z), 0.0)) + min(max(q.x, max(p.y, q.z)), 0.0)),
      length(max(float3(q.x, q.y, p.z), 0.0)) + min(max(q.x, max(q.y, p.z)), 0.0));
}


float sdf_torus(float3 p, float2 t)
{
    float2 q = float2(length(p.xz) - t.x, p.y);
    return length(q) - t.y;
}

float sdf_link(float3 p, float le, float r1, float r2)
{
    float3 q = float3(p.x, max(abs(p.y) - le, 0.0), p.z);
    return length(float2(length(q.xy) - r1, q.z)) - r2;
}

float2 sdf_uv_planarX(float3 p, float3 b)
{
    float3 q = abs(p) - b;
    float dist = length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
    
    float normalizedX = remap(-b.z, b.z, 0, 1, p.z);
    float normalizedY = remap(-b.y, b.y, 0, 1, p.y);
    return float2(normalizedX, normalizedY);
}

float2 sdf_uv_planarY(float3 p, float3 b)
{
    float3 q = abs(p) - b;
    float dist = length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
    
    float normalizedX = remap(-b.x, b.x, 0, 1, p.x);
    float normalizedY = remap(-b.z, b.z, 0, 1, p.z);
    return float2(normalizedX, normalizedY);
}

float2 sdf_uv_planarZ(float3 p, float3 b)
{
    float3 q = abs(p) - b;
    float dist = length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
    
    float normalizedX = remap(-b.x, b.x, 0, 1, p.x);
    float normalizedY = remap(-b.y, b.y, 0, 1, p.y);
    return float2(normalizedX, normalizedY);
}

float2 sdf_uv_triplanar(float3 p, float3 b, float3 normal)
{
    float2 yzPlane = sdf_uv_planarX(p, b);
    float2 xzPlane = sdf_uv_planarY(p, b);
    float2 xyPlane = sdf_uv_planarZ(p, b);

    return yzPlane * normal.x + xzPlane * normal.y + xyPlane * normal.z;
}

float sdf_plane(float3 p, float3 n, float h)
{
    // n must be normalized
    return dot(p, n) + h;
}

float sdf_roundedCone(float3 p, float r1, float r2, float h)
{
    h = max(h, 0.00001);
    float2 q = float2(length(p.xz), p.y);
    
    float b = (r1 - r2) / h;
    float a = sqrt(1.0 - b * b);
    float k = dot(q, float2(-b, a));
    
    if (k < 0.0)
        return length(q) - r1;
    
    if (k > a * h)
        return length(q - float2(0.0, h)) - r2;
        
    return dot(q, float2(a, b)) - r1;
}

float sdf_sphere(float3 p, float radius)
{
    return length(p) - radius;
}

float2 sdf_uv_sphere(float3 p, float radius)
{
    float len = length(p);
    float distance = len - radius;
    float3 normalized = p / len;
    float verticalness = dot(normalized, float3(0, 1, 0));
    float normalizedHeight = (verticalness + 1.0) * 0.5;
    
    float rotatiness = (atan2(normalized.z, normalized.x) + PI) / (PI * 2);
    
    return float2(rotatiness, normalizedHeight);
}

// polynomial smooth min (k = 0.1);
float sdf_op_smin(float a, float b, float k)
{
    float h = max(k - abs(a - b), 0.0) / k;
    return min(a, b) - h * h * k * (1.0 / 4.0);
}

//// smooth min but also smoothly combines associated float3s (e.g. colours)
//float sdf_op_smin_colour(float d1, float d2, float3 v1, float3 v2, float k, float vSmoothing, out float3 vResult)
//{
//    float h = saturate(0.5 + 0.5 * (d2 - d1) / k);
//    float vH = saturate(0.5 + 0.5 * (d2 - d1) / vSmoothing);
    
//    vResult = lerp(v2, v1, vH);
//    return lerp(d2, d1, h) - k * h * (1.0 - h);
//}

// smooth min but also smoothly combines associated material
float sdf_op_smin_material(float d1, float d2, SDFMaterialGPU v1, SDFMaterialGPU v2, float k, float vSmoothing, out SDFMaterialGPU vResult)
{
    float h = saturate(0.5 + 0.5 * (d2 - d1) / k);
    float vH = saturate(0.5 + 0.5 * (d2 - d1) / vSmoothing);
    
    vResult = lerpMaterial(v2, v1, vH);
    return lerp(d2, d1, h) - k * h * (1.0 - h);
}

float sdf_op_smoothIntersection(float d1, float d2, float k)
{
    float h = saturate(0.5 - 0.5 * (d2 - d1) / k);
    return lerp(d2, d1, h) + k * h * (1.0 - h);
}

//// smooth intersection but also smoothly intersects associated float3s (e.g. colours)
//float sdf_op_smoothIntersection_colour(float d1, float d2, float3 v1, float3 v2, float k, float vSmoothing, out float3 vResult)
//{
//    float h = saturate(0.5 - 0.5 * (d2 - d1) / k);
//    float vH = saturate(0.5 - 0.5 * (d2 - d1) / vSmoothing);
    
//    vResult = lerp(v2, v1, vH);
//    return lerp(d2, d1, h) + k * h * (1.0 - h);
//}


// smooth intersection but also smoothly intersects associated materials
float sdf_op_smoothIntersection_material(float d1, float d2, SDFMaterialGPU v1, SDFMaterialGPU v2, float k, float vSmoothing, out SDFMaterialGPU vResult)
{
    //d2 = -abs(d2);
    //vSmoothing = -max(0.00001, vSmoothing);
    
    float h = saturate(0.5 - 0.5 * (d2 - d1) / k);
    float vH = saturate(0.5 - 0.5 * (d1 - d2) / vSmoothing);
    
    vResult = lerpMaterial(v2, v1, vH);
    return lerp(d2, d1, h) + k * h * (1.0 - h);
}


float sdf_op_smoothSubtraction(float d1, float d2, float k)
{
    float h = saturate(0.5 - 0.5 * (d2 + d1) / k);
    return lerp(d2, -d1, h) + k * h * (1.0 - h);
}

// smooth subtraction but also smoothly subtracts associated float3s (e.g. colours)
float sdf_op_smoothSubtraction_material(float d1, float d2, SDFMaterialGPU v1, SDFMaterialGPU v2, float k, float vSmoothing, out SDFMaterialGPU vResult)
{
    d1 = abs(d1);
    float h = saturate(0.5 + 0.5 * (d2 - d1) / k);
    float vH = saturate(0.5 + 0.5 * (d2 - d1) / vSmoothing);
    
    vResult = lerpMaterial(v2, v1, vH);
    return lerp(d2, d1, h) - k * h * (1.0 - h);
}

float3 sdf_op_twist(float3 p, float k)
{
    float c = cos(k * p.y);
    float s = sin(k * p.y);
    float2x2 m = float2x2(c, -s, s, c);
    return float3(mul(p.xz, m), p.y);
}

float3 sdf_op_elongate(float3 p, float3 h, float4x4 transform)
{
    float3 translation = float3(transform._m03, transform._m13, transform._m23);
    p = mul(transform, float4(p, 0.0)).xyz;
    p = p - clamp(p + translation, -h, h);
    return mul(float4(p, 0.0), transform).xyz;
}

float3 sdf_op_round(float3 p, float rad)
{
    return p - rad;
}

float sdf_op_onion(float dist, float thickness, int count)
{
    //count = clamp(count, 0, 16);
    
    //[fastopt]
    //for (int iter = 0; iter < count; iter++)
    //{
    //    dist = abs(dist) - thickness;
    //    thickness /= 2;
    //}
    
    return dist;
    
}

float3 sdf_op_bendX(float3 p, float angle)
{
    angle *= DEGREES_TO_RADIANS;
    
    float c = cos(angle * p.y);
    float s = sin(angle * p.y);
    float2x2 m = float2x2(c, -s, s, c);
    return float3(p.x, mul(m, p.yz));
}

float3 sdf_op_bendY(float3 p, float angle)
{
    angle *= DEGREES_TO_RADIANS;
    
    float c = cos(angle * p.x);
    float s = sin(angle * p.x);
    float2x2 m = float2x2(c, -s, s, c);
    
    float2 xz = mul(m, p.xz);
    return float3(xz.x, p.y, xz.y);
}

float3 sdf_op_bendZ(float3 p, float angle)
{
    angle *= DEGREES_TO_RADIANS;
    
    float c = cos(angle * p.y);
    float s = sin(angle * p.y);
    float2x2 m = float2x2(c, -s, s, c);
    return float3(mul(m, p.xy), p.z);
}

#endif // SDF_FUNCTIONS_INCLUDED