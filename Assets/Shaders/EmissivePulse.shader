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

Shader "Custom/EmissivePulse" {
  Properties {
    _BaseColor ("Main Color", Color) = (1,1,1,1)
    _PulseColor ("PulseColor", COLOR) = (1,1,1,1)
    _PulseFrequency("Pulse Frequency", Float) = 8
  }

  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Geometry" }
    LOD 201

    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _BaseColor;
      half4 _PulseColor;
      half _PulseFrequency;
      CBUFFER_END

      struct Attributes { float4 positionOS : POSITION; };
      struct Varyings { float4 positionHCS : SV_POSITION; };

      Varyings Vert(Attributes IN) { Varyings OUT; OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz); return OUT; }

      half4 Frag(Varyings IN) : SV_Target {
        half t = abs(sin(_Time.y * _PulseFrequency));
        return half4(lerp(_BaseColor, _PulseColor, t).rgb * 100.0h, 1.0h);
      }
      ENDHLSL
    }
  }

  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Geometry" }
    LOD 150

    Pass {
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      CBUFFER_START(UnityPerMaterial)
      half4 _BaseColor;
      half4 _PulseColor;
      half _PulseFrequency;
      CBUFFER_END

      struct Attributes { float4 positionOS : POSITION; };
      struct Varyings { float4 positionHCS : SV_POSITION; };

      Varyings Vert(Attributes IN) { Varyings OUT; OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz); return OUT; }

      half4 Frag(Varyings IN) : SV_Target {
        half t = abs(sin(_Time.y * _PulseFrequency));
        half3 c = _BaseColor.rgb + t * _PulseColor.rgb;
        return half4(c, 1.0h);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
