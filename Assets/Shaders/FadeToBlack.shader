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

Shader "Custom/FadeToBlack" {
  Properties {
    _MainColor ("MainColor", COLOR) = (1,1,1,1)
      _FadeStart ("Fade Start", Float) = 0.0
      _FadeEnd ("Fade End", Float) = 0.0
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Geometry" }
    Pass {
      Name "ForwardUnlit"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma target 3.0
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _MainColor;
      float _FadeStart;
      float _FadeEnd;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
        float3 worldPos : TEXCOORD0;
      };

      Varyings Vert(Attributes IN) {
        Varyings OUT;
        VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
        OUT.positionHCS = positionInputs.positionCS;
        OUT.worldPos = positionInputs.positionWS;
        return OUT;
      }

      half4 Frag(Varyings IN) : SV_Target {
        float fNormalizedY = max(abs(IN.worldPos.y) - _FadeStart, 0.0);
        fNormalizedY /= max(_FadeEnd - _FadeStart, 1e-5);
        fNormalizedY = clamp(fNormalizedY, 0.0, 1.0);
        half3 color = lerp(_MainColor.rgb, half3(0.0h, 0.0h, 0.0h), (half)fNormalizedY);
        return half4(color, 1.0h);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
