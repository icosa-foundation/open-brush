// Small tweak to a basic unlit shader to cheat crosshair depth to render on top of targets.
Shader "Custom/CrosshairZCheat" 
{
	Properties
	{
		_MainTex ("Base (RGB), Alpha (A)", 2D) = "white" {}
		_Color ("Main Color", Color) = (0.5,0.5,0.5,0.5)
	}
	
	SubShader
	{
		Tags { "Queue" = "Transparent" }
		Cull Off
		Lighting Off
		ZTest Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				
				#include "UnityCG.cginc"
	
				struct appdata
				{
					float4 vertex : POSITION;
					float2 texcoord : TEXCOORD0;
					fixed4 color : COLOR;
				};
	
				struct v2f
				{
					float4 vertex : SV_POSITION;
					half2 texcoord : TEXCOORD0;
					fixed4 color : COLOR;
				};
	
				sampler2D _MainTex;
				float4 _MainTex_ST;
				fixed4 _Color;
				
				v2f vert (appdata v)
				{
					// Cheat the post-mvp transformed z towards the camera.
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.vertex.z -= 0.01;
					o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
					o.color = v.color;
					return o;
				}
				
				fixed4 frag (v2f i) : COLOR
				{
					fixed4 col = tex2D(_MainTex, i.texcoord) * i.color * _Color;
					return col;
				}
			ENDCG
		}
	}
}

