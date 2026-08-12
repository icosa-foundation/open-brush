Shader "OpenBrush/XR Hand Transparent"
{
    Properties
    {
        _BaseColor ("Hand Color", Color) = (0.55, 0.55, 0.55, 1)
        _Opacity ("Opacity", Range(0,1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+901"
        }

        Pass
        {
            Name "HandColor"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back

            // Depth was already created by XR Hand Depth
            ZWrite Off

            // Only draw the nearest hand surface
            ZTest LEqual

            // REAL alpha transparency
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Opacity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return half4(
                    _BaseColor.rgb,
                    _Opacity * _BaseColor.a
                );
            }

            ENDHLSL
        }
    }

    FallBack Off
}