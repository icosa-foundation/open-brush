#ifndef SDF_2D_INCLUDED
#define SDF_2D_INCLUDED

float2 sdf_op_2d_translate(float2 p, float2 translation)
{
    return p - translation;
}

void sdf2d_Circle_float(in float2 Point, in float Radius, out float Distance)
{
    Distance = length(Point) - Radius;
}

void sdf2d_Box_float(in float2 Point, in float2 Bounds, out float Distance)
{
    float2 d = abs(Point) - Bounds;
    Distance = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

void sdf2d_RoundedBox_float(in float2 Point, in float2 Bounds, in float4 CornerRoundness, out float Distance)
{
    CornerRoundness.xy = (Point.x > 0.0) ? CornerRoundness.xy : CornerRoundness.zw;
    CornerRoundness.x  = (Point.y > 0.0) ? CornerRoundness.x  : CornerRoundness.y;
    float2 q = abs(Point) - Bounds + CornerRoundness.x;
    Distance = min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - CornerRoundness.x;
}

#endif // SDF_2D_INCLUDED