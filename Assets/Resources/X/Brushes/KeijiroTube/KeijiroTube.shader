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

Shader "Brush/Special/KeijiroTube" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
	_Shininess ("Shininess", Range (0.01, 1)) = 0.078125

    [Toggle] _OverrideTime ("Overriden Time", Float) = 0.0
    _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
    _TimeBlend("Time Blend", Float) = 0
    _TimeSpeed("Time Speed", Float) = 1.0

	_Opacity("Opacity", Range(0,1)) = 1
    _ClipStart("Clip Start", Float) = 0
    _ClipEnd("Clip End", Float) = -1
}
    SubShader {
    	LOD 200
        Cull Back

		CGPROGRAM
		#pragma target 4.0
		#pragma surface surf StandardSpecular vertex:vert alphatest:_Cutoff addshadow
		#pragma multi_compile __ AUDIO_REACTIVE
		#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

		#include "Assets/Shaders/Include/TimeOverride.cginc"
		#include "Assets/Shaders/Include/Brush.cginc"

		fixed4 _Color;
		half _Shininess;

    	uniform float _ClipStart;
	    uniform float _ClipEnd;
		uniform half _Opacity;

		struct Input {
			float2 uv_MainTex;
			float2 uv_BumpMap;
			float4 color : Color;
			float radius;
            uint id : SV_VertexID;
			float4 screenPos;
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

		void vert (inout appdata_full_plus_id i, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			// o.tangent = v.tangent;
			PrepForOds(i.vertex);
			i.color = TbVertToNative(i.color);

			float radius = i.texcoord.z;
			float wave = sin(i.texcoord.x - GetTime().z);
			float pulse = smoothstep(.45, .5, saturate(wave));
			i.vertex.xyz -= pulse * radius * i.normal.xyz;
			o.radius = radius;
            o.id = i.id;
		}

		void surf (Input IN, inout SurfaceOutputStandardSpecular o) {

	        if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
			if (_Opacity < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Opacity) discard;

			o.Albedo = _Color.rgb * IN.color.rgb;
			o.Smoothness = _Shininess;
			o.Specular = _SpecColor * IN.color.rgb;
		}
      ENDCG
    }


	FallBack "Diffuse"
}
