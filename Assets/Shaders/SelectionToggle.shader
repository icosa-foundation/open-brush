// Copyright 2020 The Tilt Brush Authors
Shader "Custom/SelectionToggle" {
  Properties {
    _Color ("Color", Color) = (0,0,0,1)
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _Shininess("Smoothness", Range(0.01,1)) = 0.13
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial)
      half4 _Color; half4 _EmissionColor; float4 _MainTex_ST;
      CBUFFER_END
      struct A{float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
      struct V{float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0;};
      V Vert(A i){V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}
      half4 Frag(V i):SV_Target {
        half duration = 4.0h;
        half t = fmod(_Time.w, duration);
        t = saturate(t - (duration - 1.0h));
        t = (sin(t + 1.0h)) / 2.0h;
        t = smoothstep(0.0h, 0.75h, t);
        float2 scrollUV = i.uv;
        half scale = lerp(1.5h, 0.8h, t);
        scrollUV = (scrollUV - 0.5h) * scale + 0.5h;
        half animated_tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrollUV).a;
        half3 static_tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
        half3 finalTex = animated_tex + static_tex;
        half3 emission = finalTex * _EmissionColor.rgb;
        half3 albedo = finalTex * _Color.rgb;
        return half4(albedo + emission, 1.0h);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
