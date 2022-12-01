// source:
// https://github.com/Colt-Zero/DualContouringGPU/blob/master/DualContouringGPU/Assets/Scripts/ComputeVoxels.compute
// thank you

#ifndef QEF_INCLUDED
#define QEF_INCLUDED

#define SVD_NUM_SWEEPS 5
#define PSUEDO_INVERSE_THRESHOLD 0.15
#define PSEUDO_INVERSE_MAT float3x3(PSUEDO_INVERSE_THRESHOLD, 0., 0., 0., PSUEDO_INVERSE_THRESHOLD, 0., 0., 0., PSUEDO_INVERSE_THRESHOLD)

typedef float mat3x3[3][3];
typedef float mat3x3_tri[6];

// SVD
/////////////////////////////////////////////////

void svd_mul_matrix_vec(inout float4 result, mat3x3 a, float4 b)
{
    result.x = dot(float4(a[0][0], a[0][1], a[0][2], 0.0f), b);
    result.y = dot(float4(a[1][0], a[1][1], a[1][2], 0.0f), b);
    result.z = dot(float4(a[2][0], a[2][1], a[2][2], 0.0f), b);
    result.w = 0.0f;
}

void givens_coeffs_sym(float a_pp, float a_pq, float a_qq, inout float c, inout float s)
{
    if (a_pq == 0.0f)
    {
        c = 1.0f;
        s = 0.0f;
        return;
    }
	
    float tau = (a_qq - a_pp) / (2.0f * a_pq);
    float stt = sqrt(1.0f + tau * tau);
    float tan = 1.0f / ((tau >= 0.0f) ? (tau + stt) : (tau - stt));
    c = rsqrt(1.0f + tan * tan);
    s = tan * c;
}

void svd_rotate_xy(inout float x, inout float y, float c, float s)
{
    float u = x;
    float v = y;
    x = c * u - s * v;
    y = s * u + c * v;
}

void svd_rotateq_xy(inout float x, inout float y, inout float a, float c, float s)
{
    float cc = c * c;
    float ss = s * s;
    float mx = 2.0f * c * s * a;
    float u = x;
    float v = y;
    x = cc * u - mx + ss * v;
    y = ss * u + mx + cc * v;
}

void svd_rotate(inout mat3x3 vtav, mat3x3 v, int a, int b)
{
    if (vtav[a][b] == 0.0f)
        return;
	
    float c, s;
    givens_coeffs_sym(vtav[a][a], vtav[a][b], vtav[b][b], c, s);
	
    float x, y, z;
    x = vtav[a][a];
    y = vtav[b][b];
    z = vtav[a][b];
    svd_rotateq_xy(x, y, z, c, s);
    vtav[a][a] = x;
    vtav[b][b] = y;
    vtav[a][b] = z;
	
    x = vtav[0][3 - b];
    y = vtav[1 - a][2];
    svd_rotate_xy(x, y, c, s);
    vtav[0][3 - b] = x;
    vtav[1 - a][2] = y;
	
    vtav[a][b] = 0.0f;
	
    x = v[0][a];
    y = v[0][b];
    svd_rotate_xy(x, y, c, s);
    v[0][a] = x;
    v[0][b] = y;
	
    x = v[1][a];
    y = v[1][b];
    svd_rotate_xy(x, y, c, s);
    v[1][a] = x;
    v[1][b] = y;
	
    x = v[2][a];
    y = v[2][b];
    svd_rotate_xy(x, y, c, s);
    v[2][a] = x;
    v[2][b] = y;
}

void svd_solve_sym(inout mat3x3_tri a, inout float4 sigma, mat3x3 v)
{
    mat3x3 vtav = { { 0.0f, 0.0f, 0.0f }, { 0.0f, 0.0f, 0.0f }, { 0.0f, 0.0f, 0.0f } };
    vtav[0][0] = a[0];
    vtav[0][1] = a[1];
    vtav[0][2] = a[2];
    vtav[1][0] = 0.0f;
    vtav[1][1] = a[3];
    vtav[1][2] = a[4];
    vtav[2][0] = 0.0f;
    vtav[2][1] = 0.0f;
    vtav[2][2] = a[5];
	
    [fastopt]
    for (int i = 0; i < SVD_NUM_SWEEPS; ++i)
    {
        svd_rotate(vtav, v, 0, 1);
        svd_rotate(vtav, v, 0, 2);
        svd_rotate(vtav, v, 1, 2);
    }
	
    sigma = float4(vtav[0][0], vtav[1][1], vtav[2][2], 0.0f);
}

