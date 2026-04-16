// Copyright 2020 The Tilt Brush Authors
Shader "Custom/ScrollingIcons" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _BaseDiffuseColor ("Base Diffuse Color", Color) = (.2,.2,.2,0)
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _IconsTex ("Icons Texture", 2D) = "white" {}
    _EmissionColor ("Emission Color", Color) = (1,1,1,1)
    _Ratio ("Scroll Ratio", Float) = 1
    _IconCount ("Icon Count", Float) = 1
    _UsedIconCount ("Used Icon Count", Float) = 1
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
      TEXTURE2D(_IconsTex); SAMPLER(sampler_IconsTex);
      CBUFFER_START(UnityPerMaterial)
      half4 _Color; half4 _BaseDiffuseColor; half4 _EmissionColor; half _Ratio; half _IconCount; half _UsedIconCount; float4 _MainTex_ST;
      CBUFFER_END
      struct A{float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
      struct V{float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0;};
      V Vert(A i){V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}
      half4 Frag(V i):SV_Target {
        half4 c = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
        float2 scrolledUVs = i.uv;
        scrolledUVs.x /= max(_IconCount, 1e-4h);
        scrolledUVs.x += _Ratio * (_UsedIconCount / max(_IconCount,1e-4h));
        half4 icon = SAMPLE_TEXTURE2D(_IconsTex,sampler_IconsTex,scrolledUVs);
        half4 f = lerp(icon, c, c.a);
        f *= _Color + _EmissionColor;
        half3 albedo = f.rgb + _BaseDiffuseColor.rgb;
        half3 emissive = f.rgb * normalize(_EmissionColor.rgb + 1e-5h);
        return half4(albedo + emissive,1);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
