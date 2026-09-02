Shader "Sonic Ether/Emissive/Textured" {
Properties {
  _EmissionColor ("Emission Color", Color) = (1,1,1,1)
  _DiffuseColor ("Diffuse Color", Color) = (1,1,1,1)
  _MainTex ("Diffuse Texture", 2D) = "white" {}
  _Illum ("Emission Texture", 2D) = "white" {}
  _EmissionGain ("Emission Gain", Range(0,1)) = 0.5
  _EmissionTextureContrast ("Emission Texture Contrast", Range(1,3)) = 1.0
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  LOD 200

  Pass {
    Tags { "LightMode"="UniversalForward" }

    HLSLPROGRAM
    #pragma target 3.0
    #pragma vertex Vert
    #pragma fragment Frag
    #pragma multi_compile_instancing
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
    #pragma multi_compile_fragment _ _SHADOWS_SOFT

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
    TEXTURE2D(_Illum); SAMPLER(sampler_Illum);

    CBUFFER_START(UnityPerMaterial)
    half4 _EmissionColor;
    half4 _DiffuseColor;
    float4 _MainTex_ST;
    float4 _Illum_ST;
    half _EmissionGain;
    half _EmissionTextureContrast;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      float3 normalOS : NORMAL;
      float2 uv : TEXCOORD0;
      UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings {
      float4 positionCS : SV_POSITION;
      float2 uvMain : TEXCOORD0;
      float2 uvIllum : TEXCOORD1;
      float3 positionWS : TEXCOORD2;
      float3 normalWS : TEXCOORD3;
      UNITY_VERTEX_INPUT_INSTANCE_ID
      UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes IN)
    {
      Varyings OUT;
      UNITY_SETUP_INSTANCE_ID(IN);
      OUT = (Varyings)0;
      UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
      UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

      VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
      VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);
      OUT.positionCS = pos.positionCS;
      OUT.positionWS = pos.positionWS;
      OUT.normalWS = nrm.normalWS;
      OUT.uvMain = TRANSFORM_TEX(IN.uv, _MainTex);
      OUT.uvIllum = TRANSFORM_TEX(IN.uv, _Illum);
      return OUT;
    }

    half4 Frag(Varyings IN) : SV_Target
    {
      UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

      half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMain);
      half3 albedo = tex.rgb * _DiffuseColor.rgb;

      Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
      half ndotl = saturate(dot(normalize(IN.normalWS), normalize(mainLight.direction)));
      half3 lit = albedo * mainLight.color * mainLight.shadowAttenuation * ndotl;

      half3 emissTex = SAMPLE_TEXTURE2D(_Illum, sampler_Illum, IN.uvIllum).rgb;
      half emissL = max(max(emissTex.r, emissTex.g), emissTex.b);
      half3 emissN = emissTex / (emissL + 0.0001h);
      emissL = pow(emissL, _EmissionTextureContrast);
      emissTex = emissN * emissL;

      half3 emission = _EmissionColor.rgb * emissTex * exp(_EmissionGain * 10.0h);
      return half4(lit + emission, tex.a * _DiffuseColor.a);
    }
    ENDHLSL
  }
}
FallBack Off
}
