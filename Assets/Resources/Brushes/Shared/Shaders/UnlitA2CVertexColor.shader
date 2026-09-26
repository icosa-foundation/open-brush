Shader "Brush/UnlitA2CVertexColor"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _DitherStrength ("Dither Strength", Range(0,0.5)) = 0.125
        _OrderedDither ("Ordered Dither (0/1)", Float) = 0
        _AlphaBias ("Alpha Bias", Range(-1,1)) = 0
        _AlphaPower ("Alpha Power", Range(0.1,4)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            AlphaToMask On
            Blend Off
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _DitherStrength;
            float _OrderedDither;
            float _AlphaBias;
            float _AlphaPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                return output;
            }

            float OrderedDither4x4(float2 pixelPos)
            {
                // 4x4 Bayer matrix scaled to 0..1
                int2 p = int2(pixelPos) & 3;
                int index = p.x + p.y * 4;
                // Values: 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5
                int d = (index == 0) ? 0 :
                        (index == 1) ? 8 :
                        (index == 2) ? 2 :
                        (index == 3) ? 10 :
                        (index == 4) ? 12 :
                        (index == 5) ? 4 :
                        (index == 6) ? 14 :
                        (index == 7) ? 6 :
                        (index == 8) ? 3 :
                        (index == 9) ? 11 :
                        (index == 10) ? 1 :
                        (index == 11) ? 9 :
                        (index == 12) ? 15 :
                        (index == 13) ? 7 :
                        (index == 14) ? 13 :
                        5;
                return (d + 0.5) / 16.0;
            }

            float InterleavedGradientNoise(float2 pixelPos)
            {
                // Simple hash-based noise in 0..1
                float2 p = floor(pixelPos);
                float f = dot(p, float2(0.06711056, 0.00583715));
                return frac(52.9829189 * frac(f));
            }

            float ObjectSeed()
            {
                float3 t = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float h = dot(t, float3(0.1031, 0.11369, 0.13787));
                return frac(sin(h) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 c = input.color;
                float alpha = saturate(pow(saturate(c.a + _AlphaBias), _AlphaPower));

                float2 pixelPos = input.positionHCS.xy;
                float seed = ObjectSeed();
                pixelPos += seed * 4096.0;
                float ditherOrdered = OrderedDither4x4(pixelPos);
                float ditherNoise = InterleavedGradientNoise(pixelPos);
                float dither = lerp(ditherNoise, ditherOrdered, step(0.5, _OrderedDither));

                alpha = saturate(alpha + (dither - 0.5) * _DitherStrength);
                return half4(c.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
