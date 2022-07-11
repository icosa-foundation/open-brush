// Copyright 2022 Chingiz Dadashov-Khandan
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

    private Batch m_TargetBatch;
    private List<Vector3> m_OldVerts;
    private List<Vector3> m_NewVerts;
    private int m_StartIndex;
    private int m_VertLength;
    private bool m_Initial;

    public SculptCommand(
        BatchSubset batchSubset, List<Vector3> newVerts, int startIndex, bool isInitial, BaseCommand parent = null) : base(parent) {
      m_TargetBatch = batchSubset.m_ParentBatch;
      m_NewVerts = newVerts;
      m_VertLength = newVerts.Count;
      m_OldVerts = m_TargetBatch.m_Geometry.m_Vertices.GetRange(startIndex, m_VertLength);
      m_StartIndex = startIndex;
      m_Initial = isInitial;
    }

    public override bool NeedsSave { get { return true; } } // Should always save.

    private void ApplySculptModification(List<Vector3> vertices) {
      m_TargetBatch.m_Geometry.EnsureGeometryResident();
      for (int i = m_StartIndex; i < m_StartIndex + m_VertLength; i++) {
        m_TargetBatch.m_Geometry.m_Vertices[i] = vertices[i - m_StartIndex];
      }

      m_TargetBatch.DelayedUpdateMesh();
    }

    protected override void OnRedo() {
      ApplySculptModification(m_NewVerts);
    }

    protected override void OnUndo() {
      ApplySculptModification(m_OldVerts);
    }

    public override bool Merge(BaseCommand other) {
      
      if (base.Merge(other)) { return true; }

      var newSculptCommand = other as SculptCommand;

      if (newSculptCommand == null || newSculptCommand.m_Initial) { 
        return false; 
      }

      m_Children.Add(other);
      return true;
    }
}
} // namespace TiltBrush
