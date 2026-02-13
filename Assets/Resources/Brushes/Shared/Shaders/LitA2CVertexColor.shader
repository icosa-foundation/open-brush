Shader "Custom/LitA2CVertexColor"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _DitherStrength ("Dither Strength", Range(0,0.5)) = 0.125
        _OrderedDither ("Ordered Dither (0/1)", Float) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 200

        AlphaToMask On
        Cull Off
        ZWrite On

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alpha:coverage
        #pragma target 3.0
        #include "UnityCG.cginc"

        fixed4 _Color;
        float _DitherStrength;
        float _OrderedDither;
        half _Metallic;
        half _Smoothness;

        struct Input
        {
            fixed4 color : COLOR;
            float4 screenPos;
        };

        float OrderedDither4x4(float2 pixelPos)
        {
            int2 p = int2(pixelPos) & 3;
            int index = p.x + p.y * 4;
            int d = (index == 0) ? 0 :
                    (index == 1) ? 8 :
                    (index == 2) ? 2 :
                    (index == 3) ? 10 :
                    (index == 4) ? 12 :
                    (index == 5) ? 4 :
                    (index == 6) ? 14 :
                    (index == 7) ? 6 :
                    (index == 8) ? 3 :
                    (index == 9) ? 11 :
                    (index == 10) ? 1 :
                    (index == 11) ? 9 :
                    (index == 12) ? 15 :
                    (index == 13) ? 7 :
                    (index == 14) ? 13 :
                    5;
            return (d + 0.5) / 16.0;
        }

        float InterleavedGradientNoise(float2 pixelPos)
        {
            float2 p = floor(pixelPos);
            float f = dot(p, float2(0.06711056, 0.00583715));
            return frac(52.9829189 * frac(f));
        }

        float ObjectSeed()
        {
            float3 t = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
            float h = dot(t, float3(0.1031, 0.11369, 0.13787));
            return frac(sin(h) * 43758.5453);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = IN.color * _Color;
            float alpha = c.a;

            float2 pixelPos = (IN.screenPos.xy / IN.screenPos.w) * _ScreenParams.xy;
            float seed = ObjectSeed();
            pixelPos += seed * 4096.0;

            float ditherOrdered = OrderedDither4x4(pixelPos);
            float ditherNoise = InterleavedGradientNoise(pixelPos);
            float dither = lerp(ditherNoise, ditherOrdered, step(0.5, _OrderedDither));

            alpha = saturate(alpha + (dither - 0.5) * _DitherStrength);

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = alpha;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
