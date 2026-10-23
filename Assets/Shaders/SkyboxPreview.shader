Shader "Custom/CubemapPreview"
{
    Properties
    {
        _MainTex ("Cubemap", CUBE) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
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
                float3 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            samplerCUBE _MainTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                // Convert 2D UV to 3D coordinates for cubemap sampling
                o.uv = float3(v.uv.x * 2 - 1, v.uv.y * 2 - 1, 1);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = texCUBE(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }
    }
}
