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

Shader "Brush/Special/DoubleTaperedMarker" {
Properties {
  _Dissolve("Dissolve", Range(0,1)) = 1
  _ClipStart("Clip Start", Float) = 0
  _ClipEnd("Clip End", Float) = -1
}

Category {
  Cull Off Lighting Off

  SubShader {
    Tags{ "DisableBatching" = "True" }
    Pass {

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0
      #pragma multi_compile_particles
      #pragma multi_compile_fog
      #pragma multi_compile __ HDR_EMULATED HDR_SIMPLE
      #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM
      #pragma multi_compile __ SELECTION_ON

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
        float2 texcoord0 : TEXCOORD0;
        float3 texcoord1 : TEXCOORD1; //per vert offset vector
        uint id : SV_VertexID;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 pos : POSITION;
        fixed4 color : COLOR;
        float2 texcoord : TEXCOORD0;
        uint id : TEXCOORD2;
        UNITY_FOG_COORDS(1)

        UNITY_VERTEX_OUTPUT_STEREO
      };

      v2f vert (appdata_t v)
      {
        PrepForOds(v.vertex);

        //
        // XXX - THIS SHADER SHOULD BE DELETED AFTER TAPERING IS DONE IN THE GEOMETRY GENERATION
        //

        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        float envelope = sin(v.texcoord0.x * 3.14159);
        float widthMultiplier = 1 - envelope;
        v.vertex.xyz += -v.texcoord1 * widthMultiplier;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.color = TbVertToNative(v.color);
        o.texcoord = v.texcoord0;
        o.id = (float2)v.id;
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
      }

      fixed4 frag (v2f i) : COLOR
      {
        #ifdef SHADER_SCRIPTING_ON
        if (_ClipEnd > 0 && !(i.id.x > _ClipStart && i.id.x < _ClipEnd)) discard;
        if (_Dissolve < 1 && Dither8x8(i.pos.xy) >= _Dissolve) discard;
        #endif

        UNITY_APPLY_FOG(i.fogCoord, i.color.rgb);
        float4 color = float4(i.color.rgb, 1);
        FRAG_MOBILESELECT(color)
        return color;
      }

      ENDCG
    }
  }
}
}
