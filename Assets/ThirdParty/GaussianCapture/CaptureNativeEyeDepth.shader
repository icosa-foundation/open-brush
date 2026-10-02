Shader "Hidden/CaptureNativeEyeDepth"
{
    Properties
    {
        _CaptureClipPlanes ("Capture clip planes", Vector) = (0.1,1000,0,1)
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_local __ CAPTURE_DEPTH_MSAA
            #include "UnityCG.cginc"
            CBUFFER_START(UnityPerMaterial)
            float4 _CaptureClipPlanes;
            CBUFFER_END
            #if defined(CAPTURE_DEPTH_MSAA)
            Texture2DMS<float> _CaptureNativeDepth;
            #else
            UNITY_DECLARE_DEPTH_TEXTURE(_CaptureNativeDepth);
            #endif

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float frag(v2f input) : SV_Target
            {
                #if defined(CAPTURE_DEPTH_MSAA)
                uint width, height, samples;
                _CaptureNativeDepth.GetDimensions(width, height, samples);
                int2 pixel = min(int2(input.uv * float2(width, height)), int2(width - 1, height - 1));
                float rawDepth = _CaptureNativeDepth.Load(pixel, 0);
                for (uint sample = 1; sample < samples; ++sample)
                {
                    float candidate = _CaptureNativeDepth.Load(pixel, sample);
                    rawDepth = _CaptureClipPlanes.w > 0 ? max(rawDepth, candidate) : min(rawDepth, candidate);
                }
                #else
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CaptureNativeDepth, input.uv);
                #endif
                float depth = lerp(rawDepth, 1 - rawDepth, _CaptureClipPlanes.w);
                float nearPlane = _CaptureClipPlanes.x;
                float farPlane = _CaptureClipPlanes.y;
                float perspectiveDepth = nearPlane * farPlane / lerp(farPlane, nearPlane, depth);
                return lerp(perspectiveDepth, lerp(nearPlane, farPlane, depth), _CaptureClipPlanes.z);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
