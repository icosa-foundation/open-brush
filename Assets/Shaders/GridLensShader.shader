// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

 // https://chrismflynn.wordpress.com/2012/09/06/fun-with-shaders-and-the-depth-buffer/
Shader "Moat/GridLens"
{
	Properties
	{
		_Color("Main Color", Color)         = (1, 1, 1, .5) //Color when not intersecting
		_MaxDistance("Max Distance", Float) = 10
		_Radius("Radius", Float)            = 5
	}
	SubShader
	{
		Tags {
			"Queue"      = "Transparent"
			"RenderType" = "Transparent"  
		}
 
		Pass
		{

		    // Result = FG * BF + BG * BF
			// Blend OneMinusDstColor OneMinusSrcColor
			Blend OneMinusDstColor OneMinusSrcAlpha

			Lighting Off
			ZWrite Off
			Cull Back
 
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			sampler2D _CameraDepthTexture;
			float4 _Color;
			float _MaxDistance;
			float _Radius;
			float _GridScale;
			float3 _WorldSpaceCursorPos;

			float4x4 _SceneMatrix;
			float4x4 _InverseSceneMatrix;

			struct v2f
			{
				float4 pos     : SV_POSITION; // vertex positions of the orb
				float4 projPos : TEXCOORD0;   // orb verts in Screen Space for accessing depth buffer
				float4 objPos  : TEXCOORD1;   // orb verts in Object Space for calculating view direction
				float3 viewDir : TEXCOORD2;   // orb vert view direction in world space
				float3 normal  : NORMAL;      // orb vert world space normal
			};
 
			v2f vert(appdata v)
			{
				v2f o;

				o.pos     = UnityObjectToClipPos(v.vertex);
				o.projPos = ComputeScreenPos(o.pos);
				o.objPos  = v.vertex;

				o.viewDir = WorldSpaceViewDir(v.vertex);
				o.normal  = mul(unity_ObjectToWorld, v.normal);

				return o;
			}

			// note that positional offset (matrix[0].w, matrix[1].w, matrix[2].w) has been REVERSE and is subtracting
			// don't ask me why I had to do this, but it had to be done to make it work in this scenario.
			float3 WorldToCanvasPos(float4x4 Matrix, float3 Point)
			{
				float3 res;
				res.x = Matrix[0].x * Point.x + Matrix[0].y * Point.y + Matrix[0].z * Point.z - Matrix[0].w;
				res.y = Matrix[1].x * Point.x + Matrix[1].y * Point.y + Matrix[1].z * Point.z - Matrix[1].w;
				res.z = Matrix[2].x * Point.x + Matrix[2].y * Point.y + Matrix[2].z * Point.z - Matrix[2].w;
				return res;
			}

			half4 frag(v2f i) : SV_Target
			{
				// this is for fragment-precise calculation of the view direction, but we might not need this
				float3 viewDir            = normalize(i.viewDir); // world-space view direction
				float  viewDot            = dot(viewDir, UNITY_MATRIX_V[2].xyz); // Cannot run this in the vertex program because it won't interpolate correctly

				//Get the distance to the camera from the depth buffer for this point
				float rayDist = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)).r) / viewDot;

				// position of the ray hit in unity world coordinates, that are then scrunched to fit the scenematrix
				float3 rayHitPos = float4(rayDist * viewDir - _WorldSpaceCameraPos, 1);
				float3 rayHitCanvasPos = WorldToCanvasPos(_SceneMatrix, rayHitPos);

				// If the two are similar, then there is an object intersecting with our object
				float rim                 = saturate(1 - abs(dot(i.normal, viewDir)) * 2);

				// Adaptive grid major lines
				float gridSubdivision = pow(2, floor(log2(_GridScale * 2)));
				float3 rayHitPosToAxisDivision = abs((rayHitCanvasPos * gridSubdivision) % 1);

				// Adaptive grid subdivisions
				float gridSubdivision2 = pow(2, floor(log2(_GridScale * 4)));
				float3 rayHitPosToAxisDivision2 = abs((rayHitCanvasPos * gridSubdivision2) % 1);


				float threshold = 0.975;
				float3 rayHitColor = 0.5 * smoothstep(threshold, 1, max(smoothstep(0.5, 1, rayHitPosToAxisDivision), smoothstep(0.5, 0, rayHitPosToAxisDivision)));

				rayHitColor += 0.5 * smoothstep(threshold, 1, max(smoothstep(0.5, 1, rayHitPosToAxisDivision2), smoothstep(0.5, 0, rayHitPosToAxisDivision2)));


				// cursor position indicator
				float cursorFacing = smoothstep(0.999995, 1, dot(viewDir, normalize(_WorldSpaceCameraPos - _WorldSpaceCursorPos)));

				float fade                = smoothstep(_MaxDistance + _Radius * 0.5, _MaxDistance - _Radius * 0.5, rayDist);
				float4 ColorC = float4(rayHitColor, smoothstep(threshold, 1, max(rayHitColor.r, max(rayHitColor.g, rayHitColor.b))));
				float4 ColorA             = float4(_Color.r, _Color.g, _Color.b, 0);
				float4 ColorB             = float4(1 - _Color.b, 1 - _Color.r, 1 - _Color.g, _Color.a * 0.5);

				float4 fogColor = (rayDist > _MaxDistance + _Radius ? 0 : lerp(ColorB, ColorA, pow(fade, 3)));

				float4 finalColor         = (rayDist > _MaxDistance + _Radius ? 0 : lerp(ColorC, 0, pow(fade, 3))) + rim + fogColor + cursorFacing;
				

				// premultiply color
				half4 c;

				c.r                       = finalColor.r * finalColor.a; // finalColor.r;
				c.g                       = finalColor.g * finalColor.a; // finalColor.g;
				c.b                       = finalColor.b * finalColor.a; // finalColor.b;
				c.a                       = finalColor.a;
 
				return c;
			}
 
			ENDCG
		}
	}
    FallBack "VertexLit"
}