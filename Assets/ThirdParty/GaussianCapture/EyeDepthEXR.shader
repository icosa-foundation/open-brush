Shader "Hidden/EyeDepthEXR"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            float _MinMeters;
            float _MaxMeters;

            float4 frag(v2f i) : SV_Target
            {
                float rawZ = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
            float d = LinearEyeDepth(rawZ);

            if (_MaxMeters > _MinMeters)
            {
                d = clamp(d, _MinMeters, _MaxMeters);
            }

            return float4(d, d, d, 1.0);
        }
        ENDCG
    }
    }
        Fallback Off
}
