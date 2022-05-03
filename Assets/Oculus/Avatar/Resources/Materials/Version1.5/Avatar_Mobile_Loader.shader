//
// OvrAvatar Mobile Single Component Loading shader
//
// Cut-down single component version of the avatar shader to be used during combined mesh loading
//
// See OvrAvatarMaterialManager implementation notes
// 

Shader "OvrAvatar/Avatar_Mobile_Loader"
{
    Properties
    {
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}

        _BaseColor("Color Tint", Color) = (1.0,1.0,1.0,1.0)
        _Dimmer("Dimmer", Range(0.0,1.0)) = 1.0
        _LoadingDimmer("Loading Dimmer", Range(0.0,1.0)) = 1.0
        _Alpha("Alpha", Range(0.0,1.0)) = 1.0

        _DiffuseIntensity("Diffuse Intensity", Range(0.0,1.0)) = 0.3
        _RimIntensity("Rim Intensity", Range(0.0,10.0)) = 5.0
    }

    SubShader
    {
        Tags { "LightMode" = "ForwardBase" "IgnoreProjector" = "True"}
        Pass
        {
            Blend One Zero
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma fragmentoption ARB_precision_hint_fastest
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"

            sampler2D _NormalMap;
            float4 _NormalMap_ST;

            float4 _BaseColor;
            float _Dimmer;
            float _LoadingDimmer;
            float _Alpha;

            float _DiffuseIntensity;
            float _RimIntensity;

            static const fixed	MOUTH_ZSCALE = 0.5f;
            static const fixed	MOUTH_DROPOFF = 0.01f;

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

                // Calculate tangents for normal mapping
                o.normalDir = normalize(UnityObjectToWorldNormal(v.normal));
                o.tangentDir = normalize(mul(unity_ObjectToWorld, half4(v.tangent.xyz, 0.0)).xyz);
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);

                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : COLOR
            {
                // Process normal map
#if (UNITY_VERSION >= 20171)
                float3 normalMap = UnpackNormal(tex2D(_NormalMap, i.uv));
#else
                float3 normalMap = tex2D(_NormalMap, i.uv) * 2.0 - 1.0;
#endif
                float3x3 tangentTransform = float3x3(i.tangentDir, i.bitangentDir, i.normalDir);
                float3 normalDirection = normalize(mul(normalMap.rgb, tangentTransform));

                // Normal/Light/View calculations
                half3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                half VdotN = saturate(dot(viewDirection, normalDirection));
                half NdotL = saturate(dot(normalDirection, _WorldSpaceLightPos0.xyz));

                // Calculate color
                float4 albedoColor;

#if !defined(UNITY_COLORSPACE_GAMMA)
                _BaseColor.rgb = LinearToGammaSpace(_BaseColor.rgb);
#endif
                // Final base color including DiffuseIntensity and NdotL for lighting gradient
                _BaseColor.rgb += _DiffuseIntensity * NdotL * _LightColor0;

                // No diffuse texture in the loader shader
                albedoColor = _BaseColor;

                // Rim term
                albedoColor.rgb += pow(1.0 - VdotN, _RimIntensity) * NdotL;

                // Global dimmer
                albedoColor.rgb *= lerp(_Dimmer, _LoadingDimmer, step(_LoadingDimmer, _Dimmer));

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
}
