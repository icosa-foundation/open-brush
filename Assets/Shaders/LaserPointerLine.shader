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

Shader "Custom/LaserPointerLine" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _ScrollSpeed("Scroll Speed", Float) = 1
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
    Blend One OneMinusSrcAlpha

    Pass {
      Name "ForwardUnlit"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half _ScrollSpeed;
      half4 _EmissionColor;
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

      half4 Frag(Varyings IN) : SV_Target {
        half alpha = (sin(IN.uv.x + _Time.x * _ScrollSpeed) + 1.0h) * 0.5h;
        half3 emission = _EmissionColor.xyz * 0.1h * alpha;
        return half4(emission, alpha);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
