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
                if (ReferenceEquals(m_VoxSelection, value)) return;
                var previous = m_VoxSelection;
                bool sameGeometry = previous != null && value != null &&
                    ReferenceEquals(previous.Floating.SourceGeometry, value.Floating.SourceGeometry);
                if (previous != null && !sameGeometry)
                {
                    if (previous.IsValid(this)) SynchronizeVoxRegionDocument(previous.Document);
                    m_VoxDragPreview?.Dispose();
                    m_VoxDragPreview = null;
                    m_VoxSelection = null;
                    if (m_VoxSourceMeshDirty && EditableVoxDocument == previous.Document)
                        RefreshEditableVoxMeshes();
                    m_VoxSourceMeshDirty = false;
                }
                if (value != null && previous == null && WidgetManager.m_Instance != null)
                    foreach (var widget in WidgetManager.m_Instance.ModelWidgets)
                        if (widget != null && widget != this && widget.VoxSelection != null) widget.VoxSelection = null;
                m_VoxSelection = value;
                if (value == null)
                {
                    m_VoxDocumentDirty = false;
                    return;
                }
                m_VoxDocumentDirty = m_VoxSourceMeshDirty = true;
                if (!value.IsValid(this)) return;
                EnsureVoxRegionPreview();
                m_VoxDragPreview.SetState(value.Floating);
                UpdateVoxRegionCollider();
            }
        }

        internal bool HasVoxRegionSelection => VoxSelection != null && VoxSelection.IsValid(this);
        internal bool VoxRegionGrabActive => m_VoxGrabActive;
        private bool m_VoxGrabActive, m_VoxDocumentDirty, m_VoxSourceMeshDirty;
        private VoxRegionSelection m_VoxGrabSelection, m_VoxGrabUndoSelection;
        private EmbedVoxModelCommand m_VoxGrabEmbedding;
        private TrTransform m_VoxGrabWidget, m_VoxGrabGrid, m_VoxVirtualGrab;
        private Vector3Int m_VoxGrabPivot, m_VoxGrabAnchorPivot, m_VoxGrabTranslation, m_VoxUndoTranslation;
        private Quaternion m_VoxGrabRotation, m_VoxUndoRotation;
        private VoxRegionDragPreview m_VoxDragPreview;
        private Bounds? m_VoxOtherModelBounds;
        private bool m_VoxGrabChanged, m_VoxGrabRecordMovements, m_VoxGrabStampArmed;

        internal static void ClearVoxRegionSelections()
        {
            foreach (var widget in WidgetManager.m_Instance.ModelWidgets)
                if (widget != null) widget.CommitVoxRegionSelection();
        }

        internal void SelectVoxRegion(RuntimeVoxDocument document, RuntimeVoxDocument.RuntimeModel model,
            Vector3Int min, Vector3Int max)
        {
            Vector3Int previousOrigin = model.SourceCellOffset;
            ClearVoxRegionSelections();
            Vector3Int rebase = model.SourceCellOffset - previousOrigin;
            min += rebase;
            max += rebase;
            var selection = new VoxRegionSelection(this, document, model, min, max);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new SetVoxRegionSelectionCommand(this, selection));
            // Merely selecting did not change the document or its native mesh.
            m_VoxDocumentDirty = m_VoxSourceMeshDirty = false;
        }

        internal void CommitVoxRegionSelection()
        {
            if (VoxSelection == null) return;
            if (VoxRegionGrabActive) OnUserEndInteracting();
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new SetVoxRegionSelectionCommand(this, null));
        }

        private void EnsureVoxRegionPreview()
        {
            if (m_VoxDragPreview != null && m_VoxDragPreview.Matches(VoxSelection.Floating)) return;
            m_VoxDragPreview?.Dispose();
            int modelIndex = 0;
            while (!ReferenceEquals(VoxSelection.Document.Models[modelIndex], VoxSelection.Model)) ++modelIndex;
            MeshFilter selected = GetOrCreateVoxModelFilter(m_ModelInstance, modelIndex, VoxSelection.Model, ghost: false);
            m_VoxOtherModelBounds = null;
            foreach (Transform child in m_ModelInstance)
            {
                if (child == selected.transform || !child.gameObject.activeSelf) continue;
                var filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                var pose = TrTransform.FromLocalTransform(child);
                var transformed = new Bounds(pose * bounds.center, Vector3.zero);
                for (int x = 0; x < 2; ++x)
                    for (int y = 0; y < 2; ++y)
                        for (int z = 0; z < 2; ++z)
                            transformed.Encapsulate(pose * new Vector3(x == 0 ? bounds.min.x : bounds.max.x,
                                y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z));
                if (m_VoxOtherModelBounds.HasValue)
                {
                    var combined = m_VoxOtherModelBounds.Value;
                    combined.Encapsulate(transformed);
                    m_VoxOtherModelBounds = combined;
                }
                else m_VoxOtherModelBounds = transformed;
            }
            m_VoxDragPreview = new VoxRegionDragPreview(VoxSelection.Document, VoxSelection.Floating,
                selected.GetComponent<MeshRenderer>(), ModelCatalog.m_Instance.m_VoxLoaderStandardMaterial,
                m_BatchId, m_Keywords);
        }

        private void UpdateVoxRegionCollider()
        {
            m_MeshBounds = m_VoxDragPreview.GetBounds(VoxSelection.Floating);
            if (m_VoxOtherModelBounds.HasValue) m_MeshBounds.Encapsulate(m_VoxOtherModelBounds.Value);
            m_BoxCollider.transform.localPosition = m_MeshBounds.center;
            m_BoxCollider.size = m_MeshBounds.size + m_ContainerBloat;
            if (SelectionManager.m_Instance != null && SelectionManager.m_Instance.IsWidgetSelected(this))
                SelectionManager.m_Instance.UpdateSelectionWidget();
        }

        private void BeginVoxRegionGrab()
        {
            if (!HasVoxRegionSelection || MultiplayerManager.m_Instance.IsViewOnly) return;
            EnsureVoxRegionPreview();
            m_VoxGrabSelection = m_VoxGrabUndoSelection = VoxSelection;
            var floating = VoxSelection.Floating;
            m_VoxGrabAnchorPivot = Vector3Int.RoundToInt(((Vector3)floating.Min + floating.Max) * 0.5f);
            m_VoxGrabPivot = m_VoxGrabAnchorPivot + floating.Model.SourceCellOffset - floating.IndexOrigin;
            m_VoxGrabWidget = m_VoxVirtualGrab = TrTransform.FromTransform(transform);
            m_VoxGrabGrid = m_VoxGrabWidget * VoxMeshBuilder.GetModelTransform(VoxSelection.Model);
            m_VoxGrabRotation = m_VoxUndoRotation = Quaternion.identity;
            m_VoxGrabTranslation = m_VoxUndoTranslation = Vector3Int.zero;
            m_VoxGrabChanged = m_VoxGrabStampArmed = false;
            m_VoxGrabRecordMovements = m_RecordMovements;
            m_RecordMovements = false;
            m_VoxGrabActive = true;
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
                if (VoxSelection.Floating.CanTransformFrom(m_VoxGrabSelection.Floating,
                        m_VoxGrabAnchorPivot, rotation, translation))
                {
                    m_VoxGrabTranslation = translation;
                    m_VoxGrabRotation = rotation;
                    m_VoxDocumentDirty = m_VoxSourceMeshDirty = true;
                    EnsureVoxRegionEmbedded();
                    m_VoxGrabChanged = translation != m_VoxUndoTranslation || Quaternion.Angle(rotation, m_VoxUndoRotation) >= 0.01f;
                    m_VoxDragPreview.SetPose(m_VoxGrabAnchorPivot, rotation, translation, m_VoxGrabSelection.Floating.Pose);
                }
            }
            if (m_VoxGrabStampArmed && InputManager.Controllers[(int)m_InteractingController]
                    .GetCommandDown(InputManager.SketchCommands.DuplicateSelection))
                StampVoxRegion();
            m_VoxGrabStampArmed = true;
        }

        private void PublishVoxRegionPose()
        {
            if (!VoxRegionGrabActive) return;
            var moved = VoxSelection.Floating.TransformFrom(m_VoxGrabSelection.Floating,
                m_VoxGrabAnchorPivot, m_VoxGrabRotation, m_VoxGrabTranslation);
            if (!moved.SamePose(VoxSelection.Floating)) VoxSelection = VoxSelection.WithFloating(moved);
        }

        // Save/clone and finalization bake the visible image. A grab, stamp,
        // release or undo only changes the immutable floating state and preview.
        internal void SynchronizeVoxRegionDocument(RuntimeVoxDocument document)
        {
            if (!m_VoxDocumentDirty || !HasVoxRegionSelection || VoxSelection.Document != document) return;
            PublishVoxRegionPose();
            if (!VoxSelection.Floating.TryCompose(document))
                throw new System.InvalidOperationException("The floating voxel selection exceeds VOX model limits.");
            m_VoxDocumentDirty = false;
        }

        internal void PrepareVoxRegionMeshExport()
        {
            if (!HasVoxRegionSelection) return;
            SynchronizeVoxRegionDocument(VoxSelection.Document);
            if (m_VoxSourceMeshDirty && EditableVoxDocument == VoxSelection.Document)
            {
                RefreshEditableVoxMeshes();
                m_VoxSourceMeshDirty = false;
            }
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
            PublishVoxRegionPose();
            var command = new MoveVoxRegionCommand(this, m_VoxGrabUndoSelection, VoxSelection, m_VoxGrabEmbedding);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(m_VoxGrabEmbedding ?? (BaseCommand)command);
            ResetVoxRegionUndoBoundary();
            return true;
        }

        private void ResetVoxRegionUndoBoundary()
        {
            m_VoxGrabUndoSelection = VoxSelection;
            m_VoxUndoRotation = m_VoxGrabRotation;
            m_VoxUndoTranslation = m_VoxGrabTranslation;
            m_VoxGrabEmbedding = null;
            m_VoxGrabChanged = false;
        }

        private void StampVoxRegion()
        {
            RecordPendingVoxRegionMove();
            PublishVoxRegionPose();
            var stamped = VoxSelection.Floating.Stamp();
            if (ReferenceEquals(stamped, VoxSelection.Floating)) return;
            EnsureVoxRegionEmbedded();
            var command = new MoveVoxRegionCommand(this, VoxSelection, VoxSelection.WithFloating(stamped), m_VoxGrabEmbedding);
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
                    VoxSelection = m_VoxGrabUndoSelection;
                    m_VoxDragPreview?.SetState(VoxSelection.Floating);
                    m_VoxGrabEmbedding?.Undo();
                }
                ClearVelocities();
                base.OnUserEndInteracting();
            }
            finally
            {
                m_RecordMovements = m_VoxGrabRecordMovements;
                m_VoxGrabActive = false;
                m_VoxGrabSelection = m_VoxGrabUndoSelection = null;
                m_VoxGrabEmbedding = null;
            }
        }

        private void DrawVoxRegionSelection()
        {
            if (!HasVoxRegionSelection || !Showing) return;
            EnsureVoxRegionPreview();
            m_VoxDragPreview.Draw(TrTransform.FromTransform(transform));
            var basis = VoxRegionGrabActive ? m_VoxGrabSelection.Floating : VoxSelection.Floating;
            Vector3Int a = basis.Min;
            Vector3Int b = basis.Max;
            if (VoxRegionGrabActive)
            {
                a = RuntimeVoxDocument.TransformRegionCell(a, m_VoxGrabAnchorPivot, m_VoxGrabRotation, m_VoxGrabTranslation);
                b = RuntimeVoxDocument.TransformRegionCell(b, m_VoxGrabAnchorPivot, m_VoxGrabRotation, m_VoxGrabTranslation);
            }
            TrTransform anchorGrid = App.Scene.ActiveCanvas.AsCanvas[transform] *
                VoxMeshBuilder.GetModelTransform(basis.AnchorOffset, basis.Model.GlobalRotation);
            VoxEditPreviewRenderer.DrawRegionBounds(anchorGrid, Vector3Int.Min(a, b), Vector3Int.Max(a, b), new Color(.2f, .9f, 1f, .9f));
        }
    }
}
