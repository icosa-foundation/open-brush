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

Shader "Custom/SketchSurface" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _BackColor ("Backside Color", Color) = (1,1,1,1)
  _BorderTex ("Border Color", 2D) = "white" {}
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
  _BackTex ("Backside Color", 2D) = "white" {}
}

SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
  LOD 100
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off

  Pass {
    Name "FrontFace"
    Tags { "LightMode"="UniversalForward" }
    HLSLPROGRAM
    #pragma vertex Vert
    #pragma fragment FragFront
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    TEXTURE2D(_BorderTex);
    SAMPLER(sampler_BorderTex);

    CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _BackColor;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      float2 uv0 : TEXCOORD0;
    };

    struct Varyings {
      float4 positionHCS : SV_POSITION;
      float2 uvMain : TEXCOORD0;
      float2 uvBorder : TEXCOORD1;
    };

    Varyings Vert(Attributes IN) {
      Varyings OUT;
      OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
      OUT.uvMain = IN.uv0;
      OUT.uvBorder = IN.uv0;
      return OUT;
    }

    half4 FragFront(Varyings IN) : SV_Target {
      half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMain) * _Color;
      half4 border = SAMPLE_TEXTURE2D(_BorderTex, sampler_BorderTex, IN.uvBorder) * _Color;
      half3 emission = c.rgb + border.rgb;
      half alpha = c.a * 0.25h + border.a;
      return half4(emission, alpha);
    }
    ENDHLSL
  }

  Pass {
    Name "BackFace"
    Tags { "LightMode"="UniversalForward" }
    Cull Front
    HLSLPROGRAM
    #pragma vertex Vert
    #pragma fragment FragBack
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_BackTex);
    SAMPLER(sampler_BackTex);
    TEXTURE2D(_BorderTex);
    SAMPLER(sampler_BorderTex);

    CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _BackColor;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      float2 uv0 : TEXCOORD0;
    };

    struct Varyings {
      float4 positionHCS : SV_POSITION;
      float2 uvMain : TEXCOORD0;
      float2 uvBorder : TEXCOORD1;
    };

    Varyings Vert(Attributes IN) {
      Varyings OUT;
      OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
      OUT.uvMain = IN.uv0;
      OUT.uvBorder = IN.uv0;
      return OUT;
    }

    half4 FragBack(Varyings IN) : SV_Target {
      half4 c = SAMPLE_TEXTURE2D(_BackTex, sampler_BackTex, IN.uvMain) * _BackColor;
      half4 border = SAMPLE_TEXTURE2D(_BorderTex, sampler_BorderTex, IN.uvBorder) * _BackColor;
      half3 emission = c.rgb + border.rgb;
      half alpha = c.a * 0.25h + border.a;
      return half4(emission, alpha);
    }
    ENDHLSL
  }
}

Fallback Off
}
