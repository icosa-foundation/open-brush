Shader "Hidden/Open Brush/URP Encoded Bloom"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _EncodedBloomTexelSize;
        float _EncodedBloomAmount;
        float _EncodedBloomThreshold;
        float _EncodedBloomUpdateEye;
        float4x4 _EncodedBloomLeftWarp;
        float4x4 _EncodedBloomRightWarp;

        void SelectUpdateEye()
        {
            if (_EncodedBloomUpdateEye >= 0)
                clip(0.5 - abs((float)unity_StereoEyeIndex - _EncodedBloomUpdateEye));
        }

        float3 DecodeGlow(float2 uv)
        {
            float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            if (color.a >= 1.0) return 0;
            // Match the old mobile HDR_SIMPLE decoder: high bits are the visible core,
            // low three RGB bits carry glow colour; alpha carries its exponent.
            uint3 bytes = (uint3)round(saturate(color.rgb) * 255.0);
            bytes *= (uint3)(bytes > 240u);
            float3 glow = float3(bytes & 7u) / 7.0;
            glow *= exp2((1.0 - color.a) * 16.0);
            // Zero reproduces the old extraction. A runtime threshold can trim the signal.
            float brightness = max(glow.r, max(glow.g, glow.b));
            return glow * max(brightness - _EncodedBloomThreshold, 0.0) / max(brightness, 1e-4);
        }

        float3 SampleGlow(float2 uv)
        {
            return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
        }

        half4 Extract(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            SelectUpdateEye();
            float2 offset = _EncodedBloomTexelSize.xy;
            float2 uv = input.texcoord;
            float3 glow = DecodeGlow(uv + offset * float2(-1, -1));
            glow += DecodeGlow(uv + offset * float2(1, -1));
            glow += DecodeGlow(uv + offset * float2(-1, 1));
            glow += DecodeGlow(uv + offset * float2(1, 1));
            return half4(glow * 0.25, 0);
        }

        half4 Blur(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            SelectUpdateEye();
            float2 offset = _EncodedBloomTexelSize.xy;
            float2 uv = input.texcoord;
            float3 glow = SampleGlow(uv + offset * float2(-1, -1));
            glow += SampleGlow(uv + offset * float2(1, -1));
            glow += SampleGlow(uv + offset * float2(-1, 1));
            glow += SampleGlow(uv + offset * float2(1, 1));
            return half4(glow * (0.25 * _EncodedBloomAmount), 0);
        }
        half4 Composite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            float2 clipXY = uv * 2 - 1;
            #if UNITY_UV_STARTS_AT_TOP
            clipXY.y = -clipXY.y;
            #endif
            float4 position = float4(clipXY, 0.5, 1);
            float4 history = unity_StereoEyeIndex == 0
                ? mul(_EncodedBloomLeftWarp, position) : mul(_EncodedBloomRightWarp, position);
            if (history.w <= 0) return 0;
            uv = history.xy / history.w;
            #if UNITY_UV_STARTS_AT_TOP
            uv.y = -uv.y;
            #endif
            uv = uv * 0.5 + 0.5;
            if (any(uv < 0) || any(uv > 1)) return 0;
            input.texcoord = uv;
            return Blur(input);
        }
        ENDHLSL

        Pass
        {
            Name "Extract"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Extract
            ENDHLSL
        }
        Pass
        {
            Name "Downsample"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Blur
            ENDHLSL
        }
        Pass
        {
            Name "Add glow"
            Blend One One
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Blur
            ENDHLSL
        }
        Pass
        {
            Name "Composite history"
            Blend One One
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
}
