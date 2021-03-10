Shader "Hidden/LIV_ForceForwardRendering"
{	
	SubShader
	{
		Tags { "Queue" = "Geometry" "LightMode" = "Always" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True"}

		Pass
		{
			Name "FORCE_FORWARD_RENDERING"
			ZTest Always ZWrite Off ColorMask 0
			Fog{ Mode Off }

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				return 1;
			}

			ENDCG
		}
	}
}
