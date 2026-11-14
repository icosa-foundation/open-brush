// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
        private bool m_StraightEdgeGesture;
        private readonly List<Vector3?> m_StraightEdgeOrigins = new();
        private PointerManager.SymmetryMode m_StraightEdgeSymmetry;

        private void BeginVoxelStraightEdge(Vector3 canvasPosition)
        {
            var pointers = PointerManager.m_Instance;
            var guide = pointers.StraightEdgeGuide;
            // Ordinary painting starts each straight-edge gesture in Line mode too.
            if (guide.TempShape != StraightEdgeGuideScript.Shape.Line)
                guide.SetTempShape(StraightEdgeGuideScript.Shape.Line);
            guide.ResolveTempShape();
            guide.ShowGuide(App.Scene.ActiveCanvas.Pose * canvasPosition);
            pointers.VoxelStraightEdgeGuideActive = true;
            m_StraightEdgeGesture = true;
            m_StraightEdgeSymmetry = pointers.CurrentSymmetryMode;
            var grid = m_CurrentDocument.GetModelTransform(m_GestureModel);
            BuildSymmetryCopies(grid, guide.GetOriginPos());
            m_StraightEdgeOrigins.Clear();
            foreach (var copy in m_VoxelCopies)
                m_StraightEdgeOrigins.Add(copy.Valid ? grid * (Vector3)copy.Center : (Vector3?)null);
        }

        private Vector3 UpdateVoxelStraightEdge(Vector3 canvasPosition)
        {
            var guide = PointerManager.m_Instance.StraightEdgeGuide;
            guide.SnapEnabled = InputManager.Brush.GetCommand(InputManager.SketchCommands.MenuContextClick) &&
                SketchControlsScript.m_Instance.ShouldRespondToPadInput(InputManager.ControllerName.Num);
            bool endpointSnapped = guide.UpdateTarget(App.Scene.ActiveCanvas.Pose * canvasPosition);
            guide.UpdateEndpointSnapHaptics(endpointSnapped);
            return guide.GetTargetPos();
        }

        private bool RestoreStraightEdgeOrigins(TrTransform grid)
        {
            if (m_StraightEdgeOrigins.Count != m_VoxelCopies.Count ||
                m_StraightEdgeSymmetry != PointerManager.m_Instance.CurrentSymmetryMode) return false;
            for (int i = 0; i < m_VoxelCopies.Count; ++i)
            {
                var copy = m_VoxelCopies[i];
                copy.HasLast = copy.Valid && m_StraightEdgeOrigins[i].HasValue;
                if (copy.HasLast) copy.Last = Vector3Int.RoundToInt(grid.inverse * m_StraightEdgeOrigins[i].Value);
            }
            return true;
        }

        private void ClearVoxelStraightEdge()
        {
            if (!m_StraightEdgeGesture) return;
            m_StraightEdgeGesture = false;
            m_StraightEdgeOrigins.Clear();
            var pointers = PointerManager.m_Instance;
            if (pointers == null) return;
            pointers.VoxelStraightEdgeGuideActive = false;
            if (pointers.StraightEdgeGuide != null)
            {
                pointers.StraightEdgeGuide.HideGuide();
                pointers.StraightEdgeGuide.UpdateEndpointSnapHaptics(false);
            }
        }
    }
}
