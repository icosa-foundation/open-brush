// Copyright 2020 The Tilt Brush Authors
Shader "Custom/StandardWithOutline" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _EmissionColor ("Emission Color", Color) = (1,1,1,1)
  _Shininess ("Smoothness", Range (0.01, 1)) = 0.013
  _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
  _OutlineWidth("Outline Width", Float) = 0.02
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
    half4 _Color; half4 _EmissionColor; float4 _MainTex_ST;
    CBUFFER_END
    struct A{float4 positionOS:POSITION; float2 uv:TEXCOORD0;};
    struct V{float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0;};
    V Vert(A i){V o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}
    half4 Frag(V i):SV_Target { half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv); return half4(tex.rgb*_Color.rgb + tex.rgb*_EmissionColor.rgb,1); }
    ENDHLSL
  }

  Pass {
    Tags { "LightMode"="SRPDefaultUnlit" }
    Cull Front
    HLSLPROGRAM
    #pragma vertex VertOutline
    #pragma fragment FragOutline
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Assets/Shaders/Include/Math.cginc"
    CBUFFER_START(UnityPerMaterial)
    float _OutlineWidth;
    CBUFFER_END
    struct A{float4 positionOS:POSITION; float3 normalOS:NORMAL;};
    struct V{float4 positionHCS:SV_POSITION;};
    V VertOutline(A v){
      V o;
      float4 worldPos = mul(unity_ObjectToWorld, v.positionOS);
      float3x3 unscaledObject2World; float3 unusedScale;
      factorRotationAndLocalScale((float3x3)unity_ObjectToWorld, unscaledObject2World, unusedScale);
      float3 worldNormal = normalize(mul(unscaledObject2World, v.normalOS));
      worldPos.xyz += worldNormal * _OutlineWidth;
      o.positionHCS = TransformWorldToHClip(worldPos.xyz);
      return o;
    }
    half4 FragOutline(V i):SV_Target { return half4(0,0,0,1); }
    ENDHLSL
  }
}
FallBack Off
}
