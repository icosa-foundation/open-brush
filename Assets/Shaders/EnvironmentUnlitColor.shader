Shader "OpenBrush/Environment/UnlitColor" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

  Pass {
    Tags { "LightMode"="UniversalForward" }

    HLSLPROGRAM
    #pragma target 3.0
    #pragma vertex Vert
    #pragma fragment Frag
    #pragma multi_compile_instancing

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings {
      float4 positionCS : SV_POSITION;
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
      return OUT;
    }

    half4 Frag(Varyings IN) : SV_Target
    {
      UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
      return _Color;
    }
    ENDHLSL
  }
}
FallBack Off
}
