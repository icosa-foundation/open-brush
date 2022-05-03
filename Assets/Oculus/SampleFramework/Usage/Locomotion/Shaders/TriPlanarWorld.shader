/************************************************************************************

Copyright   :   Copyright 2017 Oculus VR, LLC. All Rights reserved.

Licensed under the Oculus VR Rift SDK License Version 3.4.1 (the "License");
you may not use the Oculus VR Rift SDK except in compliance with the License,
which is provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

https://developer.oculus.com/licenses/sdk-3.4.1

Unless required by applicable law or agreed to in writing, the Oculus VR SDK
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

************************************************************************************/

Shader "Custom/TriPlanarWorld" {
	Properties {
		_Color("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
			float3 worldPos;
			float3 worldNormal;
			float4 vertColor : COLOR;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		void vert(inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.vertColor = v.color;
			o.vertColor = float4(1, 0, 1, 1);
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {
			float3 absNormal = abs(IN.worldNormal);

			// exponentially scale the absNormal so that it strongly biases to a single cardinal axis.
			absNormal = normalize(pow(absNormal, 5));

			fixed4 x = tex2D(_MainTex, IN.worldPos.yz);
			fixed4 y = tex2D(_MainTex, IN.worldPos.xz);
			fixed4 z = tex2D(_MainTex, IN.worldPos.xy);

			fixed4 c = x * absNormal.x + y * absNormal.y + z * absNormal.z;
			c *= _Color * IN.vertColor;
			o.Albedo = c.rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
			//o.Albedo = abs(IN.worldNormal);
		}
		ENDCG
	}
	FallBack "Diffuse"
}
