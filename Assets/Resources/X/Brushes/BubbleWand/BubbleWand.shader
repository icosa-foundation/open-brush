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

Shader "Brush/Special/BubbleWand" {
Properties {
	_MainTex ("Texture", 2D) = "white" {}
	_ScrollRate("Scroll Rate", Float) = 1.0
	_ScrollJitterIntensity("Scroll Jitter Intensity", Float) = 1.0
	_ScrollJitterFrequency("Scroll Jitter Frequency", Float) = 1.0

  [Toggle] _OverrideTime ("Overriden Time", Float) = 0.0
  _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
  _TimeBlend("Time Blend", Float) = 0
  _TimeSpeed("Time Speed", Float) = 1.0

  _Opacity ("Opacity", Range(0, 1)) = 1
  _Dissolve ("Dissolve", Range(0, 1)) = 1
  _ClipStart("Clip Start", Float) = 0
  _ClipEnd("Clip End", Float) = -1
}

    SubShader {
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Blend One One
		Cull off ZWrite Off

		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf StandardSpecular vertex:vert
		#pragma multi_compile __ AUDIO_REACTIVE
		#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
		#include "Assets/Shaders/Include/TimeOverride.cginc"
		#include "Assets/Shaders/Include/Brush.cginc"
		#include "Assets/ThirdParty/Shaders/Noise.cginc"

		sampler2D _MainTex;
		float _EmissionGain;
		float _ScrollRate;
		float _ScrollJitterIntensity;
		float _ScrollJitterFrequency;

        uniform float _ClipStart;
        uniform float _ClipEnd;
		uniform half _Dissolve;
        uniform half _Opacity;

		float4 displace(float4 pos, float timeOffset) {
			float t = GetTime().y*_ScrollRate + timeOffset;

			pos.x += sin(t + GetTime().y + pos.z * _ScrollJitterFrequency) * _ScrollJitterIntensity;
			pos.z += cos(t + GetTime().y + pos.x * _ScrollJitterFrequency) * _ScrollJitterIntensity;
			pos.y += cos(t * 1.2 + GetTime().y + pos.x * _ScrollJitterFrequency) * _ScrollJitterIntensity;

			float time = GetTime().x;
			float d = 30;
			float freq = .1;
			float3 disp = float3(1,0,0) * curlX(pos.xyz * freq + time, d);
			disp += float3(0,1,0) * curlY(pos.xyz * freq +time, d);
			disp += float3(0,0,1) * curlZ(pos.xyz * freq + time, d);
			pos.xyz = _ScrollJitterIntensity * disp * kDecimetersToWorldUnits;
			return pos;
		}

		struct Input {
			float4 color : Color;
			float2 tex : TEXCOORD0;
			float3 viewDir;
            uint id : SV_VertexID;
			INTERNAL_DATA
		};

		struct appdata_full_plus_id {
             float4 vertex : POSITION;
             float4 tangent : TANGENT;
             float3 normal : NORMAL;
             float4 texcoord : TEXCOORD0;
             float4 texcoord1 : TEXCOORD1;
             float4 texcoord2 : TEXCOORD2;
             float4 texcoord3 : TEXCOORD3;
             fixed4 color : COLOR;
             uint id : SV_VertexID;
             UNITY_VERTEX_INPUT_INSTANCE_ID
      };

		void vert (inout appdata_full_plus_id v, out Input o) {
			PrepForOds(v.vertex);

			float radius = v.texcoord.z;

			// Bulge displacement
			float wave = sin(v.texcoord.x*3.14159);
			float3 wave_displacement = radius * v.normal.xyz * wave;
			v.vertex.xyz += wave_displacement;

			// Noise displacement
			// TO DO: Need to make this scale invariant
			float4 displacement = displace(v.vertex,0);
			v.vertex.xyz += displacement.xyz;

			// Perturb normal
			v.normal = normalize(v.normal + displacement.xyz * 2.5 + wave_displacement * 2.5);

			o.color = TbVertToSrgb(o.color);
			UNITY_INITIALIZE_OUTPUT(Input, o);
		    o.tex = v.texcoord.xy;
            o.id = v.id;
		}

		// Input color is srgb
		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {

			if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
			if (_Dissolve < 1 && Dither8x8(i.pos.xy) >= _Dissolve) discard;

			// Hardcode some shiny specular values
			o.Smoothness = .9;
			o.Specular = .6 * SrgbToNative(IN.color).rgb;
			o.Albedo = 0;

			// Calculate rim
			float3 n = WorldNormalVector (IN, o.Normal);
			half rim = 1.0 - abs(dot (normalize(IN.viewDir), n));
			rim *= 1-pow(rim,5);

			//Thin slit diffraction texture ramp lookup
			float3 diffraction = tex2D(_MainTex, half2(rim + GetTime().x + o.Normal.y, rim + o.Normal.y)).xyz;
			o.Emission = rim*(.25 * diffraction * rim  + .75 * diffraction * IN.color);
	        o.Emission *= _Opacity;
	        o.Specular *= _Opacity;
	        o.Smoothness *= _Opacity;
		}
		ENDCG
    }
}
