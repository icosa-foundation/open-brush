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
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    /// Native interaction tool for editing widget-backed VOX documents.
    public sealed class VoxelTool : BaseTool
    {
        public enum EditMode
        {
            Add,
            Erase,
            Paint,
        }

        private const float kDefaultVoxelSize = 0.1f;
        private static readonly float[] kVoxelSizes = { 0.025f, 0.05f, 0.1f, 0.2f, 0.4f };

        private VoxDocumentApiWrapper m_CurrentDocument;
        private VoxModelApiWrapper m_CurrentModel;
        private ModelWidget m_TargetWidget;
        private ModelWidget m_PendingTargetWidget;
        private bool m_HasPendingTarget;
        private Switchboard m_TargetEvents;
        private bool m_CreateNewGrid;
        private bool m_GestureActive;
        private bool m_GestureChanged;
        private bool m_HasLastCell;
        private Vector3Int m_LastCell;
        private Dictionary<Vector3Int, byte> m_GestureBeforeCells;
        private Dictionary<Vector3Int, byte> m_GestureAfterCells;
        private Color32[] m_GestureBeforePalette;
        private bool m_GestureAutoVisuals;
        private ModelWidget m_GestureWidget;
        private RuntimeVoxDocument.RuntimeModel m_GestureModel;
        private bool m_DefiningGrid;
        private Vector3 m_GridStart;
        private float m_GridVoxelSize;
        private CanvasScript m_GridCanvas;
        private TextMeshPro m_TargetLabel;
        private EditMode m_Mode;

        public EditMode Mode
        {
            get => m_Mode;
            set
            {
                if (m_Mode == value) { return; }
                EndGesture();
                m_Mode = value;
                m_CreateNewGrid = false;
                m_DefiningGrid = false;
            }
        }
        public float VoxelSize { get; set; } = kDefaultVoxelSize;
        public bool ShowGrid { get; set; } = true;
        public ModelWidget CurrentTargetWidget => IsAvailableTarget(m_TargetWidget) ? m_TargetWidget : null;
        public bool HasCurrentTarget => CurrentTargetWidget != null &&
            m_CurrentModel != null && m_CurrentModel.isValid && m_CurrentModel._Model.IsVisible;
        public bool CreatingNewModel => m_CreateNewGrid;
        public string CurrentTargetDescription
        {
            get
            {
                if (CreatingNewModel) return m_DefiningGrid ? "Sizing new voxel grid"
                    : "New grid — hold trigger and drag";
                ModelWidget widget = CurrentTargetWidget;
                if (widget == null) return "No active voxel model — grab or select one";
                string name = widget.GetExportName();
                if (widget.EditableVoxDocument == null) return $"{name} — Break Apart to make it editable";
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

        public override void EnableTool(bool enable)
        {
            // Tool-change notifications can read status before the first UpdateTool call.
            if (enable) EnsureCurrentTarget();
            if (!enable)
            {
                CancelGridDrag();
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
                CancelGridDrag();
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
                UpdateGridCreation(attachPoint, canvasPosition);
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
                    m_GestureWidget.gameObject.activeInHierarchy)
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

        public float CycleVoxelSize()
        {
            VoxelSize = CyclePreset(kVoxelSizes, Mathf.Max(VoxelSize, 0.0001f));
            return VoxelSize;
        }

        public bool ToggleGrid()
        {
            ShowGrid = !ShowGrid;
            return ShowGrid;
        }

        public void RequestNewModel()
        {
            EndGesture();
            m_DefiningGrid = false;
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
            m_DefiningGrid = false;
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
            if (widget == null || widget.EditableVoxDocument == null) return;
            var document = widget.EditableVoxDocument;
            var model = document.Models.FirstOrDefault(item => item.IsVisible && item.Voxels.Count > 0)
                ?? document.Models.FirstOrDefault(item => item.IsVisible);
            if (model == null) return;
            m_CurrentDocument = new VoxDocumentApiWrapper(document, widget);
            m_CurrentModel = new VoxModelApiWrapper(document, model, m_CurrentDocument);
            VoxelSize = m_CurrentModel.voxelSize;
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
            return emptyCandidate;
        }

        private void BeginGesture(Vector3 canvasPosition)
        {
            EndGesture();
            RuntimeVoxDocument.RuntimeModel model = FindTargetModelAt(canvasPosition);
            if (model == null) return;
            m_CurrentModel = new VoxModelApiWrapper(m_CurrentDocument._Document, model, m_CurrentDocument);

            m_GestureBeforeCells = new Dictionary<Vector3Int, byte>();
            m_GestureAfterCells = new Dictionary<Vector3Int, byte>();
            m_GestureBeforePalette = (Color32[])m_CurrentDocument._Document.Palette.Clone();
            m_GestureWidget = m_CurrentDocument.Widget;
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
            if (m_CurrentModel == null || !m_CurrentModel.ContainsAt(canvasPosition))
            {
                m_HasLastCell = false;
                return;
            }
            Vector3Int cell = Vector3Int.RoundToInt(m_CurrentModel.CanvasToVoxel(canvasPosition));
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
            m_CurrentModel._Model.TryGetPaletteIndex(cell, out byte before);
            Vector3 canvasPosition = m_CurrentModel.VoxelToCanvas(cell);
            bool changed = Mode switch
            {
                EditMode.Erase => m_CurrentModel.EraseAt(canvasPosition),
                EditMode.Paint => m_CurrentModel.RecolorAt(canvasPosition, App.BrushColor.CurrentColor),
                _ => m_CurrentModel.PaintAt(canvasPosition, App.BrushColor.CurrentColor),
            };
            if (changed)
            {
                if (!m_GestureBeforeCells.ContainsKey(cell)) m_GestureBeforeCells.Add(cell, before);
                m_CurrentModel._Model.TryGetPaletteIndex(cell, out byte after);
                m_GestureAfterCells[cell] = after;
                m_GestureChanged = true;
            }
        }

        internal void FinishEditing() => EndGesture();

        private void EndGesture()
        {
            if (!m_GestureActive)
            {
                return;
            }

            var gestureDocument = m_CurrentDocument;
            bool targetShowing = m_GestureWidget != null && m_GestureWidget.Showing &&
                m_GestureWidget.gameObject.activeInHierarchy;
            bool documentIsEmpty = gestureDocument._Document.Models.All(model => model.Voxels.Count == 0);
            // Preserve already-applied edits even when an external action hid the target.
            if (m_GestureChanged && m_GestureWidget != null)
            {
                var command = new ModifyVoxDocumentCommand(
                    m_GestureWidget,
                    gestureDocument._Document,
                    m_GestureModel,
                    m_GestureBeforeCells,
                    m_GestureAfterCells,
                    m_GestureBeforePalette,
                    m_CurrentDocument._Document.Palette);
                if (documentIsEmpty)
                {
                    new HideWidgetCommand(m_GestureWidget, command);
                }
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                if (documentIsEmpty)
                {
                    m_CurrentDocument = null;
                    m_CurrentModel = null;
                }
                else if (targetShowing)
                {
                    m_CurrentDocument.Refresh();
                }
            }

            gestureDocument.SuppressDirtyTracking = false;
            gestureDocument.SetAutoVisuals(m_GestureAutoVisuals && targetShowing, true);
            m_GestureActive = false;
            m_GestureChanged = false;
            m_HasLastCell = false;
            m_GestureBeforeCells = null;
            m_GestureAfterCells = null;
            m_GestureBeforePalette = null;
            m_GestureWidget = null;
            m_GestureModel = null;
            if (m_HasPendingTarget)
            {
                ModelWidget pending = m_PendingTargetWidget;
                m_PendingTargetWidget = null;
                m_HasPendingTarget = false;
                BindTargetWidget(pending);
            }
        }

        private void CancelGridDrag()
        {
            if (!m_DefiningGrid) return;
            m_DefiningGrid = false;
            m_CreateNewGrid = false;
        }

        private void UpdateGridCreation(Transform attachPoint, Vector3 canvasPosition)
        {
            if (IsEatingInput)
            {
                CancelGridDrag();
                UpdateTargetLabel(attachPoint, false);
                return;
            }
            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                m_DefiningGrid = true;
                m_GridStart = canvasPosition;
                m_GridVoxelSize = Mathf.Max(VoxelSize, 0.0001f);
                m_GridCanvas = App.Scene.ActiveCanvas;
            }
            if (m_DefiningGrid && m_GridCanvas != App.Scene.ActiveCanvas)
            {
                CancelGridDrag();
                return;
            }
            float voxelSize = m_DefiningGrid ? m_GridVoxelSize : Mathf.Max(VoxelSize, 0.0001f);
            Vector3 start = m_DefiningGrid ? m_GridStart : canvasPosition;
            Vector3 delta = canvasPosition - start;
            Vector3 min = start;
            var dimensions = Vector3Int.one;
            for (int axis = 0; axis < 3; ++axis)
            {
                dimensions[axis] = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta[axis]) / voxelSize - 0.0001f),
                    1, RuntimeVoxDocument.MaxModelDimension);
                if (delta[axis] < 0) min[axis] -= dimensions[axis] * voxelSize;
            }
            Vector3 size = (Vector3)dimensions * voxelSize;
            VoxEditPreviewRenderer.DrawCreationBounds(min, min + size, voxelSize);
            string hint = m_DefiningGrid
                ? $"{dimensions.x} x {dimensions.y} x {dimensions.z} voxels — release to create"
                : "Hold trigger and drag opposite corners of the grid";
            UpdateTargetLabel(attachPoint, false, hint);
            if (m_DefiningGrid &&
                !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                m_DefiningGrid = false;
                if (CreateGrid(min, dimensions, voxelSize)) m_CreateNewGrid = false;
            }
        }

        private bool CreateGrid(Vector3 canvasMin, Vector3Int canvasDimensions, float voxelSize)
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
            var dimensions = new Vector3Int(canvasDimensions.x, canvasDimensions.z, canvasDimensions.y);
            document.CreateModel($"Voxel Model {modelNumber}", dimensions);
            Model model = Model.CreateEditableVoxModelFromBytes(document.ToVoxBytes());
            if (model == null) return false;

            try
            {
                Vector3 origin = canvasMin + new Vector3(0.5f, 0.5f, canvasDimensions.z - 0.5f) * voxelSize;
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
                return true;
            }
            finally
            {
                model.ReleaseFromCatalog();
            }
        }

        private void EnsureCurrentTarget()
        {
            if (HasCurrentTarget) return;
            EndGesture();
            ModelWidget widget = CurrentTargetWidget;
            // Keep the contextual external target, but never convert it implicitly.
            BindTargetWidget(widget);
        }

        private void UpdateTargetLabel(
            Transform attachPoint, bool insideTarget, string creationHint = null)
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
            string action = creationHint ?? (CreatingNewModel
                ? "Hold trigger and drag to size a new grid"
                : !HasCurrentTarget
                    ? CurrentTargetWidget != null && CurrentTargetWidget.IsExternalVox
                        ? "Use Break Apart to make this model editable"
                        : "Grab or select an editable voxel model; New Model is in settings"
                    : insideTarget
                        ? $"Trigger: {Mode}"
                        : "Outside active model — grab or select another to change target");
            m_TargetLabel.text = $"{CurrentTargetDescription}\n{action}";
        }

        private static float CyclePreset(float[] presets, float current)
        {
            int currentIndex = Array.FindIndex(presets,
                value => Mathf.Abs(value - current) < 0.0001f);
            if (currentIndex >= 0)
            {
                return presets[(currentIndex + 1) % presets.Length];
            }
            int nextIndex = Array.FindIndex(presets, value => value > current);
            return presets[nextIndex >= 0 ? nextIndex : 0];
        }
    }
}
