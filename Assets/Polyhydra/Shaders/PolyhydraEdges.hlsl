#ifndef POLYHYDRA_EDGES_INCLUDED
#define POLYHYDRA_EDGES_INCLUDED

float PolyhydraEdgeMask(float edgeUV, float3 barycentric, float distance, float thickness)
{
    // Match the mesh's polygon-edge and triangle-barycentric UV encoding.
    thickness = max(thickness, 0.00001);
    float3 triangleEdges = 1 - smoothstep(distance, distance + thickness, barycentric);
    float polygonEdge = 1 - smoothstep(1 - distance, 1 - distance - thickness, edgeUV);
    return min(polygonEdge, max(max(triangleEdges.x, triangleEdges.y), triangleEdges.z));
}

void PolyhydraEdgedSurface_float(float4 VertexColor, float4 Texture, float4 EdgeUV,
    float4 Barycentric, float3 PositionWS, float3 MeshNormalWS, float EdgeDistance,
    float EdgeThickness, float BumpStrength, float BumpOnly, out float3 Albedo, out float3 Normal)
{
    float edge = PolyhydraEdgeMask(EdgeUV.x, Barycentric.xyz, EdgeDistance, EdgeThickness);
    float luminance = dot(VertexColor.rgb, float3(0.2126, 0.7152, 0.0722));
    float3 edgeColor = luminance > 0.5 ? 0 : 1;
    Albedo = Texture.rgb * lerp(VertexColor.rgb, edgeColor, edge * (1 - BumpOnly));
    // Fragment derivatives replace the old geometry shader's per-triangle normal.
    float3 faceNormal = cross(ddy(PositionWS), ddx(PositionWS));
    float magnitude = dot(faceNormal, faceNormal);
    faceNormal = magnitude > 1e-20 ? faceNormal * rsqrt(magnitude) : normalize(MeshNormalWS);
    faceNormal *= dot(faceNormal, MeshNormalWS) < 0 ? -1 : 1;
    // Preserve the Smooth material's existing world-Z edge perturbation.
    faceNormal.z += edge * BumpStrength * BumpOnly;
    Normal = normalize(faceNormal);
}

#endif
