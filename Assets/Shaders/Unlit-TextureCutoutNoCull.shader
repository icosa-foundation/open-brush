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

Shader "Unlit/TextureCutoutNoCull" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout" }
  Cull Off

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
    half _Cutoff;
    float4 _MainTex_ST;
    CBUFFER_END

    half _SceneFadeAmount;

    struct Attributes {
      float4 positionOS : POSITION;
      float2 uv : TEXCOORD0;
      half4 color : COLOR;
    };

    struct Varyings {
      float4 positionHCS : SV_POSITION;
      float2 uv : TEXCOORD0;
      half4 color : TEXCOORD1;
    };

    Varyings Vert(Attributes IN) {
      Varyings OUT;
      OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
      OUT.uv = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
      OUT.color = IN.color;
      return OUT;
    }

    half4 Frag(Varyings IN) : SV_Target {
      half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
      c.rgb *= _SceneFadeAmount;
      c.rgb *= IN.color.rgb;
      c.a *= IN.color.a;
      clip(c.a - _Cutoff);
      return half4(c.rgb, 1.0h);
    }
    ENDHLSL
  }

  Pass {
    Name "ShadowCaster"
    Tags { "LightMode"="ShadowCaster" }

    HLSLPROGRAM
    #pragma vertex VertShadow
    #pragma fragment FragShadow

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
    half _Cutoff;
    float4 _MainTex_ST;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      float2 uv : TEXCOORD0;
      half4 color : COLOR;
    };

    struct Varyings {
      float4 positionCS : SV_POSITION;
      float2 uv : TEXCOORD0;
      half alphaMul : TEXCOORD1;
    };

    Varyings VertShadow(Attributes IN) {
      Varyings OUT;
      OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
      OUT.uv = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
      OUT.alphaMul = IN.color.a;
      return OUT;
    }

    half4 FragShadow(Varyings IN) : SV_Target {
      half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a * IN.alphaMul;
      clip(alpha - _Cutoff);
      return 0;
    }
    ENDHLSL
  }
}

FallBack Off
}
