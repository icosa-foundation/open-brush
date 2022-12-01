#ifndef COMMON_STUFF_INCLUDED
#define COMMON_STUFF_INCLUDED

////////////////////////////////////////////// GENERAL MATHS STUFF //////////////////////////////////////////////

#ifndef UNITY_COMMON_INCLUDED
#define PI 3.1415926
#endif

#define DEGREES_TO_RADIANS 0.0174533

#define UP float3(0, 1, 0)
#define RIGHT float3(1, 0, 0)
#define FORWARD float3(0, 0, 1)

#define IDENTITY_MATRIX float4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1)

// returns square magnitude
float dot2(float3 v)
{
    return dot(v, v);
}

float invLerp(float from, float to, float value)
{
    return (value - from) / (to - from);
}

float4 invLerp(float4 from, float4 to, float4 value)
{
    return (value - from) / (to - from);
}

float remap(float origFrom, float origTo, float targetFrom, float targetTo, float value)
{
    float rel = invLerp(origFrom, origTo, value);
    return lerp(targetFrom, targetTo, rel);
}

float4 remap(float4 origFrom, float4 origTo, float4 targetFrom, float4 targetTo, float4 value)
{
    float4 rel = invLerp(origFrom, origTo, value);
    return lerp(targetFrom, targetTo, rel);
}

// Interpolate values between a cube of points.
float4 TrilinearInterpolate(float3 fracs, float4 a, float4 b, float4 c, float4 d, float4 e, float4 f, float4 g, float4 h)
{
    /*     g-------h
    *     /|      /|
    *    / |     / |
    *   c--|----d  |
    *   |  e----|--f
    *   | /     | /
    *   a-------b
    */

    // x axis
    float4 aToB = lerp(a, b, fracs.x);
    float4 cToD = lerp(c, d, fracs.x);
    float4 eToF = lerp(e, f, fracs.x);
    float4 gToH = lerp(g, h, fracs.x);

    // y axis
    float4 y1 = lerp(aToB, cToD, fracs.y);
    float4 y2 = lerp(eToF, gToH, fracs.y);

    // finally, z axis
    return lerp(y1, y2, fracs.z);
}


// Interpolate values between a cube of points.
float3 TrilinearInterpolate(float3 fracs, float3 a, float3 b, float3 c, float3 d, float3 e, float3 f, float3 g, float3 h)
{
    /*     g-------h
    *     /|      /|
    *    / |     / |
    *   c--|----d  |
    *   |  e----|--f
    *   | /     | /
    *   a-------b
    */

    // x axis
    float3 aToB = lerp(a, b, fracs.x);
    float3 cToD = lerp(c, d, fracs.x);
    float3 eToF = lerp(e, f, fracs.x);
    float3 gToH = lerp(g, h, fracs.x);

    // y axis
    float3 y1 = lerp(aToB, cToD, fracs.y);
    float3 y2 = lerp(eToF, gToH, fracs.y);

    // finally, z axis
    return lerp(y1, y2, fracs.z);
}


// Interpolate values between a cube of points.
float2 TrilinearInterpolate(float3 fracs, float2 a, float2 b, float2 c, float2 d, float2 e, float2 f, float2 g, float2 h)
{
    /*     g-------h
    *     /|      /|
    *    / |     / |
    *   c--|----d  |
    *   |  e----|--f
    *   | /     | /
    *   a-------b
    */

    // x axis
    float2 aToB = lerp(a, b, fracs.x);
    float2 cToD = lerp(c, d, fracs.x);
    float2 eToF = lerp(e, f, fracs.x);
    float2 gToH = lerp(g, h, fracs.x);

    // y axis
    float2 y1 = lerp(aToB, cToD, fracs.y);
    float2 y2 = lerp(eToF, gToH, fracs.y);

    // finally, z axis
    return lerp(y1, y2, fracs.z);
}

// Interpolate values between a cube of points.
float TrilinearInterpolate(float3 fracs, float a, float b, float c, float d, float e, float f, float g, float h)
{
    /*     g-------h
    *     /|      /|
    *    / |     / |
    *   c--|----d  |
    *   |  e----|--f
    *   | /     | /
    *   a-------b
    */

    // x axis
    float aToB = lerp(a, b, fracs.x);
    float cToD = lerp(c, d, fracs.x);
    float eToF = lerp(e, f, fracs.x);
    float gToH = lerp(g, h, fracs.x);

    // y axis
    float y1 = lerp(aToB, cToD, fracs.y);
    float y2 = lerp(eToF, gToH, fracs.y);

    // finally, z axis
    return lerp(y1, y2, fracs.z);
}


 //https://github.com/keijiro/ShurikenPlus/blob/master/Assets/ShurikenPlus/Shaders/Common.hlsl
