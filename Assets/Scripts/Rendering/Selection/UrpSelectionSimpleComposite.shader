Shader "Hidden/UrpSelectionSimpleComposite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_SimpleSelectionColor);
            TEXTURE2D_X(_SimpleSelectionMask);

            float4 _SimpleSelectionMask_TexelSize;
            float _SimpleSelectionColorFlipY;
            float _SimpleSelectionMaskFlipY;
            float _SimpleSelectionMode;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = float4(input.positionOS.xy, input.positionOS.z, 1.0);
                output.uv = input.uv;
                return output;
            }

            half MaskAt(float2 uv)
            {
                uv.y = lerp(uv.y, 1.0 - uv.y, saturate(_SimpleSelectionMaskFlipY));
                return SAMPLE_TEXTURE2D_X(
                    _SimpleSelectionMask,
                    sampler_LinearClamp,
                    UnityStereoTransformScreenSpaceTex(uv)).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float2 colorUv = uv;
                colorUv.y = lerp(
                    colorUv.y,
                    1.0 - colorUv.y,
                    saturate(_SimpleSelectionColorFlipY));
                float2 stereoUv = UnityStereoTransformScreenSpaceTex(colorUv);
                half4 source = SAMPLE_TEXTURE2D_X(
                    _SimpleSelectionColor,
                    sampler_LinearClamp,
                    stereoUv);

                half center = MaskAt(uv);
                float2 texel = _SimpleSelectionMask_TexelSize.xy;
                half outer = center;
                if (_SimpleSelectionMode < 1.5)
                {
                    outer = max(outer, MaskAt(uv + texel * float2( 2.0,  0.0)));
                    outer = max(outer, MaskAt(uv + texel * float2(-2.0,  0.0)));
                    outer = max(outer, MaskAt(uv + texel * float2( 0.0,  2.0)));
                    outer = max(outer, MaskAt(uv + texel * float2( 0.0, -2.0)));
                }
                if (_SimpleSelectionMode < 0.5)
                {
                    outer = max(outer, MaskAt(uv + texel * float2( 1.5,  1.5)));
                    outer = max(outer, MaskAt(uv + texel * float2(-1.5,  1.5)));
                    outer = max(outer, MaskAt(uv + texel * float2( 1.5, -1.5)));
                    outer = max(outer, MaskAt(uv + texel * float2(-1.5, -1.5)));
                }

                half outline = (_SimpleSelectionMode > 1.5)
                    ? 0.0h
                    : saturate(outer - center);
                half pulse = 0.65h + 0.35h * sin(_Time.y * 5.0h);
                half3 selectionColor = lerp(
                    half3(0.10h, 0.85h, 1.0h),
                    half3(1.0h, 1.0h, 1.0h),
                    pulse);

                half tintStrength = (_SimpleSelectionMode > 1.5) ? 0.35h : 0.28h;
                half sourceStrength = 1.0h - tintStrength;
                half3 tinted = lerp(
                    source.rgb,
                    source.rgb * sourceStrength + selectionColor * tintStrength,
                    center);
                tinted = lerp(tinted, selectionColor, outline * 0.9h);
                return half4(tinted, source.a);
            }
            ENDHLSL
        }
    }
}
