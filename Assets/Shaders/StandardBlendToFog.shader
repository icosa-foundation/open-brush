// Copyright 2020 The Tilt Brush Authors
Shader "Custom/StandardBlendToFog" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
  _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
  _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
  _BumpMap ("Normalmap", 2D) = "bump" {}
  _WorldSpaceFogRange("Fog Range", Vector) = (75.0, 100.0, 0)
}
SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "RenderType"="Opaque" }
  LOD 200
  Pass {
    Tags { "LightMode"="UniversalForward" }
    HLSLPROGRAM
    #pragma vertex Vert
    #pragma fragment Frag
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
    CBUFFER_START(UnityPerMaterial)
    half4 _Color; half4 _SpecColor; half _Shininess; half2 _WorldSpaceFogRange; float4 _MainTex_ST;
    CBUFFER_END

    struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
    struct V { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float3 worldPos:TEXCOORD1; };

    V Vert(A i){V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.worldPos=TransformObjectToWorld(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}

    half4 Frag(V i):SV_Target {
      half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
      half3 albedo = tex.rgb * _Color.rgb;
      half dist = length(i.worldPos);
      half falloff = smoothstep(_WorldSpaceFogRange.x, _WorldSpaceFogRange.y, dist);
      half3 fogColor = MixFog(half3(0,0,0), 1.0h);
      half3 col = lerp(albedo + _SpecColor.rgb * _Shininess, fogColor, falloff);
      return half4(col, 1);
    }
    ENDHLSL
  }
}
FallBack Off
}
