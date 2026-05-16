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

Shader "Custom/SketchbookButton" {

  Properties {
    _Color ("Color", Color) = (1,1,1,1)
    _Tex_0 ("Texture", 2D) = "white" {}
    _Tex_1 ("Texture", 2D) = "white" {}
    _Distance ("Distance", Range (0,1)) = 0
    _Grayscale ("Grayscale", Float) = 0
    _Cutoff ("Alpha Cutoff", Range (0,1)) = 0.5
  }

  CGINCLUDE
  #include "UnityCG.cginc"
  #include "Assets/Shaders/Include/Hdr.cginc"
  #include "Assets/Shaders/Include/Brush.cginc"
  #pragma target 3.0

  sampler2D _Tex_0;
  sampler2D _Tex_1;
  float4 _Tex_0_ST;
  float4 _Color;
  float _Distance;
  float _Cutoff;
  float4 _GrabHighlightActiveColor;
  uniform float _Activated;
  float _Grayscale;
  uniform float _PanelMipmapBias;

  struct appdata_t {
    float4 vertex : POSITION;
    float4 color : COLOR;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 texcoord : TEXCOORD0;

    UNITY_VERTEX_INPUT_INSTANCE_ID
  };

  struct v2f {
    float4 vertex : SV_POSITION;
    float4 color : COLOR;
    float4 texcoord : TEXCOORD0;
    float3 viewDir : TEXCOORD1;

    UNITY_VERTEX_OUTPUT_STEREO
  };

  v2f vert (appdata_t v) {
    v2f o;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.color = 0;
    o.texcoord = float4(TRANSFORM_TEX(v.texcoord,_Tex_0).xy, 0, _PanelMipmapBias);
    o.viewDir = ObjSpaceViewDir(v.vertex);
    return o;
  }

  // _Tex_0 is the front layer; _Tex_1 is the recessed background.
  // UV offset on _Tex_1 based on view angle simulates parallax depth.
  fixed4 frag (v2f i) : SV_TARGET {
    float3 viewDir = normalize(i.viewDir);
    float2 parallaxOffset = viewDir.xy * (_Distance * 0.1);
    fixed4 tex0 = tex2Dbias(_Tex_0, float4(i.texcoord.xy - parallaxOffset, 0, i.texcoord.w));
    fixed4 tex1 = tex2D(_Tex_1, i.texcoord.xy);
    tex0.rgb *= .75;
    tex1.rgb *= .75;

    // _Tex_1 is the icon overlay (front); _Tex_0 is the sketch background (back, parallax-shifted)
    fixed4 tex = (tex1.a >= _Cutoff) ? tex1 : tex0;
    float4 myColor = _Color * tex;
    myColor.a = tex.a;

    if (myColor.a < _Cutoff)
      discard;

    if (_Grayscale == 1) {
      float grayscale = dot(myColor.rgb, float3(0.3, 0.59, 0.11));
      return encodeHdr(grayscale);
    }

    return encodeHdr(myColor);
  }

  ENDCG

  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
    AlphaTest Greater .01

    Zwrite On
    Ztest LEqual
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      ENDCG
    }
  }
  FallBack "Diffuse"
}


