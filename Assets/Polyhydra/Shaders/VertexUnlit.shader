Shader "Vertex color unlit"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100
        Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
			};

			struct v2f
			{
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
				float4 vertexColor : COLOR;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.vertexColor = v.color;
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = i.vertexColor;

				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);

				return col;
			}
			ENDCG
		}
	}
}

