Shader "Hidden/LIV_ClipPlaneComplexDebug"
{
	Properties{
		_LivClipPlaneHeightMap("Clip Plane Height Map", 2D) = "black" {}
	}

	SubShader
	{
		Tags { "Queue" = "Background" "LightMode" = "Always" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True"}

		Pass {
			Name "CLIP_PLANE_COMPLEX_DEBUG"
			Cull Off ZWrite On
			Fog{ Mode Off }

			CGPROGRAM

			#pragma target 4.6

			#pragma vertex TessellationVertexProgram
			#pragma fragment FragmentProgram
			#pragma hull HullProgram
			#pragma domain DomainProgram
			#pragma geometry GeometryProgram

			#include "UnityCG.cginc"

			sampler2D _LivClipPlaneHeightMap;
			float _LivTessellation;

			struct VertexData {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 worldPos : TEXCOORD1;
			};

			struct GeomToFragData {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 barycentric : TEXCOORD1;
			};

			struct TessellationFactors {
				float edge[4] : SV_TessFactor;
				float inside[2] : SV_InsideTessFactor;
			};

			struct TessellationControlPoint {
				float4 vertex : INTERNALTESSPOS;
				float2 uv : TEXCOORD0;
			};

			[domain("quad")]
			[outputcontrolpoints(4)]
			[outputtopology("triangle_cw")]
			[partitioning("fractional_odd")]
			[patchconstantfunc("PatchConstantFunction")]
			TessellationControlPoint HullProgram(InputPatch<TessellationControlPoint, 4> patch, uint id : SV_OutputControlPointID) {
				return patch[id];
			}

			TessellationFactors PatchConstantFunction(InputPatch<TessellationControlPoint, 4> patch) {
				TessellationFactors f;
				float t = _LivTessellation;
				f.edge[0] = f.edge[1] = f.edge[2] = f.edge[3] = f.inside[0] = f.inside[1] = t;
				return f;
			}

			[domain("quad")]
			VertexData DomainProgram(TessellationFactors factors, OutputPatch<TessellationControlPoint, 4> patch, float2 uv : SV_DomainLocation) {
				VertexData data;

				data.uv = lerp(lerp(patch[0].uv, patch[1].uv, uv.x), lerp(patch[3].uv, patch[2].uv, uv.x), uv.y);
				float4 vertex = lerp(lerp(patch[0].vertex, patch[1].vertex, uv.x), lerp(patch[3].vertex, patch[2].vertex, uv.x), uv.y);
				vertex.z += tex2Dlod(_LivClipPlaneHeightMap, float4(data.uv, 0, 0)).r;
				data.vertex = UnityObjectToClipPos(vertex);
				data.worldPos = mul(unity_ObjectToWorld, vertex);

				return data;
			}

			TessellationControlPoint TessellationVertexProgram(VertexData v) {
				TessellationControlPoint p;
				p.vertex = v.vertex;
				p.uv = v.uv;
				return p;
			}

			[maxvertexcount(3)]
			void GeometryProgram(triangle VertexData p[3], inout TriangleStream<GeomToFragData> triStream) {
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

			fixed4 FragmentProgram(GeomToFragData i) : SV_Target {
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
