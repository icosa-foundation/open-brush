// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "OvrAvatar/AvatarSurfaceShaderPBS" {
	Properties{
		// Global parameters
		_Alpha("Alpha", Range(0.0, 1.0)) = 1.0
		_Albedo("Albedo (RGB)", 2D) = "" {}
		_Surface("Metallic (R) Occlusion (G) and Smoothness (A)", 2D) = "" {}
	}
	SubShader{
		Tags {
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
		}

		Pass {
			ZWrite On
			Cull Off
			ColorMask 0

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"

			struct v2f {
				float4 position : SV_POSITION;
			};
			v2f vert(appdata_full v) {
				// Output
				v2f output;
				output.position = UnityObjectToClipPos(v.vertex);
				return output;
			}

			float4 frag(v2f input) : COLOR {
				return 0;
			}
				ENDCG
	}

		LOD 200

			CGPROGRAM

// Physically based Standard lighting model, and enable shadows on all light types
#pragma surface surf Standard vertex:vert nolightmap alpha noforwardadd

float _Alpha;
sampler2D _Albedo;
float4 _Albedo_ST;
sampler2D _Surface;
float4 _Surface_ST;

struct Input {
	float2 texcoord;
};

void vert(inout appdata_full v, out Input o) {
	UNITY_INITIALIZE_OUTPUT(Input, o);
	o.texcoord = v.texcoord.xy;
}

void surf (Input IN, inout SurfaceOutputStandard o) {
	o.Albedo = tex2D(_Albedo, TRANSFORM_TEX(IN.texcoord, _Albedo)).rgb;
	float4 surfaceParams = tex2D(_Surface, TRANSFORM_TEX(IN.texcoord, _Surface));
	o.Metallic = surfaceParams.r;
	o.Occlusion = surfaceParams.g;
	o.Smoothness = surfaceParams.a;
	o.Alpha = _Alpha;
}

#pragma only_renderers d3d11 gles3 gles

ENDCG
	}
	FallBack "Diffuse"
}
