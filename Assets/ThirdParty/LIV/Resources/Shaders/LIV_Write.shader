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
			ZTest Always ZWrite Off 
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

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
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
