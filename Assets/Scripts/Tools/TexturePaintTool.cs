// Copyright 2024 The Open Brush Authors
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
using UnityEngine;

namespace TiltBrush
{
    public class TexturePaintTool : BaseTool
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
                TexturePainterManager.m_Instance.EnablePainting(false);
                WidgetManager.m_Instance.ResetActiveStencil();
            }
            m_PaintingActive = false;
        }

        override public bool ShouldShowTouch()
        {
            return false;
        }

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

            m_PaintingActive = !m_EatInput && !m_ToolHidden && (m_brushTrigger || (m_PaintingActive && m_wandTrigger));

            TexturePainterManager.m_Instance.EnablePainting(m_PaintingActive);
            TexturePainterManager.m_Instance.PointerPressure = m_brushTriggerRatio;

            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 pos = rAttachPoint.position;
            Vector3 vec = -rAttachPoint.forward;
            PointerManager.m_Instance.SetMainPointerPosition(pos);
            PointerManager.m_Instance.SetMainPointerForward(vec);
        }

        private void OnDrawGizmos()
        {
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            Vector3 pos = rAttachPoint.position;
            Vector3 vec = rAttachPoint.forward;
            Gizmos.DrawRay(pos, vec);
            Debug.DrawLine(pos, pos + vec * 2f, Color.red);

        }

        override public void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            if (controller == InputManager.ControllerName.Brush)
            {
                if (App.Instance.IsInStateThatAllowsPainting())
                {
                    InputManager.Brush.Geometry.ShowBrushSizer();
                }
            }
        }

        override public void UpdateSize(float fAdjustAmount)
        {
            float fPrevRatio = GetSize01();
            TexturePainterManager.m_Instance.AdjustBrushSize01(m_AdjustSizeScalar * fAdjustAmount);
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
            return TexturePainterManager.m_Instance.GetBrushSize01(InputManager.ControllerName.Brush);
        }

        override public bool CanAdjustSize()
        {
            return App.Instance.IsInStateThatAllowsPainting();
        }
    }

} // namespace TiltBrush
