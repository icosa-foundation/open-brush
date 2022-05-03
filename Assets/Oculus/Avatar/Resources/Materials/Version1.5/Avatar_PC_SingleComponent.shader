//
// OvrAvatar PC single component shader
// For use on non-expressive face meshes and other components
//
// Unity Surface Shader implementation
// Mobile vertex/fragment shader is recommended for use on mobile platforms for performance
//
// Uses transparent queue for fade effects
//

Shader "OvrAvatar/Avatar_PC_SingleComponent"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Color (RGB)", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _RoughnessMap("Roughness Map", 2D) = "black" {}

        _BaseColor("Color Tint", Color) = (1.0,1.0,1.0,1.0)
        _Dimmer("Dimmer", Range(0.0,1.0)) = 1.0
        _Alpha("Alpha", Range(0.0,1.0)) = 1.0

        _DiffuseIntensity("Diffuse Intensity", Range(0.0,1.0)) = 0.3
        _SmoothnessMultiplier("Smoothness Multiplier", Range(0.0,1.0)) = 1.0
        _MetallicMultiplier("Metallic Multiplier", Range(0.0,1.0)) = 1.0
        _RimIntensity("Rim Intensity", Range(0.0,10.0)) = 5.0

        [HideInInspector] _SrcBlend("", Float) = 1
        [HideInInspector] _DstBlend("", Float) = 0
    }
    
    SubShader
    {
        Blend [_SrcBlend] [_DstBlend]
        Cull Back
        CGPROGRAM
#pragma surface surf Standard keepalpha fullforwardshadows
#pragma target 3.0
#pragma fragmentoption ARB_precision_hint_fastest
#include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _RoughnessMap;

        half4 _BaseColor;
        half _Dimmer;
        half _Alpha;

        half _DiffuseIntensity;
        half _SmoothnessMultiplier;
        half _MetallicMultiplier;
        half _RimIntensity;

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
            // Diffuse texture sample
            half4 albedoColor = tex2D(_MainTex, IN.uv_MainTex);

            // Unpack normal map
#if (UNITY_VERSION >= 20171)
            o.Normal = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
#else
            o.Normal = tex2D(_NormalMap, IN.uv_MainTex) * 2.0 - 1.0;
#endif
            // Roughness contains metallic in r, smoothness in a
            half4 roughnessTex = tex2D(_RoughnessMap, IN.uv_MainTex);

            // Normal/Light/View calculations
            half NdotL = saturate(dot(WorldNormalVector(IN, o.Normal), _WorldSpaceLightPos0.xyz));
            half VdotN = saturate(dot(normalize(IN.viewDir), o.Normal));

            // Color space conversions if we are in linear
#ifndef UNITY_COLORSPACE_GAMMA
            _BaseColor.rgb = LinearToGammaSpace(_BaseColor.rgb);
#endif
            // Set smoothness and metallic
            o.Smoothness = roughnessTex.a * _SmoothnessMultiplier;
            o.Metallic = roughnessTex.r * _MetallicMultiplier;

            // Final base color including DiffuseIntensity and NdotL for lighting gradient
            _BaseColor.rgb += _DiffuseIntensity * NdotL;
            
            // Multiply texture with base color
            o.Albedo = albedoColor.rgb * _BaseColor;

            // Rim term
            o.Albedo += pow(1.0 - VdotN, _RimIntensity) * NdotL;

            // Global dimmer
            o.Albedo *= _Dimmer;

            // Convert back to linear color space if we are in linear
#if !defined(UNITY_COLORSPACE_GAMMA)
            o.Albedo = GammaToLinearSpace(o.Albedo);
#endif
            o.Albedo = saturate(o.Albedo);

            // Global alpha
            o.Alpha = albedoColor.a * _Alpha;
        }
        ENDCG
    }
    Fallback "Diffuse"
}