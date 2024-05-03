// Copyright 2020 The Tilt Brush Authors
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

Shader "Custom/UnlitHDRColorButton" {
    Properties {
        _Color ("Color", Color) = (0,0,0,0)
        _SecondaryColor ("Fade Color", Color) = (0,0,0,0)
        _EdgeFalloff ("Edge Falloff", Float) = 2.0
        _EdgeWidth ("Edge Width", Float) = 1.5
    }
    SubShader {

        Tags {"RenderType"="Opaque"}
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #pragma target 3.0

            uniform float4 _Color;
            uniform float4 _SecondaryColor;
            float _EdgeFalloff;
            float _EdgeWidth;

            struct Input {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (Input v) {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(Input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : COLOR {
                float vignette = pow( abs(i.uv - .5) * _EdgeWidth, _EdgeFalloff);
                return lerp(_Color, _SecondaryColor, saturate(vignette));
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
