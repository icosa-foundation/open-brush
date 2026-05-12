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

Shader "Custom/TeleporterFeet" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _Color2 ("Main Color 2", Color) = (1,1,1,1)
    _MainTex ("Image", 2D) = "" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Geometry" }

    ZTest Always
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
      half4 _Color2;
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

      half4 Frag(Varyings IN) : SV_Target {
        half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
        half t = abs(sin(_Time.y * 4.0h));
        half3 emission = c.rgb * lerp(_Color2.rgb, _Color.rgb, t);
        clip(c.a - _Cutoff);
        return half4(emission, c.a);
      }
      ENDHLSL
    }
  }

  FallBack Off
}
