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

Shader "Brush/Special/Drafting" {
  Properties {
    _MainTex ("Texture", 2D) = "white" {}

    _Opacity ("Opacity", Range(0, 1)) = 1
    _Dissolve ("Dissolve", Range(0, 1)) = 1
	  _ClipStart("Clip Start", Float) = 0
	  _ClipEnd("Clip End", Float) = -1
  }

  SubShader {
    Tags {
      "Queue"="Transparent"
      "IgnoreProjector"="True"
      "RenderType"="Transparent"
    }
    Blend One One // SrcAlpha One
    BlendOp Add, Min
    AlphaTest Greater .01
    ColorMask RGBA
    Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"

      uniform float _DraftingVisibility01;
      sampler2D _MainTex;

      uniform half _ClipStart;
      uniform half _ClipEnd;
      uniform half _Dissolve;
      uniform half _Opacity;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
        float2 texcoord : TEXCOORD0;
        uint id : SV_VertexID;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        uint id : TEXCOORD2;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      float4 _MainTex_ST;

      v2f vert (appdata_t v) {
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = v.color * _DraftingVisibility01;
        o.id = (float2)v.id;
        return o;
      }

      fixed4 frag (v2f i) : COLOR {

        #ifdef SHADER_SCRIPTING_ON
        if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
        if (_Dissolve < 1 && Dither8x8(i.vertex.xy) >= _Dissolve) discard;
        #endif

        half4 c = i.color * tex2D(_MainTex, i.texcoord );
        c = encodeHdr(c.rgb * c.a * _Opacity);
        return c * _Opacity;
      }
      ENDCG
    }
  }
}
