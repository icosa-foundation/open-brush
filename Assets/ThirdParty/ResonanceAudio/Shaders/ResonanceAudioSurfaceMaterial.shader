// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

/// A shader to show color coding of surface materials mapped to triangles.
Shader "ResonanceAudio/SurfaceMaterial" {
  SubShader {
    Tags { "DisableBatching" = "True" "RenderType" = "Acoustic" }
    Pass {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 3.5

      #include "UnityCG.cginc"

      struct vertexInput {
        float4 vertex : POSITION;
      };

      struct fragmentInput {
        float4 pos : SV_POSITION;
      };

      // Color coding for surface materials. Will be set as a global vector
      // array by calling Shader.SetGlobalVectorArray().
      float4 _SurfaceMaterialColors[64];
      int _NumSubMeshes = 0;

      // The ends of a contiguous range of triangle ids for each sub-mesh.
      float _SubMeshEnds[256];

      // The surface material mapped from a sub-mesh.
      float _SubMeshSurfaceMaterials[256];

      fragmentInput vert (vertexInput v) {
        fragmentInput o;
        o.pos = UnityObjectToClipPos(v.vertex);
        return o;
      }

      half4 frag (fragmentInput i,
                  uint triangleId : SV_PrimitiveID) : SV_Target {
        half4 ret;

        // Find the surface material. We use the triangle id to search which
        // sub-mesh this triangle belongs to and then find the mapped surface
        // material of the sub-mesh.
        int subMeshIndex = 0;
        for (; subMeshIndex < _NumSubMeshes; ++subMeshIndex) {
          if (triangleId < _SubMeshEnds[subMeshIndex]) {
            break;
          }
        }
        float3 f =
            _SurfaceMaterialColors[(int)_SubMeshSurfaceMaterials[subMeshIndex]];
        ret = half4(f[0], f[1], f[2], 0.5);
        return ret;
      }
      ENDCG
    }
  }

  FallBack Off
}
