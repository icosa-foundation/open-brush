Shader "Hidden/LIV_CombineAlpha"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "black" {}
	}

	SubShader
	{		
		Tags { "Queue" = "Overlay" "LightMode" = "Always" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True"}

		Pass
		{
			Name "COMBINE_ALPHA"
			Blend One OneMinusSrcAlpha
			Ztest Always
			Zwrite Off
			Cull Off
			ColorMask[_LivColorMask]
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
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				return tex2D(_MainTex, i.uv).a;
			}
			ENDCG
		}
	}
}
