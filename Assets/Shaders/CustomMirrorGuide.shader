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

Shader "Custom/CustomMirrorGuide" {
  Properties {
    _Color ("Main Color", Color) = (0.5,0.5,0.5,1)
    _MainTex ("Image", 2D) = "" {}
    _OutlineWidth("Outline Width", Float) = -0.01
  }

  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+20" "RenderType"="Geometry" }

    Pass {
      Name "CustomMirrorGuideMain"
      Tags { "LightMode"="UniversalForward" }

      HLSLPROGRAM
      #pragma vertex VertMain
      #pragma fragment FragMain
      #pragma multi_compile_instancing

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      float _OutlineWidth;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
        UNITY_VERTEX_OUTPUT_STEREO
      };

      Varyings VertMain(Attributes IN) {
        Varyings OUT;
        UNITY_SETUP_INSTANCE_ID(IN);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
        OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
        return OUT;
      }

      half4 FragMain(Varyings IN) : SV_Target {
        return half4(_Color.rgb, 1.0h);
      }
      ENDHLSL
    }

    Pass {
      Name "CustomMirrorGuideOutline"
      Tags { "LightMode"="SRPDefaultUnlit" }
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
        UNITY_VERTEX_OUTPUT_STEREO
      };

      Varyings VertOutline(Attributes IN) {
        Varyings OUT;
        UNITY_SETUP_INSTANCE_ID(IN);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

        float4 worldPos = mul(unity_ObjectToWorld, IN.positionOS);
        float3x3 unscaledObject2World;
        float3 unusedScale;
        factorRotationAndLocalScale((float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);
        float3 worldNormal = normalize(mul(unscaledObject2World, IN.normalOS));
        worldPos.xyz += worldNormal * _OutlineWidth;
        OUT.positionHCS = TransformWorldToHClip(worldPos.xyz);
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
