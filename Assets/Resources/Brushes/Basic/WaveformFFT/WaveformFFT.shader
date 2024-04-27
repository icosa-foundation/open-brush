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

Shader "Brush/Visualizer/WaveformFFT" {
Properties {
  _MainTex ("Particle Texture", 2D) = "white" {}
  _EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
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
      #pragma target 3.0
      #pragma multi_compile_particles
      #pragma multi_compile __ AUDIO_REACTIVE
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/Hdr.cginc"

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
        float4 vertex : POSITION;
        float4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        float4 unbloomedColor : TEXCOORD1;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);

        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = bloomColor(v.color, _EmissionGain);
        o.unbloomedColor = v.color;
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        // Envelope
        float envelope = 1; //sin(i.texcoord.x * 3.14159);

#ifdef AUDIO_REACTIVE
        float waveform = (tex2D(_FFTTex, float2(.5 - i.texcoord.x,0)).b);
#else
        float waveform = .15 * sin( -30 * i.unbloomedColor.r * _Time.w + i.texcoord.x * 100   * i.unbloomedColor.r);
        waveform += .15 * sin( -40 * i.unbloomedColor.g * _Time.w + i.texcoord.x * 100   * i.unbloomedColor.g);
        waveform += .15 * sin( -50 * i.unbloomedColor.b * _Time.w + i.texcoord.x * 100   * i.unbloomedColor.b);
#endif

        float pinch = (1 - envelope) * 40 + 20;
        //float procedural_line = saturate(1 - pinch*abs(i.texcoord.y +  -waveform * envelope));
        float procedural_line = abs(i.texcoord.y - .5) > waveform ? 0 : waveform ; //saturate(1 - pinch*abs(i.texcoord.y +  -waveform * envelope));
        float4 color = 1;
        color.rgb *= envelope * procedural_line;
        color = i.color * color;
        return encodeHdr(color.rgb * color.a);
      }
      ENDCG
    }
  }
}
}
