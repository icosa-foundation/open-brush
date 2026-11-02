Shader "Hidden/OpenBrush/ResolveCaptureAlpha"
{
    Properties
    {
        _MainTex ("Resolved colour", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 Frag(v2f_img input) : SV_Target
            {
                float4 colour = tex2D(_MainTex, input.uv);
                colour.rgb /= max(colour.a, 0.000001);
                return colour;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
