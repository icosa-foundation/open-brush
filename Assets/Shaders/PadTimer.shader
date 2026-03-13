// Copyright 2020 The Tilt Brush Authors
Shader "Custom/PadTimer" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _BaseDiffuseColor ("Base Diffuse Color", Color) = (.2,.2,.2,1)
    _MainTex ("Texture", 2D) = "white" {}
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _Shininess("Smoothness", Range(0.01, 1)) = 0.013
    _Ratio ("Scroll Ratio", Float) = 1
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
    LOD 100
    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
      CBUFFER_START(UnityPerMaterial)
      half4 _Color; half4 _BaseDiffuseColor; half4 _EmissionColor; half _Ratio; float4 _MainTex_ST;
      CBUFFER_END
      struct A{float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
      struct V{float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0;};
      V Vert(A i){V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}
      half4 Frag(V i):SV_Target {
        half adjustedRatio = 0.5h - _Ratio;
        half4 c = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
        half angle = atan2(i.uv.x - 0.5h, -i.uv.y + 0.5h) / (6.28318h);
        if (angle > adjustedRatio) c = 1 - c;
        c *= _Color + _EmissionColor;
        half3 albedo = c.rgb + _BaseDiffuseColor.rgb;
        half3 emissive = c.rgb * normalize(_EmissionColor.rgb + 1e-5h);
        return half4(albedo + emissive,1);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
