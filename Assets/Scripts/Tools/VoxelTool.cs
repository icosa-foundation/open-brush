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

using System;
using System.Linq;
using TMPro;
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
        private Model m_ExternalPreviewSource;
        private ModelWidget m_PendingTargetWidget;
        private bool m_HasPendingTarget;
        private Switchboard m_TargetEvents;
        private bool m_CreateNewGrid;
        private bool m_GestureActive;
        private bool m_GestureChanged;
        private bool m_HasLastCell;
        private Vector3Int m_LastCell;
        private RuntimeVoxDocument.Snapshot m_GestureBefore;
        private CreateWidgetCommand m_GestureCreationCommand;
        private EmbedVoxModelCommand m_GestureEmbeddingCommand;
        private bool m_GestureAutoVisuals;
        private ModelWidget m_GestureWidget;
        private RuntimeVoxDocument.RuntimeModel m_GestureModel;
        private TextMeshPro m_TargetLabel;
        private EditMode m_Mode;
        private Texture2D m_AddIcon;
        private Texture2D m_EraseIcon;
        private Texture2D m_PaintIcon;

        public EditMode Mode
        {
            get => m_Mode;
            set
            {
                if (m_Mode == value) { return; }
                EndGesture();
                m_Mode = value;
                m_CreateNewGrid = false;
            }
        }
        public bool ShowGrid { get; set; } = true;
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
            m_AddIcon = Resources.Load<Texture2D>("Icons/guide_cube");
            m_EraseIcon = Resources.Load<Texture2D>("Icons/eraser");
            m_PaintIcon = Resources.Load<Texture2D>("Icons/repaint");
            m_TargetEvents = App.Switchboard;
            m_TargetEvents.ModelWidgetGrabbed += SetTargetWidget;
            m_TargetEvents.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        protected override void OnDestroy()
        {
            if (m_TargetEvents != null)
            {
                m_TargetEvents.ModelWidgetGrabbed -= SetTargetWidget;
                m_TargetEvents.SelectionChanged -= OnSelectionChanged;
            }
            base.OnDestroy();
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

        public override bool ShouldShowPointer() => true;

        public override void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            if (controller != InputManager.ControllerName.Brush) return;
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
            if (enable)
            {
                EnsureCurrentTarget();
                if (!HasCurrentTarget && !CreatingNewModel) ChooseActivationTarget();
            }
            if (!enable)
            {
                EndGesture();
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
            }
            base.HideTool(hide);
            if (m_TargetLabel != null)
            {
                m_TargetLabel.gameObject.SetActive(!hide);
            }
        }

        public override void UpdateTool()
        {
            base.UpdateTool();
            EnsureCurrentTarget();
            if (ToolHidden()) return;

            // Process mode changes before interpreting this frame's drawing input.
            if (!IsEatingInput &&
                InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleReshape))
            {
                CycleMode();
                InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);
            }

            Transform attachPoint = InputManager.Brush.Geometry.ToolAttachPoint;
            PointerManager.m_Instance.SetMainPointerPosition(attachPoint.position);
            Vector3 canvasPosition = App.Scene.ActiveCanvas.AsCanvas[attachPoint].translation;
            if (CreatingNewModel)
            {
                UpdateNewModelDrawing(attachPoint, canvasPosition);
                return;
            }
            var previewModel = m_GestureActive ? m_GestureModel : FindTargetModelAt(canvasPosition);
            bool insideTarget = m_GestureActive
                ? HasCurrentTarget && m_CurrentModel.ContainsAt(canvasPosition)
                : previewModel != null;
            UpdateTargetLabel(attachPoint, insideTarget);
            if (HasCurrentTarget)
            {
                var model = previewModel;
                if (model != null)
                {
                    Color guideColor = Mode switch
                    {
                        EditMode.Erase => new Color(0.9f, 0.12f, 0.08f),
                        EditMode.Paint => new Color(0.18f, 0.48f, 0.95f),
                        _ => new Color(0.12f, 0.8f, 0.28f),
                    };
                    Color targetColor = Mode == EditMode.Erase ? guideColor : App.BrushColor.CurrentColor;
                    var preview = new VoxModelApiWrapper(m_CurrentDocument._Document, model, m_CurrentDocument);
                    preview.PreviewForTool(canvasPosition, targetColor, guideColor, ShowGrid);
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
                InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
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

        public bool ToggleGrid()
        {
            ShowGrid = !ShowGrid;
            return ShowGrid;
        }

        public void RequestNewModel()
        {
            EndGesture();
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
                if (!model.IsInBounds(cell)) continue;
                if (model.TryGetPaletteIndex(cell, out _)) return model;
                if (emptyCandidate == null) emptyCandidate = model;
            }
            if (m_CurrentModel.ContainsAt(canvasPosition)) return m_CurrentModel._Model;
            return emptyCandidate ?? (Mode == EditMode.Add ? m_CurrentModel._Model : null);
        }

        private void BeginGesture(Vector3 canvasPosition)
        {
            EndGesture();
            RuntimeVoxDocument.RuntimeModel model = FindTargetModelAt(canvasPosition);
            if (model == null) return;
            m_CurrentModel = new VoxModelApiWrapper(m_CurrentDocument._Document, model, m_CurrentDocument);

            m_GestureBefore = m_CurrentDocument._Document.CaptureSnapshot();
            m_GestureWidget = CurrentTargetWidget;
            m_GestureModel = m_CurrentModel._Model;
            m_GestureAutoVisuals = m_CurrentDocument.autoVisuals;
            m_CurrentDocument.SuppressDirtyTracking = true;
            // Batch per-cell mutations; UpdateTool explicitly refreshes once per frame.
            m_CurrentDocument.SetAutoVisuals(false, true);
            m_GestureChanged = false;
            m_GestureActive = true;
            m_HasLastCell = false;
        }

        private void ApplyAt(Vector3 canvasPosition)
        {
            if (m_CurrentModel == null) return;
            Vector3Int cell = Vector3Int.RoundToInt(m_CurrentModel.CanvasToVoxel(canvasPosition));
            if (Mode == EditMode.Add)
            {
                var previousDefinition = m_CurrentModel._Model.Definition;
                if (!m_CurrentDocument._Document.TryGrowDrawingBounds(m_CurrentModel._Model, ref cell, out var shift))
                {
                    m_HasLastCell = false;
                    return;
                }
                if (m_HasLastCell) m_LastCell += shift;
                // Structural preview work alone must not embed an external model.
                if (!m_GestureWidget.IsExternalVox)
                    m_GestureChanged |= !ReferenceEquals(previousDefinition, m_CurrentModel._Model.Definition);
            }
            else if (!m_CurrentModel._Model.IsInBounds(cell))
            {
                m_HasLastCell = false;
                return;
            }
            if (m_HasLastCell && cell == m_LastCell) return;
            Vector3Int current = m_HasLastCell ? m_LastCell : cell;
            if (!m_HasLastCell) ApplyCell(current);
            Vector3Int delta = cell - current;
            Vector3Int step = new Vector3Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y), System.Math.Sign(delta.z));
            Vector3 interval = new Vector3(
                delta.x == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(delta.x),
                delta.y == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(delta.y),
                delta.z == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(delta.z));
            Vector3 next = interval * 0.5f;
            // Traverse every voxel crossed between successive cell centers.
            while (current != cell)
            {
                float crossing = Mathf.Min(next.x, Mathf.Min(next.y, next.z));
                if (next.x <= crossing) { current.x += step.x; next.x += interval.x; }
                if (next.y <= crossing) { current.y += step.y; next.y += interval.y; }
                if (next.z <= crossing) { current.z += step.z; next.z += interval.z; }
                ApplyCell(current);
            }
            m_LastCell = cell;
            m_HasLastCell = true;
        }

        private void ApplyCell(Vector3Int cell)
        {
            Vector3 canvasPosition = m_CurrentModel.VoxelToCanvas(cell);
            bool changed = Mode switch
            {
                EditMode.Erase => m_CurrentModel.EraseAt(canvasPosition),
                EditMode.Paint => m_CurrentModel.RecolorAt(canvasPosition, App.BrushColor.CurrentColor),
                _ => m_CurrentModel.PaintAt(canvasPosition, App.BrushColor.CurrentColor),
            };
            if (changed && m_GestureWidget.IsExternalVox)
            {
                var document = m_CurrentDocument._Document;
                m_GestureEmbeddingCommand = new EmbedVoxModelCommand(m_GestureWidget, document);
                m_GestureEmbeddingCommand.Redo();
                m_CurrentDocument = new VoxDocumentApiWrapper(document, m_GestureWidget)
                    { SuppressDirtyTracking = true };
                m_CurrentModel = new VoxModelApiWrapper(document, m_GestureModel, m_CurrentDocument);
                m_GestureAutoVisuals = true;
                m_ExternalPreviewSource = null;
            }
            m_GestureChanged |= changed;
        }

        internal void FinishEditing()
        {
            EndGesture();
        }

        private void EndGesture()
        {
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
            m_HasLastCell = false;
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

        private void UpdateNewModelDrawing(Transform attachPoint, Vector3 canvasPosition)
        {
            UpdateTargetLabel(attachPoint, false);
            float voxelSize = SelectionManager.m_Instance.ConfiguredSnappingGridSize;
            // Snap-grid points define voxel corners; centers sit halfway through each cell.
            // Floor also chooses the containing cell correctly at negative coordinates.
            Vector3 center = GetNewModelCellCenter(canvasPosition, voxelSize);
            Vector3 min = center - Vector3.one * 1.5f * voxelSize;
            VoxEditPreviewRenderer.DrawCreationBounds(min, min + Vector3.one * 3f * voxelSize, voxelSize);
            if (IsEatingInput || !InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate)) return;
            if (!CreateGrid(center, voxelSize)) return;
            m_CreateNewGrid = false;
            BeginGesture(center);
            ApplyAt(center);
            m_CurrentDocument.Refresh();
        }

        private static Vector3 GetNewModelCellCenter(Vector3 canvasPosition, float voxelSize)
            => ((Vector3)Vector3Int.FloorToInt(canvasPosition / voxelSize) + Vector3.one * 0.5f) * voxelSize;

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

        private void UpdateTargetLabel(
            Transform attachPoint, bool insideTarget, string actionHint = null)
        {
            if (m_TargetLabel == null)
            {
                var labelObject = new GameObject("VoxelTargetStatus");
                labelObject.layer = attachPoint.gameObject.layer;
                labelObject.transform.SetParent(transform, false);
                m_TargetLabel = labelObject.AddComponent<TextMeshPro>();
                m_TargetLabel.alignment = TextAlignmentOptions.Center;
                m_TargetLabel.enableAutoSizing = true;
                m_TargetLabel.fontSizeMin = 1;
                m_TargetLabel.fontSizeMax = 2;
                m_TargetLabel.richText = false;
                m_TargetLabel.rectTransform.sizeDelta = new Vector2(8, 1.5f);
            }

            Transform head = ViewpointScript.Head;
            m_TargetLabel.transform.SetPositionAndRotation(
                attachPoint.position + head.up * 0.06f, head.rotation);
            m_TargetLabel.transform.localScale = Vector3.one * 0.05f;
            m_TargetLabel.color = CreatingNewModel ? Color.yellow : Color.white;
            string action = actionHint ?? (CreatingNewModel
                ? "Press trigger to start drawing a new model"
                : !HasCurrentTarget
                    ? CurrentTargetWidget != null && CurrentTargetWidget.IsExternalVox
                        ? "VOX source is not loaded yet"
                        : "Grab or select a voxel model; New Model is in settings"
                    : insideTarget
                        ? $"Trigger: {Mode} — grid follows the voxels"
                        : "Outside active model — grab or select another to change target");
            m_TargetLabel.text = $"{CurrentTargetDescription}\n{action}";
        }

    }
}
