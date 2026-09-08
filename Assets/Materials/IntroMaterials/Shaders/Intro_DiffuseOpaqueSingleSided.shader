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

Shader "Brush/Intro/DiffuseOpaqueSingleSided" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _IntroDissolve ("Intro Dissolve", Range(0,1)) = 0
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout" }

  Pass {
    Tags { "LightMode"="UniversalForward" }
    Cull Back

    HLSLPROGRAM
    #pragma target 3.0
    #pragma vertex Vert
    #pragma fragment Frag

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half _IntroDissolve;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      float3 normalOS : NORMAL;
      float2 uv : TEXCOORD0;
      half4 color : COLOR;
    };

    struct Varyings {
      float4 positionCS : SV_POSITION;
      half4 color : COLOR;
      float introReveal : TEXCOORD0;
      float3 normalWS : TEXCOORD1;
      float3 positionWS : TEXCOORD2;
    };

    Varyings Vert(Attributes IN) {
      Varyings OUT;

      VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
      VertexNormalInputs normal = GetVertexNormalInputs(IN.normalOS);
      OUT.positionCS = pos.positionCS;
      OUT.positionWS = pos.positionWS;
      OUT.normalWS = normal.normalWS;
      OUT.color = IN.color;
      OUT.introReveal = IN.uv.y;
      return OUT;
    }

    half4 Frag(Varyings IN) : SV_Target {
      clip(IN.introReveal - _IntroDissolve);

      half3 normalWS = normalize(IN.normalWS);
      Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
      half3 lighting = SampleSH(normalWS);
      lighting += saturate(dot(normalWS, mainLight.direction)) * mainLight.color * mainLight.shadowAttenuation;

      half3 rgb = _Color.rgb * IN.color.rgb * lighting;
      return half4(rgb, 1.0h);
    }
    ENDHLSL
  }
}

Fallback Off
}