float3x3 Euler3x3(float3 v)
{ //
    v *= DEGREES_TO_RADIANS;
    
    float sx, cx;
    float sy, cy;
    float sz, cz;

    sincos(v.x, sx, cx);
    sincos(v.y, sy, cy);
    sincos(v.z, sz, cz);

    float3 row1 = float3(sx * sy * sz + cy * cz, sx * sy * cz - cy * sz, cx * sy);
    float3 row3 = float3(sx * cy * sz - sy * cz, sx * cy * cz + sy * sz, cx * cy);
    float3 row2 = float3(cx * sz, cx * cz, -sx);

    return float3x3(row1, row2, row3);
}

float3x3 AngleAxis3x3(float angle, float3 axis)
{
    float c, s;
    sincos(angle, s, c);

    float t = 1 - c;
    float x = axis.x;
    float y = axis.y;
    float z = axis.z;

    return float3x3(
			t * x * x + c, t * x * y - s * z, t * x * z + s * y,
			t * x * y + s * z, t * y * y + c, t * y * z - s * x,
			t * x * z - s * y, t * y * z + s * x, t * z * z + c
			);
}

float4x4 BasisMatrix3x3(float3 right, float3 up, float3 forward)//
{
    float3 xaxis = right;
    float3 yaxis = up;
    float3 zaxis = forward;
    return float4x4(
		xaxis.x, yaxis.x, zaxis.x, 0,
		xaxis.y, yaxis.y, zaxis.y, 0,
		xaxis.z, yaxis.z, zaxis.z, 0,
		0, 0, 0, 1
	);
}

float4x4 LookAtMatrix4x4(float3 forward, float3 up)
{
    float3 xaxis = normalize(cross(forward, up));
    float3 yaxis = up;
    float3 zaxis = forward;
    return BasisMatrix3x3(xaxis, yaxis, zaxis);
}

float hash(float n)//->0:1
{
    return frac(sin(n) * 3538.5453);
}

float3 RandomSphereDirection(float2 rnd)
{
    float s = rnd.x * PI * 2.;
    float t = rnd.y * 2. - 1.;
    return float3(sin(s), cos(s), t) / sqrt(1.0 + t * t); //
}

float3 RandomHemisphereDirection(float3 dir, float i)
{
    float3 v = RandomSphereDirection(float2(hash(i + 1.), hash(i + 2.)));
    return v * sign(dot(v, dir));
}


// shove 2 floating point values into one float at half precision
// for values between 0 and 1
// https://stackoverflow.com/questions/17638800/storing-two-float-values-in-a-single-float-variable
float Pack2In1(float2 input, float precision = 4096)
{
    input = saturate(input);
    
    float2 output = input;
    output.x = floor(output.x * (precision - 1.0));
    output.y = floor(output.y * (precision - 1.0));
        
    return (output.x * precision) + output.y;
}

// restore 2 floating point values that were shoved into one float at half precision
// for values between 0 and 1
// https://stackoverflow.com/questions/17638800/storing-two-float-values-in-a-single-float-variable
float2 Unpack2In1(float input, float precision = 4096)
{
    float2 output = float2(0, 0);

    output.y = fmod(input, precision);
    output.x = floor(input / precision);
        
    return output / (precision - 1.0);
}


///////////////////////////////////////////////////////////////////////////////////////////////////////////////////


////////////////////////////////////////////// TRIANGLE STUFF //////////////////////////////////////////////

// Compute barycentric coordinates (u, v, w) for
// point p with respect to triangle (a, b, c)
void GetBarycentricCoordinates(float3 p, float3 a, float3 b, float3 c, out float u, out float v, out float w)
{
    float3 v0 = b - a;
    float3 v1 = c - a;
    float3 v2 = p - a;
    
    float d00 = dot(v0, v0);
    float d01 = dot(v0, v1);
    float d11 = dot(v1, v1);
    float d20 = dot(v2, v0);
    float d21 = dot(v2, v1);
    float denom = d00 * d11 - d01 * d01;
    
    v = (d11 * d20 - d01 * d21) / denom;
    w = (d00 * d21 - d01 * d20) / denom;
    u = 1.0 - v - w;
}

float4 BarycentricInterpolation(float3 p, float3 v1, float3 v2, float3 v3, float4 val1, float4 val2, float4 val3)
{
    float w1;
    float w2;
    float w3;
    GetBarycentricCoordinates(p, v1, v2, v3, w1, w2, w3);

    return w1 * val1 + w2 * val2 + w3 * val3;
}

