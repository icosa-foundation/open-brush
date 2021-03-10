Shader "Hidden/LIV_ClipPlaneSimple"
{	
	SubShader
	{
		Tags { "Queue" = "Overlay" "LightMode" = "Always" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True"}
		
		Pass {
			Name "CLIP_PLANE_SIMPLE"
			Cull Off
			ZWrite On
			Blend Off
			Fog{ Mode Off }
			ColorMask[_LivColorMask]
			
			CGPROGRAM

			#pragma target 4.6

			#pragma vertex VertexProgram
			#pragma fragment FragmentProgram

			#include "UnityCG.cginc"

			struct VertexData {
				float4 vertex : POSITION;
			};

			struct VertexToFragData {
				float4 vertex : POSITION;
			};

			VertexToFragData VertexProgram(VertexData v)
			{
				VertexToFragData o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fixed4 FragmentProgram(VertexToFragData i) : SV_Target
			{
				return fixed4(0, 0, 0, 0);
			}

			ENDCG
		}
	}
}
