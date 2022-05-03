//
// OvrAvatar eye lens shader
//
// Generates glint on the eye lens of expressive avatars
//

Shader "OvrAvatar/Avatar_EyeLens"
{
    Properties
    {
        _Cube("Cubemap Reflection", CUBE) = "black" {}
        _ReflectionIntensity("Reflection Intensity", Range(0.0,1.0)) = 0.2
        _GlintStrength("Glint Strength", Range(0, 10)) = 1.57
        _GlintSpead("Glint Spead", Range(32, 2048)) = 600
        _Alpha("Alpha", Range(0.0,1.0)) = 1.0
    }

    SubShader
    {
        Tags { "LightMode" = "ForwardBase" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            #include "AutoLight.cginc"

            samplerCUBE _Cube;
            half _ReflectionIntensity;
            half _GlintStrength;
            half _GlintSpead;
            half _Alpha;

            struct VertexInput
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct VertexOutput
            {
                float4 pos : SV_POSITION;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
            };

            VertexOutput vert(VertexInput v)
            {
                VertexOutput o = (VertexOutput)0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(VertexOutput i) : COLOR
            {
                i.normalDir = normalize(i.normalDir);
                half3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                half NdotLV = max(0, dot(i.normalDir, normalize(_WorldSpaceLightPos0.xyz + viewDirection)));
                half3 spec = pow(NdotLV, _GlintSpead) * _GlintStrength;

                // Sample the default reflection cubemap using the reflection vector
                half3 viewReflectDirection = reflect(-viewDirection, i.normalDir);
                half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, viewReflectDirection);
                // Decode cubemap data into actual color
                half3 reflectionColor = DecodeHDR(skyData, unity_SpecCube0_HDR);

                half4 finalColor;
                finalColor.rgb = reflectionColor.rgb * _ReflectionIntensity;
                finalColor.rgb += spec;
                finalColor.a = (finalColor.r + finalColor.g + finalColor.b) / 3;

                return finalColor;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
