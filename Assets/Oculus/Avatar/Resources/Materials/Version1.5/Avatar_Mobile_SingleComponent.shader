//
// OvrAvatar Mobile single component shader
// For use on non-expressive face meshes and other components
//
// Unity vertex-fragnment implementation
// Simplified lighting model recommended for use on mobile supporting one directional light
// Surface shader recommended on PC
//
// Uses transparent queue for fade effects
//
// Simple mouth animation with speech done with vertex perturbation
//
// Shader keywords:
// - SECONDARY_LIGHT_ON SECONDARY_LIGHT_OFF
//   Enable SECONDARY_LIGHT_ON for a second "light" comprised of _SecondaryLightDirection and
//   _SecondaryLightColor This will influence the rim effect providing a lit contour to the avatar
//

Shader "OvrAvatar/Avatar_Mobile_SingleComponent"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Main Texture", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _RoughnessMap("Roughness Map", 2D) = "black" {}

        _BaseColor("Color Tint", Color) = (1.0,1.0,1.0,1.0)
        _Dimmer("Dimmer", Range(0.0,1.0)) = 1.0
        _Alpha("Alpha", Range(0.0,1.0)) = 1.0

        _DiffuseIntensity("Diffuse Intensity", Range(0.0,1.0)) = 0.3
        _RimIntensity("Rim Intensity", Range(0.0,10.0)) = 5.0
        _ReflectionIntensity("Reflection Intensity", Range(0.0,1.0)) = 0.0

        _Voice("Voice", Range(0.0,1.0)) = 0.0
        [HideInInspector] _MouthPosition("Mouth position", Vector) = (0,0,0,1)
        [HideInInspector] _MouthDirection("Mouth direction", Vector) = (0,0,0,1)
        [HideInInspector] _MouthEffectDistance("Mouth Effect Distance", Float) = 0.03
        [HideInInspector] _MouthEffectScale("Mouth Effect Scaler", Float) = 1

        [HideInInspector] _SrcBlend("", Float) = 1
        [HideInInspector] _DstBlend("", Float) = 0
    }

    SubShader
    {
        Tags { "LightMode" = "ForwardBase" "IgnoreProjector" = "True"}
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile SECONDARY_LIGHT_OFF SECONDARY_LIGHT_ON
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            sampler2D _MainTex;
            sampler2D _NormalMap;
            float4 _NormalMap_ST;
            sampler2D _RoughnessMap;

            half4 _BaseColor;
            half _Dimmer;
            half _Alpha;

            half _DiffuseIntensity;
            half _RimIntensity;
            half _ReflectionIntensity;

            half3 _SecondaryLightDirection;
            half4 _SecondaryLightColor;

            half _Voice;
            half4 _MouthPosition;
            half4 _MouthDirection;
            half _MouthEffectDistance;
            half _MouthEffectScale;

            static const fixed MOUTH_ZSCALE = 0.5f;
            static const fixed MOUTH_DROPOFF = 0.01f;

            struct appdata
            {
                float4 vertex: POSITION;
                float3 normal: NORMAL;
                float4 tangent: TANGENT;
                float4 uv: TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 posWorld: TEXCOORD1;
                float3 normalDir: TEXCOORD2;
                float3 tangentDir: TEXCOORD3;
                float3 bitangentDir: TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;

                // Mouth vertex animation with voice
                float4 worldVert = mul(unity_ObjectToWorld, v.vertex);
                float3 delta = _MouthPosition - worldVert;
                delta.z *= MOUTH_ZSCALE;
                half dist = length(delta);
                half scaledMouthDropoff = _MouthEffectScale * MOUTH_DROPOFF;
                half scaledMouthEffect = _MouthEffectScale * _MouthEffectDistance;
                half displacement = _Voice * smoothstep(scaledMouthEffect + scaledMouthDropoff, scaledMouthEffect, dist);
                worldVert.xyz -= _MouthDirection * displacement;
                v.vertex = mul(unity_WorldToObject, worldVert);

                // Calculate tangents for normal mapping
                o.normalDir = normalize(UnityObjectToWorldNormal(v.normal));
                o.tangentDir = normalize(mul(unity_ObjectToWorld, half4(v.tangent.xyz, 0.0)).xyz);
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);

                o.posWorld = worldVert;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : COLOR
            {
                // Diffuse texture sample
                half4 albedoColor = tex2D(_MainTex, i.uv);

                // Process normal map
#if (UNITY_VERSION >= 20171)
                float3 normalMap = UnpackNormal(tex2D(_NormalMap, i.uv));
#else
                float3 normalMap = tex2D(_NormalMap, i.uv) * 2.0 - ONE;
#endif
                float3x3 tangentTransform = float3x3(i.tangentDir, i.bitangentDir, i.normalDir);
                float3 normalDirection = normalize(mul(normalMap.rgb, tangentTransform));

                // Roughness contains metallic in r, smoothness in a, mask region in b and mask control in g
                half4 roughnessTex = tex2D(_RoughnessMap, i.uv);

                // Normal/Light/View calculations
                half3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                half VdotN = saturate(dot(viewDirection, normalDirection));
                half NdotL = saturate(dot(normalDirection, _WorldSpaceLightPos0.xyz));

                // Sample the default reflection cubemap using the reflection vector
                float3 worldReflection = reflect(-viewDirection, normalDirection);
                half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, worldReflection);
                // Decode cubemap data into actual color
                half3 reflectionColor = DecodeHDR(skyData, unity_SpecCube0_HDR);

#ifndef UNITY_COLORSPACE_GAMMA
                _BaseColor.rgb = LinearToGammaSpace(_BaseColor.rgb);
#endif
                // Multiply in base color
                albedoColor.rgb *= _BaseColor.rgb;

                // Lerp diffuseIntensity with roughness map
                _DiffuseIntensity = lerp(_DiffuseIntensity, 1.0, roughnessTex.a);

                // Apply main light with a lerp between DiffuseIntensity and 1 based on the roughness
                albedoColor.rgb += _DiffuseIntensity * NdotL * _LightColor0;

                // Rim term
#ifdef SECONDARY_LIGHT_ON
                // Secondary light proxy (direction and color) passed into the rim term
                NdotL = saturate(dot(normalDirection, _SecondaryLightDirection));
                albedoColor.rgb += pow(1.0 - VdotN, _RimIntensity) * NdotL * _SecondaryLightColor;
#else
                albedoColor.rgb += pow(1.0 - VdotN, _RimIntensity) * NdotL;
#endif
                // Reflection from cubemap
                albedoColor.rgb += reflectionColor * (roughnessTex.a * _ReflectionIntensity) * NdotL;

                // Global dimmer
                albedoColor.rgb *= _Dimmer;

                // Convert back to linear color space if we are in linear
#if !defined(UNITY_COLORSPACE_GAMMA)
                albedoColor.rgb = GammaToLinearSpace(albedoColor.rgb);
#endif
                albedoColor.rgb = saturate(albedoColor.rgb);

                // Set alpha
                albedoColor.a *= _Alpha;

                // Return clamped final color
                return albedoColor;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}