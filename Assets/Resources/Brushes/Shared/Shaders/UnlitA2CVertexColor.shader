Shader "Custom/UnlitA2CVertexColor"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _DitherStrength ("Dither Strength", Range(0,0.5)) = 0.125
        _OrderedDither ("Ordered Dither (0/1)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 100

        Pass
        {
            AlphaToMask On
            Blend Off
            ZWrite On
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _DitherStrength;
            float _OrderedDither;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float4 screenPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            float OrderedDither4x4(float2 pixelPos)
            {
                // 4x4 Bayer matrix scaled to 0..1
                int2 p = int2(pixelPos) & 3;
                int index = p.x + p.y * 4;
                // Values: 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5
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
                // Simple hash-based noise in 0..1
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

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = i.color;
                float alpha = c.a;

                float2 pixelPos = (i.screenPos.xy / i.screenPos.w) * _ScreenParams.xy;
                float seed = ObjectSeed();
                pixelPos += seed * 4096.0;
                float ditherOrdered = OrderedDither4x4(pixelPos);
                float ditherNoise = InterleavedGradientNoise(pixelPos);
                float dither = lerp(ditherNoise, ditherOrdered, step(0.5, _OrderedDither));

                alpha = saturate(alpha + (dither - 0.5) * _DitherStrength);
                return fixed4(c.rgb, alpha);
            }
            ENDCG
        }
    }
}
