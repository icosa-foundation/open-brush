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

Shader "Brush/Disco" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
    _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
    _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
    _BumpMap ("Normalmap", 2D) = "bump" {}


    _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
    _TimeBlend("Time Blend", Float) = 0
    _TimeSpeed("Time Speed", Float) = 1.0

    _Dissolve("_Dissolve", Range(0,1)) = 1
    _ClipStart("Clip Start", Float) = 0
    _ClipEnd("Clip End", Float) = -1
  }

  SubShader {
    Cull Back
    CGPROGRAM
    #pragma target 4.0
    #pragma surface surf StandardSpecular vertex:vert noshadow
    #pragma multi_compile __ AUDIO_REACTIVE
    #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
    #pragma multi_compile __ SELECTION_ON

    #include "Assets/Shaders/Include/Brush.cginc"

    #include "Assets/Shaders/Include/MobileSelection.cginc"

    struct Input {
      float2 uv_MainTex;
      float2 uv_BumpMap;
      float4 color : Color;
      float3 worldPos;
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

    sampler2D _MainTex;
    sampler2D _BumpMap;
    fixed4 _Color;
    half _Shininess;

    uniform half _ClipStart;
    uniform half _ClipEnd;
    uniform half _Dissolve;

    void vert (inout appdata_full_plus_id v, out Input o) {
      UNITY_INITIALIZE_OUTPUT(Input, o);
      PrepForOds(v.vertex);
      v.color = TbVertToNative(v.color);
      float t, uTileRate, waveIntensity;

      float radius = v.texcoord.z;

#ifdef AUDIO_REACTIVE
      t = _BeatOutputAccum.z * 5;
      uTileRate = 5;
      waveIntensity = (_PeakBandLevels.y * .8 + .5);
      float waveform = tex2Dlod(_WaveFormTex, float4(v.texcoord.x * 2, 0, 0, 0)).b - .5f;
      v.vertex.xyz += waveform * v.normal.xyz * .2;
#else
      t = GetTime().z;
      uTileRate = 10;
      waveIntensity = .6;
#endif
      // Ensure the t parameter wraps (1.0 becomes 0.0) to avoid cracks at the seam.
      float theta = fmod(v.texcoord.y, 1);
      v.vertex.xyz += pow(1 -(sin(t + v.texcoord.x * uTileRate + theta * 10) + 1),2)
              * v.normal.xyz * waveIntensity
              * radius;
      o.id = v.id;
    }

    // Input color is _native_
    void surf (Input IN, inout SurfaceOutputStandardSpecular o) {

      #ifdef SHADER_SCRIPTING_ON
      if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
      if (_Dissolve < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Dissolve) discard;
      #endif

      fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
      o.Albedo = tex.rgb * _Color.rgb * IN.color.rgb;
      o.Smoothness = _Shininess;
      o.Specular = _SpecColor * IN.color.rgb;
      o.Normal =  float3(0,0,1);

      // XXX need to convert world normal to tangent space normal somehow...
      float3 worldNormal = normalize(cross(ddy(IN.worldPos), ddx(IN.worldPos)));
      o.Normal = -cross(cross(o.Normal, worldNormal), worldNormal);
      o.Normal = normalize(o.Normal);

      // Add a fake "disco ball" hot spot
      float fakeLight = pow( abs(dot(worldNormal, float3(0,1,0))),100);
      o.Emission = IN.color.rgb * fakeLight * 200;
      SURF_FRAG_MOBILESELECT(o);
    }
    ENDCG
  }

  FallBack "Transparent/Cutout/VertexLit"
}