float3 BarycentricInterpolation(float3 p, float3 v1, float3 v2, float3 v3, float3 val1, float3 val2, float3 val3)
{
    float w1;
    float w2;
    float w3;
    GetBarycentricCoordinates(p, v1, v2, v3, w1, w2, w3);
    
    return w1 * val1 + w2 * val2 + w3 * val3;
}

float2 BarycentricInterpolation(float3 p, float3 v1, float3 v2, float3 v3, float2 val1, float2 val2, float2 val3)
{
    float w1;
    float w2;
    float w3;
    GetBarycentricCoordinates(p, v1, v2, v3, w1, w2, w3);
    
    return w1 * val1 + w2 * val2 + w3 * val3;
}

// Möller–Trumbore algorithm for ray-triangle intersection
bool RayIntersectsTriangle(float3 o, float3 d, float3 v0, float3 v1, float3 v2, out float3 intersectionPoint)
{
    const float EPSILON = 0.0000001;

    float3 e1, e2, h, s, q;
    float a, f, u, v, t;

    e1 = v1 - v0;
    e2 = v2 - v0;

    h = cross(d, e2);
    a = dot(e1, h);

    if (abs(a) < EPSILON)
    {
        return false; // ray is parallel to triangle
    }

    f = 1.0 / a;
    s = o - v0;
    u = f * dot(s, h);

    if (u < 0.0 || u > 1.0)
        return false;

    q = cross(s, e1);
    v = f * dot(d, q);

    if (v < 0.0 || u + v > 1.0)
        return false;

    t = f * dot(e2, q);

    if (t >= 0.0)
    {
        intersectionPoint = o + d * t;
        return true;
    }
    
    intersectionPoint = float3(0, 0, 0);
    return false;
}

// this is a triangle unsigned distance field function
// created by the ESTEEMED inigo quilez
// https://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm
float DistanceToTriangle(float3 p, float3 a, float3 b, float3 c)
{
    float3 ba = b - a;
    float3 pa = p - a;
    float3 cb = c - b;
    float3 pb = p - b;
    float3 ac = a - c;
    float3 pc = p - c;
    
    float3 nor = cross(ba, ac);

    return sqrt(
    (sign(dot(cross(ba, nor), pa)) +
     sign(dot(cross(cb, nor), pb)) +
     sign(dot(cross(ac, nor), pc)) < 2.0)
     ?
     min(min(
     dot2(ba * clamp(dot(ba, pa) / dot2(ba), 0.0, 1.0) - pa),
     dot2(cb * clamp(dot(cb, pb) / dot2(cb), 0.0, 1.0) - pb)),
     dot2(ac * clamp(dot(ac, pc) / dot2(ac), 0.0, 1.0) - pc))
     :
     dot(nor, pa) * dot(nor, pa) / dot2(nor));
}

// returns a curviture value, only for use in the 'InterpolateSurfacePosition' function
// http://cs.engr.uky.edu/~cheng/PUBL/Paper_Nagata.pdf
float3 C(float3 D, float3 _n0, float3 _n1)
{
    float3 _v = (_n0 + _n1) * 0.5;
    float3 dv = (_n0 - _n1) * 0.5;
    float d = dot(D, _v);
    float dd = dot(D, dv);
    float c = dot(_n0, _n0 - 2 * dv);
    float dc = dot(_n0, dv);

    if (c == -1 || c == 1)
        return float3(0, 0, 0);

    return dd / (1 - dc) * _v + d / dc * dv;
}

// given a point on a triangle, the three vertices of the triangle, and the normals at those vertices,
// return a position corresponding to the position on the surface of the curve 'implied' by those normals.
// i hope that makes sense.
// http://cs.engr.uky.edu/~cheng/PUBL/Paper_Nagata.pdf
float3 InterpolateSurfacePosition(float3 p, float3 v_0, float3 v_1, float3 v_2, float3 n_0, float3 n_1, float3 n_2)
{
    float u;
    float v;
    float w;
    
    GetBarycentricCoordinates(p, v_0, v_1, v_2, u, v, w);

    float3 d1 = v_1 - v_0;
    float3 d2 = v_2 - v_1;
    float3 d3 = v_2 - v_0;

    float3 c1 = C(d1, n_0, n_1);
    float3 c2 = C(d2, n_1, n_2);
    float3 c3 = C(d3, n_0, n_2);

    // u = 1−η
    // v = η−ζ
    // w = ζ
        
    return
        (v_0 * u) +
        (v_1 * v) +
        (v_2 * w)
        -
        (c1 * u * v) -
        (c2 * v * w) -
        (c3 * u * w);
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////




#endif // COMMON_STUFF_INCLUDED