float svd_invdet(float x, float tol)
{
    return (abs(x) < tol || abs(1.0f / x) < tol) ? 0.0f : (1.0f / x);
}

void svd_pseudoinverse(inout mat3x3 o, float4 sigma, mat3x3 v)
{
    float d0 = svd_invdet(sigma.x, PSUEDO_INVERSE_THRESHOLD);
    float d1 = svd_invdet(sigma.y, PSUEDO_INVERSE_THRESHOLD);
    float d2 = svd_invdet(sigma.z, PSUEDO_INVERSE_THRESHOLD);

    o[0][0] = v[0][0] * d0 * v[0][0] + v[0][1] * d1 * v[0][1] + v[0][2] * d2 * v[0][2];
    o[0][1] = v[0][0] * d0 * v[1][0] + v[0][1] * d1 * v[1][1] + v[0][2] * d2 * v[1][2];
    o[0][2] = v[0][0] * d0 * v[2][0] + v[0][1] * d1 * v[2][1] + v[0][2] * d2 * v[2][2];
    o[1][0] = v[1][0] * d0 * v[0][0] + v[1][1] * d1 * v[0][1] + v[1][2] * d2 * v[0][2];
    o[1][1] = v[1][0] * d0 * v[1][0] + v[1][1] * d1 * v[1][1] + v[1][2] * d2 * v[1][2];
    o[1][2] = v[1][0] * d0 * v[2][0] + v[1][1] * d1 * v[2][1] + v[1][2] * d2 * v[2][2];
    o[2][0] = v[2][0] * d0 * v[0][0] + v[2][1] * d1 * v[0][1] + v[2][2] * d2 * v[0][2];
    o[2][1] = v[2][0] * d0 * v[1][0] + v[2][1] * d1 * v[1][1] + v[2][2] * d2 * v[1][2];
    o[2][2] = v[2][0] * d0 * v[2][0] + v[2][1] * d1 * v[2][1] + v[2][2] * d2 * v[2][2];
}

void svd_solve_ATA_Atb(inout mat3x3_tri ATA, float4 Atb, inout float4 x)
{
    mat3x3 V = { { 1.0f, 0.0f, 0.0f }, { 0.0f, 1.0f, 0.0f }, { 0.0f, 0.0f, 1.0f } };
	
    float4 sigma = float4(0, 0, 0, 0);
    svd_solve_sym(ATA, sigma, V);
	
    mat3x3 Vinv = { { 0.0f, 0.0f, 0.0f }, { 0.0f, 0.0f, 0.0f }, { 0.0f, 0.0f, 0.0f } };
    svd_pseudoinverse(Vinv, sigma, V);
    svd_mul_matrix_vec(x, Vinv, Atb);
}

void svd_vmul_sym(inout float4 result, mat3x3_tri A, float4 v)
{
    float4 A_row_x = float4(A[0], A[1], A[2], 0);
    result.x = dot(A_row_x, v);
    result.y = A[1] * v.x + A[3] * v.y + A[4] * v.z;
    result.z = A[2] * v.x + A[4] * v.y + A[5] * v.z;
}

// QEF
/////////////////////////////////////////////////

void qef_add(float3 n, float3 p, inout mat3x3_tri ATA, inout float4 Atb, inout float4 pointaccum, inout float btb)
{
    ATA[0] += n.x * n.x;
    ATA[1] += n.x * n.y;
    ATA[2] += n.x * n.z;
    ATA[3] += n.y * n.y;
    ATA[4] += n.y * n.z;
    ATA[5] += n.z * n.z;
	
    float b = dot(p, n);
    Atb.x += n.x * b;
    Atb.y += n.y * b;
    Atb.z += n.z * b;
    btb += b * b;
	
    pointaccum.x += p.x;
    pointaccum.y += p.y;
    pointaccum.z += p.z;
    pointaccum.w += 1.0f;
}

float qef_calc_error(mat3x3_tri A, float4 x, float4 b)
{
    float4 tmp = float4(0, 0, 0, 0);
    svd_vmul_sym(tmp, A, x);
    tmp = b - tmp;
	
    return dot(tmp, tmp);
}

