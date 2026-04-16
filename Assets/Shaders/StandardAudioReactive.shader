// Copyright 2020 The Tilt Brush Authors
Shader "Custom/Standard_AudioReactive" {
  Properties {
    _EmissionColor ("Color", Color) = (1,1,1,1)
    _MainTex ("Albedo (RGB)", 2D) = "white" {}
    _Glossiness ("Smoothness", Range(0,1)) = 0.5
    _Metallic ("Metallic", Range(0,1)) = 0.0
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+20" "RenderType"="Opaque" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #pragma multi_compile __ AUDIO_REACTIVE
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      TEXTURE2D(_WaveFormTex); SAMPLER(sampler_WaveFormTex);

      CBUFFER_START(UnityPerMaterial)
      half4 _EmissionColor;
      float4 _MainTex_ST;
      CBUFFER_END

      float4 _BeatOutput;

      struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
      struct V { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };
      V Vert(A i){V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}

      half4 Frag(V i):SV_Target {
        half index = i.uv.y;
        half4 c;
        #ifdef AUDIO_REACTIVE
        half wav = SAMPLE_TEXTURE2D(_WaveFormTex, sampler_WaveFormTex, float2(index,0)).r - 0.5h;
        c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + half2(wav,0) * 0.1h) * _EmissionColor;
        c += c * _BeatOutput.x * 10.0h;
        #else
        c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _EmissionColor;
        #endif
        return half4(c.rgb, c.a);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
