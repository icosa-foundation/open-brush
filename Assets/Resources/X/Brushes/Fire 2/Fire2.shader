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

Shader "Brush/Special/Fire2" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _DisplaceTex ("Displace Texture", 2D) = "white" {}

  _Scroll1 ("Scroll1", Float) = 0
  _Scroll2 ("Scroll2", Float) = 0
  _DisplacementIntensity("Displacement", Float) = .1
  _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5

  _FlameFadeMin ("Fade Flame Min", Float) = 1
  _FlameFadeMax ("Fade Flame Max", Float) = 30

      [Toggle] _OverrideTime ("Overriden Time", Float) = 0.0
  _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
  _TimeBlend("Time Blend", Float) = 0
  _TimeSpeed("Time Speed", Float) = 1.0

    _Dissolve("Dissolve", Range(0, 1)) = 1
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One // SrcAlpha One
  BlendOp Add, Min
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

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/TimeOverride.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"

      sampler2D _MainTex;
      sampler2D _DisplaceTex;

      uniform float _ClipStart;
      uniform float _ClipEnd;
      uniform half _Dissolve;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
#if SHADER_TARGET >= 40
        centroid float2 texcoord : TEXCOORD0;
#else
        float2 texcoord : TEXCOORD0;
#endif
        float3 worldPos : TEXCOORD1;
        uint id : SV_VertexID;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 vertex : POSITION;
        float4 color : COLOR;
#if SHADER_TARGET >= 40
        centroid float2 texcoord : TEXCOORD0;
#else
        float2 texcoord : TEXCOORD0;
#endif
        float3 worldPos : TEXCOORD1;
        uint id : TEXCOORD2;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      float4 _MainTex_ST;
      fixed _Scroll1;
      fixed _Scroll2;
      half _DisplacementIntensity;
      half _EmissionGain;

      half _FlameFadeMax;
      half _FlameFadeMin;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);


        v.color = TbVertToSrgb(v.color);
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = bloomColor(v.color, _EmissionGain);
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.worldPos = mul(unity_ObjectToWorld, v.vertex);
        o.id = (float2)v.id;
        return o;
      }

      // Note: input color is srgb
      fixed4 frag (v2f i) : COLOR
      {

        if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
        if (_Dissolve < 1 && Dither8x8(i.vertex.xy) >= _Dissolve) discard;

        half2 displacement;
        float procedural_line = 0;
        float flame_fade_mix = 0;

        displacement = tex2D( _DisplaceTex, i.texcoord ).xy;
        displacement =  displacement * 2.0 - 1.0;
        displacement *= _DisplacementIntensity;

        half mask =   tex2D(_MainTex, i.texcoord).y;

#ifdef AUDIO_REACTIVE
        flame_fade_mix = 1.0- saturate(_BeatOutput.w);
#else
#endif
        half2 uv = i.texcoord;
        uv += displacement;

        half flame1 = tex2D(_MainTex, uv * .7 + half2(-GetTime().x * _Scroll1, 0)).x;
        half flame2 = tex2D(_MainTex, half2(uv.x,1.0-uv.y) + half2(-GetTime().x * _Scroll2, -GetTime().x * _Scroll2 / 4 )).x;

        half flames = saturate( flame2 + flame1 ) / 2.0;
        flames = smoothstep( 0, 0.8, mask*flames);
        flames *= mask;

        half4 tex = half4(flames,flames,flames,1.0);
        float flame_fade  = lerp(_FlameFadeMin,_FlameFadeMax,flame_fade_mix);

        tex.xyz *= pow(1.0-i.texcoord.x, flame_fade) * (flame_fade*2);

        float4 color = i.color * tex;
        color = encodeHdr(color.rgb * color.a);
        color = SrgbToNative(color);
        return color * _Dissolve;
      }
      ENDCG
    }
  }
}
}
