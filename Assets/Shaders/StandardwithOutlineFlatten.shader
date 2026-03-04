// Copyright 2020 The Tilt Brush Authors
Shader "Custom/StandardWithOutlineFlatten" {
  Properties{
    _Color("Main Color", Color) = (1,1,1,1)
    _MainTex("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _Shininess("Smoothness", Range(0.01, 1)) = 0.013
    _OutlineWidth("Outline Width", Float) = 0.02
    _FlattenAmount("Flatten Amount", Range(1, 0)) = 0
  }
  SubShader{
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
    LOD 100

    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      CBUFFER_START(UnityPerMaterial)
      half4 _Color; half _FlattenAmount;
      CBUFFER_END
      struct A{float4 positionOS:POSITION;};
      struct V{float4 positionHCS:SV_POSITION;};
      V Vert(A v){V o; float3 p=v.positionOS.xyz; p.z = p.z - p.z * _FlattenAmount; o.positionHCS=TransformObjectToHClip(p); return o;}
      half4 Frag(V i):SV_Target { return half4(_Color.rgb,1); }
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
      float _OutlineWidth; float _FlattenAmount;
      CBUFFER_END
      struct A{float4 positionOS:POSITION; float3 normalOS:NORMAL;};
      struct V{float4 positionHCS:SV_POSITION;};
      V VertOutline(A v){
        V o;
        float3 p = v.positionOS.xyz;
        p.z = p.z - p.z * _FlattenAmount;
        float4 worldPos = mul(unity_ObjectToWorld, float4(p,1));
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

    Pass {
      Tags { "LightMode"="ShadowCaster" }
      HLSLPROGRAM
      #pragma vertex VertShadow
      #pragma fragment FragShadow
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      CBUFFER_START(UnityPerMaterial)
      float _FlattenAmount;
      CBUFFER_END
      struct A{float4 positionOS:POSITION;};
      struct V{float4 positionCS:SV_POSITION;};
      V VertShadow(A v){V o; float3 p=v.positionOS.xyz; p.z=p.z-p.z*_FlattenAmount; o.positionCS=TransformObjectToHClip(p); return o;}
      half4 FragShadow(V i):SV_Target { return 0; }
      ENDHLSL
    }
  }
  FallBack Off
}
