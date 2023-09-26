// Copyright 2020 The Tilt Brush Authors
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

using UnityEngine;

namespace TiltBrush
{

    public partial class FreePaintTool : BaseTool
    {
        [SerializeField] private float m_AdjustSizeScalar;

        [SerializeField] private float m_HapticInterval = .1f;
        [SerializeField] private float m_HapticSizeUp;
        [SerializeField] private float m_HapticSizeDown;

        private bool m_PaintingActive;

        public bool m_brushTrigger { get; private set; }
        public bool m_brushTriggerDown { get; private set; }
        public bool m_wandTrigger { get; private set; }
        public bool m_wandTriggerDown { get; private set; }

        public bool m_brushUndoButton { get; private set; }
        public bool m_brushUndoButtonDown { get; private set; }

        public bool m_brushUndoButtonUp { get; private set; }
        public bool m_brushUndoButtonTapped { get; private set; }
        private bool m_brushUndoButtonTapInvalid { get; set; }
        private float m_brushUndoButtonTapExpiry { get; set; }
        private const float TapDelayTime = 0.333f;
        public bool m_brushUndoButtonHeld { get; private set; }

        public float m_brushTriggerRatio { get; private set; }
        public float m_wandTriggerRatio { get; private set; }

        override public void Init()
        {
            base.Init();
            m_PaintingActive = false;
            InitBimanualTape();
        }

        public override bool ShouldShowPointer()
        {
            return !PanelManager.m_Instance.IntroSketchbookMode;
        }

