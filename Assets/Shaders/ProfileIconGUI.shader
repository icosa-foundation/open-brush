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

Shader "Custom/ProfileIconGUI" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Avatar Texture", 2D) = "white" {}
        _AlphaMask ("Mask", 2D) = "white" {}
    }
    SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"
    "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True"}
        LOD 200

    Cull Off
    ZWrite Off
    Blend One OneMinusSrcAlpha

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
            TEXTURE2D(_AlphaMask);
            SAMPLER(sampler_AlphaMask);

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uvMain : TEXCOORD0;
                float2 uvMask : TEXCOORD1;
            };

            Varyings Vert(Attributes IN) {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uvMain = IN.uv0;
                OUT.uvMask = IN.uv0;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMain) * _Color;
                half maskAlpha = SAMPLE_TEXTURE2D(_AlphaMask, sampler_AlphaMask, IN.uvMask).a;
                return half4(c.rgb, maskAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
