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

Shader "Brush/Special/Petal" {
  Properties{
    _SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
    _Shininess("Shininess", Range(0.01, 1)) = 0.3
    _MainTex("Base (RGB) TransGloss (A)", 2D) = "white" {}

    _Dissolve("Dissolve", Range(0,1)) = 1
    _ClipStart("Clip Start", Float) = 0
    _ClipEnd("Clip End", Float) = -1
  }

  SubShader{
    Tags {"IgnoreProjector" = "True" "RenderType" = "Opaque"}
    Cull Off

    CGPROGRAM
      #pragma target 4.0
      #pragma surface surf StandardSpecular vertex:vert addshadow
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER
      #pragma multi_compile __ SELECTION_ON
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      struct Input {
        float2 uv_MainTex;
        float4 color : Color;
        fixed vface : VFACE;
        uint id : SV_VertexID;
        float4 screenPos;
      };

      half _Shininess;

  	  uniform float _ClipStart;
      uniform float _ClipEnd;
      uniform half _Dissolve;

      struct appdata_full_plus_id {
        float4 vertex : POSITION;
        float4 tangent : TANGENT;
        float3 normal : NORMAL;
        float4 texcoord : TEXCOORD0;
        float4 texcoord1 : TEXCOORD1;
        float4 texcoord2 : TEXCOORD2;
        float4 texcoord3 : TEXCOORD3;
        fixed4 color : COLOR;
        uint id : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      void vert(inout appdata_full_plus_id i, out Input o) {
        UNITY_INITIALIZE_OUTPUT(Input, o);
        PrepForOds(i.vertex);
        i.color = TbVertToNative(i.color);
        o.id = i.id;
      }

      void surf(Input IN, inout SurfaceOutputStandardSpecular o) {

        if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
        if (_Dissolve < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Dissolve) discard;

        // Fade from center outward (dark to light)
        float4 darker_color = IN.color;
        darker_color *= 0.6;
        float4 finalColor = lerp(IN.color, darker_color, 1- IN.uv_MainTex.x);

        float fAO = IN.vface == -1 ? .5 * IN.uv_MainTex.x : 1;
        o.Albedo = finalColor * fAO;
        o.Smoothness = _Shininess;
        o.Specular = _SpecColor * fAO;
        o.Alpha = 1;

        SURF_FRAG_MOBILESELECT(o);
      }
    ENDCG
  }
}
