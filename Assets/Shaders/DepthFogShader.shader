// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

//Highlights intersections with other objects
 // https://chrismflynn.wordpress.com/2012/09/06/fun-with-shaders-and-the-depth-buffer/
Shader "Moat/DepthFog"
{
	Properties
	{
		_Color("Main Color", Color) = (1, 1, 1, .5) //Color when not intersecting
		_MaxDistance("Max Distance", Float) = 10
		_Radius("Radius", Float) = 5
	}
	SubShader
	{
		Tags {
			"Queue" = "Transparent"
			"RenderType"="Transparent"  
		}
 
		Pass
		{

		// Result = FG * BF + BG * BF
			Blend OneMinusDstColor OneMinusSrcAlpha

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

			sampler2D _CameraDepthTexture; //Depth Texture
			float4 _Color;
			float _MaxDistance;
			float _Radius;
			// float4x4 _SceneMatrix;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float4 projPos : TEXCOORD0; //Screen position of pos
				float4 objPos : TEXCOORD1;
				float3 viewDir : TEXCOORD2;
				float3 normal : NORMAL;

			};
 
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.projPos = ComputeScreenPos(o.pos);
				o.objPos = v.vertex;
				o.normal = UnityObjectToWorldNormal(v.normal);
				o.viewDir = normalize(UnityWorldSpaceViewDir(mul(unity_ObjectToWorld, v.vertex)));

				return o;
			}
 
			half4 frag(v2f i) : SV_Target
			{
				float3 viewDir = normalize(ObjSpaceViewDir(i.objPos));

				float viewDot = -viewDir.z;
				//Get the distance to the camera from the depth buffer for this point
				float pixelDistance = LinearEyeDepth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)).r) / viewDot;

				// float geometryDistance = distance(float4(0,0,0,0), i.objPos);

				//If the two are similar, then there is an object intersecting with our object
				float rim = saturate(1 - abs(dot(i.normal, normalize(i.viewDir))) * 2);

				float fade = smoothstep(_MaxDistance + _Radius * 0.5, _MaxDistance - _Radius * 0.5, pixelDistance);
				float4 ColorA = float4(_Color.r, _Color.g, _Color.b, 0);
				float4 ColorB = float4(1 - _Color.b, 1 - _Color.r, 1 - _Color.g, _Color.a);
				float4 finalColor = (pixelDistance > _MaxDistance + _Radius ? 0 : lerp(ColorB, ColorA, pow(fade, 3))) + pow(rim, 3);

				half4 c;
				c.r = finalColor.r * finalColor.a;//finalColor.r;
				c.g = finalColor.g * finalColor.a;//finalColor.g;
				c.b = finalColor.b * finalColor.a;//finalColor.b;
				c.a = finalColor.a;
 
				return c;
			}
 
			ENDCG
		}
	}
    FallBack "VertexLit"
}