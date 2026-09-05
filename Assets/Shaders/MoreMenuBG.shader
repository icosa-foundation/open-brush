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

Shader "Custom/MoreMenuBG" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _InteriorColor ("Interior Color", Color) = (0,0,0,0)
    _MainTex ("Image", 2D) = "" {}
    _OutlineWidth("Outline Width", Float) = 0.015
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Geometry" }

    Pass {
      Name "Interior"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex VertInterior
      #pragma fragment FragInterior
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _InteriorColor;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;

        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
      };

      Varyings VertInterior(Attributes IN) {
        Varyings OUT;
        UNITY_SETUP_INSTANCE_ID(IN);
        UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
        OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
        return OUT;
      }

      half4 FragInterior(Varyings IN) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
        return half4(_InteriorColor.rgb, 1.0h);
      }
      ENDHLSL
    }

    Pass {
      Name "Outline"
      Tags { "LightMode"="UniversalForward" }
      Cull Front
      HLSLPROGRAM
      #pragma vertex VertOutline
      #pragma fragment FragOutline
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Assets/Shaders/Include/Math.cginc"

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      float _OutlineWidth;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;

        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
      };

      Varyings VertOutline(Attributes IN) {
        Varyings OUT;
        UNITY_SETUP_INSTANCE_ID(IN);
        UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
        float4 worldPos = mul(unity_ObjectToWorld, IN.positionOS);
        float3x3 unscaledObject2World;
        float3 unusedScale;
        factorRotationAndLocalScale((float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);
        float3 worldNormal = normalize(mul(unscaledObject2World, IN.normalOS));
        worldPos.xyz += worldNormal * _OutlineWidth;
        float4 objectPos = mul(unity_WorldToObject, worldPos);
        OUT.positionHCS = TransformObjectToHClip(objectPos.xyz);
        return OUT;
      }

      half4 FragOutline(Varyings IN) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
        return half4(_Color.rgb, 1.0h);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
