//CTODO: copyright info
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

using UnityEngine;
using System;
using System.Collections.Generic;

namespace TiltBrush {
  public class SculptCommand : BaseCommand {

    private BatchSubset m_TargetBatchSubset;
    private List<Vector3> m_OldVerts;
    private List<Vector3> m_NewVerts;
    private int m_StartIndex;
    private int m_VertLength;
    private bool m_Initial;

    // CTODO: maybe just pass the transformation data instead? (to call GeometryPool.ApplyVertexTransformation or whatever it was called.)
    public SculptCommand(
        BatchSubset batchSubset, List<Vector3> newVerts, int startIndex, bool isInitial, BaseCommand parent = null) : base(parent) {
      m_TargetBatchSubset = batchSubset;
      m_OldVerts = new List<Vector3>(m_TargetBatchSubset.m_ParentBatch.m_Geometry.m_Vertices);
      m_NewVerts = newVerts;
      m_VertLength = newVerts.Count;
      m_StartIndex = startIndex;
      m_Initial = isInitial;
    }

    public override bool NeedsSave { get { return true; } } // should always save

    private void ApplySculptModification(List<Vector3> vertices) {
      
      // CTODO: probably not needed anymore
      // if (m_TargetBatchSubset.m_ParentBatch.m_Geometry == null) { 
      //   Debug.LogWarning("Batch Geometry Lost");
      //   return;
      // }

      for (int i = m_StartIndex; i < m_VertLength; i++) {
        m_TargetBatchSubset.m_ParentBatch.m_Geometry.m_Vertices[i] = vertices[i - m_StartIndex];
      }
      m_TargetBatchSubset.m_ParentBatch.DelayedUpdateMesh();
    }

    protected override void OnRedo() {
      ApplySculptModification(m_NewVerts);
    }

    protected override void OnUndo() {
      ApplySculptModification(m_OldVerts);
    }

    public override bool Merge(BaseCommand other) {
      Debug.Log("SculptCommand::Merge() executed");
      
      if (base.Merge(other)) { return true; }

      var newSculptCommand = other as SculptCommand;

      if (newSculptCommand == null || newSculptCommand.m_Initial) { 
        return false; 
      }

      m_Children.Add(other);
      return true;
    }
    //CTODO: need to implement OnDispose()?
}
} // namespace TiltBrush
