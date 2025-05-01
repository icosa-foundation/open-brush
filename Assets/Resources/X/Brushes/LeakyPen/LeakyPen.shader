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

Shader "Brush/Special/LeakyPen" {
	Properties {
		_MainTex ("Alpha Mask", 2D) = "white" {}
		_SecondaryTex("Diffuse Tex", 2D) = "white" {}
		_Cutoff("Alpha cutoff", Range(0,1)) = 0.5

		_Dissolve("Dissolve", Range(0,1)) = 1
	    _ClipStart("Clip Start", Float) = 0
	    _ClipEnd("Clip End", Float) = -1
	}

	Category {
		Cull Back
		SubShader {
			Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }

			LOD 200
			CGPROGRAM
			#pragma surface surf StandardSpecular vertex:vert alphatest:_Cutoff addshadow
			#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
			#pragma target 4.0

			#include "UnityCG.cginc"
			#include "Assets/Shaders/Include/Brush.cginc"

			sampler2D _MainTex;
			sampler2D _SecondaryTex;

			uniform half _ClipStart;
			uniform half _ClipEnd;
			uniform half _Dissolve;

			struct Input {
				float2 uv_MainTex;
				float2 uv_SecondaryTex;
				float4 color : Color;
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

			void vert(inout appdata_full_plus_id i, out Input o) {
                UNITY_INITIALIZE_OUTPUT(Input, o);
				PrepForOds(i.vertex);
				i.color = TbVertToNative(i.color);
                o.id = i.id;
			}

			void surf (Input IN, inout SurfaceOutputStandardSpecular o) {

				#ifdef SHADER_SCRIPTING_ON
				if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
                if (_Dissolve < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Dissolve) discard;
				#endif

				float3 secondary_tex = tex2D(_MainTex, IN.uv_SecondaryTex).rgb;

				// Apply the alpha mask
				float primary_tex = tex2D(_MainTex, IN.uv_MainTex).w;

				// Combine the two texture elements
				float3 tex = secondary_tex * primary_tex;
				o.Specular = 0;
				o.Albedo = IN.color.rgb;
				o.Alpha = tex * IN.color.a;

			}
			ENDCG
		}
	}
	FallBack "Diffuse"
}
