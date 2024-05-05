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

Shader "Brush/Visualizer/RainbowTube" {
Properties {
  _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5

  [Toggle] _OverrideTime ("Overriden Time", Float) = 0.0
  _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
  _TimeBlend("Time Blend", Float) = 0
  _TimeSpeed("Time Speed", Float) = 1.0

  _Dissolve ("Dissolve", Range(0, 1)) = 1
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One //SrcAlpha One
  BlendOp Add, Min
  AlphaTest Greater .01
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0
      #pragma multi_compile_particles
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON
      #pragma target 3.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/TimeOverride.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      float _EmissionGain;

      uniform float _ClipStart;
      uniform float _ClipEnd;
      uniform half _Dissolve;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        uint id : SV_VertexID;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 pos : POSITION;
        float4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float4 unbloomedColor : TEXCOORD1;
        uint id : TEXCOORD2;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);
        v.color = TbVertToSrgb(v.color);
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.pos = UnityObjectToClipPos(v.vertex);
        o.texcoord = v.texcoord;
        o.color = bloomColor(v.color, _EmissionGain);
        o.unbloomedColor = v.color;
        o.id = (float2)v.id;
        return o;
      }

      // Input color is srgb
      fixed4 frag (v2f i) : COLOR
      {
        if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
        // It's hard to get alpha curves right so use dithering for hdr shaders
        if (_Dissolve < 1 && Dither8x8(i.pos.xy) >= _Dissolve) discard;

        // Envelope
        float envelope = sin(i.texcoord.x * 3.14159);
        i.texcoord.y += i.texcoord.x * 3 + _BeatOutputAccum.b*3;
#ifdef AUDIO_REACTIVE
        float waveform_r =  .5*(tex2D(_WaveFormTex, float2(i.texcoord.x,0)).r - .5f);
        float waveform_g =  .5*(tex2D(_WaveFormTex, float2(i.texcoord.x*1.8,0)).r - .5f);
        float waveform_b =  .5*(tex2D(_WaveFormTex, float2(i.texcoord.x*2.4,0)).r - .5f);
#else
        float waveform_r = .15 * sin( -20 * i.unbloomedColor.r * GetTime().w + i.texcoord.x * 100 * i.unbloomedColor.r);
        float waveform_g = .15 * sin( -30 * i.unbloomedColor.g * GetTime().w + i.texcoord.x * 100 * i.unbloomedColor.g);
        float waveform_b = .15 * sin( -40 * i.unbloomedColor.b * GetTime().w + i.texcoord.x * 100 * i.unbloomedColor.b);
#endif
          i.texcoord.y = fmod(i.texcoord.y + i.texcoord.x, 1);
        float procedural_line_r = saturate(1 - 40*abs(i.texcoord.y - .5 + waveform_r));
        float procedural_line_g = saturate(1 - 40*abs(i.texcoord.y - .5 + waveform_g));
        float procedural_line_b = saturate(1 - 40*abs(i.texcoord.y - .5 + waveform_b));
        float4 color = procedural_line_r * float4(1,0,0,0) + procedural_line_g * float4(0,1,0,0) + procedural_line_b * float4(0,0,1,0);
        color.w = 1;
        color = i.color * color;
        color = encodeHdr(color.rgb * color.a);
        color = SrgbToNative(color);
        FRAG_MOBILESELECT(color)
        return color * _Dissolve;
      }
      ENDCG
    }
  }
}
}
