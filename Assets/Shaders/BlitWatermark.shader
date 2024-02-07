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

Shader "Custom/BlitWatermark" {
Properties {
 _MainTex ("", 2D) = "white" {}
}

SubShader {

ZTest Always Cull Off ZWrite Off Fog { Mode Off } //Rendering settings
Blend SrcAlpha OneMinusSrcAlpha // Alpha blending

 Pass{
  CGPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #include "UnityCG.cginc"
  //we include "UnityCG.cginc" to use the appdata_img struct

  struct v2f {
   float4 pos : POSITION;
   half2 uv : TEXCOORD0;

   UNITY_VERTEX_OUTPUT_STEREO
  };

  //Our Vertex Shader
  v2f vert (appdata_img v){
    v2f o;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.pos = UnityObjectToClipPos (v.vertex);
    o.uv = MultiplyUV (UNITY_MATRIX_TEXTURE0, v.texcoord.xy);
    return o;
  }

  UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex); //Reference in Pass is necessary to let us use this variable in shaders

 //Our Fragment Shader
 fixed4 frag (v2f i) : COLOR{
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  fixed4 tex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.uv); //Get the orginal rendered color
     return tex;
  }
  ENDCG
 }
}
 FallBack "Diffuse"
}
