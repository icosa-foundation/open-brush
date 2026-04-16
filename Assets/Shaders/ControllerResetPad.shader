// Copyright 2020 The Tilt Brush Authors
Shader "Custom/ControllerResetPad" {
  Properties {
    _Color ("Color", Color) = (0,0,0,1)
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _Shininess ("Smoothness", Range(0.01,1)) = 0.13
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
        float2 UVs=i.uv;
        half duration=5.0h;
        half t=fmod(_Time.w, duration);
        t=saturate(t - (duration - 1.0h));
        t=sin(t * (3.14159h / 2.0h));
        half amount = 6.28318h * t + 0.4h;
        half s=sin(amount); half c=cos(amount);
        float2x2 rot=float2x2(c,-s,s,c);
        float2 animUV = mul(i.uv - 0.5h, rot) + 0.5h;
        half3 animated_tex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,animUV).rgb;

        t=fmod(_Time.w + 2.0h, duration);
        t=saturate(t / duration);
        t=(sin(t*3.14159h)+1.0h)/2.0h;
        t=1.0h - pow(t, 100.0h);
        t=smoothstep(0.0h,0.9h,t);

        half scale = lerp(1.2h,0.8h,t);
        UVs = (UVs - 0.5h) * scale + 0.5h;
        UVs.y += 0.035h;
        half person_tex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,UVs).a;
        half person_outline = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,(UVs - 0.5h) * 0.8h + 0.5h).a;

        half3 tex = animated_tex * (1.0h - person_outline) + person_tex;
        half3 emission = tex * _EmissionColor.rgb;
        half3 albedo = tex * _Color.rgb;
        return half4(albedo + emission,1);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
