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

Shader "Unlit/Backfaces" {

Properties {
  _Color ("Main Color", Color) = (1,1,1,1)
}

SubShader {
    Pass {
        Lighting Off
        Cull Front

        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
    #pragma multi_compile_fog

    #include "UnityCG.cginc"

        struct appdata_t {
            float4 vertex : POSITION;

            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct v2f {
            float4 vertex : POSITION;
            UNITY_FOG_COORDS(1)
            UNITY_VERTEX_OUTPUT_STEREO
        };

    fixed4 _Color;

        v2f vert (appdata_t v)
        {
            v2f o;

            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

            o.vertex = UnityObjectToClipPos(v.vertex);
      UNITY_TRANSFER_FOG(o,o.vertex);
            return o;
        }

        fixed4 frag (v2f i) : COLOR
        {
            fixed4 col = _Color;
      UNITY_APPLY_FOG(i.fogCoord, col);
      return col;
        }

        ENDCG
    }
}

}