        override public void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);
            if (!bEnable)
            {
                PointerManager.m_Instance.EnableLine(false);
                WidgetManager.m_Instance.ResetActiveStencil();
                EndBimanualTape();
                EndBimanualIntersect();
            }
            m_PaintingActive = false;
        }

        override public bool ShouldShowTouch()
        {
            return false;
        }

        static Quaternion sm_OrientationAdjust = Quaternion.Euler(new Vector3(0, 180, 0));
        override public void UpdateTool()
        {
            // Don't call base.UpdateTool() because we have a different 'stop eating input' check
            // for FreePaintTool.
            m_wandTriggerRatio = InputManager.Wand.GetTriggerRatio();
            m_wandTrigger = InputManager.Wand.GetCommand(InputManager.SketchCommands.Activate);
            m_wandTriggerDown = InputManager.Wand.GetCommandDown(InputManager.SketchCommands.Activate);
            m_brushTriggerRatio = InputManager.Brush.GetTriggerRatio();
            m_brushTrigger = InputManager.Brush.GetCommand(InputManager.SketchCommands.Activate);
            m_brushTriggerDown = InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Activate);

            m_brushUndoButton = InputManager.Brush.GetCommand(InputManager.SketchCommands.Undo);
            m_brushUndoButtonDown = InputManager.Brush.GetCommandDown(InputManager.SketchCommands.Undo);

            m_brushUndoButtonUp = InputManager.Brush.GetCommandUp(InputManager.SketchCommands.Undo);
            m_brushUndoButtonTapped = m_brushUndoButtonUp && !m_brushUndoButtonTapInvalid;
            if (m_brushUndoButtonDown)
            {
                m_brushUndoButtonTapInvalid = false;
                m_brushUndoButtonTapExpiry = TapDelayTime;
            }

            if (!m_brushUndoButtonTapInvalid)
            {
                m_brushUndoButtonTapExpiry = Mathf.MoveTowards(m_brushUndoButtonTapExpiry, 0, Time.deltaTime);
                if (m_brushTriggerDown || m_brushUndoButtonTapExpiry <= 0)
                    m_brushUndoButtonTapInvalid = true;
            }

            m_brushUndoButtonHeld = m_brushUndoButtonTapInvalid && m_brushUndoButton;


            if (m_EatInput && !m_brushTrigger)
                m_EatInput = false;

            if (m_ExitOnAbortCommand && InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Abort))
                m_RequestExit = true;

            PositionPointer();

            if (PanelManager.m_Instance.AdvancedModeActive() && !m_BimanualTape && !m_PaintingActive && m_wandTrigger && !InputManager.Wand.GetControllerGrip() && SketchControlsScript.m_Instance.IsFreepaintToolReady())
                BeginBimanualTape();

            m_PaintingActive = !m_EatInput && !m_ToolHidden && (m_brushTrigger || (m_PaintingActive && !m_RevolverActive && m_LazyInputActive && m_BimanualTape && m_wandTrigger));

            // Allow API command to override painting mode
            m_PaintingActive = m_PaintingActive || ApiManager.Instance.ForcePaintingOn;

            if (m_BimanualTape)
            {
                if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ShowPinCushion))
                {
                    EndBimanualTape();
                }
                else if (!m_wandTrigger && !m_brushTrigger)
                    EndBimanualTape();
                else
                {
                    UpdateBimanualGuideLineT();
                    UpdateBimanualGuideVisuals();

                    if (m_bimanualControlPointerPosition)
                        UpdateBimanualIntersectVisuals();

                    if (m_brushUndoButtonTapped)
                        BeginRevolver();
                }
            }
            else if (m_brushUndoButtonTapped)
            {
                if (m_brushTrigger)
                {
                    if (m_LazyInputActive)
                        m_LazyInputTangentMode = !m_LazyInputTangentMode;
                }
                else
                    m_LazyInputActive = !m_LazyInputActive;
            }

            PointerManager.m_Instance.EnableLine(m_PaintingActive);
            PointerManager.m_Instance.PointerPressure = m_brushTriggerRatio;

        }

        override public void LateUpdateTool()
        {
            // When the pointer manager is processing our line, don't stomp its position.
            if (!PointerManager.m_Instance.IsMainPointerProcessingLine())
            {
                PositionPointer();
            }
        }

        override public void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            if (controller == InputManager.ControllerName.Brush)
            {
                if (App.Instance.IsInStateThatAllowsPainting())
                {
                    if (m_PaintingActive)
                    {
                        // TODO: Make snap work with non-line shapes.
                        if (PointerManager.m_Instance.StraightEdgeModeEnabled &&
                            PointerManager.m_Instance.StraightEdgeGuideIsLine)
                        {
                            InputManager.Brush.Geometry.TogglePadSnapHint(
                                PointerManager.m_Instance.StraightEdgeGuide.SnapEnabled,
                                enabled: true);
                        }
                    }
                    else
                    {
                        InputManager.Brush.Geometry.ShowBrushSizer();
                        if (PanelManager.m_Instance.AdvancedModeActive())
                        {
                            if (m_BimanualTape)
                                InputManager.Brush.Geometry.TogglePadRevolverHint(m_RevolverActive, enabled: true);
                            else
                                InputManager.Brush.Geometry.TogglePadLazyInputHint(m_LazyInputActive, m_LazyInputTangentMode, enabled: true);
                        }
                        else
                        {
                            m_LazyInputActive = false;
                            m_LazyInputTangentMode = false;
                            InputManager.Brush.Geometry.TogglePadLazyInputHint(m_LazyInputActive, m_LazyInputTangentMode, enabled: false);
                        }

                    }
                }
            }
        }

        void PositionPointer()
        {
            // Angle the pointer according to the user-defined pointer angle.
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 pos = rAttachPoint.position;
            Quaternion rot = rAttachPoint.rotation * sm_OrientationAdjust;

            // Modify pointer position and rotation with stencils.
            WidgetManager.m_Instance.MagnetizeToStencils(ref pos, ref rot);

            if (m_BimanualTape)
            {
                ApplyBimanualTape(ref pos, ref rot);
                ApplyRevolver(ref pos, ref rot);
            }
            else
            {
                ApplyLazyInput(ref pos, ref rot);
            }

            if (SelectionManager.m_Instance.CurrentSnapGridIndex != 0)
            {
                pos = SnapToGrid(pos);
            }

            if (PointerManager.m_Instance.positionJitter > 0)
            {
                pos = PointerManager.m_Instance.GenerateJitteredPosition(pos, PointerManager.m_Instance.positionJitter);
            }

            PointerManager.m_Instance.SetPointerTransform(InputManager.ControllerName.Brush, pos, rot);
        }

        override public void UpdateSize(float fAdjustAmount)
        {
            float fPrevRatio = GetSize01();
            PointerManager.m_Instance.AdjustAllPointersBrushSize01(m_AdjustSizeScalar * fAdjustAmount);
            PointerManager.m_Instance.MarkAllBrushSizeUsed();
            float fCurrentRatio = GetSize01();

            float fHalfInterval = m_HapticInterval * 0.5f;
            int iPrevInterval = (int)((fPrevRatio + fHalfInterval) / m_HapticInterval);
            int iCurrentInterval = (int)((fCurrentRatio + fHalfInterval) / m_HapticInterval);
            if (!App.VrSdk.AnalogIsStick(InputManager.ControllerName.Brush))
            {
                if (iCurrentInterval > iPrevInterval)
                {
                    InputManager.m_Instance.TriggerHaptics(
                        InputManager.ControllerName.Brush, m_HapticSizeUp);
                }
                else if (iCurrentInterval < iPrevInterval)
                {
                    InputManager.m_Instance.TriggerHaptics(
                        InputManager.ControllerName.Brush, m_HapticSizeDown);
                }
            }
        }

        override public float GetSize01()
        {
            return PointerManager.m_Instance.GetPointerBrushSize01(InputManager.ControllerName.Brush);
        }

        override public bool CanAdjustSize()
        {
            return App.Instance.IsInStateThatAllowsPainting();
        }
    }
} // namespace TiltBrush
