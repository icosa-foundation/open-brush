// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
        private readonly List<VoxelGridResizeHandle> m_ResizeHandles = new();
        private bool m_ExpandingGrid;
        private RuntimeVoxDocument m_ExpansionDocument;
        private RuntimeVoxDocument.RuntimeModel m_ExpansionModel;
        private RuntimeVoxDocument.VoxelDefinition m_ExpansionDefinition;
        private ModelWidget m_ExpansionWidget;
        private VoxelGridResizeHandle m_ExpansionHandle;
        private Vector3 m_ExpansionPointerStart;
        private int m_ExpansionFace;
        private int m_ExpansionGrowth;
        private int m_MinResizeGrowth;

        private bool ExpansionIsValid => m_ExpandingGrid && HasCurrentTarget &&
            m_ExpansionWidget == CurrentTargetWidget &&
            ReferenceEquals(m_ExpansionDocument, m_CurrentDocument._Document) &&
            m_ExpansionDocument.ContainsModel(m_ExpansionModel) &&
            ReferenceEquals(m_ExpansionDefinition, m_ExpansionModel.Definition);

        private void CancelExpansion()
        {
            if (!m_ExpandingGrid) return;
            m_ExpandingGrid = false;
            m_ExpansionHandle = null;
            m_ExpansionDocument = null;
            m_ExpansionModel = null;
            m_ExpansionDefinition = null;
            m_ExpansionWidget = null;
            m_ExpansionGrowth = 0;
            ApplyPendingTarget();
        }

        internal void BeginGridResize(VoxelGridResizeHandle handle, Vector3 worldPointer)
        {
            if (!CanGrabResizeHandle || !m_CurrentDocument._Document.ContainsModel(handle.Model)) return;
            EndGesture();
            m_ExpandingGrid = true;
            m_ExpansionHandle = handle;
            m_ExpansionDocument = m_CurrentDocument._Document;
            m_ExpansionModel = handle.Model;
            m_ExpansionDefinition = handle.Model.Definition;
            m_ExpansionWidget = CurrentTargetWidget;
            m_ExpansionFace = handle.Face;
            m_ExpansionGrowth = 0;
            int axis = handle.Face / 2;
            int size = handle.Model.Size[axis];
            int removable = size - Mathf.Min(3, size);
            // Scan occupied cells once per grab, not per frame or per handle.
            foreach (var cell in handle.Model.Voxels.Keys)
            {
                int padding = handle.Face % 2 == 0 ? cell[axis] : size - 1 - cell[axis];
                removable = Mathf.Min(removable, padding);
            }
            m_MinResizeGrowth = -removable;
            m_ExpansionPointerStart = m_CurrentDocument.GetModelTransform(handle.Model).inverse *
                (App.Scene.ActiveCanvas.Pose.inverse * worldPointer);
        }

        internal bool CanGrabResizeHandle => isActiveAndEnabled && !ToolHidden() &&
            HasCurrentTarget && !CreatingNewModel && !m_GestureActive && !m_ExpandingGrid && !IsEatingInput;

        internal void UpdateGridResize(VoxelGridResizeHandle handle, Vector3 worldPointer)
        {
            if (handle != m_ExpansionHandle) return;
            if (!ExpansionIsValid)
            {
                CancelExpansion();
                return;
            }
            Vector3 local = m_CurrentDocument.GetModelTransform(m_ExpansionModel).inverse *
                (App.Scene.ActiveCanvas.Pose.inverse * worldPointer);
            int axis = m_ExpansionFace / 2;
            float direction = m_ExpansionFace % 2 == 0 ? -1f : 1f;
            m_ExpansionGrowth = Mathf.Clamp(Mathf.RoundToInt(
                direction * (local[axis] - m_ExpansionPointerStart[axis])),
                m_MinResizeGrowth, RuntimeVoxDocument.MaxModelDimension - m_ExpansionModel.Size[axis]);
        }

        internal void EndGridResize(VoxelGridResizeHandle handle)
        {
            if (handle != m_ExpansionHandle) return;
            try
            {
                if (!ExpansionIsValid || m_ExpansionGrowth == 0) return;
                var lower = Vector3Int.zero;
                var upper = Vector3Int.zero;
                if (m_ExpansionFace % 2 == 0) lower[m_ExpansionFace / 2] = m_ExpansionGrowth;
                else upper[m_ExpansionFace / 2] = m_ExpansionGrowth;
                var before = m_ExpansionDocument.CaptureSnapshot();
                try
                {
                    m_ExpansionDocument.ResizeModelBounds(m_ExpansionModel, lower, upper);
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
            finally { CancelExpansion(); }
        }

        internal void CancelGridResize(VoxelGridResizeHandle handle)
        {
            if (handle == m_ExpansionHandle) CancelExpansion();
        }

        internal void DrawHeldGridResize(VoxelGridResizeHandle handle)
        {
            // SketchControls does not update the painting tool during a native grab.
            if (handle == m_ExpansionHandle && isActiveAndEnabled) UpdateResizeHandles();
        }

        private void HideResizeHandles()
        {
            foreach (var handle in m_ResizeHandles)
                if (handle != null) handle.SetAvailable(false);
        }

        private void DestroyResizeHandles()
        {
            CancelExpansion();
            foreach (var handle in m_ResizeHandles)
            {
                if (handle == null) continue;
                handle.OnPreDestroy();
                Destroy(handle.gameObject);
            }
            m_ResizeHandles.Clear();
        }

        private void UpdateResizeHandles()
        {
            if (m_ExpandingGrid && !ExpansionIsValid) CancelExpansion();
            if (!HasCurrentTarget || CreatingNewModel || (ToolHidden() && !m_ExpandingGrid))
            {
                HideResizeHandles();
                return;
            }
            int index = 0;
            foreach (var model in m_CurrentDocument._Document.Models)
            {
                if (!model.IsVisible) continue;
                Vector3 min = Vector3.one * -0.5f;
                Vector3 max = (Vector3)model.Size - Vector3.one * 0.5f;
                bool expanding = m_ExpandingGrid && ReferenceEquals(model, m_ExpansionModel);
                if (expanding)
                {
                    if (m_ExpansionFace % 2 == 0) min[m_ExpansionFace / 2] -= m_ExpansionGrowth;
                    else max[m_ExpansionFace / 2] += m_ExpansionGrowth;
                }
                var pose = m_CurrentDocument.GetModelTransform(model);
                int highlighted = expanding ? m_ExpansionFace : -1;
                for (int face = 0; face < 6; ++face)
                {
                    if (index == m_ResizeHandles.Count || m_ResizeHandles[index] == null)
                    {
                        var obj = new GameObject("Voxel grid resize handle");
                        obj.transform.SetParent(App.Scene.ActiveCanvas.transform, false);
                        var created = obj.AddComponent<VoxelGridResizeHandle>();
                        if (index == m_ResizeHandles.Count) m_ResizeHandles.Add(created);
                        else m_ResizeHandles[index] = created;
                    }
                    var handle = m_ResizeHandles[index++];
                    handle.Configure(this, model, face, pose, min, max);
                    if (!expanding && handle.Hovered) highlighted = face;
                }
                VoxEditPreviewRenderer.DrawExpansionHandles(pose, min, max, highlighted);
            }
            for (; index < m_ResizeHandles.Count; ++index)
                if (m_ResizeHandles[index] != null) m_ResizeHandles[index].SetAvailable(false);
            if (m_ExpandingGrid)
            {
                var size = m_ExpansionModel.Size;
                size[m_ExpansionFace / 2] += m_ExpansionGrowth;
                UpdateTargetLabel(InputManager.Brush.Geometry.ToolAttachPoint, false,
                    $"{size.x} x {size.y} x {size.z} voxels — release grip to resize");
            }
        }
    }
}
