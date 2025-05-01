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

Shader "Brush/Special/Streamers" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _Scroll1 ("Scroll1", Float) = 0
  _Scroll2 ("Scroll2", Float) = 0
  _DisplacementIntensity("Displacement", Float) = .1
  _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5


  _TimeOverrideValue("Time Override Value", Vector) = (0,0,0,0)
  _TimeBlend("Time Blend", Float) = 0
  _TimeSpeed("Time Speed", Float) = 1.0

  _Dissolve("Dissolve", Range(0, 1)) = 1
	_ClipStart("Clip Start", Float) = 0
	_ClipEnd("Clip End", Float) = -1
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One // SrcAlpha One
  BlendOp Add, Min
  AlphaTest Greater .01
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile_particles
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON
      #pragma target 3.0 // Required -> compiler error: too many instructions for SM 2.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"
      #include "Assets/Shaders/Include/MobileSelection.cginc"

      sampler2D _MainTex;

      uniform half _ClipStart;
      uniform half _ClipEnd;
      uniform half _Dissolve;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
        float2 texcoord : TEXCOORD0;
        uint id : SV_VertexID;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 pos : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float4 worldPos : TEXCOORD1;
        uint id : TEXCOORD2;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      float4 _MainTex_ST;
      fixed _Scroll1;
      fixed _Scroll2;
      half _DisplacementIntensity;
      half _EmissionGain;

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);
        v.color = TbVertToSrgb(v.color);

        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.worldPos = mul(unity_ObjectToWorld, v.vertex);
        o.pos = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = v.color;
        o.id = (float2)v.id;
        return o;
      }

      float rand_1_05(in float2 uv)
      {
        float2 noise = (frac(sin(dot(uv ,float2(12.9898,78.233)*2.0)) * 43758.5453));
        return abs(noise.x + noise.y) * 0.5;
      }

      // Input color is srgb
      fixed4 frag (v2f i) : COLOR
      {
        #ifdef SHADER_SCRIPTING_ON
        if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
        // It's hard to get alpha curves right so use dithering for hdr shaders
        if (_Dissolve < 1 && Dither8x8(i.pos.xy) >= _Dissolve) discard;
        #endif

        // Create parametric flowing UV's
        half2 uvs = i.texcoord;
        float row_id = floor(uvs.y * 5);
        float row_rand = rand_1_05(row_id.xx);
        uvs.x += row_rand * 200;

        half2 sins = sin(uvs.x * half2(10,23) + GetTime().z * half2(5,3));
        uvs.y = 5 * uvs.y + dot(half2(.05, -.05), sins);

#ifdef AUDIO_REACTIVE
        // Scrolling UVs
        uvs.x *= .5 + row_rand * .3;
        uvs.x -= _BeatOutputAccum.x * (1 + fmod(row_id * 1.61803398875, 1) - 0.5);
#else
        // Scrolling UVs
        uvs.x *= .5 + row_rand * .3;
        uvs.x -= GetTime().y * (1 + fmod(row_id * 1.61803398875, 1) - 0.5);
#endif

        // Sample final texture
        half4 tex = tex2D(_MainTex, uvs);

        // Boost hot spot in texture
        tex += pow(tex, 2) * 55;

        // Clean up border pixels filtering artifacts
        tex *= fmod(uvs.y,1); // top edge
        tex *= fmod(uvs.y,1); // top edge
        tex *= 1 - fmod(uvs.y,1); // bottom edge
        tex *= 1 - fmod(uvs.y,1); // bottom edge

#ifdef AUDIO_REACTIVE
        tex += tex * _BeatOutput.x;
#endif

        float4 color = i.color * tex * exp(_EmissionGain * 5.0f);
        color = encodeHdr(color.rgb * color.a);
        color = SrgbToNative(color);
        FRAG_MOBILESELECT(color)
        return color * _Dissolve;
      }
      ENDCG
    }
  }
}
}
