// Copyright 2022 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

Shader "Custom/VertexColorTransparant"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.8
        _Saturation ("Saturation", Range(0,4)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent"
        }
        LOD 200

        Blend One SrcAlpha
        Zwrite Off
        Cull Off

        CGPROGRAM
        #pragma surface surf StandardSpecular vertex:vert fullforwardshadows nofog
        #pragma target 3.0
        #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
        #pragma multi_compile __ SELECTION_ON HIGHLIGHT_ON

        #include "Assets/Shaders/Include/Brush.cginc"
        #include "Assets/Shaders/Include/MobileSelection.cginc"

        uniform float _Frequency;
        uniform float _Jitter;
        float _Saturation;
        half _Smoothness;
        fixed4 _Tint;

        struct Input
        {
            float2 uv_MainTex;
            float3 localPos;
            float3 worldRefl;
            float3 viewDir;
            float3 vertexColor;
            INTERNAL_DATA
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            PrepForOds(v.vertex);
            o.localPos = v.vertex.xyz;
            o.vertexColor = v.color;
        }

        float BlackAndWhite(float3 col) {return float(col.r * 0.299 + col.g * 0.587 + col.b * 0.114);}
        float3 Saturation(float3 col) {return lerp(BlackAndWhite(col.rgb), col.rgb, _Saturation);}

        void surf(Input IN, inout SurfaceOutputStandardSpecular o)
        {
            o.Specular = Saturation(IN.vertexColor);
            o.Smoothness = _Smoothness;
            o.Albedo = Saturation(IN.vertexColor) * _Tint;
            SURF_FRAG_MOBILESELECT(o);
        }
        ENDCG
    }

    FallBack "Diffuse"
}