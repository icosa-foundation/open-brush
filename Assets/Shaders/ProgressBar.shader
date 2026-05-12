// Copyright 2020 The Tilt Brush Authors
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

Shader "Custom/ProgressBar" {
    Properties {
        _Color ("Color", Color) = (0,0,0,1)
        _ProgressColor("Progress Color", Color) = (78,217,255,255)
        _MainTex ("Progress Bar Mask", 2D) = "white" {}
        _Ratio ("Progress Ratio", Range(0,1)) = 0
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5

    }
    SubShader {
        Tags{ "RenderPipeline"="UniversalPipeline" "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }

        LOD 100
        Pass {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _ProgressColor;
            float _Ratio;
            float _Cutoff;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes IN) {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                clip(tex.a - _Cutoff);
                half3 col = (IN.uv.x < _Ratio) ? _ProgressColor.rgb : _Color.rgb;
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
