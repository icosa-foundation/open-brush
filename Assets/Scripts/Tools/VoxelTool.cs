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
            Select,
        }

        private const int kDefaultModelSize = 128;
        private const float kDefaultVoxelSize = 0.1f;
        private static readonly int[] kModelSizes = { 16, 32, 64, 128, 256 };
        private static readonly float[] kVoxelSizes = { 0.025f, 0.05f, 0.1f, 0.2f, 0.4f };

        private VoxDocumentApiWrapper m_CurrentDocument;
        private VoxModelApiWrapper m_CurrentModel;
        private bool m_CreateNewOnNextAdd;
        private bool m_GestureActive;
        private bool m_GestureChanged;
        private bool m_HasLastCell;
        private Vector3Int m_LastCell;
        private Dictionary<Vector3Int, byte> m_GestureBeforeCells;
        private Dictionary<Vector3Int, byte> m_GestureAfterCells;
        private Color32[] m_GestureBeforePalette;
        private bool m_GestureAutoVisuals;
        private ModelWidget m_GestureWidget;
        private CreateWidgetCommand m_CreateWidgetCommand;
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
                m_CreateNewOnNextAdd = false;
            }
        }
        public int ModelSize { get; set; } = kDefaultModelSize;
        public float VoxelSize { get; set; } = kDefaultVoxelSize;
        public bool ShowGrid { get; set; } = true;
        public bool HasCurrentTarget => m_CurrentModel != null;
        public bool CreatingNewModel => m_CreateNewOnNextAdd;
        public string CurrentTargetDescription => m_CurrentModel == null
            ? (CreatingNewModel ? "New model ready — trigger to place" : "No active model — touch a voxel to select")
            : $"Active: {m_CurrentModel.name} ({m_CurrentModel.sizeX}x{m_CurrentModel.sizeY}x{m_CurrentModel.sizeZ})";

        public override bool ShouldShowPointer() => true;

        public override void EnableTool(bool enable)
        {
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

            Transform attachPoint = InputManager.Brush.Geometry.ToolAttachPoint;
            PointerManager.m_Instance.SetMainPointerPosition(attachPoint.position);
            Vector3 canvasPosition = App.Scene.ActiveCanvas.AsCanvas[attachPoint].translation;
            // Resolve a candidate only while selecting, never during an edit gesture.
            VoxModelApiWrapper candidate = !m_GestureActive && !CreatingNewModel &&
                (Mode == EditMode.Select || !HasCurrentTarget)
                ? VoxApiWrapper.FindModelAt(canvasPosition)
                : null;
            UpdateTargetLabel(attachPoint, canvasPosition, candidate);

            if (m_CurrentModel != null && candidate == null)
            {
                Color guideColor = Mode switch
                {
                    EditMode.Erase => new Color(0.9f, 0.12f, 0.08f),
                    EditMode.Paint => new Color(0.18f, 0.48f, 0.95f),
                    EditMode.Select => Color.cyan,
                    _ => new Color(0.12f, 0.8f, 0.28f),
                };
                Color targetColor = Mode == EditMode.Erase
                    ? guideColor
                    : App.BrushColor.CurrentColor;
                m_CurrentModel.PreviewForTool(canvasPosition, targetColor, guideColor, ShowGrid);
            }
            if (candidate != null)
            {
                candidate.PreviewForTool(canvasPosition, Color.cyan, Color.cyan, ShowGrid);
            }

            if (IsEatingInput)
            {
                return;
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleReshape))
            {
                CycleMode();
                InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                if (Mode == EditMode.Select || (!HasCurrentTarget && !CreatingNewModel))
                {
                    if (candidate != null)
                    {
                        SetCurrentModel(candidate);
                        Mode = EditMode.Add;
                    }
                    // Selecting never edits on the same trigger press.
                }
                else
                {
                    BeginGesture(canvasPosition);
                }
            }

            if (m_GestureActive &&
                InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                ApplyAt(canvasPosition);
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
                EditMode.Paint => EditMode.Select,
                _ => EditMode.Add,
            };
            ControllerConsoleScript.m_Instance?.AddNewLine($"Voxel mode: {Mode}", true);
            return Mode;
        }

        public int CycleModelSize()
        {
            ModelSize = CyclePreset(kModelSizes, Mathf.Clamp(ModelSize, 1,
                RuntimeVoxDocument.MaxModelDimension));
            ControllerConsoleScript.m_Instance?.AddNewLine(
                $"New voxel model size: {ModelSize}x{ModelSize}x{ModelSize}", true);
            return ModelSize;
        }

        public float CycleVoxelSize()
        {
            VoxelSize = CyclePreset(kVoxelSizes, Mathf.Max(VoxelSize, 0.0001f));
            ControllerConsoleScript.m_Instance?.AddNewLine(
                $"New voxel size: {VoxelSize:0.###} m", true);
            return VoxelSize;
        }

        public bool ToggleGrid()
        {
            ShowGrid = !ShowGrid;
            ControllerConsoleScript.m_Instance?.AddNewLine(
                $"Voxel grid: {(ShowGrid ? "shown" : "hidden")}", true);
            return ShowGrid;
        }

        public void RequestNewModel()
        {
            EndGesture();
            m_CurrentDocument = null;
            m_CurrentModel = null;
            Mode = EditMode.Add;
            m_CreateNewOnNextAdd = true;
            ControllerConsoleScript.m_Instance?.AddNewLine(
                "Voxel tool: trigger to place a new model", true);
        }

        private void BeginGesture(Vector3 canvasPosition)
        {
            EndGesture();
            if (m_CurrentModel == null)
            {
                if (!CreatingNewModel || Mode != EditMode.Add || !CreateWidgetAt(canvasPosition))
                {
                    return;
                }
            }
            else if (!m_CurrentModel.ContainsAt(canvasPosition))
            {
                return;
            }

            bool createdThisGesture = m_CreateWidgetCommand != null;
            m_GestureBeforeCells = new Dictionary<Vector3Int, byte>();
            m_GestureAfterCells = new Dictionary<Vector3Int, byte>();
            m_GestureBeforePalette = (Color32[])m_CurrentDocument._Document.Palette.Clone();
            m_GestureWidget = m_CurrentDocument.Widget;
            m_GestureAutoVisuals = m_CurrentDocument.autoVisuals;
            m_CurrentDocument.SuppressDirtyTracking = true;
            m_CurrentDocument.SetAutoVisuals(false, true);
            m_GestureChanged = createdThisGesture;
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
                    m_CurrentModel.index,
                    m_GestureBeforeCells,
                    m_GestureAfterCells,
                    m_GestureBeforePalette,
                    m_CurrentDocument._Document.Palette,
                    m_CreateWidgetCommand);
                if (documentIsEmpty)
                {
                    new HideWidgetCommand(m_GestureWidget, command);
                }
                if (m_CreateWidgetCommand == null)
                {
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                }
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
            m_CreateWidgetCommand = null;
        }

        private bool CreateWidgetAt(Vector3 canvasPosition)
        {
            int size = Mathf.Clamp(ModelSize, 1, RuntimeVoxDocument.MaxModelDimension);
            var document = new RuntimeVoxDocument();
            int modelNumber = 1;
            while (WidgetManager.m_Instance.ModelWidgets.Any(widget =>
                widget.EditableVoxDocument?.Models.Any(model =>
                    model.Name == $"Voxel Model {modelNumber}") == true))
            {
                ++modelNumber;
            }
            RuntimeVoxDocument.RuntimeModel runtimeModel = document.CreateModel(
                $"Voxel Model {modelNumber}", new Vector3Int(size, size, size));
            byte paletteIndex = document.GetOrAddPaletteColor(App.BrushColor.CurrentColor);
            int center = size / 2;
            runtimeModel.AddOrUpdateVoxel(new Vector3Int(center, center, center), paletteIndex);
            Model model = Model.CreateEditableVoxModelFromBytes(document.ToVoxBytes());
            if (model == null)
            {
                return false;
            }

            try
            {
                Transform attachPoint = InputManager.Brush.Geometry.ToolAttachPoint;
                m_CreateWidgetCommand = new CreateWidgetCommand(
                    WidgetManager.m_Instance.ModelWidgetPrefab,
                    TrTransform.TRS(attachPoint.position, attachPoint.rotation, VoxelSize),
                    forceTransform: true);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(m_CreateWidgetCommand);
                var widget = m_CreateWidgetCommand.Widget as ModelWidget;
                widget.LoadingFromSketch = true;
                widget.Model = model;
                widget.AdoptEditableVoxDocument(document);
                widget.Show(true);

                m_CurrentDocument = new VoxDocumentApiWrapper(document, widget);
                m_CurrentDocument.SetAutoVisuals(true, true);
                m_CurrentModel = m_CurrentDocument.Model();
                // Initial placement belongs to the creation command, not an untracked Lua edit.
                m_CurrentDocument.SuppressDirtyTracking = true;
                try
                {
                    m_CurrentModel.PlaceAt(canvasPosition, Mathf.Max(VoxelSize, 0.0001f));
                }
                finally
                {
                    m_CurrentDocument.SuppressDirtyTracking = false;
                }
                m_CreateWidgetCommand.SetWidgetCost(widget.GetTiltMeterCost());
                m_CreateNewOnNextAdd = false;
                return true;
            }
            finally
            {
                model.ReleaseFromCatalog();
            }
        }

        private void SetCurrentModel(VoxModelApiWrapper model)
        {
            bool targetChanged = !ReferenceEquals(m_CurrentDocument?._Document, model.document._Document) ||
                !ReferenceEquals(m_CurrentModel?._Model, model._Model);
            m_CurrentModel = model;
            m_CurrentDocument = model.document;
            m_CurrentDocument.SetAutoVisuals(true, true);
            VoxelSize = model.voxelSize;
            m_CreateNewOnNextAdd = false;
            if (targetChanged)
            {
                ControllerConsoleScript.m_Instance?.AddNewLine(
                    $"Voxel target: {CurrentTargetDescription}", true);
            }
        }

        private void EnsureCurrentTarget()
        {
            if (m_CurrentDocument == null)
            {
                return;
            }
            ModelWidget widget = m_CurrentDocument.Widget;
            if (!m_CurrentDocument.isValid || widget == null || !widget.Showing ||
                !widget.gameObject.activeInHierarchy || !string.IsNullOrEmpty(widget.Subtree))
            {
                EndGesture();
                m_CurrentDocument = null;
                m_CurrentModel = null;
                return;
            }

            if (widget != null && !ReferenceEquals(widget.EditableVoxDocument, m_CurrentDocument._Document))
            {
                if (widget.EditableVoxDocument == null || widget.EditableVoxDocument.Models.Count == 0)
                {
                    EndGesture();
                    m_CurrentDocument = null;
                    m_CurrentModel = null;
                    return;
                }
                int modelIndex = Mathf.Clamp(m_CurrentModel?.index ?? 0, 0,
                    widget.EditableVoxDocument.Models.Count - 1);
                m_CurrentDocument = new VoxDocumentApiWrapper(widget.EditableVoxDocument, widget);
                m_CurrentDocument.SetAutoVisuals(true, true);
                m_CurrentModel = m_CurrentDocument.Model(modelIndex);
            }
            if (!m_CurrentModel._Model.IsVisible)
            {
                EndGesture();
                m_CurrentDocument = null;
                m_CurrentModel = null;
            }
        }

        private void UpdateTargetLabel(
            Transform attachPoint, Vector3 canvasPosition, VoxModelApiWrapper candidate)
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
            string action = CreatingNewModel
                ? "Trigger: create new model"
                : Mode == EditMode.Select || !HasCurrentTarget
                    ? candidate == null ? "Touch an existing voxel; New Model is in settings"
                        : $"Trigger: select {candidate.name}"
                    : m_CurrentModel.ContainsAt(canvasPosition)
                        ? $"Trigger: {Mode} · Select mode changes target"
                        : "Outside active model · Select mode changes target";
            m_TargetLabel.text = $"{CurrentTargetDescription}\n{action}";
        }

        private static int CyclePreset(int[] presets, int current)
        {
            int currentIndex = Array.IndexOf(presets, current);
            if (currentIndex >= 0)
            {
                return presets[(currentIndex + 1) % presets.Length];
            }
            int nextIndex = Array.FindIndex(presets, value => value > current);
            return presets[nextIndex >= 0 ? nextIndex : 0];
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
