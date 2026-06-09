Shader "OpenBrush/Environment/ParticleAdditive" {
Properties {
  _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
  _MainTex ("Particle Texture", 2D) = "white" {}
  _InvFade ("Soft Particles Factor", Range(0.01,3)) = 1.0
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend SrcAlpha One
  Cull Off
  ZWrite Off

  Pass {
    Tags { "LightMode"="UniversalForward" }

    HLSLPROGRAM
    #pragma target 3.0
    #pragma vertex Vert
    #pragma fragment Frag
    #pragma multi_compile_instancing

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
    half4 _TintColor;
    float4 _MainTex_ST;
    half _InvFade;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      float2 uv : TEXCOORD0;
      UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings {
      float4 positionCS : SV_POSITION;
      float2 uv : TEXCOORD0;
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
      OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
      OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
      return OUT;
    }

    half4 Frag(Varyings IN) : SV_Target
    {
      UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
      return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _TintColor * 2.0h;
    }
    ENDHLSL
  }
}
FallBack Off
}
