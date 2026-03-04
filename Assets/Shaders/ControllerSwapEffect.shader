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

Shader "Custom/ControllerSwapEffect" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _OutlineWidth("Outline Width", Float) = 0.02
    _Intensity("Intensity", Float) = 1

  }
  SubShader {
    Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}

    Blend One One
    ZWrite Off

    Pass {
      Name "ForwardUnlit"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      TEXTURE2D(_MainTex);
      SAMPLER(sampler_MainTex);

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      float _OutlineWidth;
      float _Intensity;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float2 uv : TEXCOORD0;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
        float2 uv : TEXCOORD0;
      };

      Varyings Vert(Attributes IN) {
        Varyings OUT;
        float4 worldPos = mul(unity_ObjectToWorld, IN.positionOS);
        float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(IN.normalOS, 1.0)).xyz);
        worldPos.xyz += worldNormal * _OutlineWidth;
        float4 objectPos = mul(unity_WorldToObject, worldPos);
        OUT.positionHCS = TransformObjectToHClip(objectPos.xyz);
        OUT.uv = IN.uv;
        return OUT;
      }

      half4 Frag(Varyings IN) : SV_Target {
        half3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - _Time.z).xyz;
        half3 emission = 10.0h * (half)_Intensity * _Color.xyz * col;
        return half4(emission, 1.0h);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
