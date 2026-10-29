// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using UnityEngine;

namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
        private bool m_ExpandingGrid;
        private RuntimeVoxDocument m_ExpansionDocument;
        private RuntimeVoxDocument.RuntimeModel m_ExpansionModel;
        private RuntimeVoxDocument.VoxelDefinition m_ExpansionDefinition;
        private ModelWidget m_ExpansionWidget;
        private Vector3 m_ExpansionPointerStart;
        private int m_ExpansionFace;

        private void CancelExpansion()
        {
            if (!m_ExpandingGrid) return;
            m_ExpandingGrid = false;
            m_ExpansionDocument = null;
            m_ExpansionModel = null;
            m_ExpansionDefinition = null;
            m_ExpansionWidget = null;
            ApplyPendingTarget();
        }

        private void UpdateGridExpansion(Transform attachPoint, Vector3 canvasPosition)
        {
            if (!HasCurrentTarget || IsEatingInput)
            {
                CancelExpansion();
                UpdateTargetLabel(attachPoint, false, CurrentTargetWidget != null && CurrentTargetWidget.IsExternalVox
                    ? "Break Apart this imported model before expanding its grid"
                    : "Grab or select an editable voxel model to expand");
                return;
            }
            if (m_ExpandingGrid && (m_ExpansionWidget != CurrentTargetWidget ||
                !ReferenceEquals(m_ExpansionDocument, m_CurrentDocument._Document) ||
                !m_ExpansionDocument.ContainsModel(m_ExpansionModel) ||
                !ReferenceEquals(m_ExpansionDefinition, m_ExpansionModel.Definition)))
            {
                CancelExpansion();
                return;
            }

            RuntimeVoxDocument.RuntimeModel hoveredModel = null;
            int hoveredFace = -1;
            float closest = float.PositiveInfinity;
            if (!m_ExpandingGrid)
            {
                foreach (var model in m_CurrentDocument._Document.Models)
                {
                    if (!model.IsVisible) continue;
                    TrTransform pose = m_CurrentDocument.GetModelTransform(model);
                    Vector3 local = pose.inverse * canvasPosition;
                    float handleSize = VoxEditPreviewRenderer.GetExpansionHandleSize(pose);
                    for (int face = 0; face < 6; ++face)
                    {
                        Vector3 center = ((Vector3)model.Size - Vector3.one) * 0.5f;
                        int axis = face / 2;
                        center[axis] = face % 2 == 0 ? -0.5f : model.Size[axis] - 0.5f;
                        float distance = (local - center).sqrMagnitude;
                        if (distance > handleSize * handleSize * 0.81f || distance >= closest) continue;
                        closest = distance;
                        hoveredModel = model;
                        hoveredFace = face;
                    }
                }
                if (hoveredModel != null && hoveredModel.Size[hoveredFace / 2] < RuntimeVoxDocument.MaxModelDimension &&
                    InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
                {
                    m_ExpandingGrid = true;
                    m_ExpansionDocument = m_CurrentDocument._Document;
                    m_ExpansionModel = hoveredModel;
                    m_ExpansionDefinition = hoveredModel.Definition;
                    m_ExpansionWidget = CurrentTargetWidget;
                    m_ExpansionFace = hoveredFace;
                    m_ExpansionPointerStart = m_CurrentDocument.GetModelTransform(hoveredModel).inverse * canvasPosition;
                }
            }

            int growth = 0;
            if (m_ExpandingGrid)
            {
                Vector3 local = m_CurrentDocument.GetModelTransform(m_ExpansionModel).inverse * canvasPosition;
                int axis = m_ExpansionFace / 2;
                float direction = m_ExpansionFace % 2 == 0 ? -1f : 1f;
                growth = Mathf.Clamp(Mathf.CeilToInt(
                    direction * (local[axis] - m_ExpansionPointerStart[axis]) - 0.0001f),
                    0, RuntimeVoxDocument.MaxModelDimension - m_ExpansionModel.Size[axis]);
            }
            foreach (var model in m_CurrentDocument._Document.Models)
            {
                if (!model.IsVisible) continue;
                Vector3 min = Vector3.one * -0.5f;
                Vector3 max = (Vector3)model.Size - Vector3.one * 0.5f;
                bool expanding = m_ExpandingGrid && ReferenceEquals(model, m_ExpansionModel);
                if (expanding)
                {
                    if (m_ExpansionFace % 2 == 0) min[m_ExpansionFace / 2] -= growth;
                    else max[m_ExpansionFace / 2] += growth;
                }
                VoxEditPreviewRenderer.DrawExpansionHandles(m_CurrentDocument.GetModelTransform(model),
                    model.Size, min, max, expanding ? m_ExpansionFace : ReferenceEquals(model, hoveredModel) ? hoveredFace : -1);
            }
            string hint = "Touch a face handle, hold the trigger, and pull outward";
            if (m_ExpandingGrid)
            {
                Vector3Int size = m_ExpansionModel.Size;
                size[m_ExpansionFace / 2] += growth;
                hint = $"{size.x} x {size.y} x {size.z} voxels — release to expand";
            }
            else if (hoveredModel != null && hoveredModel.Size[hoveredFace / 2] == RuntimeVoxDocument.MaxModelDimension)
                hint = "This grid is at the 256-cell limit on this axis";
            UpdateTargetLabel(attachPoint, false, hint);

            if (m_ExpandingGrid && !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                try
                {
                    if (growth > 0)
                    {
                        var lower = Vector3Int.zero;
                        var upper = Vector3Int.zero;
                        if (m_ExpansionFace % 2 == 0) lower[m_ExpansionFace / 2] = growth;
                        else upper[m_ExpansionFace / 2] = growth;
                        var before = m_ExpansionDocument.CaptureSnapshot();
                        try
                        {
                            m_ExpansionDocument.ExpandModelBounds(m_ExpansionModel, lower, upper);
                            var after = m_ExpansionDocument.CaptureSnapshot();
                            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                                new ModifyVoxDocumentCommand(m_ExpansionWidget, before, after));
                        }
                        catch
                        {
                            m_ExpansionDocument.RestoreSnapshot(before);
                            throw;
                        }
                    }
                }
                finally
                {
                    CancelExpansion();
                }
            }
        }
    }
}
