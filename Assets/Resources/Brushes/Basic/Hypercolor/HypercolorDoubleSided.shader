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

Shader "Brush/Special/HypercolorDoubleSided" {
Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
  _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
  _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
  _MainTex ("Base (RGB) TransGloss (A)", 2D) = "white" {}
  _BumpMap ("Normalmap", 2D) = "bump" {}
  _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5


 _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
 _TimeBlend("Time Blend", Float) = 0
 _TimeSpeed("Time Speed", Float) = 1.0

 _Dissolve("Dissolve", Range(0,1)) = 1
 _ClipStart("Clip Start", Float) = 0
 _ClipEnd("Clip End", Float) = -1
}
    SubShader {
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    Cull Off
    LOD 100

    CGPROGRAM
    #pragma target 4.0
    #pragma surface surf StandardSpecular vertex:vert addshadow
    #pragma multi_compile __ AUDIO_REACTIVE
    #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
    #pragma multi_compile __ SELECTION_ON
    // Faster compiles
    #pragma skip_variants INSTANCING_ON

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
      float2 uv_MainTex;
      float2 uv_BumpMap;
      float4 color : Color;
      float3 worldPos;
      fixed vface : VFACE;
      uint id : TEXCOORD2;
      float4 screenPos;
    };

    sampler2D _MainTex;
    sampler2D _BumpMap;
    fixed4 _Color;
    half _Shininess;
    fixed _Cutoff;

    uniform half _ClipStart;
    uniform half _ClipEnd;
    uniform half _Dissolve;

    void vert (inout appdata v, out Input o) {
      UNITY_INITIALIZE_OUTPUT(Input, o);
      PrepForOds(v.vertex);
      v.color = TbVertToSrgb(v.color);

      float t = 0.0;

      float strokeWidth = abs(v.texcoord.z) * 1.2;

#ifdef AUDIO_REACTIVE
      t = _BeatOutputAccum.z * 5;
      float waveIntensity = _BeatOutput.z * .1 * strokeWidth;
      v.vertex.xyz += (pow(1 - (sin(t + v.texcoord.x * 5 + v.texcoord.y * 10) + 1), 2)
                * cross(v.tangent.xyz, v.normal.xyz)
                * waveIntensity)
              ;
#endif
      o.id = v.id;
    }

    void surf (Input IN, inout SurfaceOutputStandardSpecular o) {

      #ifdef SHADER_SCRIPTING_ON
      if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
      if (_Dissolve < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Dissolve) discard;
      #endif

      fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);

      float scroll = GetTime().z;
#ifdef AUDIO_REACTIVE
      float3 localPos = mul(xf_I_CS, float4(IN.worldPos, 1.0)).xyz;
      float t = length(localPos) * .5;
      scroll =  _BeatOutputAccum.y*30;
      float angle = atan2(localPos.x, localPos.y);
      float waveform = tex2D(_WaveFormTex, float2(angle * 6,0)).g*2;

      tex.rgb =  float3(1,0,0) * (sin(tex.r*2 + scroll*0.5 - t) + 1);
      tex.rgb += float3(0,1,0) * (sin(tex.r*3 + scroll*1 - t) + 1);
      tex.rgb += float3(0,0,1) * (sin(tex.r*4 + scroll*0.25 - t) + 1);
#else
      tex.rgb =  float3(1,0,0) * (sin(tex.r * 2 + scroll*0.5 - IN.uv_MainTex.x) + 1) * 2;
      tex.rgb += float3(0,1,0) * (sin(tex.r * 3.3 + scroll*1 - IN.uv_MainTex.x) + 1) * 2;
      tex.rgb += float3(0,0,1) * (sin(tex.r * 4.66 + scroll*0.25 - IN.uv_MainTex.x) + 1) * 2;
#endif

      o.Albedo = SrgbToNative(tex * IN.color).rgb;
      o.Smoothness = _Shininess;
      o.Specular = SrgbToNative(_SpecColor * tex).rgb;
      o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
      o.Alpha = tex.a * IN.color.a;
      if (o.Alpha < _Cutoff) {
        discard;
      }
      o.Alpha = 1;
#ifdef AUDIO_REACTIVE
      o.Emission = o.Albedo;
      o.Albedo = .2;
      o.Specular *= .5;
#endif
      SURF_FRAG_MOBILESELECT(o);
      o.Normal.z *= IN.vface;
    }
    ENDCG
    }

  FallBack "Transparent/Cutout/VertexLit"
}



