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

Shader "Brush/Special/MylarTube" {
Properties {
	_MainTex ("Texture", 2D) = "white" {}
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.078125
	_SqueezeAmount("Squeeze Amount", Range(0.0,1)) = 0.825

	[Toggle] _OverrideTime ("Overriden Time", Float) = 0.0
	_TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
	_TimeBlend("Time Blend", Float) = 0
	_TimeSpeed("Time Speed", Float) = 1.0

	_Dissolve("Dissolve", Range(0,1)) = 1.0
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1
}

Category {
	Cull Back
    SubShader {

		CGPROGRAM
		#pragma target 4.0
		#pragma surface surf StandardSpecular vertex:vert addshadow
		#pragma multi_compile __ AUDIO_REACTIVE
		#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
		#include "Assets/Shaders/Include/TimeOverride.cginc"
		#include "Assets/Shaders/Include/Brush.cginc"
		#include "Assets/ThirdParty/Shaders/Noise.cginc"

		sampler2D _MainTex;
		fixed4 _Color;
		half _Shininess;
		half _SqueezeAmount;

		uniform float _ClipStart;
		uniform float _ClipEnd;
		uniform half _Dissolve;

		struct Input {
			float4 color : Color;
			float2 tex : TEXCOORD0;
			float3 viewDir;
			uint id : SV_VertexID;
			float4 screenPos;
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

			void vert(inout appdata_full_plus_id v, out Input o) {
			PrepForOds(v.vertex);

			float radius = v.texcoord.z;

			// Squeeze displacement
			float squeeze = sin(v.texcoord.x*3.14159);
			float3 squeeze_displacement = radius * v.normal.xyz * squeeze;
			v.vertex.xyz -= squeeze_displacement * _SqueezeAmount;

			// Perturb normal
			v.normal = normalize(v.normal + squeeze_displacement * 2.5);

			o.color = TbVertToSrgb(o.color);
			UNITY_INITIALIZE_OUTPUT(Input, o);
		    o.tex = v.texcoord.xy;
            o.id = v.id;
		}

		// Input color is srgb
		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {

			if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
			if (_Dissolve < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Dissolve) discard;

		    o.Albedo =  _Color.rgb * IN.color.rgb;
			//o.Emission =  _Color.rgb * IN.color.rgb;
			o.Smoothness = _Shininess;
			o.Specular = _SpecColor * IN.color.rgb;

			// Calculate rim
			float3 n = WorldNormalVector (IN, o.Normal);
			half rim = 1.0 - abs(dot (normalize(IN.viewDir), n));
			rim *= 1-pow(rim,5);

			//Thin slit diffraction texture ramp lookup
			float3 diffraction = tex2D(_MainTex, half2(rim + GetTime().x + o.Normal.y, rim + o.Normal.y)).xyz;
			o.Emission = rim*(.25 * diffraction * rim  + .75 * diffraction * IN.color);

		}
		ENDCG
    }
}
	  FallBack "Diffuse"
}
