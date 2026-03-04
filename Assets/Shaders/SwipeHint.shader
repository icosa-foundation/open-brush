// Copyright 2020 The Tilt Brush Authors
Shader "Custom/SwipeHint" {
  Properties{
    _MainTex("Base (RGB) Transgloss (A)", 2D) = "white" {}
    _Color("Color", Color) = (1,1,1,1)
    _Shininess("Smoothness", Range(0.01, 1)) = 0.13
    _PulseColor("Pulse Color", Color) = (3,3,3,3)
    _PulseColorDark("Pulse Color Dark", Color) = (3,3,3,3)
    _PulseFrequency ("Pulse Frequency", Float) = 10
    _PulseIntensity ("Pulse Intensity", Float) = 10
  }
  SubShader{
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType" = "Opaque" }
    LOD 100
    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial)
      half4 _Color; half4 _PulseColor; half4 _PulseColorDark; half _PulseFrequency; half _PulseIntensity; float4 _MainTex_ST;
      CBUFFER_END
      struct A{float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
      struct V{float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0;};
      V Vert(A i){V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}
      half4 Frag(V i):SV_Target {
        half4 tex = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
        half4 c = tex * _Color;
        half t = sin(_Time.y * _PulseFrequency) * 0.5h + 0.5h;
        half3 lerped = lerp((_PulseColor * c).rgb, (_PulseColorDark * c).rgb, t);
        return half4(lerped * _PulseIntensity, 1.0h);
      }
      ENDHLSL
    }
  }
  Fallback Off
}
