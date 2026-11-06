// Copyright 2026 The Open Brush Authors
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

using OpenBrush.Multiplayer;
using System;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Native interaction tool for editing widget-backed VOX documents.
    public sealed partial class VoxelTool : BaseTool
    {
        public enum EditMode
        {
            Add,
            Erase,
            Paint,
        }

        private VoxDocumentApiWrapper m_CurrentDocument;
        private VoxModelApiWrapper m_CurrentModel;
        private ModelWidget m_TargetWidget;
        private ModelWidget m_LastInteractedVoxWidget;
        private const float kDefaultVoxelSize = 0.1f;
        private float m_LastVoxelSize = kDefaultVoxelSize;
        private Model m_ExternalPreviewSource;
        private ModelWidget m_PendingTargetWidget;
        private bool m_HasPendingTarget;
        private Switchboard m_TargetEvents;
        private bool m_CreateNewGrid;
        private bool m_GestureActive;
        private bool m_GestureChanged;
        private bool m_ReportedPaletteFull;
        private RuntimeVoxDocument.Snapshot m_GestureBefore;
        private CreateWidgetCommand m_GestureCreationCommand;
        private EmbedVoxModelCommand m_GestureEmbeddingCommand;
        private bool m_GestureAutoVisuals;
        private ModelWidget m_GestureWidget;
        private RuntimeVoxDocument.RuntimeModel m_GestureModel;
        private EditMode m_Mode;
        [SerializeField] private Texture2D m_AddIcon;
        [SerializeField] private Texture2D m_EraseIcon;
        [SerializeField] private Texture2D m_PaintIcon;

        public EditMode Mode
        {
            get => m_Mode;
            set
            {
                if (m_Mode == value && !SelectingRegion) { return; }
                EndGesture();
                SelectingRegion = false;
                CancelRegionDrag();
                m_Mode = value;
                m_CreateNewGrid = false;
            }
        }
        public ModelWidget CurrentTargetWidget => IsAvailableTarget(m_TargetWidget) ? m_TargetWidget : null;
        public bool HasCurrentTarget => CurrentTargetWidget != null &&
            m_CurrentModel != null && m_CurrentModel.isValid && m_CurrentModel._Model.IsVisible &&
            (ReferenceEquals(CurrentTargetWidget.EditableVoxDocument, m_CurrentDocument._Document) ||
                (CurrentTargetWidget.IsExternalVox && m_ExternalPreviewSource == CurrentTargetWidget.Model &&
                    m_CurrentDocument._Document.ViewState.Widget == null));
        public bool CreatingNewModel => m_CreateNewGrid;
        public string CurrentTargetDescription
        {
            get
            {
                if (CreatingNewModel) return "New model — press trigger to start drawing";
                ModelWidget widget = CurrentTargetWidget;
                if (widget == null) return "No active voxel model — grab or select one";
                string name = widget.GetExportName();
                if (widget.IsExternalVox) return $"{name} — editing embeds a copy in this sketch";
                if (!HasCurrentTarget) return $"No visible voxel grid in {name}";
                if (widget.EditableVoxDocument.Models.Count == 1 &&
                    !string.IsNullOrEmpty(widget.EditableVoxDocument.Models[0].Name))
                    name = widget.EditableVoxDocument.Models[0].Name;
                return $"Active: {name}";
            }
        }

        public override void Init()
        {
            base.Init();
            m_TargetEvents = App.Switchboard;
            m_TargetEvents.ModelWidgetGrabbed += SetTargetWidget;
            m_TargetEvents.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        protected override void OnDestroy()
        {
            ClearVoxelStraightEdge();
            m_SymmetryPreview.Dispose();
            if (m_TargetEvents != null)
            {
                m_TargetEvents.ModelWidgetGrabbed -= SetTargetWidget;
                m_TargetEvents.SelectionChanged -= OnSelectionChanged;
            }
            base.OnDestroy();
        }

        private void OnDisable()
        {
            ClearVoxelStraightEdge();
            m_SymmetryPreview.Dispose();
        }

        private void OnSelectionChanged()
        {
            var selection = SelectionManager.m_Instance;
            if (selection == null || selection.SelectedStrokes.Any()) return;
            GrabWidget[] widgets = selection.SelectedWidgets.Take(2).ToArray();
            if (widgets.Length == 1 && widgets[0] is ModelWidget widget)
                SetTargetWidget(widget);
        }

        private static bool IsAvailableTarget(ModelWidget widget)
            => widget != null && widget.Showing && widget.gameObject.activeInHierarchy &&
                string.IsNullOrEmpty(widget.Subtree) &&
                (widget.IsExternalVox || widget.EditableVoxDocument != null);

        public override void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            if (controller != InputManager.ControllerName.Brush) return;
            if (SelectingRegion)
            {
                InputManager.Brush.Geometry.ShowSelectionToggle();
                if (CurrentTargetWidget?.HasVoxRegionSelection == true)
                    InputManager.Brush.Geometry.ShowDuplicateOption();
                return;
            }
            if (m_StraightEdgeGesture)
            {
                InputManager.Brush.Geometry.TogglePadSnapHint(PointerManager.m_Instance.StraightEdgeGuide.SnapEnabled, enabled: true);
                return;
            }
            Texture2D icon = Mode switch
            {
                EditMode.Erase => m_EraseIcon,
                EditMode.Paint => m_PaintIcon,
                _ => m_AddIcon,
            };
            InputManager.Brush.Geometry.ShowModeIcon(true, icon);
        }

        public override void EnableTool(bool enable)
        {
            // Tool-change notifications can read status before the first UpdateTool call.
            if (enable && !SelectingRegion && !MultiplayerManager.m_Instance.IsViewOnly)
            {
                EnsureCurrentTarget();
                if (!HasCurrentTarget && !CreatingNewModel) ChooseActivationTarget();
            }
            if (!enable)
            {
                EndGesture();
                CancelRegionDrag();
                // Other tools operate on committed voxel geometry. Temporary hiding
                // (including popups) keeps the selection floating through HideTool.
                ModelWidget.ClearVoxRegionSelections();
            }
            base.EnableTool(enable);
            if (enable)
            {
                EatInput();
            }
        }

        public override void HideTool(bool hide)
        {
            if (hide)
            {
                EndGesture();
                CancelRegionDrag();
            }
            base.HideTool(hide);
        }

        public override void UpdateTool()
        {
            base.UpdateTool();
            if (MultiplayerManager.m_Instance.IsViewOnly)
            {
                EndGesture();
                CancelRegionDrag();
                EatInput();
                return;
            }
            EnsureCurrentTarget();
            if (ToolHidden()) return;

            if (SelectingRegion)
            {
                Vector3 selectionPosition = App.Scene.ActiveCanvas.AsCanvas[
                    InputManager.Brush.Geometry.ToolAttachPoint].translation;
                UpdateRegionSelection(selectionPosition);
                return;
            }

            // Process mode changes before interpreting this frame's drawing input.
            if (!IsEatingInput && !m_StraightEdgeGesture &&
                !(PointerManager.m_Instance.StraightEdgeModeEnabled &&
                    InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate)) &&
                InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleReshape))
            {
                CycleMode();
                InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);
            }

            Transform attachPoint = InputManager.Brush.Geometry.ToolAttachPoint;
            Vector3 canvasPosition = App.Scene.ActiveCanvas.AsCanvas[attachPoint].translation;
            if (CreatingNewModel)
            {
                UpdateNewModelDrawing(canvasPosition);
                return;
            }
            if (m_GestureActive && m_StraightEdgeGesture)
                canvasPosition = UpdateVoxelStraightEdge(canvasPosition);
            var previewModel = m_GestureActive ? m_GestureModel : FindTargetModelAt(canvasPosition);
            if (HasCurrentTarget)
            {
                var model = previewModel;
                if (model != null)
                {
                    DrawSymmetryPreview(model, m_CurrentDocument.GetModelTransform(model), canvasPosition);
                }
            }

            if (IsEatingInput)
            {
                return;
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                BeginGesture(canvasPosition);
            }

            if (m_GestureActive &&
                InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate) && !m_StraightEdgeGesture)
            {
                ApplyAt(canvasPosition);
                // Interpolation can change many cells in one frame. Show the actual edits
                // once after that batch; Refresh skips a clean document. Never revive a
                // widget hidden by final-voxel erasure or another action during the gesture.
                if (m_GestureWidget != null && m_GestureWidget.Showing &&
                    m_GestureWidget.gameObject.activeInHierarchy &&
                    ReferenceEquals(m_GestureWidget.EditableVoxDocument, m_CurrentDocument._Document))
                {
                    m_CurrentDocument.Refresh();
                }
            }

            if (m_GestureActive &&
                !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                if (m_StraightEdgeGesture) ApplyAt(canvasPosition, straightEdge: true);
                EndGesture();
            }
        }

        public EditMode CycleMode()
        {
            Mode = Mode switch
            {
                EditMode.Add => EditMode.Erase,
                EditMode.Erase => EditMode.Paint,
                _ => EditMode.Add,
            };
            return Mode;
        }

        public void RequestNewModel()
        {
            EndGesture();
            SelectingRegion = false;
            CancelRegionDrag();
            RememberVoxelSize(m_LastInteractedVoxWidget);
            m_TargetWidget = null;
            m_PendingTargetWidget = null;
            m_HasPendingTarget = false;
            m_CurrentDocument = null;
            m_CurrentModel = null;
            Mode = EditMode.Add;
            m_CreateNewGrid = true;
        }

        internal void SetTargetWidget(ModelWidget widget)
        {
            if (!IsAvailableTarget(widget)) return;
            RememberVoxelSize(widget);
            if (m_GestureActive)
            {
                // A grab/selection from the other hand cannot redirect an active stroke.
                m_PendingTargetWidget = widget;
                m_HasPendingTarget = true;
                return;
            }
            m_CreateNewGrid = false;
            BindTargetWidget(widget);
        }

        private void BindTargetWidget(ModelWidget widget)
        {
            if (!IsAvailableTarget(widget)) widget = null;
            RememberVoxelSize(widget);
            if (m_TargetWidget == widget && HasCurrentTarget) return;
            m_TargetWidget = widget;
            m_CurrentDocument = null;
            m_CurrentModel = null;
            m_ExternalPreviewSource = null;
            if (widget == null) return;
            var document = widget.EditableVoxDocument ?? widget.Model.CreateEditableVoxDocument();
            if (document == null) return;
            var model = document.Models.FirstOrDefault(item => item.IsVisible && item.Voxels.Count > 0)
                ?? document.Models.FirstOrDefault(item => item.IsVisible);
            if (model == null) return;
            if (widget.IsExternalVox)
            {
                // Preview a private document without adopting it, creating a widget or dirtying the sketch.
                m_ExternalPreviewSource = widget.Model;
                document.ViewState.WidgetTransform = App.Scene.ActiveCanvas.AsCanvas[widget.transform];
                m_CurrentDocument = new VoxDocumentApiWrapper(document);
                m_CurrentDocument.SetAutoVisuals(false);
            }
            else m_CurrentDocument = new VoxDocumentApiWrapper(document, widget);
            m_CurrentModel = new VoxModelApiWrapper(document, model, m_CurrentDocument);
        }

        private RuntimeVoxDocument.RuntimeModel FindTargetModelAt(Vector3 canvasPosition)
        {
            if (!HasCurrentTarget) return null;
            var document = m_CurrentDocument._Document;
            RuntimeVoxDocument.RuntimeModel emptyCandidate = null;
            for (int i = document.Models.Count - 1; i >= 0; --i)
            {
                var model = document.Models[i];
                if (!model.IsVisible) continue;
                Vector3Int cell = Vector3Int.RoundToInt(
                    m_CurrentDocument.GetModelTransform(model).inverse * canvasPosition);
                if (!BrushFootprint.IntersectsVolume(cell, model.Size)) continue;
                if (model.TryGetPaletteIndex(cell, out _)) return model;
                if (emptyCandidate == null) emptyCandidate = model;
            }
            Vector3Int currentCell = Vector3Int.RoundToInt(m_CurrentModel.CanvasToVoxel(canvasPosition));
            if (BrushFootprint.IntersectsVolume(currentCell, m_CurrentModel._Model.Size)) return m_CurrentModel._Model;
            var symmetry = PointerManager.m_Instance.CurrentSymmetryMode;
            bool mirrored = symmetry == PointerManager.SymmetryMode.SinglePlane || symmetry == PointerManager.SymmetryMode.MultiMirror;
            return emptyCandidate ?? (Mode == EditMode.Add || mirrored ? m_CurrentModel._Model : null);
        }

        private void BeginGesture(Vector3 canvasPosition)
        {
            EndGesture();
            CurrentTargetWidget?.CommitVoxRegionSelection();
            RuntimeVoxDocument.RuntimeModel model = FindTargetModelAt(canvasPosition);
            if (model == null) return;
            m_CurrentModel = new VoxModelApiWrapper(m_CurrentDocument._Document, model, m_CurrentDocument);

            // TODO(VOX undo): For large/dense documents, replace these before/after full-cell
            // snapshots with changed-cell deltas plus bounds/origin metadata. Retained undo
            // memory currently scales with the whole document, even for a one-cell gesture.
            m_GestureBefore = m_CurrentDocument._Document.CaptureSnapshot();
            m_GestureWidget = CurrentTargetWidget;
            m_GestureModel = m_CurrentModel._Model;
            m_GestureAutoVisuals = m_CurrentDocument.autoVisuals;
            m_CurrentDocument.SuppressDirtyTracking = true;
            // Batch per-cell mutations; UpdateTool explicitly refreshes once per frame.
            m_CurrentDocument.SetAutoVisuals(false, true);
            m_GestureChanged = false;
            m_ReportedPaletteFull = false;
            m_GestureActive = true;
            ResetSymmetryPaths();
            if (PointerManager.m_Instance.StraightEdgeModeEnabled) BeginVoxelStraightEdge(canvasPosition);
        }

        private void ReportPaletteFull()
        {
            if (m_ReportedPaletteFull) return;
            m_ReportedPaletteFull = true;
            OutputWindowScript.m_Instance?.CreateInfoCardAtController(
                InputManager.ControllerName.Brush,
                "Voxel palette full: no slot is available for this colour and material.",
                alsoConsole: false);
        }

        internal void FinishEditing()
        {
            EndGesture();
        }

        private void EndGesture()
        {
            ClearVoxelStraightEdge();
            if (!m_GestureActive)
            {
                return;
            }

            var gestureDocument = m_CurrentDocument;
            bool targetShowing = m_GestureWidget != null && m_GestureWidget.Showing &&
                m_GestureWidget.gameObject.activeInHierarchy;
            // Keep a fully erased model as an editable 3x3x3 grid.
            if (m_GestureChanged && m_GestureWidget != null)
            {
                if (Mode != EditMode.Paint) gestureDocument._Document.FitDrawingBounds(m_GestureModel);
                BaseCommand parent = m_GestureEmbeddingCommand ?? (BaseCommand)m_GestureCreationCommand;
                var command = new ModifyVoxDocumentCommand(m_GestureWidget, m_GestureBefore,
                    gestureDocument._Document.CaptureSnapshot(), parent);
                if (m_GestureEmbeddingCommand != null)
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(m_GestureEmbeddingCommand);
                else if (m_GestureCreationCommand == null)
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                else
                {
                    // Creation is already on the undo stack; its first stroke is a child.
                    if (targetShowing) gestureDocument.Refresh();
                    m_GestureCreationCommand.SetWidgetCost(m_GestureWidget.GetTiltMeterCost());
                    SaveLoadScript.m_Instance?.SketchChanged();
                }
            }

            // An attempted Add may have grown detached preview bounds without changing a cell.
            if (!m_GestureChanged && m_GestureWidget != null && m_GestureWidget.IsExternalVox)
                gestureDocument._Document.RestoreSnapshot(m_GestureBefore);
            gestureDocument.SuppressDirtyTracking = false;
            gestureDocument.SetAutoVisuals(m_GestureAutoVisuals && targetShowing, true);
            m_GestureActive = false;
            m_GestureChanged = false;
            m_ReportedPaletteFull = false;
            ResetSymmetryPaths();
            m_GestureBefore = null;
            m_GestureCreationCommand = null;
            m_GestureEmbeddingCommand = null;
            m_GestureWidget = null;
            m_GestureModel = null;
            ApplyPendingTarget();
        }

        private void ApplyPendingTarget()
        {
            if (m_HasPendingTarget)
            {
                ModelWidget pending = m_PendingTargetWidget;
                m_PendingTargetWidget = null;
                m_HasPendingTarget = false;
                BindTargetWidget(pending);
            }
        }

        private void UpdateNewModelDrawing(Vector3 canvasPosition)
        {
            float voxelSize = GetVoxelGridSize(useLastModelWhenSnappingDisabled: true);
            // Snap-grid points define voxel corners; centers sit halfway through each cell.
            // Floor also chooses the containing cell correctly at negative coordinates.
            Vector3 center = GetNewModelCellCenter(canvasPosition, voxelSize);
            Vector3 origin = center + new Vector3(-1f, -1f, 1f) * voxelSize;
            var grid = TrTransform.TRS(origin, Quaternion.identity, voxelSize) * VoxMeshBuilder.GetModelTransform(m_CreationPreviewModel);
            DrawSymmetryPreview(m_CreationPreviewModel, grid, center);
            if (IsEatingInput || !InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate)) return;
            if (!CreateGrid(center, voxelSize)) return;
            m_CreateNewGrid = false;
            BeginGesture(center);
            if (!m_StraightEdgeGesture)
            {
                ApplyAt(center);
                m_CurrentDocument.Refresh();
            }
        }

        private static Vector3 GetNewModelCellCenter(Vector3 canvasPosition, float voxelSize)
            => ((Vector3)Vector3Int.FloorToInt(canvasPosition / voxelSize) + Vector3.one * 0.5f) * voxelSize;

        private void RememberVoxelSize(ModelWidget widget)
        {
            if (widget == null) return;
            m_LastInteractedVoxWidget = widget;
            float size = widget.GetVoxelGridSize_CS();
            if (size > 0) m_LastVoxelSize = size;
        }

        // Creation can inherit the last model's size; explicit grid alignment uses
        // the default grid when snapping is disabled, never the target's own size.
        private float GetVoxelGridSize(bool useLastModelWhenSnappingDisabled)
        {
            var selection = SelectionManager.m_Instance;
            if (selection.SnappingGridSize > 0) return selection.SnappingGridSize;
            if (!useLastModelWhenSnappingDisabled) return kDefaultVoxelSize;
            // Read at use time so resizing the last target is reflected, even
            // if the voxel tool was inactive during the grab. Retain the value if deleted.
            RememberVoxelSize(m_LastInteractedVoxWidget);
            return m_LastVoxelSize;
        }

        private bool CreateGrid(Vector3 center, float voxelSize)
        {
            var document = new RuntimeVoxDocument();
            int modelNumber = 1;
            while (WidgetManager.m_Instance.ModelWidgets.Any(widget =>
                widget.EditableVoxDocument?.Models.Any(model =>
                    model.Name == $"Voxel Model {modelNumber}") == true))
            {
                ++modelNumber;
            }
            // VOX is Z-up: local Y points towards negative canvas Z, local Z towards canvas Y.
            document.CreateModel($"Voxel Model {modelNumber}", Vector3Int.one * 3);
            Model model = Model.CreateEditableVoxModelFromBytes(document.ToVoxBytes());
            if (model == null) return false;

            try
            {
                Vector3 origin = center + new Vector3(-1f, -1f, 1f) * voxelSize;
                TrTransform canvasPose = TrTransform.TRS(origin, Quaternion.identity, voxelSize);
                var command = new CreateWidgetCommand(WidgetManager.m_Instance.ModelWidgetPrefab,
                    App.Scene.ActiveCanvas.Pose * canvasPose, forceTransform: true);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                var widget = command.Widget as ModelWidget;
                widget.LoadingFromSketch = true;
                widget.Model = model;
                widget.AdoptEditableVoxDocument(document);
                widget.Show(true, false);
                m_TargetWidget = widget;
                m_CurrentDocument = new VoxDocumentApiWrapper(document, widget);
                m_CurrentDocument.SuppressDirtyTracking = true;
                try
                {
                    m_CurrentDocument.SetTransform(canvasPose);
                }
                finally
                {
                    m_CurrentDocument.SuppressDirtyTracking = false;
                }
                m_CurrentDocument.SetAutoVisuals(true, true);
                m_CurrentModel = m_CurrentDocument.Model();
                RememberVoxelSize(widget);
                command.SetWidgetCost(widget.GetTiltMeterCost());
                m_GestureCreationCommand = command;
                return true;
            }
            finally
            {
                model.ReleaseFromCatalog();
            }
        }

        private void EnsureCurrentTarget()
        {
            if (HasCurrentTarget)
            {
                if (CurrentTargetWidget.IsExternalVox)
                    m_CurrentDocument._Document.ViewState.WidgetTransform =
                        App.Scene.ActiveCanvas.AsCanvas[CurrentTargetWidget.transform];
                return;
            }
            EndGesture();
            ModelWidget widget = CurrentTargetWidget;
            // Rebind after undo/redo changes between embedded content and an external reference.
            BindTargetWidget(widget);
        }

    }
}
