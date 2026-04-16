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

Shader "Custom/ToolPanel" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
}

SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
  LOD 200
  Cull Off

  Pass {
    Name "PanelOuter"
    Tags { "LightMode"="UniversalForward" }
    Blend SrcAlpha OneMinusSrcAlpha
    ZWrite Off
    HLSLPROGRAM
    #pragma vertex VertOuter
    #pragma fragment FragOuter
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
    half4 _Color;
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

    Varyings VertOuter(Attributes IN) {
      Varyings OUT;
      float3 offsetPos = IN.positionOS.xyz - IN.normalOS * 0.3;
      OUT.positionHCS = TransformObjectToHClip(offsetPos);
      OUT.uv = IN.uv;
      return OUT;
    }

    half4 FragOuter(Varyings IN) : SV_Target {
      half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
      return half4(c.rgb, c.a * 0.25h);
    }
    ENDHLSL
  }

  Pass {
    Name "PanelGlow"
    Tags { "LightMode"="UniversalForward" }
    Blend One One
    ZWrite Off
    HLSLPROGRAM
    #pragma vertex VertGlow
    #pragma fragment FragGlow
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
    half4 _Color;
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

    Varyings VertGlow(Attributes IN) {
      Varyings OUT;
      float3 offsetPos = IN.positionOS.xyz - IN.normalOS * 0.15;
      OUT.positionHCS = TransformObjectToHClip(offsetPos);
      OUT.uv = IN.uv;
      return OUT;
    }

    half4 FragGlow(Varyings IN) : SV_Target {
      half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
      return half4(c.rgb * 0.7h, c.a);
    }
    ENDHLSL
  }

  Pass {
    Name "PanelMain"
    Tags { "LightMode"="UniversalForward" }
    Blend SrcAlpha OneMinusSrcAlpha
    ZWrite Off
    HLSLPROGRAM
    #pragma vertex VertMain
    #pragma fragment FragMain
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
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

    Varyings VertMain(Attributes IN) {
      Varyings OUT;
      OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
      OUT.uv = IN.uv;
      return OUT;
    }

    half4 FragMain(Varyings IN) : SV_Target {
      half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
      return c;
    }
    ENDHLSL
  }
}

Fallback Off
}
