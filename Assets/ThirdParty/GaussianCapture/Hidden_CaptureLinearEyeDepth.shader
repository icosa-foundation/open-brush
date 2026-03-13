Shader "Hidden/CaptureLinearEyeDepth"
{
    SubShader
    {
        Tags { "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "Always" }
            ZWrite On
            ZTest LEqual
            Cull Back
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            sampler2D _BaseMap; float4 _BaseMap_ST;
            sampler2D _BaseColorMap; float4 _BaseColorMap_ST;

            float4 _Color;
            float4 _BaseColor;
            float4 _TintColor;

            float  _AlphaThreshold; 
            float  _CaptureFar;     

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos  : SV_POSITION;
                float2 uv0  : TEXCOORD0;
                float  eyeZ : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 viewPos = UnityObjectToViewPos(v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.eyeZ = -viewPos.z;     
                o.uv0 = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float ComputeAlpha(float2 uv)
            {
                float a = 1.0;

                float4 cMain = tex2D(_MainTex, uv);
                a *= (cMain.a == 0 ? 1 : cMain.a);

                float2 uvBase = TRANSFORM_TEX(uv, _BaseMap);
                float4 cBase = tex2D(_BaseMap, uvBase);
                a *= (cBase.a == 0 ? 1 : cBase.a);

                float2 uvBC = TRANSFORM_TEX(uv, _BaseColorMap);
                float4 cBC = tex2D(_BaseColorMap, uvBC);
                a *= (cBC.a == 0 ? 1 : cBC.a);

                a *= (_Color.a == 0 ? 1 : _Color.a);
                a *= (_BaseColor.a == 0 ? 1 : _BaseColor.a);
                a *= (_TintColor.a == 0 ? 1 : _TintColor.a);

                return a;
            }

            float4 frag(v2f i) : SV_Target
            {
                float a = ComputeAlpha(i.uv0);
                clip(a - _AlphaThreshold);

                float depth = (i.eyeZ > 0) ? i.eyeZ : _CaptureFar;
                return float4(depth, 0, 0, 1);
            }
            ENDHLSL
        }
    }
        Fallback Off
}
