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

Shader "Brush/Intro/SoftHighlighter" {
Properties {
  _MainTex ("Texture", 2D) = "white" {}
}

Category {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Blend SrcAlpha One
  AlphaTest Greater .01
  ColorMask RGB
  Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

  SubShader {
    Pass {
      Tags { "LightMode"="UniversalForward" }

      CGPROGRAM
      #pragma multi_compile_instancing
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"
      #include "Packages/com.icosa.open-brush-unity-tools/Runtime/Shaders/Include/Brush.cginc"

      sampler2D _MainTex;
      half _IntroDissolve;

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float3 normal : NORMAL;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
      };

      float4 _MainTex_ST;

      v2f vert (appdata_t v)
      {
        UNITY_SETUP_INSTANCE_ID(v);
        v2f o = (v2f)0;
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
        o.color = TbVertToNative(v.color);
        o.vertex = UnityObjectToClipPos(v.vertex);
        // Custom curve for the intro dissolve effect
        o.color.a *= lerp(1.0, v.texcoord.y * (1.0 - _IntroDissolve), pow(_IntroDissolve, .05));
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
         UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
         half4 c = tex2D(_MainTex, i.texcoord );
        return i.color * c;
      }
      ENDCG
    }
  }
}
}
