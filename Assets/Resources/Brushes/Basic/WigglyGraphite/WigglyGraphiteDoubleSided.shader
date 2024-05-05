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

Shader "Brush/Special/WigglyGraphiteDoubleSided" {
  Properties{
    _MainTex("Main Texture", 2D) = "white" {}
    _SecondaryTex("Diffuse Tex", 2D) = "white" {}
    _Cutoff("Alpha cutoff", Range(0,1)) = 0.5

    [Toggle] _OverrideTime ("Overriden Time", Float) = 0.0
    _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
    _TimeBlend("Time Blend", Float) = 0
    _TimeSpeed("Time Speed", Float) = 1.0

    _Dissolve("Dissolve", Range(0,1)) = 1
    _ClipStart("Clip Start", Float) = 0
    _ClipEnd("Clip End", Float) = -1
  }

  SubShader{
    Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
    Cull Off

    CGPROGRAM
      #pragma target 4.0
      #pragma surface surf StandardSpecular vertex:vert addshadow
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON
      // Faster compiles
      #pragma skip_variants INSTANCING_ON

      #include "Assets/Shaders/Include/TimeOverride.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/ThirdParty/Shaders/Noise.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      struct appdata {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;
        float2 texcoord1 : TEXCOORD1;
        float2 texcoord2 : TEXCOORD2;
        half3 normal : NORMAL;
        fixed4 color : COLOR;
        float4 tangent : TANGENT;
        uint id : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Input {
        float2 uv_MainTex;
        float4 color : Color;
        fixed vface : VFACE;
        uint id : SV_VertexID;
        float4 vertex : SV_POSITION;
        float4 screenPos;
      };

      sampler2D _MainTex;
      float _Cutoff;

      uniform float _ClipStart;
	    uniform float _ClipEnd;
      uniform half _Dissolve;

      void vert(inout appdata i, out Input o) {
        UNITY_INITIALIZE_OUTPUT(Input, o);
        o.vertex = UnityObjectToClipPos(i.vertex);
        PrepForOds(i.vertex);
        o.color = TbVertToSrgb(i.color);
        o.id = i.id;
      }

      void surf(Input IN, inout SurfaceOutputStandardSpecular o) {

        if (_ClipEnd > 0 && !(IN.id.x > _ClipStart && IN.id.x < _ClipEnd)) discard;
        if (_Dissolve < 1 && Dither8x8(IN.screenPos.xy / IN.screenPos.w * _ScreenParams) >= _Dissolve) discard;

        fixed2 scrollUV = IN.uv_MainTex;

        // Animate flipbook motion. Currently tuned to taste.
#ifdef AUDIO_REACTIVE
        float anim = ceil(fmod(GetTime().y * 3.0 + _BeatOutput.x * 3.0, 6.0));
#else
        float anim = ceil(fmod(GetTime().y * 12.0, 6.0));
#endif
        scrollUV.x += anim;
        scrollUV.x *= 1.1;

        o.Specular = 0;
        o.Smoothness = 0;
        o.Albedo = IN.color.rgb;
        o.Alpha = tex2D(_MainTex, scrollUV).w * IN.color.a;
        if (o.Alpha < _Cutoff) {
          discard;
        }
        o.Alpha = 1;
        o.Normal.z *= IN.vface;

        SURF_FRAG_MOBILESELECT(o);
      }
    ENDCG
  }
  FallBack "Diffuse"
}
