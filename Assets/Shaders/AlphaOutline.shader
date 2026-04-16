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

Shader "Custom/AlphaOutline" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _OutlineWidth("Outline Width", Float) = 0.02

  }
  SubShader {
    Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
    Blend SrcAlpha OneMinusSrcAlpha
    ZWrite Off

    Pass {
      Name "OutlineShell"
      Tags { "LightMode"="UniversalForward" }
      Cull Front
      HLSLPROGRAM
      #pragma vertex VertOutline
      #pragma fragment FragOutline
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      float _OutlineWidth;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
      };

      Varyings VertOutline(Attributes IN) {
        Varyings OUT;
        float4 worldPos = mul(unity_ObjectToWorld, IN.positionOS);
        float3 worldNormal = normalize(mul(unity_ObjectToWorld, float4(IN.normalOS, 1.0)).xyz);
        worldPos.xyz += worldNormal * _OutlineWidth;
        float4 objectPos = mul(unity_WorldToObject, worldPos);
        OUT.positionHCS = TransformObjectToHClip(objectPos.xyz);
        return OUT;
      }

      half4 FragOutline(Varyings IN) : SV_Target {
        return half4(0.0h, 0.0h, 0.0h, _Color.a);
      }
      ENDHLSL
    }

    Pass {
      Name "MainFill"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex VertMain
      #pragma fragment FragMain
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
      };

      Varyings VertMain(Attributes IN) {
        Varyings OUT;
        OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
        return OUT;
      }

      half4 FragMain(Varyings IN) : SV_Target {
        return half4(_Color.rgb, _Color.a);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
