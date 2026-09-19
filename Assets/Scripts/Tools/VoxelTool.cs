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

        private const int kDefaultModelSize = 128;
        private const float kDefaultVoxelSize = 0.1f;

        private VoxDocumentApiWrapper m_CurrentDocument;
        private VoxModelApiWrapper m_CurrentModel;
        private bool m_CreateNewOnNextAdd;
        private bool m_GestureActive;
        private bool m_GestureChanged;
        private bool m_HasLastCell;
        private Vector3Int m_LastCell;
        private byte[] m_GestureBefore;
        private ModelWidget m_GestureWidget;
        private CreateWidgetCommand m_CreateWidgetCommand;

        public EditMode Mode { get; set; }
        public int ModelSize { get; set; } = kDefaultModelSize;
        public float VoxelSize { get; set; } = kDefaultVoxelSize;
        public bool ShowGrid { get; set; } = true;

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
        }

        public override void UpdateTool()
        {
            base.UpdateTool();
            EnsureCurrentTarget();

            Transform attachPoint = InputManager.Brush.Geometry.ToolAttachPoint;
            PointerManager.m_Instance.SetMainPointerPosition(attachPoint.position);
            Vector3 canvasPosition = App.Scene.ActiveCanvas.AsCanvas[attachPoint].translation;

            if (m_CurrentModel != null && ShowGrid)
            {
                Color guideColor = Mode switch
                {
                    EditMode.Erase => new Color(0.9f, 0.12f, 0.08f),
                    EditMode.Paint => new Color(0.18f, 0.48f, 0.95f),
                    _ => new Color(0.12f, 0.8f, 0.28f),
                };
                Color targetColor = Mode == EditMode.Erase
                    ? guideColor
                    : App.BrushColor.CurrentColor;
                m_CurrentModel.PreviewForTool(canvasPosition, targetColor, guideColor);
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
                BeginGesture(canvasPosition);
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
                _ => EditMode.Add,
            };
            ControllerConsoleScript.m_Instance?.AddNewLine($"Voxel mode: {Mode}", true);
            return Mode;
        }

        public void RequestNewModel()
        {
            EndGesture();
            m_CurrentDocument = null;
            m_CurrentModel = null;
            m_CreateNewOnNextAdd = true;
            Mode = EditMode.Add;
            ControllerConsoleScript.m_Instance?.AddNewLine("Voxel tool: new model", true);
        }

        private void BeginGesture(Vector3 canvasPosition)
        {
            EndGesture();
            if (!m_CreateNewOnNextAdd)
            {
                VoxModelApiWrapper pointedModel = VoxApiWrapper.FindModelAt(canvasPosition);
                if (pointedModel != null)
                {
                    SetCurrentModel(pointedModel);
                }
            }

            if (m_CurrentModel == null)
            {
                if (Mode != EditMode.Add || !CreateWidgetAt(canvasPosition))
                {
                    return;
                }
            }
            else if (!m_CurrentModel.ContainsAt(canvasPosition))
            {
                return;
            }

            bool createdThisGesture = m_CreateWidgetCommand != null;
            if (!createdThisGesture)
            {
                m_GestureBefore = m_CurrentDocument._Document.ToVoxBytes();
            }
            m_GestureWidget = m_CurrentDocument.Widget;
            m_GestureChanged = createdThisGesture;
            m_GestureActive = true;
            m_HasLastCell = false;
        }

        private void ApplyAt(Vector3 canvasPosition)
        {
            if (m_CurrentModel == null || !m_CurrentModel.ContainsAt(canvasPosition))
            {
                return;
            }

            Vector3Int cell = Vector3Int.RoundToInt(m_CurrentModel.CanvasToVoxel(canvasPosition));
            if (m_HasLastCell && cell == m_LastCell)
            {
                return;
            }

            bool changed = Mode switch
            {
                EditMode.Erase => m_CurrentModel.EraseAt(canvasPosition),
                EditMode.Paint => m_CurrentModel.RecolorAt(canvasPosition, App.BrushColor.CurrentColor),
                _ => m_CurrentModel.PaintAt(canvasPosition, App.BrushColor.CurrentColor),
            };
            m_LastCell = cell;
            m_HasLastCell = true;
            m_GestureChanged |= changed;
        }

        private void EndGesture()
        {
            if (!m_GestureActive)
            {
                return;
            }

            if (m_GestureChanged && m_GestureWidget != null)
            {
                byte[] after = m_CurrentDocument._Document.ToVoxBytes();
                var command = new ModifyVoxDocumentCommand(
                    m_GestureWidget,
                    m_GestureBefore,
                    after,
                    m_CreateWidgetCommand);
                bool documentIsEmpty = m_CurrentDocument._Document.Models.All(
                    model => model.Voxels.Count == 0);
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
                else
                {
                    m_CurrentDocument.Refresh();
                }
            }

            m_GestureActive = false;
            m_GestureChanged = false;
            m_HasLastCell = false;
            m_GestureBefore = null;
            m_GestureWidget = null;
            m_CreateWidgetCommand = null;
        }

        private bool CreateWidgetAt(Vector3 canvasPosition)
        {
            int size = Mathf.Clamp(ModelSize, 1, RuntimeVoxDocument.MaxModelDimension);
            var document = new RuntimeVoxDocument();
            RuntimeVoxDocument.RuntimeModel runtimeModel = document.CreateModel(
                "model_0", new Vector3Int(size, size, size));
            m_GestureBefore = document.ToVoxBytes();
            byte paletteIndex = document.GetOrAddPaletteColor(App.BrushColor.CurrentColor);
            int center = size / 2;
            runtimeModel.AddOrUpdateVoxel(new Vector3Int(center, center, center), paletteIndex);
            Model model = Model.CreateEditableVoxModelFromBytes(document.ToVoxBytes());
            if (model == null)
            {
                m_GestureBefore = null;
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
                m_CurrentDocument.SetAutoVisuals(true, true, false);
                m_CurrentModel = m_CurrentDocument.Model();
                m_CurrentModel.PlaceAt(canvasPosition, Mathf.Max(VoxelSize, 0.0001f));
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
            m_CurrentModel = model;
            m_CurrentDocument = model.document;
            m_CurrentDocument.SetAutoVisuals(true, true, false);
            VoxelSize = model.voxelSize;
            m_CreateNewOnNextAdd = false;
        }

        private void EnsureCurrentTarget()
        {
            if (m_CurrentDocument == null)
            {
                return;
            }
            if (!m_CurrentDocument.isValid)
            {
                EndGesture();
                m_CurrentDocument = null;
                m_CurrentModel = null;
                return;
            }

            ModelWidget widget = m_CurrentDocument.Widget;
            if (widget != null && !ReferenceEquals(widget.EditableVoxDocument, m_CurrentDocument._Document))
            {
                int modelIndex = Mathf.Clamp(m_CurrentModel?.index ?? 0, 0,
                    widget.EditableVoxDocument.Models.Count - 1);
                m_CurrentDocument = new VoxDocumentApiWrapper(widget.EditableVoxDocument, widget);
                m_CurrentDocument.SetAutoVisuals(true, true, false);
                m_CurrentModel = m_CurrentDocument.Model(modelIndex);
            }
        }
    }
}
