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

Shader "Custom/ReferenceImage" {
    Properties {
        _Color("Main Color", Color) = (1,1,1,1)
        _MainTex("Main Texture", 2D) = "white" {}
        _Aspect("Aspect Ratio", Float) = 1
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        _Grayscale("Grayscale", Float) = 0
		[Enum(UnityEngine.Rendering.CullMode)] _CullMode ("CullMode", Int) = 2
    }
    SubShader {
        Tags{ "Queue" = "AlphaTest+20" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
        Pass {
            Lighting Off
            Cull [_CullMode]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ SELECTION_ON HIGHLIGHT_ON
            #include "UnityCG.cginc"
            #include "Assets/Shaders/Include/Hdr.cginc"
            #include "Assets/Shaders/Include/MobileSelection.cginc"

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            uniform float _Activated;
            uniform float _Aspect;
            uniform float _Cutoff;
            uniform float _Grayscale;
            uniform float _LegacyReferenceImageTint;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v) {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                v.uv -= 0.5;

                // Landscape format images
                if (_Aspect > 1.0) {
                    v.uv.x /= _Aspect;
                }

                // Portrait format images
                else {
                    v.uv.y *= _Aspect;
                }

                v.uv += 0.5;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;

                if (c.a < _Cutoff) discard;

                // Config flag for reproducing old, broken behavior.
                if (_LegacyReferenceImageTint > 0) {
                    c.rgb *= .75;
                }

                if (_Grayscale == 1) {
                    float grayscale = dot(c.rgb, float3(0.3, 0.59, 0.11));
                    return encodeHdr(grayscale);
                }

                FRAG_MOBILESELECT(c)

                return encodeHdr(c);
            }
            ENDCG
        }
    }
}
