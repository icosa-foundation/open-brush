// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0

Shader "Custom/StandardforIcons" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _EmissionColor ("Emission Color", Color) = (1,1,1,1)
  _Shininess ("Smoothness", Range (0.01, 1)) = 0.013
  _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
}
SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  LOD 100
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
    half4 _Color; half4 _EmissionColor; float4 _MainTex_ST;
    CBUFFER_END
    struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0;
      UNITY_VERTEX_INPUT_INSTANCE_ID
    };
    struct V { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0;
      UNITY_VERTEX_INPUT_INSTANCE_ID
      UNITY_VERTEX_OUTPUT_STEREO
    };
    V Vert(A i){V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionHCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=TRANSFORM_TEX(i.uv,_MainTex); return o;}
    half4 Frag(V i):SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); half4 tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv); return half4(tex.rgb*_Color.rgb + tex.rgb*_EmissionColor.rgb,1); }
    ENDHLSL
  }
}
FallBack Off
}
