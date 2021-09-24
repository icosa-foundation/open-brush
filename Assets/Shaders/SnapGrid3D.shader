// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// MIT License
//
// Copyright (c) 2020 fuqunaga
// https://github.com/fuqunaga/Grid3D
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

Shader "Custom/Grid3D"
{
    Properties
    {
        _InMin("InMin", Float) = 0
        _InMax("InMax", Float) = 1
        _OutMin("OutMin", Float) = 0
        _OutMax("OutMax", Float) = 1
        _Color("Color", color) = (1,1,1,1)
        _Origin("Origin", Vector) = (100,100,100,0)
        _GridCount("GridCount", Vector) = (100,100,100,0)
        _GridInterval("GridInterval", Float) = 10
        _LineWidth("LineWidth", Float) = 0.01
        _LineLength("LineLength", Float) = 0.2

        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTest("ZTest", Float) = 4              // LEqual
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" }
        Blend One One
        Cull Off Lighting Off ZWrite Off
        LOD 100

        Pass
        {
            Cull Off
            ZTest [_ZTest]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            float _InMin;
            float _InMax;
            float _OutMin;
            float _OutMax;
            float3 _Origin;
            float4 _GridCount;
            float _GridInterval;
            float _LineWidth;
            float _LineLength;
            float4x4 _CanvasMatrix;

            struct appdata
            {
                uint id : SV_VERTEXID;
                float4 vertex : POSITION;
                float4 pos : TEXCOORD2;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 pos : TEXCOORD2;
            };

            float3 calcQuadVertex(uint vtx_idx) // face 0:x 1:y 2:z
            {
                float3 offset;
                if      ( vtx_idx == 0) offset = float3(-0.5, -0.5, 0);
                else if ( vtx_idx == 1) offset = float3( 0.5, -0.5, 0);
                else if ( vtx_idx == 2) offset = float3(-0.5,  0.5, 0);
                else if ( vtx_idx == 3) offset = float3( 0.5,  0.5, 0);
                else if ( vtx_idx == 4) offset = float3(-0.5,  0.5, 0);
                else if ( vtx_idx == 5) offset = float3( 0.5, -0.5, 0);

                offset.x *= _LineWidth * _GridInterval;
                offset.y *= _LineLength * _GridInterval;

                return offset;
            }

            float remap(float input, float2 inMinMax, float2 outMinMax)
                {
	                float inMinMaxDiff = inMinMax.y - inMinMax.x;
	                float outMinMaxDiff = outMinMax.y - outMinMax.x;
	                return outMinMax.x + (input - inMinMax.x) * outMinMaxDiff / inMinMaxDiff;
                }
            float quickdist(float val)
            {
                // Distance-ish
                val /= (_GridCount.x + _GridCount.y + _GridCount.z) / 3.0;
                val =  abs(0.5 - (abs(0.5 - val) * 1.0));
                val = val * val * 2.0;
                val = remap(val, float2(_InMin, _InMax), float2(_OutMin, _OutMax));
                return val;

            }

            v2f vert (appdata v)
            {
                v2f o;
                
                uint vtx_per_quad = 6;
                uint vtx_per_line = vtx_per_quad * 2;
                uint vtx_per_star = vtx_per_line * 3;

                uint quad_vtx_idx = v.id % vtx_per_quad;
                float3 quad = calcQuadVertex(quad_vtx_idx);

                uint line_vtx_idx = v.id % vtx_per_line;
                bool rot90 = line_vtx_idx >= vtx_per_quad;

                quad.xyz = rot90 ? quad.zyx : quad.xyz;

                uint line_idx_per_star = (v.id % vtx_per_star) / vtx_per_line;
                switch(line_idx_per_star)
                {
                    case 0: quad.xyz = quad.yxz; break;
                    case 2: quad.xyz = quad.xzy; break;
                }

                uint star_idx = v.id / vtx_per_star;
                int3 gridCount = _GridCount;

                float3 xyz_idx = float3(
                    (star_idx / gridCount.z) % gridCount.x + 0.5,
                    star_idx / (gridCount.x * gridCount.z) + 0.5,
                    star_idx % gridCount.z + 0.5
                );

                float3 origin = _GridCount * -0.5;
                float3 pos = (xyz_idx + origin) * _GridInterval + quad;
                float3 g = 1.0/_GridInterval;
                float3 quantizedOrigin = float3(
                    round(_Origin.x * g.x) / g.x,
                    round(_Origin.y * g.y) / g.y,
                    round(_Origin.z * g.z) / g.z
                );
                float3 remainder = (quantizedOrigin - _Origin) / 2.0;
                float3 worldPos = mul(_CanvasMatrix, pos);
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos + quantizedOrigin, 1));
                float d = (
                    quickdist(xyz_idx.x + remainder.x) +
                    quickdist(xyz_idx.y + remainder.y) +
                    quickdist(xyz_idx.z + remainder.z)
                ) / 3;
                o.pos = half4(d, d, d, 1);
                return o;
            }

            fixed4 _Color;

            fixed4 frag (v2f i) : SV_Target
            {
                return saturate(i.pos) * _Color;
                // return i.pos * _Color;
            }
            ENDCG
        }
    }
}
