// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    internal sealed class VoxRegionSelection
    {
        internal readonly RuntimeVoxDocument Document;
        internal readonly Model Source;
        internal readonly VoxFloatingRegion Floating;
        internal RuntimeVoxDocument.RuntimeModel Model => Floating.Model;
        internal Vector3Int CurrentMin => Floating.Min + Model.SourceCellOffset;
        internal Vector3Int CurrentMax => Floating.Max + Model.SourceCellOffset;
        internal VoxRegionSelection(ModelWidget widget, RuntimeVoxDocument document,
            RuntimeVoxDocument.RuntimeModel model, Vector3Int min, Vector3Int max)
            : this(document, widget.Model, new VoxFloatingRegion(model, min, max)) { }
        private VoxRegionSelection(RuntimeVoxDocument document, Model source, VoxFloatingRegion floating)
        { Document = document; Source = source; Floating = floating; }
        internal VoxRegionSelection WithFloating(VoxFloatingRegion floating) => new(Document, Source, floating);
        internal bool IsValid(ModelWidget widget) => Document.ContainsModel(Model) && Model.IsVisible &&
            (ReferenceEquals(widget.EditableVoxDocument, Document) ||
                (widget.IsExternalVox && !Floating.Lifted && widget.Model == Source));
    }

    public partial class ModelWidget
    {
        internal VoxRegionSelection VoxSelection { get; private set; }
        internal bool HasVoxRegionSelection => VoxSelection != null && VoxSelection.IsValid(this);
        internal bool VoxRegionGrabActive => m_VoxGrabActive;
        private bool m_VoxGrabActive, m_VoxGrabChanged, m_VoxGrabStampArmed, m_VoxRecordMovements;
        private VoxRegionSelection m_VoxGrabStart, m_VoxUndoState;
        private BaseCommand m_VoxGrabCommand;
        private TrTransform m_VoxGrabWidget, m_VoxGrabGrid, m_VoxVirtualGrab;
        private Vector3Int m_VoxPivot, m_VoxAnchorPivot, m_VoxTranslation, m_VoxUndoTranslation;
        private Quaternion m_VoxRotation, m_VoxUndoRotation;
        private VoxRegionDragPreview m_VoxDragPreview;
        private int m_VoxRegionHighlightFrame = -1;

        internal void ApplyVoxRegionState(VoxRegionSelection state)
        {
            var previous = VoxSelection;
            if (previous != null) previous.Document.FloatingSelection = null;
            if (state == null || (previous != null && !ReferenceEquals(previous.Floating.Selection, state.Floating.Selection)))
            {
                m_VoxDragPreview?.Dispose();
                m_VoxDragPreview = null;
                m_VoxRegionHighlightFrame = -1;
            }
            VoxSelection = state;
            UpdateVoxSaveState();
        }

        internal void RestoreVoxRegionState(VoxRegionSelection state)
        {
            ApplyVoxRegionState(state);
            // History can restore a floating region after another tool was activated.
            // Bind its existing widget before activation so no new model is created.
            var panel = SketchSurfacePanel.m_Instance;
            if (HasVoxRegionSelection && panel != null &&
                panel.ActiveToolType != BaseTool.ToolType.VoxelTool &&
                panel.GetToolOfType(BaseTool.ToolType.VoxelTool) is VoxelTool tool)
            {
                tool.SetTargetWidget(this);
                panel.EnableSpecificTool(BaseTool.ToolType.VoxelTool);
            }
        }

        internal void RefreshVoxRegionVisuals(bool cellsChanged)
        {
            if (cellsChanged) RefreshEditableVoxMeshes();
            if (HasVoxRegionSelection && VoxSelection.Floating.Lifted)
            {
                var region = VoxSelection.Floating;
                if (m_VoxChunks == null) RefreshEditableVoxMeshes();
                if (m_VoxDragPreview == null || !ReferenceEquals(m_VoxDragPreview.Payload, region.Payload))
                {
                    m_VoxDragPreview?.Dispose();
                    m_VoxDragPreview = new VoxRegionDragPreview(VoxSelection.Document, region,
                        m_VoxChunks.Layout, m_VoxChunks.Materials, m_BatchId);
                }
                m_VoxDragPreview.SetPose(m_VoxGrabActive ? DragPose() : region.Pose);
                RecalculateColliderBounds();
            }
            else if (!cellsChanged && EditableVoxDocument != null) RecalculateColliderBounds();
        }

        internal void RefreshVoxPaletteVisuals()
        {
            m_VoxDragPreview?.Dispose();
            m_VoxDragPreview = null;
            RefreshVoxRegionVisuals(true);
        }

        internal static void ClearVoxRegionSelections()
        {
            foreach (var widget in WidgetManager.m_Instance.ModelWidgets)
                if (widget != null) widget.CommitVoxRegionSelection();
        }

        internal void SelectVoxRegion(RuntimeVoxDocument document, RuntimeVoxDocument.RuntimeModel model,
            Vector3Int min, Vector3Int max)
        {
            ClearVoxRegionSelections();
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new SetVoxRegionSelectionCommand(this,
                new VoxRegionSelection(this, document, model, min, max)));
        }

        internal void CommitVoxRegionSelection()
        {
            if (VoxSelection == null) return;
            if (!HasVoxRegionSelection) { ApplyVoxRegionState(null); return; }
            if (m_VoxGrabActive) OnUserEndInteracting();
            var before = VoxSelection;
            if (before.Floating.Lifted)
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new MoveVoxRegionCommand(this,
                    before, null, VoxCellChange.Write(before.Floating)));
            else SketchMemoryScript.m_Instance.PerformAndRecordCommand(new SetVoxRegionSelectionCommand(this, null));
            if (EditableVoxDocument != null && EditableVoxDocument.NormalizeDrawingBounds()) RefreshEditableVoxMeshes();
        }

        private void BeginVoxRegionGrab()
        {
            if (!HasVoxRegionSelection || MultiplayerManager.m_Instance.IsViewOnly) return;
            m_VoxGrabStart = m_VoxUndoState = VoxSelection;
            var region = VoxSelection.Floating;
            m_VoxAnchorPivot = Vector3Int.RoundToInt(((Vector3)region.Min + region.Max) * .5f);
            m_VoxPivot = m_VoxAnchorPivot + region.Model.SourceCellOffset;
            m_VoxGrabWidget = m_VoxVirtualGrab = TrTransform.FromTransform(transform);
            m_VoxGrabGrid = m_VoxGrabWidget * VoxMeshBuilder.GetModelTransform(region.Model);
            m_VoxRotation = m_VoxUndoRotation = Quaternion.identity;
            m_VoxTranslation = m_VoxUndoTranslation = Vector3Int.zero;
            m_VoxGrabChanged = m_VoxGrabStampArmed = false;
            m_VoxRecordMovements = m_RecordMovements;
            m_RecordMovements = false;
            m_VoxGrabActive = true;
            m_VoxGrabCommand = null;
            ClearVelocities();
        }

        private bool EnsureVoxRegionLifted()
        {
            if (VoxSelection.Floating.Lifted) return true;
            var lifted = VoxSelection.Floating.Lift();
            if (lifted == null) return false;
            if (IsExternalVox)
            {
                var embedding = new EmbedVoxModelCommand(this, VoxSelection.Document);
                embedding.Redo();
                _ = new VoxDocumentApiWrapper(VoxSelection.Document, this);
                m_VoxGrabCommand = embedding;
            }
            else m_VoxGrabCommand = new BaseCommand();
            var command = new MoveVoxRegionCommand(this, VoxSelection, VoxSelection.WithFloating(lifted),
                parent: m_VoxGrabCommand, lift: new VoxRegionLift(VoxSelection.Document, lifted));
            command.Redo();
            // The drag basis now has a payload but retains the original grab pose.
            m_VoxGrabStart = m_VoxGrabStart.WithFloating(lifted);
            m_VoxUndoState = VoxSelection;
            return true;
        }

        public override TrTransform GetGrabbedTrTransform() => m_VoxGrabActive ? m_VoxVirtualGrab : base.GetGrabbedTrTransform();
        public override void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            if (VoxRegionGrabActive)
            {
                if (controller == m_InteractingController)
                    InputManager.GetControllerGeometry(controller).ShowDuplicateOption();
                return;
            }
            base.AssignControllerMaterials(controller);
        }

        public override void RecordAndSetSize(float value) { if (!HasVoxRegionSelection) base.RecordAndSetSize(value); }
        public override void SetVelocities(Vector3 linear, Vector3 angular, Vector3 pivot)
        {
            if (HasVoxRegionSelection) ClearVelocities();
            else base.SetVelocities(linear, angular, pivot);
        }

        public override void RecordAndSetPosRot(TrTransform input)
        {
            if (!m_VoxGrabActive) { base.RecordAndSetPosRot(input); return; }
            input.scale = m_VoxGrabWidget.scale;
            m_VoxVirtualGrab = input;
            RuntimeVoxDocument.GetRegionGrabDelta(m_VoxGrabWidget, m_VoxGrabGrid, input, m_VoxPivot,
                out Quaternion rotation, out Vector3Int translation);
            if ((translation != m_VoxTranslation || Quaternion.Angle(rotation, m_VoxRotation) >= .01f) &&
                EnsureVoxRegionLifted() && VoxSelection.Floating.CanTransformFrom(m_VoxGrabStart.Floating,
                    m_VoxAnchorPivot, rotation, translation))
            {
                m_VoxTranslation = translation;
                m_VoxRotation = rotation;
                m_VoxGrabChanged = translation != m_VoxUndoTranslation || Quaternion.Angle(rotation, m_VoxUndoRotation) >= .01f;
                TrTransform pose = DragPose();
                m_VoxDragPreview.SetPose(pose);
                VoxSelection.Document.FloatingSelection = new VoxFloatingSaveState(VoxSelection.Floating, pose);
            }
            if (m_VoxGrabStampArmed && InputManager.Controllers[(int)m_InteractingController]
                    .GetCommandDown(InputManager.SketchCommands.DuplicateSelection)) StampVoxRegion();
            m_VoxGrabStampArmed = true;
        }

        private TrTransform DragPose() => TrTransform.TR((Vector3)m_VoxAnchorPivot + m_VoxTranslation -
            m_VoxRotation * (Vector3)m_VoxAnchorPivot, m_VoxRotation) * m_VoxGrabStart.Floating.Pose;

        private void PublishVoxRegionPose()
        {
            if (!VoxSelection.Floating.Lifted) return;
            ApplyVoxRegionState(VoxSelection.WithFloating(VoxSelection.Floating.TransformFrom(m_VoxGrabStart.Floating,
                m_VoxAnchorPivot, m_VoxRotation, m_VoxTranslation)));
            m_VoxDragPreview?.SetPose(VoxSelection.Floating.Pose);
        }

        private bool RecordPendingVoxRegionMove()
        {
            if (!m_VoxGrabChanged) return false;
            var before = m_VoxUndoState;
            PublishVoxRegionPose();
            m_VoxGrabCommand ??= new BaseCommand();
            _ = new MoveVoxRegionCommand(this, before, VoxSelection, parent: m_VoxGrabCommand);
            // Lift and pose changes are already applied for feedback. Record them
            // through the existing applied-command API without replaying the edits.
            SketchMemoryScript.m_Instance.RecordCommand(m_VoxGrabCommand);
            SaveLoadScript.m_Instance?.SketchChanged();
            m_VoxGrabCommand = null;
            m_VoxUndoState = VoxSelection;
            m_VoxUndoTranslation = m_VoxTranslation;
            m_VoxUndoRotation = m_VoxRotation;
            m_VoxGrabChanged = false;
            RecalculateColliderBounds();
            return true;
        }

        private void StampVoxRegion()
        {
            if (!EnsureVoxRegionLifted()) return;
            RecordPendingVoxRegionMove();
            PublishVoxRegionPose();
            var changes = VoxCellChange.Write(VoxSelection.Floating);
            if (changes.Count == 0) return;
            if (m_VoxGrabCommand != null)
            {
                SketchMemoryScript.m_Instance.RecordCommand(m_VoxGrabCommand);
                m_VoxGrabCommand = null;
            }
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new MoveVoxRegionCommand(this,
                VoxSelection, VoxSelection, changes));
            m_VoxUndoState = VoxSelection;
        }

        protected override void OnUserEndInteracting()
        {
            if (!m_VoxGrabActive) { base.OnUserEndInteracting(); return; }
            try
            {
                if (!RecordPendingVoxRegionMove() && m_VoxGrabCommand != null)
                {
                    m_VoxGrabCommand.Undo();
                    m_VoxGrabCommand.Dispose();
                }
                ClearVelocities();
                base.OnUserEndInteracting();
            }
            finally
            {
                m_RecordMovements = m_VoxRecordMovements;
                m_VoxGrabActive = false;
                m_VoxGrabCommand = null;
                m_VoxGrabStart = m_VoxUndoState = null;
                UpdateVoxSaveState();
            }
        }

        private void UpdateVoxSaveState()
        {
            if (VoxSelection != null)
                VoxSelection.Document.FloatingSelection = VoxSelection.Floating.Lifted
                    ? new VoxFloatingSaveState(VoxSelection.Floating, VoxSelection.Floating.Pose) : (VoxFloatingSaveState?)null;
        }

        internal bool TryGetVoxRegionCell(RuntimeVoxDocument.RuntimeModel model, Vector3Int raw, out byte color)
        {
            if (HasVoxRegionSelection && VoxSelection.Model == model && VoxSelection.Floating.Lifted)
            {
                var region = VoxSelection.Floating;
                TrTransform pose = m_VoxGrabActive ? DragPose() : region.Pose;
                Vector3Int source = Vector3Int.RoundToInt(pose.inverse * (Vector3)(raw - model.SourceCellOffset));
                if (region.Payload.TryGetValue(source, out color)) return true;
            }
            return model.TryGetPaletteIndex(raw, out color);
        }

        private void DrawVoxRegionSelection()
        {
            if (!HasVoxRegionSelection || !Showing) return;
            var region = VoxSelection.Floating;
            TrTransform pose = m_VoxGrabActive && region.Lifted ? DragPose() : region.Pose;
            if (region.Lifted) m_VoxDragPreview?.Draw(TrTransform.FromTransform(transform), gameObject.layer, m_Keywords);
            Vector3Int a = Vector3Int.RoundToInt(pose * (Vector3)region.OriginalMin);
            Vector3Int b = Vector3Int.RoundToInt(pose * (Vector3)region.OriginalMax);
            TrTransform anchor = App.Scene.ActiveCanvas.AsCanvas[transform] *
                VoxMeshBuilder.GetModelTransform(region.AnchorOffset, region.Model.GlobalRotation);
            // Hover requests may arrive after this widget's update, so retain one frame.
            bool highlighted = m_VoxRegionHighlightFrame >= 0 &&
                Time.frameCount - m_VoxRegionHighlightFrame <= 1 && !SelectionEffect.DisableSelectionEffects;
            VoxEditPreviewRenderer.DrawRegionBounds(anchor, Vector3Int.Min(a, b), Vector3Int.Max(a, b),
                highlighted ? Color.white : new Color(.2f, .9f, 1f, .9f), highlighted ? .07f : .035f);
        }
    }
}
