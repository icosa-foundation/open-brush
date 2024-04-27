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

Shader "Custom/Panel" {
Properties {
  _Color ("Color", Color) = (1,1,1,1)
  _MainTex ("Texture", 2D) = "white" {}
}

SubShader {
  Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
  Lighting Off Cull Off ZWrite Off Fog { Mode Off }
  Blend SrcAlpha OneMinusSrcAlpha
  LOD 100

  Pass {
    CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"

      struct appdata_t {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        float4 wpos : TEXCOORD1;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      uniform float4 _Color;
      sampler2D _MainTex;
      float4 _MainTex_ST;

      v2f vert (appdata_t v)
      {
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.wpos = mul(unity_ObjectToWorld, v.vertex);
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
        return o;
      }

      fixed4 frag (v2f i) : SV_Target
      {
        fixed4 tex = tex2D(_MainTex, i.texcoord);
        fixed4 c = _Color;
        c.w *= tex.a;
        return c;
      }
    ENDCG
  }
}

}
