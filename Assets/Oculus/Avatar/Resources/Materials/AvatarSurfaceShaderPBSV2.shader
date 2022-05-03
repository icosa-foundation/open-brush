Shader "OvrAvatar/AvatarSurfaceShaderPBSV2" {
	Properties {
		_AlbedoMultiplier ("Albedo Multiplier", Color) = (1,1,1,1)
		_Albedo ("Albedo (RGB)", 2D) = "white" {}
		_Metallicness("Metallicness", 2D) = "grey" {}
		_GlossinessScale ("Glossiness Scale", Range(0,1)) = 0.5
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _Albedo;
		sampler2D _Metallicness;

		struct Input {
			float2 uv_Albedo;
		};

		float _GlossinessScale;
		float4 _AlbedoMultiplier;

		void surf (Input IN, inout SurfaceOutputStandard o) {
			fixed4 c = tex2D (_Albedo, IN.uv_Albedo) * _AlbedoMultiplier;
			o.Albedo = c.rgb;
			o.Metallic = tex2D (_Metallicness, IN.uv_Albedo).r;
			o.Smoothness = _GlossinessScale;
			o.Alpha = 1.0;
		}
		ENDCG
	}
	FallBack "Diffuse"
}