//
// OvrAvatar PC single component expressive face shader
// For use on expressive face meshes
//
// Unity Surface Shader implementation
// Mobile vertex/fragment shader is recommended for use on mobile platforms for performance.
//
// Uses transparent queue for fade effects
//
// Color and appearance of the facial regions controlled via G&B channels in roughness texture
// Pupil size controlled by manipulating UV coordinates
//

Shader "OvrAvatar/Avatar_PC_SingleComponentExpressive"
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
        _MetallicMultiplier("Metallic Multiplier", Range(0.0,1.0)) = 0.3
        _RimIntensity("Rim Intensity", Range(0.0,10.0)) = 5.0

        _PupilSize("Pupil Size", Range(-1, 2)) = 0
        _LipSmoothness("Lip Smoothness", Range(0, 1)) = 0

        _MaskColorIris("Iris Color", Color) = (0.0,0.0,0.0,1.0)
        _MaskColorLips("Lips Color", Color) = (0.0,0.0,0.0,1.0)
        _MaskColorBrows("Brows Color", Color) = (0.0,0.0,0.0,1.0)
        _MaskColorLashes("Lashes Color", Color) = (0.0,0.0,0.0,1.0)
        _MaskColorSclera("Sclera Color", Color) = (0.0,0.0,0.0,1.0)
        _MaskColorGums("Gums Color", Color) = (0.0,0.0,0.0,1.0)
        _MaskColorTeeth("Teeth Color", Color) = (0.0,0.0,0.0,1.0)

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
        half _SmoothnessMultiplierLips;
        half _MetallicMultiplier;
        half _RimIntensity;

        half _PupilSize;
        half _LipSmoothness;

        fixed4 _MaskColorIris;
        fixed4 _MaskColorLips;
        fixed4 _MaskColorBrows;
        fixed4 _MaskColorLashes;
        fixed4 _MaskColorLashesEnd;
        fixed4 _MaskColorSclera;
        fixed4 _MaskColorGums;
        fixed4 _MaskColorTeeth;

        static const int ONE = 1;
        static const fixed ALPHA_CLIP_THRESHOLD = 0.7;
        static const int IRIS_BRIGHTNESS_MODIFIER = 2;
         static const fixed SCLERA_BRIGHTNESS_MODIFIER = 1.2;
        static const fixed LIP_SMOOTHNESS_MULTIPLIER = 0.5;
        static const fixed LIP_SMOOTHNESS_MIN_NDOTL = 0.3;
        static const fixed BROWS_LASHES_DIFFUSEINTENSITY = ONE - 0.25;
        static const int COLOR_MULTIPLIER = 255;
        static const half2 PUPIL_CENTER_UV = half2(0.127, 0.1175);
        static const half DILATION_ENVELOPE = 0.024;
        static const half2 EYE_REGION_UV = PUPIL_CENTER_UV + DILATION_ENVELOPE;

        static const int MASK_SLICE_SIZE = 17;
        static const half MASK_SLICE_THRESHOLD = MASK_SLICE_SIZE * 0.5f;
        static const int MASK_INDEX_IRIS = 255;
        static const int MASK_INDEX_SCLERA = 238;
        static const int MASK_INDEX_LASHES = 221;
        static const int MASK_INDEX_LIPS = 204;
        static const int MASK_INDEX_GUMS = 187;
        static const int MASK_INDEX_TEETH = 170;
        static const int MASK_INDEX_BROWS = 153;

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
            // Pupil size offsets uv coords
            if (all(IN.uv_MainTex < EYE_REGION_UV))
            {
                IN.uv_MainTex -= PUPIL_CENTER_UV;
                half pupil = saturate(length(IN.uv_MainTex) / DILATION_ENVELOPE);
                IN.uv_MainTex *= lerp(ONE, pupil, _PupilSize);
                IN.uv_MainTex += PUPIL_CENTER_UV;
            }

            // Diffuse texture sample
            half4 albedoColor = tex2D(_MainTex, IN.uv_MainTex);

            // Unpack normal map
    #if (UNITY_VERSION >= 20171)
            o.Normal = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
    #else
            o.Normal = tex2D(_NormalMap, IN.uv_MainTex) * 2.0 - ONE;
    #endif
            // Roughness contains metallic in r, smoothness in a, mask region in b and mask control in g
            half4 roughnessTex = tex2D(_RoughnessMap, IN.uv_MainTex);

            // Normal/Light/View calculations
            half NdotL = saturate(dot(WorldNormalVector(IN, o.Normal), _WorldSpaceLightPos0.xyz));
            half VdotN = saturate(dot(normalize(IN.viewDir), o.Normal));

            // Color space conversions if we are in linear
    #ifndef UNITY_COLORSPACE_GAMMA
            _BaseColor.rgb = LinearToGammaSpace(_BaseColor.rgb);
            _MaskColorIris.rgb = LinearToGammaSpace(_MaskColorIris.rgb);
            _MaskColorLips.rgb = LinearToGammaSpace(_MaskColorLips.rgb);
            _MaskColorBrows.rgb = LinearToGammaSpace(_MaskColorBrows.rgb);
            _MaskColorLashes.rgb = LinearToGammaSpace(_MaskColorLashes.rgb);
            _MaskColorLashesEnd.rgb = LinearToGammaSpace(_MaskColorLashesEnd.rgb);
            _MaskColorSclera.rgb = LinearToGammaSpace(_MaskColorSclera.rgb);
            _MaskColorGums.rgb = LinearToGammaSpace(_MaskColorGums.rgb);
            _MaskColorTeeth.rgb = LinearToGammaSpace(_MaskColorTeeth.rgb);
    #endif

            // Mask regions and colors
            half irisScalar = abs(roughnessTex.b * COLOR_MULTIPLIER - MASK_INDEX_IRIS) <= MASK_SLICE_THRESHOLD ? roughnessTex.g : 0.0f;
            half lipsScalar = abs(roughnessTex.b * COLOR_MULTIPLIER - MASK_INDEX_LIPS) <= MASK_SLICE_THRESHOLD ? roughnessTex.g : 0.0f;
            half browsScalar = abs(roughnessTex.b * COLOR_MULTIPLIER - MASK_INDEX_BROWS) <= MASK_SLICE_THRESHOLD ? roughnessTex.g : 0.0f;;
            half lashesScalar = abs(roughnessTex.b * COLOR_MULTIPLIER - MASK_INDEX_LASHES) <= MASK_SLICE_THRESHOLD ? roughnessTex.g : 0.0f;
            half scleraScalar = abs(roughnessTex.b * COLOR_MULTIPLIER - MASK_INDEX_SCLERA) <= MASK_SLICE_THRESHOLD ? roughnessTex.g : 0.0f;
            half teethScalar = abs(roughnessTex.b * COLOR_MULTIPLIER - MASK_INDEX_TEETH) <= MASK_SLICE_THRESHOLD ? roughnessTex.g : 0.0f;;
            half gumsScalar = abs(roughnessTex.b * COLOR_MULTIPLIER - MASK_INDEX_GUMS) <= MASK_SLICE_THRESHOLD ? roughnessTex.g : 0.0f;

            half3 maskIris = irisScalar * (_MaskColorIris.rgb * IRIS_BRIGHTNESS_MODIFIER - _BaseColor.rgb);
            half3 maskBrows = browsScalar * (_MaskColorBrows.rgb - _BaseColor.rgb);
            half3 maskLashes = lashesScalar * (_MaskColorLashes.rgb - _BaseColor.rgb);
            half3 maskSclera = scleraScalar * (_MaskColorSclera.rgb * SCLERA_BRIGHTNESS_MODIFIER - _BaseColor.rgb);
            half3 maskTeeth = teethScalar * (_MaskColorTeeth.rgb - _BaseColor.rgb);
            half3 maskGums = gumsScalar * (_MaskColorGums.rgb - _BaseColor.rgb);
            // Lip tint excluded from color mask as it lerps with texture color
            half3 colorMask = maskIris + maskBrows + maskLashes + maskSclera + maskTeeth + maskGums;
        
            // Set smoothness
            o.Smoothness = roughnessTex.a * _SmoothnessMultiplier;

            // Force no smoothness on gums & teeth
            o.Smoothness *= ONE - saturate(teethScalar + gumsScalar);

            // Use global smoothness or lip smoothness modifier
            o.Smoothness += (_LipSmoothness * LIP_SMOOTHNESS_MULTIPLIER) * lipsScalar;

            // Set metallic with global modifier
            o.Metallic = roughnessTex.r * _MetallicMultiplier;

            // Brows and lashes modify DiffuseIntensity
            _DiffuseIntensity *= ONE - (saturate(browsScalar + lashesScalar) * BROWS_LASHES_DIFFUSEINTENSITY);

            // Modify base color with DiffuseIntensity * NdotL for lighting gradient
            _BaseColor.rgb += _DiffuseIntensity * NdotL;
        
            // Add in color mask
            _BaseColor.rgb += colorMask;

            // Multiply texture with base color with special case for lips
            o.Albedo.rgb = lerp(albedoColor.rgb * _BaseColor.rgb, _MaskColorLips.rgb, lipsScalar * _MaskColorLips.a);

            // Rim term
            o.Albedo += pow(ONE - VdotN, _RimIntensity) * NdotL;

            // Global dimmer
            o.Albedo *= _Dimmer;

            // Convert back to linear color space if we are in linear
    #if !defined(UNITY_COLORSPACE_GAMMA)
            o.Albedo = GammaToLinearSpace(o.Albedo);
    #endif
            o.Albedo = saturate(o.Albedo);

            // Set alpha, with special case for lashes
            o.Alpha = saturate(albedoColor.a * lerp(ONE, _Alpha, ONE - lashesScalar) * _Alpha);

            // Clip fragments in the lash region for clean lash transparency
            clip(o.Alpha - lerp(0.0, ALPHA_CLIP_THRESHOLD, lashesScalar));
        }
        ENDCG
    }
    Fallback "Diffuse"
}