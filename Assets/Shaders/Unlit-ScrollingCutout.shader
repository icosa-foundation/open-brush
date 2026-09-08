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

Shader "Unlit/ScrollingCutout" {
Properties {
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  _Color ("Color", Color) = (1,1,1,1)
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout" }

  Pass {
    Tags { "LightMode"="UniversalForward" }
    HLSLPROGRAM
    #pragma vertex Vert
    #pragma fragment Frag
    #pragma multi_compile_instancing
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half _Cutoff;
    float4 _MainTex_ST;
    CBUFFER_END

    struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0;
      UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0;
      UNITY_VERTEX_INPUT_INSTANCE_ID
      UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes IN) {
      Varyings OUT;
      UNITY_SETUP_INSTANCE_ID(IN);
      UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
      UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
      OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
      OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
      return OUT;
    }

    half4 Frag(Varyings IN) : SV_Target {
      UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
      float2 timeUVs = IN.uv;
      timeUVs.x += _Time.x * 2.0;
      timeUVs.y -= _Time.x * 1.0;
      half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, timeUVs) * _Color;
      clip(c.a - _Cutoff);
      return half4(c.rgb, 1.0h);
    }
    ENDHLSL
  }

  Pass {
    Tags { "LightMode"="ShadowCaster" }
    HLSLPROGRAM
    #pragma vertex Vert
    #pragma fragment FragShadow
    #pragma multi_compile_instancing
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
    half _Cutoff;
    float4 _MainTex_ST;
    CBUFFER_END

    struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0;
      UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0;
      UNITY_VERTEX_INPUT_INSTANCE_ID
      UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes IN) {
      Varyings OUT;
      UNITY_SETUP_INSTANCE_ID(IN);
      UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
      UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
      OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
      OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
      return OUT;
    }

    half4 FragShadow(Varyings IN) : SV_Target {
      UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
      float2 timeUVs = IN.uv;
      timeUVs.x += _Time.x * 2.0;
      timeUVs.y -= _Time.x * 1.0;
      half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, timeUVs).a;
      clip(a - _Cutoff);
      return 0;
    }
    ENDHLSL
  }
}

Fallback Off
}
