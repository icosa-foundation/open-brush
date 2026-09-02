// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

Shader "Custom/HDR Panoramic Skybox"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex("Panorama", 2D) = "grey" {}
        _Tint("Tint", Color) = (0.5, 0.5, 0.5, 1)
        _Exposure("Exposure", Range(0, 8)) = 1
        _Rotation("Rotation", Range(0, 360)) = 0
        [Enum(Mono, 0, StereoOverUnder, 2)] _Layout("Layout", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Tint;
            float _Exposure;
            float _Rotation;
            float _Layout;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateAroundY(float3 direction, float degrees)
            {
                float angle = radians(degrees);
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float3(
                    cosine * direction.x - sine * direction.z,
                    direction.y,
                    sine * direction.x + cosine * direction.z);
            }

            float2 ToRadialCoords(float3 direction)
            {
                direction = normalize(direction);
                float latitude = acos(direction.y);
                float longitude = atan2(direction.z, direction.x);
                return float2(
                    longitude * (0.5 / UNITY_PI) + 0.5,
                    1.0 - latitude / UNITY_PI);
            }

            // The texture remains HDR. Only the displayed skybox is mapped into the output range.
            float3 ToneMap(float3 color)
            {
                const float a = 2.51;
                const float b = 0.03;
                const float c = 2.43;
                const float d = 0.59;
                const float e = 0.14;
                // Normalize the curve's a/c asymptote to 1 so finite highlights approach white
                // without crossing it and requiring a hard clamp.
                return ((color * (a * color + b)) / (color * (c * color + d) + e)) * (c / a);
            }

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.vertex = UnityObjectToClipPos(input.vertex);
                output.direction = RotateAroundY(input.vertex.xyz, _Rotation);
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = ToRadialCoords(input.direction);
                if (_Layout > 1.5)
                {
                    uv.y = (uv.y + unity_StereoEyeIndex) * 0.5;
                }

                float3 color = tex2D(_MainTex, uv).rgb;
                color *= _Tint.rgb * unity_ColorSpaceDouble.rgb;
                color *= _Exposure;
                return half4(ToneMap(max(color, 0.0)), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
