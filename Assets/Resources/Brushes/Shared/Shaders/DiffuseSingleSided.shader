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

Shader "Brush/DiffuseSingleSided" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5

  _Opacity("Opacity", Range(0,1)) = 1
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1
}

SubShader {
  Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
  LOD 200
  Cull Back

CGPROGRAM
#pragma surface surf Lambert vertex:vert alphatest:_Cutoff addshadow
#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
#pragma multi_compile __ SELECTION_ON
#pragma target 4.0
#include "Assets/Shaders/Include/Brush.cginc"
#include "Assets/Shaders/Include/MobileSelection.cginc"

sampler2D _MainTex;
fixed4 _Color;

uniform float _ClipStart;
uniform float _ClipEnd;
uniform half _Opacity;

struct Input {
  float2 uv_MainTex;
  float4 color : COLOR;
  uint id : SV_VertexID;
  float4 screenPos;
};

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

void vert (inout appdata_full_plus_id v, out Input o) {
  PrepForOds(v.vertex);
  v.color = TbVertToNative(v.color);
  UNITY_INITIALIZE_OUTPUT(Input, o);
  o.id = v.id;
}

void surf (Input IN, inout SurfaceOutput o) {

  if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
  if (_Opacity < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Opacity) discard;

  fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
  o.Albedo = c.rgb * IN.color.rgb;
  o.Alpha = c.a * IN.color.a;
  SURF_FRAG_MOBILESELECT(o);
}

ENDCG
}


// MOBILE VERSION
SubShader {
  Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
  LOD 100

CGPROGRAM
#pragma surface surf Lambert vertex:vert alphatest:_Cutoff
#pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
#pragma target 4.0
#include "Assets/Shaders/Include/Brush.cginc"

sampler2D _MainTex;
fixed4 _Color;

uniform float _ClipStart;
uniform float _ClipEnd;
uniform half _Opacity;

struct Input {
  float2 uv_MainTex;
  float4 color : COLOR;
  uint id : SV_VertexID;
  float4 vertex : POSITION;
  float4 screenPos;
  fixed vface : VFACE;
};

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

void vert (inout appdata_full_plus_id v, out Input o) {
  UNITY_INITIALIZE_OUTPUT(Input, o);
  PrepForOds(v.vertex);
  v.color = TbVertToNative(v.color);
}

void surf (Input IN, inout SurfaceOutput o) {

  if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
  if (_Opacity < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Opacity) discard;

  fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
  o.Albedo = c.rgb * IN.color.rgb;
  o.Alpha = c.a * IN.color.a;
}
ENDCG
}

Fallback "Transparent/Cutout/VertexLit"
}
