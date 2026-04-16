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

// Shader calculates normals per triangle using a geometry shader.
// Uses Blinn-Phong lighting model for the main directional light and SH
// for all additional lighting.
Shader "Brush/FlatLit" {

Properties {
  _MainTex("Texture", 2D) = "white" {}
  _Smoothness("Smoothness", Range(0, 1)) = 0.5
  _Metallic("Metallic", Range(0, 1)) = 0

  _Dissolve("Dissolve", Range(0, 1)) = 1
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1
}

SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" }
  Pass {
    Tags { "LightMode" = "UniversalForward" }
    Blend SrcAlpha OneMinusSrcAlpha
    Cull Back
    HLSLPROGRAM

    #pragma vertex vert
    #pragma geometry geom
    #pragma fragment frag
    #pragma target 4.5
    #pragma multi_compile __ SHADER_SCRIPTING_ON
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
    #pragma multi_compile_fragment _ _SHADOWS_SOFT

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float _Smoothness;
    float _Metallic;
    half _ClipStart;
    half _ClipEnd;
    half _Dissolve;
    CBUFFER_END

    struct Attributes {
      float4 positionOS : POSITION;
      float2 uv : TEXCOORD0;
      float4 color : COLOR;
      uint id : SV_VertexID;
    };

    struct VaryingsToGeom {
      float4 positionCS : SV_POSITION;
      float2 uv : TEXCOORD0;
      float3 positionWS : TEXCOORD1;
      float4 color : TEXCOORD2;
      float id : TEXCOORD3;
    };

    struct Varyings {
      float4 positionCS : SV_POSITION;
      float2 uv : TEXCOORD0;
      float3 normalWS : TEXCOORD1;
      float3 positionWS : TEXCOORD2;
      float4 color : TEXCOORD3;
      float id : TEXCOORD4;
    };

    float Dither8x8(float2 position) {
      const float DitherSize = 8.0;
      float2 ditherPosition = position % DitherSize;
      int x = int(ditherPosition.x);
      int y = int(ditherPosition.y);

      const float dither8x8[64] = {
        0,32,8,40,2,34,10,42,
        48,16,56,24,50,18,58,26,
        12,44,4,36,14,46,6,38,
        60,28,52,20,62,30,54,22,
        3,35,11,43,1,33,9,41,
        51,19,59,27,49,17,57,25,
        15,47,7,39,13,45,5,37,
        63,31,55,23,61,29,53,21
      };

      return dither8x8[y * 8 + x] / 64.0;
    }

    VaryingsToGeom vert(Attributes v) {
      VaryingsToGeom o;
      VertexPositionInputs posInput = GetVertexPositionInputs(v.positionOS.xyz);
      o.positionCS = posInput.positionCS;
      o.positionWS = posInput.positionWS;
      o.uv = TRANSFORM_TEX(v.uv, _MainTex);
      o.color = v.color;
      o.id = (float)v.id;
      return o;
    }

    // Called once per triangle primitive, values outputted to triangle's pixels.
    [maxvertexcount(3)]
    void geom(triangle VaryingsToGeom i[3], inout TriangleStream<Varyings> stream) {
      float3 p0 = i[0].positionWS;
      float3 p1 = i[1].positionWS;
      float3 p2 = i[2].positionWS;

      float3 triangleNormal = normalize(cross(p1 - p0, p2 - p0));

      Varyings o;
      o.normalWS = triangleNormal;

      o.positionCS = i[0].positionCS;
      o.uv = i[0].uv;
      o.positionWS = i[0].positionWS;
      o.color = i[0].color;
      o.id = i[0].id;
      stream.Append(o);

      o.positionCS = i[1].positionCS;
      o.uv = i[1].uv;
      o.positionWS = i[1].positionWS;
      o.color = i[1].color;
      o.id = i[1].id;
      stream.Append(o);

      o.positionCS = i[2].positionCS;
      o.uv = i[2].uv;
      o.positionWS = i[2].positionWS;
      o.color = i[2].color;
      o.id = i[2].id;
      stream.Append(o);
    }

    half4 frag(Varyings i) : SV_TARGET {
      #ifdef SHADER_SCRIPTING_ON
      if (_ClipEnd > 0 && !(i.id > _ClipStart && i.id < _ClipEnd)) discard;
      if (_Dissolve < 1 && Dither8x8(i.positionCS.xy) >= _Dissolve) discard;
      #endif

      half3 normalWS = normalize(i.normalWS);
      Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
      half3 lightColor = mainLight.color * mainLight.shadowAttenuation;

      half3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
      half3 halfDir = normalize(mainLight.direction + viewDir);
      half nDotL = saturate(dot(normalWS, mainLight.direction));

      half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * (1 - _Metallic);
      half3 specularTint = albedo * _Metallic;

      half3 diffuse = albedo * lightColor * nDotL;
      half3 specular = specularTint * lightColor * pow(saturate(dot(halfDir, normalWS)), _Smoothness * 100);
      half3 lighting = diffuse + specular;
      lighting += SampleSH(normalWS) * 0.5;

      return half4(lighting * i.color.rgb, i.color.a);
    }

    ENDHLSL
  }

  // Cast shadows
  Pass {
    Tags { "LightMode" = "ShadowCaster"}

    HLSLPROGRAM
    #pragma target 4.5
    #pragma vertex vert
    #pragma fragment frag
    #pragma geometry geom

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    struct Attributes {
      float4 positionOS : POSITION;
    };

    struct VaryingsToGeom {
      float4 positionCS : SV_POSITION;
    };

    struct Varyings {
      float4 positionCS : SV_POSITION;
    };

    VaryingsToGeom vert(Attributes v) {
      VaryingsToGeom o;
      o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
      return o;
    }

    [maxvertexcount(3)]
    void geom(triangle VaryingsToGeom i[3], inout TriangleStream<Varyings> stream) {
      Varyings o;
      o.positionCS = i[0].positionCS;
      stream.Append(o);
      o.positionCS = i[1].positionCS;
      stream.Append(o);
      o.positionCS = i[2].positionCS;
      stream.Append(o);
    }

    half4 frag() : SV_TARGET {
      return 0;
    }

    ENDHLSL
  }
}
}

