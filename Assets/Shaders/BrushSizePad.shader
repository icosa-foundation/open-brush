// Copyright 2020 The Tilt Brush Authors
Shader "Custom/BrushSizePad" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _BaseDiffuseColor ("Base Diffuse Color", Color) = (1,1,1,0)
    _MainTex ("Texture", 2D) = "white" {}
    _Shininess("Smoothness", Range(0.01, 1)) = 0.013
    _BrushIconTex ("Brush Icon Texture", 2D) = "white" {}
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _Ratio ("Brush Size Ratio", Float) = 1
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
      TEXTURE2D(_BrushIconTex); SAMPLER(sampler_BrushIconTex);
      CBUFFER_START(UnityPerMaterial)
      half4 _Color; half4 _BaseDiffuseColor; half4 _EmissionColor; half _Ratio; float4 _MainTex_ST;
      CBUFFER_END
      struct A{float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
      struct V{float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0;};
      V Vert(A i){V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}
      half4 Frag(V i):SV_Target {
        half4 c = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
        float2 iconUVs=i.uv;
        iconUVs -= float2(0.5,0.5);
        iconUVs *= lerp(5.0,1.6,_Ratio);
        iconUVs += float2(0.5,0.5);
        iconUVs.x += lerp(1.925,-0.3,_Ratio);
        c.rgb += SAMPLE_TEXTURE2D(_BrushIconTex,sampler_BrushIconTex,iconUVs).rgb;
        if (iconUVs.x < lerp(0.4,0.55,_Ratio)) { c.rgb += c.a; }
        c *= _Color;
        half3 albedo = c.rgb + _BaseDiffuseColor.rgb;
        half3 emissive = c.rgb * normalize(_EmissionColor.rgb + 1e-5h);
        return half4(albedo + emissive,1);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
