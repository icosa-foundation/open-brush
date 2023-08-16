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

Shader "Brush/Visualizer/WaveformPulse" {

Properties {
  _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5

    [Toggle] _OverrideTime ("Overriden Time", Float) = 1.0
    _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
    _TimeBlend("Time Blend", Float) = 0
    _TimeSpeed("Time Speed", Float) = 1.0

  _Opacity ("Opacity", Range(0, 1)) = 1
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1

}

SubShader {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One
  Cull Off ZWrite Off

  CGPROGRAM
  #pragma target 4.0
  #pragma surface surf StandardSpecular vertex:vert

  #pragma multi_compile __ AUDIO_REACTIVE
  #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
  #pragma multi_compile __ SELECTION_ON
  // Faster compiles
  #pragma skip_variants INSTANCING_ON

    #include "Assets/Shaders/Include/TimeOverride.cginc"
  #include "Assets/Shaders/Include/Brush.cginc"
  #include "Assets/Shaders/Include/MobileSelection.cginc"

  struct appdata {
    float4 vertex : POSITION;
    float3 texcoord : TEXCOORD0;
    float3 texcoord1 : TEXCOORD1;
    float3 texcoord2 : TEXCOORD2;
    half3 normal : NORMAL;
    fixed4 color : COLOR;
    float4 tangent : TANGENT;
    uint id : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
  };

  struct Input {
    float4 color : Color;
    float2 tex : TEXCOORD0;
    float3 viewDir;
    float3 worldNormal;
    uint id : SV_VertexID;
    INTERNAL_DATA
  };

  float _EmissionGain;

  uniform float _ClipStart;
  uniform float _ClipEnd;
  uniform float _Opacity;

  void vert (inout appdata i, out Input o) {
    PrepForOds(i.vertex);
    UNITY_INITIALIZE_OUTPUT(Input, o);
    o.color = TbVertToSrgb(o.color);
    o.id = (float2)i.id;
  }

  // Input color is srgb
  void surf (Input IN, inout SurfaceOutputStandardSpecular o) {

    float completion = _ClipEnd < 0 || (IN.id > _ClipStart && IN.id < _ClipEnd) ? 1 : -1;
    clip(completion);

    o.Smoothness = .8;
    o.Specular = .05;
    float audioMultiplier = 1;
#ifdef AUDIO_REACTIVE
    audioMultiplier += audioMultiplier * _BeatOutput.x;
    IN.tex.x -= _BeatOutputAccum.z;
    IN.color += IN.color * _BeatOutput.w * .25;
#else
    IN.tex.x -= GetTime().x*15;
#endif
    IN.tex.x = fmod( abs(IN.tex.x),1);
    float neon = saturate(pow( 10 * saturate(.2 - IN.tex.x),5) * audioMultiplier);
    float4 bloom = bloomColor(IN.color, _EmissionGain);
    float3 n = WorldNormalVector (IN, o.Normal);
    half rim = 1.0 - saturate(dot (normalize(IN.viewDir), n));
    bloom *= pow(1-rim,5);
    o.Emission = SrgbToNative(bloom * neon);
    o.Alpha *= _Opacity;
    o.Emission *= _Opacity;
    o.Albedo *= _Opacity;
    o.Specular *= _Opacity;
    SURF_FRAG_MOBILESELECT(o);
  }
  ENDCG
}  // SubShader

}  // Shader
