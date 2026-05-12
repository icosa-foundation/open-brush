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

Shader "Custom/UnlitOutlineFlatten" {
  Properties{
    _Color("Main Color", Color) = (1,1,1,1)
    _MainTex("Image", 2D) = "" {}
    _OutlineWidth("Outline Width", Float) = 0.02
    _FlattenAmount("Flatten Amount", Range(1, 0)) = 0

  }
    SubShader{
    Tags{ "RenderPipeline"="UniversalPipeline" "Queue" = "AlphaTest+20" "RenderType" = "Geometry" }

    Pass {
      Name "MainFlatten"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex VertMain
      #pragma fragment FragMain
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      float _FlattenAmount;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
      };

      Varyings VertMain(Attributes IN) {
        Varyings OUT;
        float3 pos = IN.positionOS.xyz;
        pos.z = pos.z - pos.z * _FlattenAmount;
        OUT.positionHCS = TransformObjectToHClip(pos);
        return OUT;
      }

      half4 FragMain(Varyings IN) : SV_Target {
        return half4(_Color.rgb, 1.0h);
      }
      ENDHLSL
    }

    Pass {
      Name "OutlineFlatten"
      Tags { "LightMode"="UniversalForward" }
      Cull Front
      HLSLPROGRAM
      #pragma vertex VertOutline
      #pragma fragment FragOutline
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Assets/Shaders/Include/Math.cginc"

      CBUFFER_START(UnityPerMaterial)
      float _OutlineWidth;
      float _FlattenAmount;
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
        float4 objectPos = IN.positionOS;
        objectPos.z = objectPos.z - objectPos.z * _FlattenAmount;

        float4 worldPos = mul(unity_ObjectToWorld, objectPos);
        float3x3 unscaledObject2World;
        float3 unusedScale;
        factorRotationAndLocalScale((float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);
        float3 worldNormal = normalize(mul(unscaledObject2World, IN.normalOS));
        worldPos.xyz += worldNormal * _OutlineWidth;

        float4 resultObjectPos = mul(unity_WorldToObject, worldPos);
        OUT.positionHCS = TransformObjectToHClip(resultObjectPos.xyz);
        return OUT;
      }

      half4 FragOutline(Varyings IN) : SV_Target {
        return half4(0.0h, 0.0h, 0.0h, 1.0h);
      }
      ENDHLSL
    }
  }
    FallBack Off
}
