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

Shader "Custom/GrabHighlightMask" {
Properties {
}

//
// Stencil Mask Subshader
//
// Writes a stencil bit for objects which are selected to enable outline rendering in a later pass
//
Category {
  SubShader {
  Pass {
    ZTest Always
    ZWrite Off
    ColorMask 0
    Cull Off

    Stencil {
            Ref 1
            Comp always
            Pass replace
        }

      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.0

      #include "UnityCG.cginc"
      #include "Assets/Shaders/Include/Brush.cginc"
      #include "Assets/Shaders/Include/ColorSpace.cginc"

      struct appdata_t {
        float4 vertex : POSITION;
        fixed4 color : COLOR;

        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 vertex : POSITION;

        UNITY_VERTEX_OUTPUT_STEREO
      };

      v2f vert (appdata_t v) {
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        o.vertex = UnityObjectToClipPos(v.vertex);
        return o;
      }

      void frag (v2f i, out fixed4 col : SV_Target) {
        col = float4(1,1,1,1);
      }
      ENDCG
      }
    }
  }
}
