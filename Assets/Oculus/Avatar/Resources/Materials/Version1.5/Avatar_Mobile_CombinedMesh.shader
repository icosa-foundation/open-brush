//
// OvrAvatar Mobile combined mesh shader
// For use on non-expressive face meshes and other components
// Texture array approach for rendering a combined mesh avatar
// Coupled with OvrAvatarMaterialManager to populate the texture arrays
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

Shader "OvrAvatar/Avatar_Mobile_CombinedMesh"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Main Texture Array", 2DArray) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map Array", 2DArray) = "bump" {}
        [NoScaleOffset] _RoughnessMap("Roughness Map Array", 2DArray) = "black" {}

        _Dimmer("Dimmer", Range(0.0,1.0)) = 1.0
        _Alpha("Alpha", Range(0.0,1.0)) = 1.0

        // Index into the texture array needs an offset for precision
        _Slices("Texture Array Slices", int) = 4.97

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
            #pragma target 3.5
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile SECONDARY_LIGHT_OFF SECONDARY_LIGHT_ON
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            UNITY_DECLARE_TEX2DARRAY(_NormalMap);
            float4 _NormalMap_ST;
            UNITY_DECLARE_TEX2DARRAY(_RoughnessMap);

            int _Slices;

            half _Dimmer;
            half _Alpha;

            half4 _BaseColor[5];
            half _DiffuseIntensity[5];
            half _RimIntensity[5];
            half _ReflectionIntensity[5];

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
                float2 texcoord: TEXCOORD0;
                float4 vertexColor : COLOR0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 uv : TEXCOORD0;
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
                o.uv.xy = v.texcoord;
                o.uv.z = v.vertexColor.x * _Slices;
                return o;
            }

            fixed4 frag(v2f i) : COLOR
            {
                // Diffuse texture sample
                float4 albedoColor = UNITY_SAMPLE_TEX2DARRAY(_MainTex, i.uv);

                // Process normal map
                float3 transformedNormalUV = i.uv;
                transformedNormalUV.xy = float2(TRANSFORM_TEX(i.uv.xy, _NormalMap));
                float3 normalMap = UNITY_SAMPLE_TEX2DARRAY(_NormalMap, transformedNormalUV) * 2.0 - 1.0;
                float3x3 tangentTransform = float3x3(i.tangentDir, i.bitangentDir, i.normalDir);
                float3 normalDirection = normalize(mul(normalMap.rgb, tangentTransform));
                
                // Roughness contains metallic in r, smoothness in a, mask region in b and mask control in g
                half4 roughnessTex = UNITY_SAMPLE_TEX2DARRAY(_RoughnessMap, i.uv);

                // Normal/Light/View calculations
                half3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                half VdotN = saturate(dot(viewDirection, normalDirection));
                half NdotL = saturate(dot(normalDirection, _WorldSpaceLightPos0.xyz));

                // Sample the default reflection cubemap using the reflection vector
                float3 worldReflection = reflect(-viewDirection, normalDirection);
                half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, worldReflection);
                // Decode cubemap data into actual color
                half3 reflectionColor = DecodeHDR(skyData, unity_SpecCube0_HDR);

                // Get index into texture array
                int componentIndex = floor(i.uv.z + 0.5);

                // Base color from array
                float4 baseColor = _BaseColor[componentIndex];

                // Diffuse intensity from array
                half diffuseIntensity = _DiffuseIntensity[componentIndex];

                // Multiply in base color
                albedoColor.rgb *= baseColor.rgb;

                // Lerp diffuseIntensity with roughness map
                diffuseIntensity = lerp(diffuseIntensity, 1.0, roughnessTex.a);

                // Apply main light with a lerp between DiffuseIntensity and 1 based on the roughness
                albedoColor.rgb += diffuseIntensity * NdotL * _LightColor0;

                // Reflection from cubemap
                albedoColor.rgb += reflectionColor * (roughnessTex.a * _ReflectionIntensity[componentIndex]) * NdotL;

                // Rim term
#ifdef SECONDARY_LIGHT_ON
                // Secondary light proxy (direction and color) passed into the rim term
                NdotL = saturate(dot(normalDirection, _SecondaryLightDirection));
                albedoColor.rgb += pow(1.0 - VdotN, _RimIntensity[componentIndex]) * NdotL * _SecondaryLightColor;
#else
                albedoColor.rgb += pow(1.0 - VdotN, _RimIntensity[componentIndex]) * NdotL;
#endif

                // Global dimmer
                albedoColor.rgb *= _Dimmer;

#if !defined(UNITY_COLORSPACE_GAMMA)
                albedoColor.rgb = GammaToLinearSpace(albedoColor.rgb);
#endif
                albedoColor.rgb = saturate(albedoColor.rgb);

                // Set alpha, with special case for lashes
                albedoColor.a *= _Alpha;

                // Return clamped final color
                return albedoColor;
            }
            ENDCG
        }
    }
}
