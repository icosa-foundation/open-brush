Shader "Hidden/LIV_Font"
{
	Properties
	{
		_FontTex("Font Texture", 2D) = "black" {}
		_DataTex("Data Texture", 2D) = "black" {}
		_CharSize("Chars X, Y, 1/x, 1/y", Vector) = (18, 6, 0.05555556, 0.1666667)
	}
	SubShader
	{
		Tags { "Queue" = "Overlay" "LightMode" = "Always" "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True"}

		Pass
		{
			Name "WRITE"

			Blend Off
			ZTest Always
			ZWrite Off
			Cull Off
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
				float4 vertex : SV_POSITION;
				float2 dataUV : TEXCOORD0;
				float2 fontUV : TEXCOORD1;
			};

			sampler2D _FontTex;
			sampler2D _DataTex;

      CBUFFER_START(UnityPerMaterial)
			float4 _CharSize;
			float4 _DataTex_TexelSize;
      CBUFFER_END
			
			v2f vert(appdata v)
			{
				v2f o;
				float2 uv = -1.0 + v.vertex.xy * 2.0;
				uv.y *= _ProjectionParams.x;
				o.vertex.xy = uv;
				o.vertex.z = 0.0;
				o.vertex.w = 1.0;
				o.dataUV = float2(v.uv.x, 1.0 - v.uv.y);
				o.fontUV = v.uv;
				return o;
			}
			
			fixed4 frag(v2f i) : SV_Target
			{
				float2 uv = fmod(i.fontUV / _DataTex_TexelSize.xy, 1.0);
				int charIndex = floor(tex2D(_DataTex, i.dataUV).a * 255) - 32;
				clip(charIndex);
				float2 char = float2(charIndex % _CharSize.x, _CharSize.y - floor(charIndex / _CharSize.x) - 1.0);
				return tex2D(_FontTex, (char + uv) * _CharSize.zw).r;
			}

			ENDCG
		}
	}
}
