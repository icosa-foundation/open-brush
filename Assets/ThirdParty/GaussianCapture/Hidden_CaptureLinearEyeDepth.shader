Shader "Hidden/CaptureLinearEyeDepth"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        _BaseMap ("BaseMap", 2D) = "white" {}
        _BaseColorMap ("BaseColorMap", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _BaseColor ("BaseColor", Color) = (1,1,1,1)
        _TintColor ("TintColor", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "Always" }
            ZWrite On
            ZTest LEqual
            Cull Off
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
                float2 uvMain : TEXCOORD0;
                float2 uvBase : TEXCOORD1;
                float2 uvBaseColor : TEXCOORD2;
                float  eyeZ : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 viewPos = UnityObjectToViewPos(v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.eyeZ = -viewPos.z;
                o.uvMain = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvBase = TRANSFORM_TEX(v.uv, _BaseMap);
                o.uvBaseColor = TRANSFORM_TEX(v.uv, _BaseColorMap);
                return o;
            }

            float ComputeAlpha(v2f i)
            {
                float4 cMain = tex2D(_MainTex, i.uvMain);
                float4 cBase = tex2D(_BaseMap, i.uvBase);
                float4 cBC = tex2D(_BaseColorMap, i.uvBaseColor);

                float alphaFromTextures = max(cMain.a, max(cBase.a, cBC.a));
                float alphaFromColors = max(_Color.a, max(_BaseColor.a, _TintColor.a));

                return alphaFromTextures * alphaFromColors;
            }

            float4 frag(v2f i) : SV_Target
            {
                float a = ComputeAlpha(i);
                clip(a - _AlphaThreshold);

                float depth = (i.eyeZ > 0) ? i.eyeZ : _CaptureFar;
                return float4(depth, 0, 0, 1);
            }
            ENDHLSL
        }
    }
        Fallback Off
}
