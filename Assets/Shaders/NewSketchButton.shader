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

Shader "Custom/NewSketchButton" {

  Properties {
    _Color ("Color", Color) = (1,1,1,1)
	_BrushColor ("Brush Color", Color) = (0.21, 0.48, 0.42, 1)
    _Tex_0 ("Texture", 2D) = "white" {}
    _Tex_1 ("Texture", 2D) = "white" {}
    _Tex_2 ("Texture", 2D) = "white" {}
    _Tex_3("Texture", 2D) = "white" {}
    _Distance ("Distance", Range (0,1)) = 0
    _Grayscale ("Grayscale", Float) = 0
    _Cutoff ("Alpha Cutoff", Range (0,1)) = 0.5
  }

  CGINCLUDE
  #include "UnityCG.cginc"
  #include "Assets/Shaders/Include/Hdr.cginc"
  #pragma target 3.0

  sampler2D _Tex_0;
  sampler2D _Tex_1;
  sampler2D _Tex_2;
  sampler2D _Tex_3;
  float4 _Tex_0_ST;
  float4 _Color;
  float4 _BrushColor;
  float _Distance;
  float _Cutoff;
  uniform float _Activated;
  float _Grayscale;

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
    float2 texcoord : TEXCOORD0;
    float3 viewDir : TEXCOORD1;

    UNITY_VERTEX_INPUT_INSTANCE_ID

    UNITY_VERTEX_OUTPUT_STEREO
  };

  v2f vert (appdata_t v) {
    v2f o;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.vertex = UnityObjectToClipPos(v.vertex);
    o.color = 0;
    o.texcoord = TRANSFORM_TEX(v.texcoord, _Tex_0);
    o.viewDir = ObjSpaceViewDir(v.vertex);
    return o;
  }

  // URP only renders one pass per object. Layers are composited front-to-back:
  // _Tex_0 (front) -> _Tex_3 -> _Tex_2 -> _Tex_1 (back).
  // This matches the original multi-pass Z-order where passes 2 and 3 shared
  // the same depth, making pass 3 (_Tex_3) override pass 2 (_Tex_2).
  // Back layers receive a larger UV parallax offset to simulate depth separation.
  fixed4 frag (v2f i) : SV_TARGET {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 viewDir = normalize(i.viewDir);
    float2 offset1 = viewDir.xy * (_Distance * 0.05);
    float2 offset2 = viewDir.xy * (_Distance * 0.10);

    fixed4 tex0 = tex2D(_Tex_0, i.texcoord.xy);
    fixed4 tex1 = tex2D(_Tex_1, i.texcoord.xy - offset2);
    fixed4 tex2 = tex2D(_Tex_2, i.texcoord.xy - offset2);
    fixed4 tex3 = tex2D(_Tex_3, i.texcoord.xy - offset1);

    float4 myColor;

    if (tex0.a >= _Cutoff) {
      tex0.rgb *= 0.75;
      myColor = _Color * tex0;
      myColor.a = tex0.a;
    } else if (tex3.a >= _Cutoff) {
      tex3.rgb *= 0.75;
      myColor = _Color * tex3;
      myColor.a = tex3.a;
    } else if (tex2.a * 0.7 >= _Cutoff) {
      myColor = _Color * tex2 * _BrushColor * 0.7;
      myColor.a = tex2.a * 0.7;
    } else if (tex1.a * 0.8 >= _Cutoff) {
      myColor = _Color * tex1 * _BrushColor * 0.8;
      myColor.a = tex1.a * 0.8;
    } else {
      discard;
    }

    if (_Grayscale == 1) {
      float grayscale = dot(myColor.rgb, float3(0.3, 0.59, 0.11));
      return encodeHdr(grayscale);
    }

    return encodeHdr(myColor);
  }

  ENDCG

  SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout" }
    AlphaTest Greater .01

    Zwrite On
    Ztest LEqual
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      ENDCG
    }
  }
  FallBack "Diffuse"
}
