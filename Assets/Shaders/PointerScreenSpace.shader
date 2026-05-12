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

// This shader creates an outline by projecting an enlarged back face version
// of the geometry. The geometry needs to have smoothed normals.
//
// For non-uniform scales, this shader works best when all vertices are cubic
// because it's taking out the scaling component of the transform to keep the
// original orientation of the normals with respect to the geometry.
Shader "Custom/PointerScreenSpace" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _OutlineWidth("Outline Width", Float) = 0.02
    _BaseThickness("Thickness of Pointer Geometry", Float) = 0.025
    _MaxDistance("Screen Space Max Distance", Float) = 7.0
    _RevealSpeed("Reveal Speed", Float) = 2.0
    _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
  }

  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Geometry" }

    Pass {
      Name "MainScreenSpace"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex VertMain
      #pragma fragment FragMain
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Assets/Shaders/Include/Math.cginc"

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      float _BaseThickness;
      float _MaxDistance;
      float _RevealSpeed;
      float _RevealStartTime;
      half _Cutoff;
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

      Varyings VertMain(Attributes IN) {
        Varyings OUT;
        float4 worldPos = mul(unity_ObjectToWorld, IN.positionOS);
        float3 camPos = _WorldSpaceCameraPos.xyz;
        float vertexDistance = length(worldPos.xyz - camPos);

        if (vertexDistance > _MaxDistance) {
          float sizeIncrease = 0.5 * (vertexDistance / _MaxDistance - 1.0) * _BaseThickness;
          float3x3 unscaledObject2World;
          float3 unusedScale;
          factorRotationAndLocalScale((float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);
          float3 worldNormal = normalize(mul(unscaledObject2World, IN.normalOS));
          worldPos.xyz += worldNormal * sizeIncrease;
        }

        float4 objectPos = mul(unity_WorldToObject, worldPos);
        OUT.positionHCS = TransformObjectToHClip(objectPos.xyz);
        OUT.uv = IN.uv;
        return OUT;
      }

      half4 FragMain(Varyings IN) : SV_Target {
        half alpha = 1.0h;
        if (_RevealStartTime != 0.0) {
          alpha = IN.uv.x < (_Time.y - _RevealStartTime) * _RevealSpeed ? 1.0h : 0.0h;
        }
        clip(alpha - _Cutoff);
        return half4(_Color.rgb, alpha);
      }
      ENDHLSL
    }

    Pass {
      Name "OutlineScreenSpace"
      Tags { "LightMode"="UniversalForward" }
      Cull Front
      HLSLPROGRAM
      #pragma vertex VertOutline
      #pragma fragment FragOutline
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Assets/Shaders/Include/Math.cginc"

      CBUFFER_START(UnityPerMaterial)
      float _OutlineWidth;
      float _BaseThickness;
      float _MaxDistance;
      float _RevealSpeed;
      float _RevealStartTime;
      half _Cutoff;
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

      Varyings VertOutline(Attributes IN) {
        Varyings OUT;
        float4 worldPos = mul(unity_ObjectToWorld, IN.positionOS);
        float3 camPos = _WorldSpaceCameraPos.xyz;
        float vertexDistance = length(worldPos.xyz - camPos);

        float sizeIncrease = vertexDistance > _MaxDistance ?
            0.5 * (vertexDistance / _MaxDistance * (_BaseThickness + 2.0 * _OutlineWidth) - _BaseThickness) :
            _OutlineWidth;

        float3x3 unscaledObject2World;
        float3 unusedScale;
        factorRotationAndLocalScale((float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);
        float3 worldNormal = normalize(mul(unscaledObject2World, IN.normalOS));
        worldPos.xyz += worldNormal * sizeIncrease;

        float4 objectPos = mul(unity_WorldToObject, worldPos);
        OUT.positionHCS = TransformObjectToHClip(objectPos.xyz);
        OUT.uv = IN.uv;
        return OUT;
      }

      half4 FragOutline(Varyings IN) : SV_Target {
        half alpha = 1.0h;
        if (_RevealStartTime != 0.0 && _RevealSpeed != 0.0) {
          alpha = IN.uv.x < (_Time.y - _RevealStartTime) * _RevealSpeed ? 1.0h : 0.0h;
        }
        clip(alpha - _Cutoff);
        return half4(0.0h, 0.0h, 0.0h, alpha);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
