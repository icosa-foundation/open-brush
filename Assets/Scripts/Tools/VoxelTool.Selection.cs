// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
        public bool SelectingRegion { get; private set; }
        [SerializeField] private float m_RegionDoubleTapDuration = 0.4f;
        private bool m_RegionDragging, m_RegionClearTap;
        private Vector3Int m_RegionStart, m_RegionEnd;
        private RuntimeVoxDocument.RuntimeModel m_RegionModel;
        private ModelWidget m_RegionWidget;
        private float m_RegionLastEmptyTap = float.NegativeInfinity;

        public void ToggleRegionSelection()
        {
            EndGesture();
            CancelRegionDrag();
            SelectingRegion = !SelectingRegion;
            m_CreateNewGrid = false;
            m_RegionLastEmptyTap = float.NegativeInfinity;
            if (SelectingRegion)
            {
                // Region grabbing acts directly on its model, not the scene's
                // aggregate SelectionWidget and its moving SelectionCanvas.
                SelectionManager.m_Instance.ClearActiveSelection();
            }
            EatInput();
        }

        public void UseEditingModes()
        {
            SelectingRegion = false;
            CancelRegionDrag();
            EatInput();
        }

        private void CancelRegionDrag()
        {
            m_RegionDragging = false;
            m_RegionModel = null;
            m_RegionWidget = null;
        }

        private void UpdateRegionSelection(Vector3 canvasPosition)
        {
            if (IsEatingInput) return;
            bool down = InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate);
            bool held = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
            if (down && HasCurrentTarget)
            {
                m_RegionModel = FindTargetModelAt(canvasPosition) ?? m_CurrentModel._Model;
                m_RegionWidget = CurrentTargetWidget;
                TrTransform grid = m_CurrentDocument.GetModelTransform(m_RegionModel);
                Vector3Int cell = Vector3Int.RoundToInt(grid.inverse * canvasPosition);
                bool empty = !m_RegionWidget.TryGetVoxRegionCell(m_RegionModel, cell, out _);
                m_RegionClearTap = empty;
                if (empty && Time.realtimeSinceStartup - m_RegionLastEmptyTap <= m_RegionDoubleTapDuration)
                {
                    ModelWidget.ClearVoxRegionSelections();
                    CancelRegionDrag();
                    m_RegionLastEmptyTap = float.NegativeInfinity;
                    EatInput();
                    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);
                    return;
                }
                m_RegionStart = m_RegionEnd = cell;
                m_RegionDragging = true;
            }
            if (!m_RegionDragging) return;
            if (m_RegionWidget != CurrentTargetWidget || !HasCurrentTarget)
            {
                CancelRegionDrag();
                return;
            }
            TrTransform modelToCanvas = m_CurrentDocument.GetModelTransform(m_RegionModel);
            m_RegionEnd = Vector3Int.RoundToInt(modelToCanvas.inverse * canvasPosition);
            if (m_RegionEnd != m_RegionStart)
            {
                m_RegionClearTap = false;
                m_RegionLastEmptyTap = float.NegativeInfinity;
            }
            Vector3Int min = Vector3Int.Min(m_RegionStart, m_RegionEnd);
            Vector3Int max = Vector3Int.Max(m_RegionStart, m_RegionEnd);
            VoxEditPreviewRenderer.DrawRegionBounds(modelToCanvas, min, max, new Color(0.2f, 0.9f, 1f, 0.9f));
            if (held) return;
            if (m_RegionClearTap)
            {
                // An empty-space tap is a possible clear gesture, not a replacement
                // selection. Keep the existing cuboid through the first tap.
                m_RegionLastEmptyTap = Time.realtimeSinceStartup;
            }
            else
            {
                m_RegionLastEmptyTap = float.NegativeInfinity;
                Vector3Int oldOffset = m_RegionModel.SourceCellOffset;
                ModelWidget.ClearVoxRegionSelections();
                Vector3Int shift = m_RegionModel.SourceCellOffset - oldOffset;
                min += shift;
                max += shift;
                if (m_RegionModel.Definition.Grid.Region(min, max).Any())
                    m_RegionWidget.SelectVoxRegion(m_CurrentDocument._Document, m_RegionModel, min, max);
            }
            CancelRegionDrag();
        }
    }
}
