Shader "Hidden/LIV_Write"
{
	Properties
	{
		_MainTex("Texture", 2D) = "black" {}
	}
	SubShader
	{
		Tags { "Queue" = "Background" "LightMode" = "Always" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True"}

		Pass
		{
			Name "WRITE"

			Blend Off
			ZTest Always
			ZWrite Off
			Cull Off
			ColorMask [_LivColorMask]
			Fog{ Mode Off }

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			sampler2D _MainTex;
      CBUFFER_START(UnityPerMaterial)
			uniform float4 _MainTex_ST;
      CBUFFER_END

			v2f vert(appdata v)
			{
				v2f o;
				float2 uv = -1.0 + v.vertex.xy * 2.0;
				uv.y *= _ProjectionParams.x;
				o.vertex.xy = uv;
				o.vertex.z = 0.0;
				o.vertex.w = 1.0;
				o.uv = TRANSFORM_TEX(v.uv.xy, _MainTex);				
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				return tex2D(_MainTex, i.uv);
			}

			ENDCG
		}
	}
}