float qef_solve(mat3x3_tri ATA, float4 Atb, float4 pointaccum, inout float4 x)
{
    float4 masspoint = pointaccum / pointaccum.w;
	
    float4 A_mp = float4(0, 0, 0, 0);
    svd_vmul_sym(A_mp, ATA, masspoint);
    A_mp = Atb - A_mp;
	
    svd_solve_ATA_Atb(ATA, A_mp, x);
	
    float error = qef_calc_error(ATA, x, Atb);
    x += masspoint;
	
    return error;
}

float3 SolveQEF(int edgeIntersectionCount, float3 localEdgeIntersectionNormals[12], float3 localEdgeIntersectionPoints[12], float3 averageEdgeIntersectionPoint)
{
    // https://www.mattkeeter.com/projects/qef/
    
    float3 result = float3(0., 0., 0.);
    
    // "A is a s×d matrix, where s is the number of samples and d is the dimension."
    float3x3 _A[SVD_NUM_SWEEPS];
    
    // "B is a s×1 matrix, with one row per sample."
    float3 b[SVD_NUM_SWEEPS];
    
    for (int i = 0; i < SVD_NUM_SWEEPS; i++)
    {
        _A[i] = float3x3(0., 0., 0., 0., 0., 0., 0., 0., 0.);
        b[i] = float3(0., 0., 0.);
    }
    
    float oneMinusPseudoInverse = 1.0 - PSUEDO_INVERSE_THRESHOLD;
    
    for (uint j = 0; j < uint(edgeIntersectionCount); j++)
    {
        uint jDiv3 = j / 3u;
        switch (j % 3u)
        {
            case 0:
                _A[jDiv3]._m00_m01_m02 = oneMinusPseudoInverse * localEdgeIntersectionNormals[j];
                b[jDiv3].x = oneMinusPseudoInverse * dot(localEdgeIntersectionNormals[j], localEdgeIntersectionPoints[j]);
                break;
            case 1:
                _A[jDiv3]._m10_m11_m12 = oneMinusPseudoInverse * localEdgeIntersectionNormals[j];
                b[jDiv3].y = oneMinusPseudoInverse * dot(localEdgeIntersectionNormals[j], localEdgeIntersectionPoints[j]);
                break;
            case 2:
                _A[jDiv3]._m20_m21_m22 = oneMinusPseudoInverse * localEdgeIntersectionNormals[j];
                b[jDiv3].z = oneMinusPseudoInverse * dot(localEdgeIntersectionNormals[j], localEdgeIntersectionPoints[j]);
                break;
        }
    }
    
    
    _A[4] = PSEUDO_INVERSE_MAT;
    b[4] = PSUEDO_INVERSE_THRESHOLD * averageEdgeIntersectionPoint;

    float3x3 _A2 = float3x3(0., 0., 0., 0., 0., 0., 0., 0., 0.);
    
    // AtA
    for (int k = 0; k < SVD_NUM_SWEEPS; k++)
    {
        _A2 += mul(transpose(_A[k]), _A[k]);
    }
    
    float detInv =
        1. /
        (_A2._m00 * (_A2._m11 * _A2._m22 - _A2._m21 * _A2._m12) -
        _A2._m01 * (_A2._m10 * _A2._m22 - _A2._m12 * _A2._m20) +
        _A2._m02 * (_A2._m10 * _A2._m21 - _A2._m11 * _A2._m20));
    
    _A2 = float3x3
    (
        (_A2._m11 * _A2._m22 - _A2._m21 * _A2._m12) * detInv,
        (_A2._m02 * _A2._m21 - _A2._m01 * _A2._m22) * detInv,
        (_A2._m01 * _A2._m12 - _A2._m02 * _A2._m11) * detInv,
        (_A2._m12 * _A2._m20 - _A2._m10 * _A2._m22) * detInv,
        (_A2._m00 * _A2._m22 - _A2._m02 * _A2._m20) * detInv,
        (_A2._m10 * _A2._m02 - _A2._m00 * _A2._m12) * detInv,
        (_A2._m10 * _A2._m21 - _A2._m20 * _A2._m11) * detInv,
        (_A2._m20 * _A2._m01 - _A2._m00 * _A2._m21) * detInv,
        (_A2._m00 * _A2._m11 - _A2._m10 * _A2._m01) * detInv
    );
            
    for (int l = 0; l < SVD_NUM_SWEEPS; l++)
    {
        result += mul(mul(_A2, transpose(_A[l])), b[l]);
    }

    // x = pseudoInverse(AtA) * (AtB)
    return result;
}


#endif