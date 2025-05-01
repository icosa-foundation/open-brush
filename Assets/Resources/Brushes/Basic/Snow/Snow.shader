﻿// Copyright 2020 The Tilt Brush Authors
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

Shader "Brush/Particle/Snow" {
Properties {
  _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
  _MainTex ("Particle Texture", 2D) = "white" {}
  _ScrollRate("Scroll Rate", Float) = 1.0
  _ScrollDistance("Scroll Distance", Vector) = (1.0, 0, 0)
  _ScrollJitterIntensity("Scroll Jitter Intensity", Float) = 1.0
  _ScrollJitterFrequency("Scroll Jitter Frequency", Float) = 1.0
  _SpreadRate ("Spread Rate", Range(0.3, 5)) = 1.539


  _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
  _TimeBlend("Time Blend", Float) = 0
  _TimeSpeed("Time Speed", Float) = 1.0

  _Opacity ("Opacity", Range(0, 1)) = 1
  _Dissolve ("Dissolve", Range(0, 1)) = 1
  _ClipStart("Clip Start", Float) = 0
  _ClipEnd("Clip End", Float) = -1
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "DisableBatching"="True" }
  Blend SrcAlpha One
  AlphaTest Greater .01
  ColorMask RGB
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_particles
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON
      #pragma target 3.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Particles.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;
      fixed4 _TintColor;

      uniform half _ClipStart;
      uniform half _ClipEnd;
      uniform half _Dissolve;
      uniform half _Opacity;

      struct v2f {
        float4 vertex : SV_POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        uint id : TEXCOORD2;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      float4 _MainTex_ST;
      float _ScrollRate;
      float3 _ScrollDistance;
      float _ScrollJitterIntensity;
      float _ScrollJitterFrequency;
      float _SpreadRate;

      v2f vert (ParticleVertexWithSpread_t v)
      {
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        v.color = TbVertToSrgb(v.color);
        float birthTime = v.texcoord.w;
        float rotation = v.texcoord.z;
        float halfSize = GetParticleHalfSize(v.corner.xyz, v.center, birthTime);
        float spreadProgress = SpreadProgress(birthTime, _SpreadRate);
        float4 center = SpreadParticle(v, spreadProgress);
        float4 center_WS = mul(unity_ObjectToWorld, center);

        // Custom vertex animation
        float scrollAmount = GetTime().y;
        float t = fmod(scrollAmount * _ScrollRate + v.color.a, 1);
        float4 dispVec = (t - .5f) * float4(_ScrollDistance, 0.0);
        dispVec.x += sin(t * _ScrollJitterFrequency + GetTime().y) * _ScrollJitterIntensity;
        dispVec.z += cos(t * _ScrollJitterFrequency * .5 + GetTime().y) * _ScrollJitterIntensity;
        dispVec.xyz = spreadProgress * dispVec * kDecimetersToWorldUnits;
        center_WS += mul(xf_CS, dispVec);

        PrepForOdsWorldSpace(center_WS);

        float4 corner_WS = OrientParticle_WS(center_WS.xyz, halfSize, v.vid, rotation);
#ifdef AUDIO_REACTIVE
        o.color = musicReactiveColor(v.color, _BeatOutput.w);
        corner_WS = musicReactiveAnimationWorldSpace(corner_WS, v.color, _BeatOutput.w, corner_WS.y*5);
#else
        o.color = v.color;
#endif

        o.vertex = mul(UNITY_MATRIX_VP, corner_WS);
        o.color.a = pow(1 - abs(2*(t - .5)), 3);
        o.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
        o.id = (float2)v.id;
        return o;
      }

      // Input color is srgb
      fixed4 frag (v2f i) : SV_Target
      {
        #ifdef SHADER_SCRIPTING_ON
        if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
        if (_Dissolve < 1 && Dither8x8(i.vertex.xy) >= _Dissolve) discard;
        #endif

        float4 texCol = tex2D(_MainTex, i.texcoord);
        float4 color = SrgbToNative(2.0f * i.color * _TintColor * texCol);
#if SELECTION_ON
        color.rgb = GetSelectionColor() * texCol.r;
        color.a = texCol.a;
#endif
        color.a *= _Opacity;
        return color;
      }
      ENDCG
    }
  }
}
}
