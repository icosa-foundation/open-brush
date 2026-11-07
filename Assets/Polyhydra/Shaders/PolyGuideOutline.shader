Shader "PolyGuideOutline"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _EdgeDistance ("Edge Distance", Range(0,1)) = .1
        _EdgeThickness ("Edge Thickness", Range(0,.5)) = .1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "GuideOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Polyhydra/Shaders/PolyhydraEdges.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _EdgeDistance;
                float _EdgeThickness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 edgeUV : TEXCOORD1;
                float4 barycentric : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 edgeUV : TEXCOORD0;
                float4 barycentric : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.edgeUV = input.edgeUV;
                output.barycentric = input.barycentric;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float edge = PolyhydraEdgeMask(input.edgeUV.x, input.barycentric.xyz, _EdgeDistance, _EdgeThickness);
                return half4(_BaseColor.rgb, edge);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
