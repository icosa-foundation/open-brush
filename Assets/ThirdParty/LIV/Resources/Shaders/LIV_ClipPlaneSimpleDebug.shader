Shader "Hidden/LIV_ClipPlaneSimpleDebug"
{	
	SubShader
	{
		Tags { "Queue" = "Overlay" "LightMode" = "Always" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True"}
		
		Pass {
			Name "CLIP_PLANE_SIMPLE_DEBUG"
			Cull Off
			ZWrite On
			Fog{ Mode Off }

			CGPROGRAM

			#pragma target 4.6

			#pragma vertex VertexProgram
			#pragma fragment FragmentProgram
			#pragma geometry GeometryProgram

			#include "UnityCG.cginc"

			struct VertexData {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct VertexToGeomData {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};
	
			struct GeomToFragData {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 barycentric : TEXCOORD1;
			};

			VertexToGeomData VertexProgram(VertexData v)
			{
				VertexToGeomData o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			[maxvertexcount(3)]
			void GeometryProgram(triangle VertexToGeomData p[3], inout TriangleStream<GeomToFragData> triStream) {
				GeomToFragData pIn;

				pIn.vertex = p[0].vertex;
				pIn.uv = p[0].uv;
				pIn.barycentric.xyz = float3(1.0, 0, 0);
				triStream.Append(pIn);

				pIn.vertex = p[1].vertex;
				pIn.uv = p[1].uv;
				pIn.barycentric.xyz = float3(0, 1.0, 0);
				triStream.Append(pIn);

				pIn.vertex = p[2].vertex;
				pIn.uv = p[2].uv;
				pIn.barycentric.xyz = float3(0, 0, 1.0);
				triStream.Append(pIn);
			}

			fixed4 FragmentProgram(GeomToFragData i) : SV_Target
			{
				float3 barys;
				barys.xy = i.barycentric;
				barys.z = 1 - barys.x - barys.y;
				barys = smoothstep(0.0, 0.0 + fwidth(barys), barys);
				return lerp(float4(0.0, 0.0, 0.0, 0.5), float4(0.0, 1.0, 0.0, 0.5), min(barys.x, min(barys.y, barys.z)));
			}

			ENDCG
		}
	}
}
