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

Shader "Brush/Intro/StandardDoubleSided" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
  _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
  _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
  _BumpMap ("Normalmap", 2D) = "bump" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  _IntroDissolve ("Intro Dissolve", Range(0,1)) = 0
}

SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout" }

  Pass {
    Tags { "LightMode"="UniversalForward" }
    Cull Off

    HLSLPROGRAM
    #pragma target 3.0
    #pragma vertex Vert
    #pragma fragment Frag

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    TEXTURE2D(_BumpMap);
    SAMPLER(sampler_BumpMap);

    CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half4 _SpecColor;
    float4 _MainTex_ST;
    float4 _BumpMap_ST;
    half _Cutoff;
    half _Shininess;
    half _IntroDissolve;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      float3 normalOS : NORMAL;
      float4 tangentOS : TANGENT;
      float2 uv0 : TEXCOORD0;
      half4 color : COLOR;
    };

    struct Varyings {
      float4 positionCS : SV_POSITION;
      float2 uvMain : TEXCOORD0;
      float2 uvBump : TEXCOORD1;
      half4 color : COLOR;
      float3 positionWS : TEXCOORD2;
      half3 normalWS : TEXCOORD3;
      half3 tangentWS : TEXCOORD4;
      half3 bitangentWS : TEXCOORD5;
    };

    Varyings Vert(Attributes IN) {
      Varyings OUT;

      VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
      VertexNormalInputs normal = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
      OUT.positionCS = pos.positionCS;
      OUT.positionWS = pos.positionWS;
      OUT.normalWS = normal.normalWS;
      OUT.tangentWS = normal.tangentWS;
      OUT.bitangentWS = normal.bitangentWS;
      OUT.uvMain = TRANSFORM_TEX(IN.uv0, _MainTex);
      OUT.uvBump = TRANSFORM_TEX(IN.uv0, _BumpMap);

      half ramp = saturate(smoothstep(120.0h, -5.0h, IN.positionOS.y));
      half fade = lerp(1.0h, ramp * (1.0h - _IntroDissolve), _IntroDissolve);
      OUT.color = IN.color;
      OUT.color.a *= fade;
      return OUT;
    }

    half4 Frag(Varyings IN, bool isFrontFace : SV_IsFrontFace) : SV_Target {
      half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uvMain);
      half alpha = tex.a * IN.color.a;
      clip(alpha - _Cutoff);

      half3 tangentNormal = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uvBump));
      tangentNormal.z *= isFrontFace ? 1.0h : -1.0h;
      half3 normalWS = normalize(
          tangentNormal.x * IN.tangentWS +
          tangentNormal.y * IN.bitangentWS +
          tangentNormal.z * IN.normalWS);

      Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
      half3 viewDir = SafeNormalize(GetWorldSpaceViewDir(IN.positionWS));
      half3 lightDir = mainLight.direction;
      half3 halfVec = SafeNormalize(lightDir + viewDir);

      half ndotl = saturate(dot(normalWS, lightDir));
      half ndoth = saturate(dot(normalWS, halfVec));
      half spec = pow(ndoth, max(1.0h, _Shininess * 128.0h));

      half3 lighting = SampleSH(normalWS);
      lighting += ndotl * mainLight.color * mainLight.shadowAttenuation;

      half3 rgb = tex.rgb * _Color.rgb * IN.color.rgb * lighting;
      rgb += _SpecColor.rgb * spec * mainLight.color * mainLight.shadowAttenuation;
      return half4(rgb, 1.0h);
    }
    ENDHLSL
  }
}

Fallback Off
}
