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

using System;
using UnityEngine;
using System.Collections.Generic;

namespace TiltBrush {
public class RepaintStrokeCommand : BaseCommand {
  private Stroke m_TargetStroke;
  private Color m_StartColor;
  private Guid m_StartGuid;
  private Color m_EndColor;
  private Guid m_EndGuid;

  public RepaintStrokeCommand(
      Stroke stroke, Color newcolor, Guid newGuid, BaseCommand parent = null) : base(parent) {
    m_TargetStroke = stroke;
    m_StartColor = stroke.m_Color;
    m_StartGuid = stroke.m_BrushGuid;
    m_EndColor = newcolor;
    m_EndGuid = newGuid;
  }

  public override bool NeedsSave { get { return true; } }

  private void ApplyColorAndBrushToObject(Color color, Guid brushGuid) {
    m_TargetStroke.m_Color = ColorPickerUtils.ClampLuminance(
        color, BrushCatalog.m_Instance.GetBrush(brushGuid).m_ColorLuminanceMin);
    m_TargetStroke.m_BrushGuid = brushGuid;
    m_TargetStroke.InvalidateCopy();
    List<Vector3> verts = new List<Vector3>();
    List<Vector3> normals = new List<Vector3>();

    if (m_TargetStroke.m_bWasSculpted) {
      m_TargetStroke.m_BatchSubset.m_ParentBatch.m_Geometry.EnsureGeometryResident();
      verts = m_TargetStroke.m_BatchSubset.m_ParentBatch.m_Geometry.m_Vertices.GetRange(
                m_TargetStroke.m_BatchSubset.m_StartVertIndex, m_TargetStroke.m_BatchSubset.m_VertLength);
      normals = m_TargetStroke.m_BatchSubset.m_ParentBatch.m_Geometry.m_Normals.GetRange(
                m_TargetStroke.m_BatchSubset.m_StartVertIndex, m_TargetStroke.m_BatchSubset.m_VertLength);
    }
    m_TargetStroke.Uncreate();
    m_TargetStroke.Recreate();
    if (verts.Count > 0) {
      m_TargetStroke.m_BatchSubset.m_ParentBatch.m_Geometry.EnsureGeometryResident();
      m_TargetStroke.m_bWasSculpted = true;
      for (int i = 0; i < verts.Count; i++) {
        m_TargetStroke.m_BatchSubset.m_ParentBatch.m_Geometry.m_Vertices[i + m_TargetStroke.m_BatchSubset.m_StartVertIndex] = verts[i];
        m_TargetStroke.m_BatchSubset.m_ParentBatch.m_Geometry.m_Normals[i + m_TargetStroke.m_BatchSubset.m_StartVertIndex] = normals[i];
      }
    } 
  }

  protected override void OnRedo() {
    ApplyColorAndBrushToObject(m_EndColor, m_EndGuid);
  }

  protected override void OnUndo() {
    ApplyColorAndBrushToObject(m_StartColor, m_StartGuid);
  }
}
} // namespace TiltBrush

