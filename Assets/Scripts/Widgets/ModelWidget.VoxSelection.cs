// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

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
        internal Vector3Int CurrentMin => Floating.Min + Model.SourceCellOffset - Floating.IndexOrigin;
        internal Vector3Int CurrentMax => Floating.Max + Model.SourceCellOffset - Floating.IndexOrigin;

        internal VoxRegionSelection(ModelWidget widget, RuntimeVoxDocument document,
            RuntimeVoxDocument.RuntimeModel model, Vector3Int min, Vector3Int max)
            : this(document, widget.Model, new VoxFloatingRegion(model, min, max)) { }

        private VoxRegionSelection(RuntimeVoxDocument document, Model source, VoxFloatingRegion floating)
        {
            Document = document;
            Source = source;
            Floating = floating;
        }

        internal VoxRegionSelection WithFloating(VoxFloatingRegion floating)
            => new VoxRegionSelection(Document, Source, floating);

        internal bool IsValid(ModelWidget widget) => Document.ContainsModel(Model) && Model.IsVisible &&
            (ReferenceEquals(widget.EditableVoxDocument, Document) ||
                (widget.IsExternalVox && widget.Model == Source));
    }

    public partial class ModelWidget
    {
        private VoxRegionSelection m_VoxSelection;
        internal VoxRegionSelection VoxSelection
        {
            get => m_VoxSelection;
            set
            {
                // Explicit selection changes finalize their previous layer first.
                // Undo/redo assigns the immutable layer states directly.
                if (value != null && WidgetManager.m_Instance != null)
                    foreach (var widget in WidgetManager.m_Instance.ModelWidgets)
                        if (widget != null && widget != this) widget.m_VoxSelection = null;
                m_VoxSelection = value;
            }
        }
        internal bool HasVoxRegionSelection => VoxSelection != null && VoxSelection.IsValid(this);
        internal bool VoxRegionGrabActive => m_VoxGrabBefore != null;
        private RuntimeVoxDocument.Snapshot m_VoxGrabBefore;
        private VoxRegionSelection m_VoxGrabSelection, m_VoxGrabUndoSelection;
        private EmbedVoxModelCommand m_VoxGrabEmbedding;
        private TrTransform m_VoxGrabWidget, m_VoxGrabGrid, m_VoxVirtualGrab;
        private Vector3Int m_VoxGrabPivot, m_VoxGrabAnchorPivot, m_VoxGrabTranslation;
        private Quaternion m_VoxGrabRotation;
        private bool m_VoxGrabChanged, m_VoxGrabRecordMovements, m_VoxGrabStampArmed, m_VoxGrabHadPreview;

        internal static void ClearVoxRegionSelections()
        {
            foreach (var widget in WidgetManager.m_Instance.ModelWidgets)
                if (widget != null) widget.CommitVoxRegionSelection();
        }

        internal void SelectVoxRegion(RuntimeVoxDocument document, RuntimeVoxDocument.RuntimeModel model,
            Vector3Int min, Vector3Int max)
        {
            ClearVoxRegionSelections();
            var selection = new VoxRegionSelection(this, document, model, min, max);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new SetVoxRegionSelectionCommand(this, selection));
        }

        internal void CommitVoxRegionSelection()
        {
            if (VoxSelection == null) return;
            // The document already contains the visible composition. Finalization
            // discards the retained underlay; it does not move or reapply any cells.
            if (VoxRegionGrabActive) OnUserEndInteracting();
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new SetVoxRegionSelectionCommand(this, null));
        }

        private void BeginVoxRegionGrab()
        {
            if (!HasVoxRegionSelection || MultiplayerManager.m_Instance.IsViewOnly) return;
            m_VoxGrabSelection = m_VoxGrabUndoSelection = VoxSelection;
            var floating = VoxSelection.Floating;
            // A cell-center pivot keeps mixed odd/even cuboids on the integer lattice.
            m_VoxGrabAnchorPivot = Vector3Int.RoundToInt(((Vector3)floating.Min + floating.Max) * 0.5f);
            m_VoxGrabPivot = m_VoxGrabAnchorPivot + floating.Model.SourceCellOffset - floating.IndexOrigin;
            m_VoxGrabBefore = VoxSelection.Document.CaptureSnapshot();
            m_VoxGrabWidget = m_VoxVirtualGrab = TrTransform.FromTransform(transform);
            m_VoxGrabGrid = m_VoxGrabWidget * VoxMeshBuilder.GetModelTransform(VoxSelection.Model);
            m_VoxGrabRotation = Quaternion.identity;
            m_VoxGrabTranslation = Vector3Int.zero;
            m_VoxGrabChanged = m_VoxGrabStampArmed = m_VoxGrabHadPreview = false;
            m_VoxGrabRecordMovements = m_RecordMovements;
            m_RecordMovements = false;
            ClearVelocities();
        }

        public override TrTransform GetGrabbedTrTransform()
            => VoxRegionGrabActive ? m_VoxVirtualGrab : base.GetGrabbedTrTransform();

        public override void RecordAndSetSize(float value)
        {
            if (!HasVoxRegionSelection) base.RecordAndSetSize(value);
        }

        public override void SetVelocities(Vector3 linear, Vector3 angular, Vector3 pivot)
        {
            if (HasVoxRegionSelection) ClearVelocities();
            else base.SetVelocities(linear, angular, pivot);
        }

        public override void RecordAndSetPosRot(TrTransform input)
        {
            if (!VoxRegionGrabActive)
            {
                base.RecordAndSetPosRot(input);
                return;
            }
            input.scale = m_VoxGrabWidget.scale;
            m_VoxVirtualGrab = input;
            RuntimeVoxDocument.GetRegionGrabDelta(m_VoxGrabWidget, m_VoxGrabGrid, input, m_VoxGrabPivot,
                out Quaternion rotation, out Vector3Int translation);
            if (translation != m_VoxGrabTranslation || Quaternion.Angle(rotation, m_VoxGrabRotation) >= 0.01f)
            {
                m_VoxGrabTranslation = translation;
                m_VoxGrabRotation = rotation;
                var moved = VoxSelection.Floating.TransformFrom(m_VoxGrabSelection.Floating,
                    m_VoxGrabAnchorPivot, rotation, translation);
                if (moved.TryCompose(VoxSelection.Document))
                {
                    EnsureVoxRegionEmbedded();
                    VoxSelection = VoxSelection.WithFloating(moved);
                    m_VoxGrabChanged = !moved.SamePose(m_VoxGrabUndoSelection.Floating);
                    m_VoxGrabHadPreview = true;
                    RefreshEditableVoxMeshes();
                }
            }
            // A press during the grab stamps. Holding the button before the grab
            // has no clone behavior, and stamping also works while holding still.
            if (m_VoxGrabStampArmed && InputManager.Controllers[(int)m_InteractingController]
                    .GetCommandDown(InputManager.SketchCommands.DuplicateSelection))
                StampVoxRegion();
            m_VoxGrabStampArmed = true;
        }

        private void EnsureVoxRegionEmbedded()
        {
            if (!IsExternalVox) return;
            m_VoxGrabEmbedding = new EmbedVoxModelCommand(this, VoxSelection.Document);
            m_VoxGrabEmbedding.Redo();
            _ = new VoxDocumentApiWrapper(VoxSelection.Document, this);
        }

        private bool RecordPendingVoxRegionMove()
        {
            if (!m_VoxGrabChanged) return false;
            var document = VoxSelection.Document;
            var command = new MoveVoxRegionCommand(this, m_VoxGrabBefore, document.CaptureSnapshot(),
                m_VoxGrabUndoSelection, VoxSelection, m_VoxGrabEmbedding);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(m_VoxGrabEmbedding ?? (BaseCommand)command);
            ResetVoxRegionUndoBoundary();
            return true;
        }

        private void ResetVoxRegionUndoBoundary()
        {
            m_VoxGrabBefore = VoxSelection.Document.CaptureSnapshot();
            m_VoxGrabUndoSelection = VoxSelection;
            m_VoxGrabEmbedding = null;
            m_VoxGrabChanged = m_VoxGrabHadPreview = false;
        }

        private void StampVoxRegion()
        {
            var stamped = VoxSelection.Floating.Stamp();
            if (ReferenceEquals(stamped, VoxSelection.Floating)) return;
            // Finish the preceding motion first: Undo should remove the stamp
            // separately from moving the floating region to this location.
            RecordPendingVoxRegionMove();
            EnsureVoxRegionEmbedded();
            var before = VoxSelection;
            var after = before.WithFloating(stamped);
            var snapshot = before.Document.CaptureSnapshot();
            var command = new MoveVoxRegionCommand(this, snapshot, snapshot, before, after, m_VoxGrabEmbedding);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(m_VoxGrabEmbedding ?? (BaseCommand)command);
            ResetVoxRegionUndoBoundary();
        }

        protected override void OnUserEndInteracting()
        {
            if (!VoxRegionGrabActive)
            {
                base.OnUserEndInteracting();
                return;
            }
            try
            {
                if (!RecordPendingVoxRegionMove())
                {
                    var document = m_VoxGrabUndoSelection.Document;
                    document.RestoreSnapshot(m_VoxGrabBefore);
                    VoxSelection = m_VoxGrabUndoSelection;
                    m_VoxGrabEmbedding?.Undo();
                    if (m_VoxGrabHadPreview && EditableVoxDocument == document) RefreshEditableVoxMeshes();
                }
                ClearVelocities();
                base.OnUserEndInteracting();
            }
            finally
            {
                m_RecordMovements = m_VoxGrabRecordMovements;
                m_VoxGrabBefore = null;
                m_VoxGrabSelection = m_VoxGrabUndoSelection = null;
                m_VoxGrabEmbedding = null;
            }
        }

        private void DrawVoxRegionSelection()
        {
            if (!HasVoxRegionSelection || !Showing) return;
            TrTransform grid = App.Scene.ActiveCanvas.AsCanvas[transform] * VoxMeshBuilder.GetModelTransform(VoxSelection.Model);
            VoxEditPreviewRenderer.DrawRegionBounds(grid, VoxSelection.CurrentMin,
                VoxSelection.CurrentMax, new Color(0.2f, 0.9f, 1f, 0.9f));
        }
    }
}