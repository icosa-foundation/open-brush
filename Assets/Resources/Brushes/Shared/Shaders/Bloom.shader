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

Shader "Brush/Bloom" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
}

Category {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend One One // SrcAlpha One
  AlphaTest Greater .01
  ColorMask RGBA
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  // Split into two different LOD passes as the 200 lod (mobile) needs to use a different
  // blend mode than standard.
  CGINCLUDE
    #pragma multi_compile_particles
    #pragma multi_compile __ AUDIO_REACTIVE
    #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
    #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
    #pragma multi_compile __ SELECTION_ON

    #include "UnityCG.cginc"
    #include "Assets/Shaders/Include/Brush.cginc"
    #include "Assets/Shaders/Include/Hdr.cginc"
    #include "Assets/Shaders/Include/MobileSelection.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    float _EmissionGain;

    struct appdata_t {
      float4 vertex : POSITION;
      fixed4 color : COLOR;
      float2 texcoord : TEXCOORD0;

      UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f {
      float4 pos : POSITION;
      float4 color : COLOR;
      float2 texcoord : TEXCOORD0;

      UNITY_VERTEX_OUTPUT_STEREO
    };

    v2f vert (appdata_t v)
    {
      PrepForOds(v.vertex);
      v.color = TbVertToSrgb(v.color);
      v2f o;

      UNITY_SETUP_INSTANCE_ID(v);
      UNITY_INITIALIZE_OUTPUT(v2f, o);
      UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
      
      o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
      o.color = bloomColor(v.color, _EmissionGain);
#ifdef AUDIO_REACTIVE
      o.color = musicReactiveColor(o.color, _BeatOutput.y);
      v.vertex = musicReactiveAnimation(v.vertex, v.color, _BeatOutput.y, o.texcoord.x);
#endif
      o.pos = UnityObjectToClipPos(v.vertex);
      return o;
    }

    fixed4 frag (v2f i) : COLOR
    {
      float4 color = i.color * tex2D(_MainTex, i.texcoord);
      color = float4(color.rgb * color.a, 1.0);
      color = SrgbToNative(color);
      color = encodeHdr(color.rgb);
      FRAG_MOBILESELECT(color)
      return color;
    }

  ENDCG

  // PC (Uses 'Add' blend mode for RGB)
  SubShader {
    LOD 201
    Pass {
      BlendOp Add, Min

      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
      ENDCG
    }
  }

  // Mobile (Uses 'Max' blend mode for RGB)
  SubShader {
    LOD 150
    Pass {
      BlendOp Max, Min

      CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
      ENDCG
    }
  }
}
}
