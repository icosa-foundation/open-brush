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

Shader "Hidden/BlitDownsample"
{
  Properties
  {
    _MainTex ("Texture", 2D) = "white" {}
    _Scale("Scale", Range(0.01, 8.0)) = 1.0
  }
  SubShader
  {
    // No culling or depth
    Cull Off ZWrite Off ZTest Always

    Pass
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag

      #include "UnityCG.cginc"

      float _Scale;

      struct appdata
      {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f
      {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      v2f vert (appdata v)
      {
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
          
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv * _Scale;
        return o;
      }

      UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);

      fixed4 frag (v2f i) : SV_Target
      {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

        return UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uv);
      }
      ENDCG
    }
  }
}
