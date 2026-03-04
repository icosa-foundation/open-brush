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

Shader "TiltBrush/MobileDiffuse" {
  Properties {
    _Color ("Color", Color) = (1,1,1,1)
    _MainTex("Albedo (RGB)", 2D) = "black" {}
    _LightMap("LightMap (RGB)", 2D) = "white" {}
  }

  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
    LOD 100

    Pass {
      Tags { "LightMode" = "UniversalForward" }

      HLSLPROGRAM
      #pragma target 3.0
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_fog
      #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
      #pragma multi_compile_fragment _ _SHADOWS_SOFT

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      struct Attributes {
        float4 positionOS : POSITION;
        float2 uv0 : TEXCOORD0;
        float2 uv1 : TEXCOORD1;
        half3 normalOS : NORMAL;
      };

      struct Varyings {
        float4 positionCS : SV_POSITION;
        float2 uv0 : TEXCOORD0;
        float2 uv1 : TEXCOORD1;
        half3 normalWS : TEXCOORD2;
        float3 positionWS : TEXCOORD3;
        half fogFactor : TEXCOORD4;
      };

      TEXTURE2D(_MainTex);
      SAMPLER(sampler_MainTex);
      TEXTURE2D(_LightMap);
      SAMPLER(sampler_LightMap);

      CBUFFER_START(UnityPerMaterial)
      float4 _Color;
      float4 _MainTex_ST;
      float4 _LightMap_ST;
      CBUFFER_END

      Varyings vert(Attributes v) {
        Varyings o;
        VertexPositionInputs posInput = GetVertexPositionInputs(v.positionOS.xyz);
        VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS);
        o.positionCS = posInput.positionCS;
        o.positionWS = posInput.positionWS;
        o.uv0 = TRANSFORM_TEX(v.uv0, _MainTex);
        o.uv1 = TRANSFORM_TEX(v.uv1, _LightMap);
        o.normalWS = normalInput.normalWS;
        o.fogFactor = ComputeFogFactor(posInput.positionCS.z);
        return o;
      }

      half4 frag(Varyings i, bool isFrontFace : SV_IsFrontFace) : SV_Target {
        half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv0) * _Color;
        half3 lightMap = SAMPLE_TEXTURE2D(_LightMap, sampler_LightMap, i.uv1).rgb;

        half faceSign = isFrontFace ? 1.0h : -1.0h;
        half3 normalWS = normalize(i.normalWS * faceSign);

        Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
        half ndotl = saturate(dot(normalWS, mainLight.direction));
        half3 lighting = ndotl * mainLight.color * mainLight.shadowAttenuation;
        lighting += SampleSH(normalWS);

        half4 finalColor = albedo;
        finalColor.rgb *= lightMap * lighting;
        finalColor.rgb = MixFog(finalColor.rgb, i.fogFactor);
        return finalColor;
      }
      ENDHLSL
    } // pass
  } // subshader

Fallback "Mobile/VertexLit"
}

