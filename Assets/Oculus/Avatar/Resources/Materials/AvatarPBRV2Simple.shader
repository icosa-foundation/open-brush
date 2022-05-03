//
// OvrAvatar Simple Avatar Shader 
// Uses the Avatar Material Model on the Standard Surface Shader
//

Shader "OvrAvatar/AvatarPBRV2Simple"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Color (RGB)", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _RoughnessMap("Roughness Map", 2D) = "black" {}
    }
    
    SubShader
    {
        Blend One Zero
        Cull Back
        CGPROGRAM
#pragma surface surf Standard keepalpha fullforwardshadows

#pragma target 3.0

#pragma fragmentoption ARB_precision_hint_fastest

#include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _RoughnessMap;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_NormalMap;
            float2 uv_RoughnessMap;
            float3 viewDir;
            float3 worldNormal; INTERNAL_DATA
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {    

#if (UNITY_VERSION >= 20171)
            o.Normal = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
#else
            o.Normal = tex2D(_NormalMap, IN.uv_MainTex) * 2.0 - 1.0;
#endif
            half4 roughnessTex = tex2D(_RoughnessMap, IN.uv_MainTex);

            o.Albedo = tex2D(_MainTex, IN.uv_MainTex);
            o.Smoothness = roughnessTex.a;
            o.Metallic = roughnessTex.r;

#if !defined(UNITY_COLORSPACE_GAMMA)
            o.Albedo = GammaToLinearSpace(o.Albedo);
#endif
            o.Albedo = saturate(o.Albedo);
            o.Alpha = 1.0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}