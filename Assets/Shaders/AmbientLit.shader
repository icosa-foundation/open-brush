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

Shader "Custom/AmbientLit" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB)", 2D) = "white" {}
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
    LOD 100

    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      TEXTURE2D(_MainTex);
      SAMPLER(sampler_MainTex);

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      float4 _MainTex_ST;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
        float3 normalOS : NORMAL;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 positionWS : TEXCOORD1;
      };

      Varyings Vert(Attributes IN) {
        Varyings OUT;
        OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
        OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
        OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
        return OUT;
      }

      half4 Frag(Varyings IN) : SV_Target {
        half3 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb * _Color.rgb;
        Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
        half atten = mainLight.shadowAttenuation;
        half3 lit = baseColor * (mainLight.color * atten);
        return half4(lit, 1.0h);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
