// URP-native port of the rim-lit "glass" look from Blocks/BlocksGlass,
// used by the scripted tool preview (cube/sphere/quad/etc).
Shader "Custom/ScriptedToolPreview" {
  Properties {
    _Color ("Color", Color) = (1, 1, 1, 1)
    _RimIntensity ("Rim Intensity", Range(0, 1)) = 0.7
    _RimPower ("Rim Power", Range(0, 16)) = 4
    _Shininess ("Shininess", Range(0, 1)) = 0.8
  }

  SubShader {
    Tags {
      "RenderPipeline" = "UniversalPipeline"
      "Queue" = "Transparent"
      "RenderType" = "Transparent"
      "IgnoreProjector" = "True"
    }
    LOD 200
    Blend One SrcAlpha
    ZWrite Off
    Cull Off

    Pass {
      Tags { "LightMode" = "UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      half _RimIntensity;
      half _RimPower;
      half _Shininess;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
        float3 normalOS   : NORMAL;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
        float3 positionWS  : TEXCOORD0;
        float3 normalWS    : TEXCOORD1;
      };

      Varyings Vert(Attributes IN) {
        Varyings OUT;
        OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
        OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
        OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
        return OUT;
      }

      half4 Frag(Varyings IN, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target {
        bool isFront = IS_FRONT_VFACE(frontFace, true, false);
        half backfaceDimming = isFront ? 1.0h : 0.25h;

        float3 normalWS  = normalize(IN.normalWS) * (isFront ? 1.0 : -1.0);
        float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

        // Fresnel rim (matches original: pow(1 - saturate(dot(V,N)), power) * intensity)
        half NdotV = saturate(dot(normalWS, viewDirWS));
        half rim   = pow(1.0h - NdotV, _RimPower) * _RimIntensity * backfaceDimming;

        // Approximate the original StandardSpecular highlight with Blinn-Phong on the main light.
        Light mainLight = GetMainLight();
        float3 halfDir = normalize(viewDirWS + mainLight.direction);
        half NdotH = saturate(dot(normalWS, halfDir));
        half specExp = exp2(_Shininess * 10.0h + 1.0h);
        half3 specular = _Color.rgb * pow(NdotH, specExp) * mainLight.color.rgb * backfaceDimming;

        // Albedo is zero (like the original), so the body contributes nothing under additive blend.
        half3 rgb = specular + rim * _Color.rgb;
        return half4(rgb, _Color.a);
      }
      ENDHLSL
    }
  }
}
