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

Shader "Brush/Intro/DiffuseNoTextureDoubleSided" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _IntroDissolve ("Intro Dissolve", Range(0,1)) = 0
  _Dissolve ("Dissolve", Range(0,1)) = 1
  _ClipStart ("Clip Start", Float) = 0
  _ClipEnd ("Clip End", Float) = -1
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "DisableBatching"="True" }

  Pass {
    Tags { "LightMode"="UniversalForward" }
    Cull Off

    HLSLPROGRAM
    #pragma vertex Vert
    #pragma fragment Frag

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half _IntroDissolve;
    half _Dissolve;
    half _ClipStart;
    half _ClipEnd;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      half3 normalOS : NORMAL;
      half4 color : COLOR;
      float2 texcoord0 : TEXCOORD0;
      float3 texcoord1 : TEXCOORD1;
    };

    struct Varyings {
      float4 positionHCS : SV_POSITION;
      half4 color : COLOR;
    };

    Varyings Vert(Attributes IN) {
      Varyings OUT;

      float4 positionOS = IN.positionOS;
      float envelope = sin(IN.texcoord0.x * 3.14159) * (1.0 - _IntroDissolve);
      float widthMultiplier = 1.0 - envelope;
      positionOS.xyz += -IN.texcoord1 * widthMultiplier;

      OUT.positionHCS = TransformObjectToHClip(positionOS.xyz);
      OUT.color = IN.color * _Color;
      return OUT;
    }

    half4 Frag(Varyings IN) : SV_Target {
      return half4(IN.color.rgb, 1.0h);
    }
    ENDHLSL
  }
}

Fallback Off
}
