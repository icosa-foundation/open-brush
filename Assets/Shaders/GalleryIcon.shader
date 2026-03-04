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

Shader "Custom/GalleryIcon" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Texture", 2D) = "white" {}
    _FadeIn("FadeIn", Range(0, 1)) = 1
    _Aspect("Aspect Ratio", Float) = 1
  }
  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
    LOD 100

    Pass {
      Name "ForwardUnlit"
      Tags { "LightMode"="UniversalForward" }
      HLSLPROGRAM
      #pragma vertex Vert
      #pragma fragment Frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      TEXTURE2D(_MainTex);
      SAMPLER(sampler_MainTex);

      CBUFFER_START(UnityPerMaterial)
      float _FadeIn;
      float _Aspect;
      half4 _Color;
      CBUFFER_END

      struct Attributes {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
      };

      struct Varyings {
        float4 positionHCS : SV_POSITION;
        float2 uv : TEXCOORD0;
      };

      Varyings Vert(Attributes IN) {
        Varyings OUT;
        OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
        OUT.uv = IN.uv;
        return OUT;
      }

      half4 Frag(Varyings IN) : SV_Target {
        float2 uv = IN.uv - 0.5;
        if (_Aspect > 1.0) {
          uv.x /= _Aspect;
        } else {
          uv.y *= _Aspect;
        }
        uv += 0.5;

        half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
        float2 vignette2 = pow(abs(uv - 0.5) * 1.5, 2.0);
        float vignette = saturate(vignette2.x + vignette2.y);
        half3 emission = lerp(c.rgb, max(c.rgb, 0.15h), (half)vignette);

        float t = clamp(_FadeIn, 0.0, 1.0) * 0.5 + 0.5;
        emission = lerp(half3(0.5h, 0.5h, 0.5h), emission * _Color.rgb, (half)t);
        return half4(emission, c.a);
      }
      ENDHLSL
    }
  }
  FallBack Off
}
