//
// KinoVignette - Natural vignetting effect
//
// Copyright (C) 2015 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

// Modified by the Tilt Brush Authors.

// URP port of the original camera vignette and chromatic aberration.
Shader "Hidden/OpenBrush/CaptureVignette"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Falloff;
            float _ChromaticAberration;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 coord = (uv - 0.5) * 2;
                float radiusSquared = dot(coord, coord);
                float rf = sqrt(radiusSquared) * _Falloff;
                float rf2_1 = rf * rf + 1.0;
                float attenuation = 1.0 / (rf2_1 * rf2_1);
                float2 greenUv = uv - _BlitTexture_TexelSize.xy *
                    _ChromaticAberration * coord * radiusSquared;
                float4 green = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, greenUv);
                float4 redBlue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                return float4(float3(redBlue.r, green.g, redBlue.b) * attenuation, 1);
            }
            ENDHLSL
        }
    }
}
