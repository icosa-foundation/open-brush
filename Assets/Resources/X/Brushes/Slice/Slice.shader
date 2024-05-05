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

Shader "Brush/Special/Slice" {
Properties {
    _MainTex ("Particle Texture", 2D) = "white" {}
    _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5

    _Opacity ("Opacity", Range(0, 1)) = 1
    _Dissolve ("Dissolve", Range(0, 1)) = 1
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1
}

Category {
    Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
    Blend SrcAlpha OneMinusSrcAlpha
    AlphaTest Greater .01
    ColorMask RGB
    Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

    SubShader {
        Pass {

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles

            #include "UnityCG.cginc"
            #include "Assets/Shaders/Include/ColorSpace.cginc"

            sampler2D _MainTex;

            struct appdata_t {
                float4 vertex : POSITION;
                // fixed4 color : COLOR;
                // float3 normal : NORMAL;
                float3 texcoord : TEXCOORD0;
                uint id : SV_VertexID;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : POSITION;
                // fixed4 color : COLOR;
                float3 texcoord : TEXCOORD0;
                uint id : TEXCOORD2;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _MainTex_ST;
            half _EmissionGain;

            uniform float _ClipStart;
            uniform float _ClipEnd;
            uniform half _Dissolve;
            uniform half _Opacity;

            v2f vert (appdata_t v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.id = (float2)v.id;
                return o;
            }

            float random( float2 p )
            {
              const float2 r = float2(23.14079263, 2.7651234);
              return frac( cos( fmod( 123432189., 1e-7 + 256. * dot(p,r) ) ) );
            }

            fixed4 frag (v2f i) : COLOR
            {
                if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
                if (_Dissolve < 1 && Dither8x8(i.pos.xy) >= _Dissolve) discard;

                //float rubbishRand = random(i.texcoord.xz);
                //clip(rubbishRand-.9);

                float4 tex;
                float hue = fmod(i.texcoord.z * 5, 6);
                float3 base_rgb = hue06_to_base_rgb(hue);
                tex.rgb = cy_to_rgb(base_rgb, i.texcoord.x, i.texcoord.y);
                tex.a = _Opacity;

                // With MSAA enabled, RGB values in tex are > 1.0, but was not intented.
                return saturate(tex);
            }
            ENDCG
        }
    }
}
}
