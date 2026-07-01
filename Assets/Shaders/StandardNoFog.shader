// Copyright 2020 The Tilt Brush Authors
Shader "Custom/Standard_NoFog"
{
    Properties
    {
        _Color ("Color", Color) = (0,0,0,1)
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+20" "RenderType"="Opaque"
        }
        Pass
        {
            Tags
            {
                "LightMode"="UniversalForward"
            }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _EmissionColor;
                float4 _MainTex_ST;
                float _Glossiness;
                float _Metallic;
            CBUFFER_END

            struct A {
                float4 positionOS:POSITION;
                float2 uv:TEXCOORD0;

              UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct V {
                float4 positionHCS:SV_POSITION;
                float2 uv:TEXCOORD0;

              UNITY_VERTEX_INPUT_INSTANCE_ID
              UNITY_VERTEX_OUTPUT_STEREO
            };

            V Vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionHCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 emissionMask = tex.rgb * tex.rgb;
                return half4(tex.rgb * _Color.rgb + emissionMask * _EmissionColor.rgb, tex.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
