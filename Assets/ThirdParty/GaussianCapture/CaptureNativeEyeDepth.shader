Shader "Hidden/CaptureNativeEyeDepth"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

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
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            UNITY_DECLARE_DEPTH_TEXTURE(_CaptureNativeDepth);

            float frag(v2f input) : SV_Target
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CaptureNativeDepth, input.uv);
                return LinearEyeDepth(rawDepth);
            }
            ENDCG
        }
    }
    Fallback Off
}
