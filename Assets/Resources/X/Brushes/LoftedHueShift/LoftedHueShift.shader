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

Shader "Brush/LoftedHueShift" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
		_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
		_Shininess ("Shininess", Range (0.01, 1)) = 0.078125
		_MainTex("Base (RGB) TransGloss (A)", 2D) = "white" {}
	}
	Category {
		Cull Back
		SubShader {
			CGPROGRAM
			#pragma target 3.0
			#pragma surface surf StandardSpecular vertex:vert addshadow
			#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
			#include "Assets/Shaders/Include/Brush.cginc"
			#include "Assets/Shaders/Include/ColorSpace.cginc"

			struct Input {
				float2 uv_MainTex;
				float4 color : Color;
				float3 worldPos;
				float4 screenPos;
			};

			sampler2D _MainTex;
			fixed4 _Color;
			half _Shininess;

			void vert (inout appdata_full v) {
				PrepForOds(v.vertex);
			}

			float3 hue06_to_base_rgb_(in float hue06) {
			  float r = -1 + abs(hue06 - 3);
			  float g =  2 - abs(hue06 - 2);
			  float b =  2 - abs(hue06 - 4);
			  return saturate(float3(r, g, b));
			}

			void surf(Input IN, inout SurfaceOutputStandardSpecular o) {
				fixed4 tex = tex2D(_MainTex, IN.uv_MainTex) * IN.color;

				// Hijack colorspace to make a hue shift..this is probably awful and technically wrong?
				float shift = 5;
				shift += IN.color;
				float3 hueshift = hue06_to_base_rgb_(IN.color * shift);
				fixed4 _ColorShift = float4(hueshift, 1);
				float huevignette = pow(abs(IN.uv_MainTex - .5) * 2.0, 2.0);

				o.Albedo = lerp(tex, _ColorShift, saturate(huevignette));
				o.Smoothness = _Shininess;
				o.Specular = _SpecColor;
			}
		ENDCG
	}
}

  //FallBack "Transparent/Cutout/VertexLit"
}
