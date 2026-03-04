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

Shader "Custom/TeleporterLine" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _Color2 ("Main Color 2", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _ScrollSpeed("Scroll Speed", Float) = 1
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Geometry" }

    Pass {
      Name "MainPulse"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment FragMain
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      half4 _Color2;
      half _ScrollSpeed;
      half _Cutoff;
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

      half4 FragMain(Varyings IN) : SV_Target {
        half t = abs(sin(_Time.y * 4.0h));
        half3 emission = lerp(_Color2.rgb, _Color.rgb, t);
        half alpha = 1.2h * (sin(IN.uv.x + _Time.x * _ScrollSpeed) + 1.0h) * 0.5h;
        clip(alpha - _Cutoff);
        return half4(emission, alpha);
      }
      ENDHLSL
    }

    Pass {
      Name "InnerStrip"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment FragStrip
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half _ScrollSpeed;
      half4 _EmissionColor;
      half _Cutoff;
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

      half4 FragStrip(Varyings IN) : SV_Target {
        half strip = abs(IN.uv.y - 0.5h);
        strip = strip > 0.1h ? 0.0h : 1.0h;
        half alpha = strip * (sin(IN.uv.x + _Time.x * _ScrollSpeed) + 1.0h) * 0.5h;
        clip(alpha - _Cutoff);
        return half4(_EmissionColor.rgb, alpha);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
