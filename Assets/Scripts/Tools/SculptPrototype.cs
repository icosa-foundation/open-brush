// CTODO: adjust copyright
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{


    public class SculptPrototype : ToggleStrokeModificationTool
    {

        override public void Init() {
            base.Init();
            Debug.Log("Sculpt prototype initialized!");
        }

        override public void EnableTool(bool bEnable) {

            // Call this after setting up our tool's state.
            base.EnableTool(bEnable);
            HideTool(!bEnable);
        }

        override protected bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup) {
            var stroke = rGroup.m_Stroke;
            Batch parentBatch = rGroup.m_ParentBatch;
            int firstIdx = rGroup.m_StartVertIndex;
            int lastIdx = firstIdx + rGroup.m_VertLength;
            
            var newVertices =  parentBatch.m_MeshFilter.mesh.vertices;
            for (int i = firstIdx; i < lastIdx; i++) {
                if (Vector3.Distance(newVertices[i], m_ToolTransform.position)  < 0.5) // close enough to pointer
                {
                    Vector3 newVert = newVertices[i] + Vector3.forward * 0.2f; 
                    newVertices[i] = newVert;
                }
            }
            parentBatch.m_MeshFilter.mesh.vertices = newVertices;
            return true;
        }
    }

} // namespace TiltBrush
