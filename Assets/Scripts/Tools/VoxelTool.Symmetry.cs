// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
        private readonly List<Matrix4x4> m_VoxelMirrors = new();
        private readonly List<VoxSymmetryEdits.Copy> m_VoxelCopies = new();
        private readonly VoxSymmetryEdits m_SymmetryEdits = new();
        private readonly Dictionary<Vector3Int, Color32> m_PreviewVoxels = new();
        private readonly VoxSymmetryPreviewRenderer m_SymmetryPreview = new();
        private readonly RuntimeVoxDocument.RuntimeModel m_CreationPreviewModel = new("preview", Vector3Int.one * 3);
        private PointerManager.SymmetryMode m_VoxelSymmetryMode;

        private void ResetSymmetryPaths()
        {
            foreach (var copy in m_VoxelCopies) copy.HasLast = false;
        }

        private void BuildSymmetryCopies(TrTransform gridToCanvas, Vector3 canvasPosition)
        {
            var pointers = PointerManager.m_Instance;
            pointers.GetVoxelSymmetryMatrices(TrTransform.FromTransform(InputManager.Brush.Geometry.ToolAttachPoint), m_VoxelMirrors);
            if (m_VoxelSymmetryMode != pointers.CurrentSymmetryMode || m_VoxelCopies.Count != m_VoxelMirrors.Count)
                ResetSymmetryPaths();
            m_VoxelSymmetryMode = pointers.CurrentSymmetryMode;
            while (m_VoxelCopies.Count < m_VoxelMirrors.Count) m_VoxelCopies.Add(new VoxSymmetryEdits.Copy());
            if (m_VoxelCopies.Count > m_VoxelMirrors.Count)
                m_VoxelCopies.RemoveRange(m_VoxelMirrors.Count, m_VoxelCopies.Count - m_VoxelMirrors.Count);
            var gridToWorld = App.Scene.ActiveCanvas.Pose * gridToCanvas;
            var gridMatrix = gridToWorld.ToMatrix4x4();
            var inverseGrid = gridToWorld.inverse.ToMatrix4x4();
            Vector3 sourceCell = Vector3Int.RoundToInt(gridToCanvas.inverse * canvasPosition);
            for (int i = 0; i < m_VoxelCopies.Count; ++i)
            {
                var copy = m_VoxelCopies[i];
                var matrix = inverseGrid * m_VoxelMirrors[i] * gridMatrix;
                Vector3 center = matrix.MultiplyPoint3x4(sourceCell);
                copy.Valid = VoxToolTarget.IsFinite(center) && center.magnitude < int.MaxValue / 4f;
                if (!copy.Valid) { copy.HasLast = false; continue; }
                copy.Center = Vector3Int.RoundToInt(center);
                matrix.SetColumn(3, new Vector4(0, 0, 0, 1));
                copy.Linear = matrix;
                copy.Inverse = matrix.inverse;
                copy.Valid = Mathf.Abs(copy.Inverse.m33 - 1) < 0.00001f &&
                    VoxBrushRasterizer.TryGetBounds(BrushFootprint, copy.Linear, out copy.Min, out copy.Max);
                if (!copy.Valid) copy.HasLast = false;
                var colors = pointers.SymmetryPointerColors;
                var settings = CurrentTargetWidget != null ? VoxPaintSettings.For(CurrentTargetWidget) : null;
                copy.PaletteIndex = settings?.PaletteIndex ?? 0;
                copy.Material = settings?.DrawingMaterial;
                copy.Color = copy.PaletteIndex > 0 ? m_CurrentDocument._Document.Palette[copy.PaletteIndex - 1] :
                    pointers.CurrentSymmetryMode == PointerManager.SymmetryMode.MultiMirror && colors != null && colors.Count > 0
                        ? colors[i % colors.Count] : App.BrushColor.CurrentColor;
            }
        }

        private void ApplyAt(Vector3 canvasPosition, bool straightEdge = false)
        {
            if (m_CurrentModel == null) return;
            var document = m_CurrentDocument._Document;
            var model = m_CurrentModel._Model;
            var grid = m_CurrentDocument.GetModelTransform(model);
            BuildSymmetryCopies(grid, canvasPosition);
            if (straightEdge && !RestoreStraightEdgeOrigins(grid)) return;
            var result = m_SymmetryEdits.Apply(document, model, m_VoxelCopies, BrushFootprint, Mode,
                countBoundsChanges: !m_GestureWidget.IsExternalVox);
            if (result.PaletteFull) ReportPaletteFull();
            if (!result.Changed) return;
            if (m_GestureWidget.IsExternalVox)
            {
                m_GestureEmbeddingCommand = new EmbedVoxModelCommand(m_GestureWidget, document);
                m_GestureEmbeddingCommand.Redo();
                m_CurrentDocument = new VoxDocumentApiWrapper(document, m_GestureWidget) { SuppressDirtyTracking = true };
                m_CurrentModel = new VoxModelApiWrapper(document, m_GestureModel, m_CurrentDocument);
                m_GestureAutoVisuals = true;
                m_ExternalPreviewSource = null;
            }
            m_CurrentDocument.OnDocumentMutated();
            m_GestureChanged = true;
        }
        private void DrawSymmetryPreview(RuntimeVoxDocument.RuntimeModel model, TrTransform gridToCanvas, Vector3 canvasPosition)
        {
            BuildSymmetryCopies(gridToCanvas, canvasPosition);
            if (m_StraightEdgeGesture && !RestoreStraightEdgeOrigins(gridToCanvas)) return;
            m_SymmetryPreview.Clear();
            Color guide = Mode == EditMode.Erase ? new Color(0.9f, 0.12f, 0.08f) :
                Mode == EditMode.Paint ? new Color(0.18f, 0.48f, 0.95f) : new Color(0.12f, 0.8f, 0.28f);
            VoxEditPreviewRenderer.Draw(model, gridToCanvas, canvasPosition, m_VoxelCopies.Count > 0 ? (Color)m_VoxelCopies[0].Color : App.BrushColor.CurrentColor,
                guide, m_BrushSizeStep, Mode == EditMode.Add, drawFootprint: false);
            m_SymmetryEdits.CollectPreview(model, m_VoxelCopies, BrushFootprint, Mode, m_StraightEdgeGesture, m_PreviewVoxels);
            foreach (var copy in m_VoxelCopies)
                if (copy.PreviewInvalid) m_SymmetryPreview.AddInvalid(copy.PreviewMin, copy.PreviewMax);
            m_SymmetryPreview.Draw(gridToCanvas, m_PreviewVoxels);
        }
    }
}
