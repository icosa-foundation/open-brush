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

// Unlit shader. Simplest possible textured shader.
// - no lighting
// - no lightmap support
// - no per-material color

Shader "Unlit/Diffuse" {
Properties {
  _Color ("Color", Color) = (1,1,1,1)
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  LOD 100
  Blend One One, Zero One

  Pass {
    Tags { "LightMode"="UniversalForward" }
    HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _Color;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
      };

      Varyings Vert(Attributes IN) {
        Varyings OUT;
        OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
        return OUT;
      }

      half4 Frag(Varyings IN) : SV_Target {
        half4 color = _Color;
        color.rgb *= color.a;
        return color;
      }
    ENDHLSL
  }
}

}
