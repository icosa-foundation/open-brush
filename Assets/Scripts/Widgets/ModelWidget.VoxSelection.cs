// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    internal sealed class VoxRegionSelection
    {
        internal readonly RuntimeVoxDocument Document;
        internal readonly RuntimeVoxDocument.RuntimeModel Model;
        internal readonly Model Source;
        internal readonly Vector3Int Min, Max, IndexOrigin;

        internal VoxRegionSelection(ModelWidget widget, RuntimeVoxDocument document,
            RuntimeVoxDocument.RuntimeModel model, Vector3Int min, Vector3Int max)
        {
            Document = document;
            Model = model;
            Source = widget.Model;
            Min = min;
            Max = max;
            IndexOrigin = model.SourceCellOffset;
        }

        internal Vector3Int CurrentMin => Min + Model.SourceCellOffset - IndexOrigin;
        internal Vector3Int CurrentMax => Max + Model.SourceCellOffset - IndexOrigin;
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
                // There is one active cuboid, even when changing target models.
                if (value != null && WidgetManager.m_Instance != null)
                    foreach (var widget in WidgetManager.m_Instance.ModelWidgets)
                        if (widget != null && widget != this) widget.m_VoxSelection = null;
                m_VoxSelection = value;
            }
        }
        internal bool HasVoxRegionSelection => VoxSelection != null && VoxSelection.IsValid(this);
        internal static void ClearVoxRegionSelections()
        {
            foreach (var widget in WidgetManager.m_Instance.ModelWidgets)
                if (widget != null) widget.m_VoxSelection = null;
        }
        internal bool VoxRegionGrabActive => m_VoxGrabBefore != null;
        private RuntimeVoxDocument.Snapshot m_VoxGrabBefore;
        private VoxRegionSelection m_VoxGrabSelection;
        private EmbedVoxModelCommand m_VoxGrabEmbedding;
        private TrTransform m_VoxGrabWidget, m_VoxGrabGrid, m_VoxVirtualGrab;
        private Vector3Int m_VoxGrabMin, m_VoxGrabMax, m_VoxGrabPivot, m_VoxGrabTranslation;
        private Quaternion m_VoxGrabRotation;
        private bool m_VoxGrabClone, m_VoxGrabChanged, m_VoxGrabRecordMovements;

        private void BeginVoxRegionGrab()
        {
            if (!HasVoxRegionSelection || MultiplayerManager.m_Instance.IsViewOnly) return;
            m_VoxGrabSelection = VoxSelection;
            m_VoxGrabMin = VoxSelection.CurrentMin;
            m_VoxGrabMax = VoxSelection.CurrentMax;
            // A cell-center pivot keeps all 24 cube orientations on the integer lattice,
            // including selections with a mix of odd and even side lengths.
            m_VoxGrabPivot = Vector3Int.RoundToInt(((Vector3)m_VoxGrabMin + m_VoxGrabMax) * 0.5f);
            m_VoxGrabBefore = VoxSelection.Document.CaptureSnapshot();
            m_VoxGrabWidget = m_VoxVirtualGrab = TrTransform.FromTransform(transform);
            m_VoxGrabGrid = m_VoxGrabWidget * VoxMeshBuilder.GetModelTransform(VoxSelection.Model);
            m_VoxGrabClone = InputManager.Controllers[(int)m_InteractingController]
                .GetCommand(InputManager.SketchCommands.DuplicateSelection);
            m_VoxGrabRotation = Quaternion.identity;
            m_VoxGrabTranslation = Vector3Int.zero;
            m_VoxGrabChanged = false;
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
            // Region movement ends at release. It must never toss or drift the model.
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
            if (translation == m_VoxGrabTranslation && Quaternion.Angle(rotation, m_VoxGrabRotation) < 0.01f)
                return;
            m_VoxGrabTranslation = translation;
            m_VoxGrabRotation = rotation;
            var selection = m_VoxGrabSelection;
            selection.Document.RestoreSnapshot(m_VoxGrabBefore);
            // Always rebuild from the start of the grab. Traversed destinations must
            // not be erased, and overlapping moves/clones must keep their source data.
            m_VoxGrabChanged = selection.Document.TryTransformRegion(selection.Model,
                m_VoxGrabMin, m_VoxGrabMax, m_VoxGrabPivot, rotation, translation,
                m_VoxGrabClone, out Vector3Int shift);
            if (m_VoxGrabChanged)
            {
                if (IsExternalVox)
                {
                    m_VoxGrabEmbedding = new EmbedVoxModelCommand(this, selection.Document);
                    m_VoxGrabEmbedding.Redo();
                }
                Vector3Int a = RuntimeVoxDocument.TransformRegionCell(m_VoxGrabMin,
                    m_VoxGrabPivot, rotation, translation) + shift;
                Vector3Int b = RuntimeVoxDocument.TransformRegionCell(m_VoxGrabMax,
                    m_VoxGrabPivot, rotation, translation) + shift;
                VoxSelection = new VoxRegionSelection(this, selection.Document, selection.Model,
                    Vector3Int.Min(a, b), Vector3Int.Max(a, b));
            }
            else VoxSelection = m_VoxGrabSelection;
            if (EditableVoxDocument != null) RefreshEditableVoxMeshes();
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
                var selection = m_VoxGrabSelection;
                if (m_VoxGrabChanged)
                {
                    var command = new MoveVoxRegionCommand(this, m_VoxGrabBefore,
                        selection.Document.CaptureSnapshot(), selection, VoxSelection, m_VoxGrabEmbedding);
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(m_VoxGrabEmbedding ?? (BaseCommand)command);
                }
                else m_VoxGrabEmbedding?.Undo();
                ClearVelocities();
                // Preserve the base class's pin/home/input cleanup without adding a
                // whole-widget move command to the region edit's undo step.
                base.OnUserEndInteracting();
            }
            finally
            {
                m_RecordMovements = m_VoxGrabRecordMovements;
                m_VoxGrabBefore = null;
                m_VoxGrabSelection = null;
                m_VoxGrabEmbedding = null;
            }
        }

        private void DrawVoxRegionSelection()
        {
            if (!HasVoxRegionSelection || !Showing) return;
            TrTransform grid = App.Scene.ActiveCanvas.AsCanvas[transform] *
                VoxMeshBuilder.GetModelTransform(VoxSelection.Model);
            VoxEditPreviewRenderer.DrawRegionBounds(grid, VoxSelection.CurrentMin,
                VoxSelection.CurrentMax, new Color(0.2f, 0.9f, 1f, 0.9f));
        }
    }
}
