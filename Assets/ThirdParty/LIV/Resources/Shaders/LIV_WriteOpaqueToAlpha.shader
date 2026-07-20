Shader "Hidden/LIV_WriteOpaqueToAlpha"
{	
	SubShader
	{
		Tags { "Queue" = "Overlay" "LightMode" = "Always" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True"}
		
		Pass {
			Name "CLIP_PLANE_FIX_ALPHA"
			Blend Off
			ZTest Greater
			ZWrite Off
			Cull Off			
			ColorMask A
			Fog{ Mode Off }

			CGPROGRAM

			#pragma target 4.6

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert(appdata_base v)
			{
				v2f o;
				float2 uv = -1.0 + v.vertex.xy * 2.0;
				uv.y *= _ProjectionParams.x;
				o.vertex.xy = uv;
				o.vertex.z = 0.0;
				o.vertex.w = 1.0;
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
