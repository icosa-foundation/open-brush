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

namespace TiltBrush
{
    public class SculptCommand : BaseCommand
    {

        private Stroke m_TargetStroke;
        private List<Vector3> m_OldVerts;
        private List<Vector3> m_NewVerts;
        private int m_VertLength;
        private bool m_Initial;
        private readonly bool m_OldMeshIsEdited;

        public SculptCommand(
            BatchSubset batchSubset, List<Vector3> newVerts, int startIndex, bool isInitial, BaseCommand parent = null) : base(parent)
        {
            m_TargetStroke = batchSubset.m_Stroke;
            m_OldMeshIsEdited = m_TargetStroke?.m_MeshIsEdited ?? false;
            m_NewVerts = newVerts;
            m_VertLength = newVerts.Count;
            var geometry = batchSubset.m_ParentBatch.Geometry;
            geometry.EnsureGeometryResident();
            m_OldVerts = geometry.m_Vertices.GetRange(startIndex, m_VertLength);
            m_Initial = isInitial;
        }

        public override bool NeedsSave { get { return true; } } // Should always save.

        private void ApplySculptModification(List<Vector3> vertices, bool meshIsEdited)
        {
            // Repaint and canvas moves recreate subsets; resolve the live stroke storage.
            var subset = m_TargetStroke?.m_BatchSubset;
            if (subset?.m_ParentBatch?.Geometry == null || subset.m_VertLength != m_VertLength)
            {
                Debug.LogWarning("[OB_SCULPT_UNDO] Stroke topology changed; skipping sculpt geometry.");
                return;
            }

            var batch = subset.m_ParentBatch;
            batch.Geometry.EnsureGeometryResident();
            int startIndex = subset.m_StartVertIndex;
            for (int i = 0; i < m_VertLength; i++)
            {
                batch.Geometry.m_Vertices[startIndex + i] = vertices[i];
            }

            m_TargetStroke.m_MeshIsEdited = meshIsEdited;
            subset.RecalculateBounds();
            batch.DelayedUpdateMesh();
        }

        protected override void OnRedo()
        {
            ApplySculptModification(m_NewVerts, true);
        }

        protected override void OnUndo()
        {
            ApplySculptModification(m_OldVerts, m_OldMeshIsEdited);
        }

        public override bool Merge(BaseCommand other)
        {

            if (base.Merge(other)) { return true; }

            var newSculptCommand = other as SculptCommand;

            if (newSculptCommand == null || newSculptCommand.m_Initial)
            {
                return false;
            }

            return true;
        }
    }
} // namespace TiltBrush
