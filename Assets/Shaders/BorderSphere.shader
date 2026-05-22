// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

Shader "Custom/BorderSphere" {
  Properties {
    _MainColor ("MainColor", COLOR) = (1,1,1,1)
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Opaque" }
    Blend SrcAlpha OneMinusSrcAlpha
    ZWrite Off
    Pass {
      Name "ForwardUnlit"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #pragma multi_compile_instancing
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _MainColor;
      CBUFFER_END

      float3 _HighlightCenter;
      float _HighlightRadius;

      struct Attributes {
        float4 positionOS : POSITION;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
        float3 worldPos : TEXCOORD0;

        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
      };

      Varyings Vert(Attributes IN) {
        Varyings OUT;
        UNITY_SETUP_INSTANCE_ID(IN);
        UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
        VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
        OUT.positionHCS = positionInputs.positionCS;
        OUT.worldPos = positionInputs.positionWS;
        return OUT;
      }

      half4 Frag(Varyings IN) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
        float safeRadius = max(_HighlightRadius, 0.0001);
        float distanceToHighlight = distance(_HighlightCenter, IN.worldPos);
        float highlightRatio = saturate(distanceToHighlight / safeRadius);
        half alpha = lerp(1.0h, 0.0h, (half)highlightRatio);
        return half4(_MainColor.rgb, alpha);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
