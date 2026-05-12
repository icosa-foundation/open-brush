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

Shader "Unlit/TextureTint" {
Properties {
  _MainTex ("Base (RGB)", 2D) = "white" {}
  _Color ("Color", Color) = (1,1,1,1)
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  LOD 100

  Pass {
    Tags { "LightMode"="UniversalForward" }
    Cull Off
    HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      TEXTURE2D(_MainTex);
      SAMPLER(sampler_MainTex);

      CBUFFER_START(UnityPerMaterial)
      float4 _MainTex_ST;
      half4 _Color;
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
        OUT.uv = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
        return OUT;
      }

      half4 Frag(Varyings IN) : SV_Target {
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
      }
    ENDHLSL
  }
}

}
