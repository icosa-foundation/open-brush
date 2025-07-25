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

Shader "Brush/Special/Wireframe" {
Properties {
  _Opacity ("Opacity", Range(0, 1)) = 1
  _Dissolve ("Dissolve", Range(0, 1)) = 1
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One
  AlphaTest Greater .01
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma multi_compile __ SHADER_SCRIPTING_ON
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER
      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"

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

      uniform half _ClipStart;
      uniform half _ClipEnd;
      uniform half _Dissolve;
      uniform half _Opacity;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);

        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.texcoord = v.texcoord;
        o.color = v.color;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.id = (float2)v.id;
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        #ifdef SHADER_SCRIPTING_ON
        if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
        if (_Dissolve < 1 && Dither8x8(i.vertex.xy) >= _Dissolve) discard;
        #endif

        half w = 0;
#ifdef AUDIO_REACTIVE
        float waveform = (tex2D(_WaveFormTex, float2(i.texcoord.y,0)).r - .5f);
        float envelope = sin(i.texcoord.y * 3.141569);
        i.texcoord.x += waveform * envelope;
        w = ( abs(i.texcoord.x - .5) > .5) ? 1 : 0;
#else
        w = ( abs(i.texcoord.x - .5) > .45) ? 1 : 0;
        w += ( abs(i.texcoord.y - .5) > .45) ? 1 : 0;
#endif
        //float angle = atan2(i.texcoord.x, i.texcoord.y);
        //w += ( abs(angle - (3.14/4.0)) < .05) ? 1 : 0;
        float4 color = i.color * w;
        return color * _Opacity;
      }
      ENDCG
    }
  }
}
}
