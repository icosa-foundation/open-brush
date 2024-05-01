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

Shader "Brush/Special/MarbledRainbow" {
	Properties{
		_Color("Main Color", Color) = (1,1,1,1)
		_SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
		_Shininess("Shininess", Range(0.01, 1)) = 0.078125
		_MainTex("Base (RGB) TransGloss (A)", 2D) = "white" {}
		_SpecTex("Base (RGB) TransGloss (A)", 2D) = "white" {}
		_BumpMap("Normalmap", 2D) = "bump" {}
		_Cutoff("Alpha cutoff", Range(0,1)) = 0.5

		_Dissolve("Dissolve", Range(0,1)) = 1
	    _ClipStart("Clip Start", Float) = 0
	    _ClipEnd("Clip End", Float) = -1
	}
		SubShader{
		Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
		Cull Back
		LOD 100

		CGPROGRAM
		#pragma target 4.0
		#pragma surface surf StandardSpecular vertex:vert alphatest:_Cutoff addshadow
		#pragma multi_compile __ AUDIO_REACTIVE
		#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
		#include "Assets/Shaders/Include/Brush.cginc"

		struct Input {
			float2 uv_MainTex;
			float2 uv_BumpMap;
			float2 uv_SpecTex;
			float4 color : Color;
			float3 worldPos;
			uint id : SV_VertexID;
			float4 screenPos;
	    };

		sampler2D _MainTex;
		sampler2D _BumpMap;
		sampler2D _SpecTex;
		fixed4 _Color;
		half _Shininess;

		uniform float _ClipStart;
		uniform float _ClipEnd;
		uniform half _Dissolve;

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
        UNITY_INITIALIZE_OUTPUT(Input, o);
		PrepForOds(v.vertex);
		v.color = TbVertToSrgb(v.color);

		float t = 0.0;

		float strokeWidth = abs(v.texcoord.z) * 1.2;

#ifdef AUDIO_REACTIVE
		t = _BeatOutputAccum.z * 5;
		float waveIntensity = _BeatOutput.z * .1 * strokeWidth;
		v.vertex.xyz += (pow(1 - (sin(t + v.texcoord.x * 5 + v.texcoord.y * 10) + 1), 2)
			* cross(v.tangent.xyz, v.normal.xyz)
			* waveIntensity);
#endif
        o.id = v.id;
	}

	void surf(Input IN, inout SurfaceOutputStandardSpecular o) {

		if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
        if (_Dissolve < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Dissolve) discard;

		fixed4 spectex = tex2D(_SpecTex, IN.uv_SpecTex);
		fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);

		o.Albedo = SrgbToNative(tex * IN.color).rgb;
		o.Smoothness = _Shininess;
		o.Specular = SrgbToNative(_SpecColor * spectex).rgb;
		o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
		o.Alpha = tex.a * IN.color.a;

#ifdef AUDIO_REACTIVE
		o.Emission = o.Albedo;
		o.Albedo = .2;
		o.Specular *= .5;
#endif

	}
	ENDCG
		}

	FallBack "Transparent/Cutout/VertexLit"
}
