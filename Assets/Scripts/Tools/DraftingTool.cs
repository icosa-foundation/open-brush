// Copyright 2021 The Tilt Brush Authors
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

    public partial class DraftingTool : BaseTool
    {

        public static DraftingTool m_Instance { get; private set; }

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

        [SerializeField]
        private GrabWidget MarkerPointPrefab;

        [SerializeField]
        private Transform m_cursorTransform;

        private bool m_GridSnapActive;

        public override bool BlockPinCushion()
        {
            return m_brushTrigger || m_wandTrigger;
        }

        private void UpdateInputs()
        {
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

            bool depthGuideVisible = DepthGuide.m_instance && DepthGuide.m_instance.isActiveAndEnabled;

            m_GridSnapActive = depthGuideVisible && m_brushUndoButtonHeld;

        }

        public override void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);

            if (bEnable)
            {
                m_Instance = this;
            }
        }

        public override void UpdateTool()
        {
            UpdateInputs();

            base.UpdateTool();

            Vector3 cursorPos = InputManager.m_Instance.GetBrushControllerAttachPoint().position;
            m_cursorTransform.position = m_GridSnapActive ? FreePaintTool.SnapToGrid(cursorPos) : cursorPos;

            if (m_brushTriggerDown)
                AddMarkerPoint(m_cursorTransform);
        }

        public static void AddMarkerPoint(Transform transform)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
              new CreateWidgetCommand(m_Instance.MarkerPointPrefab, TrTransform.FromTransform(transform))
              );
            // SketchControlsScript.m_Instance.EatGazeObjectInput();
            // SelectionManager.m_Instance.RemoveFromSelection(false);

        }

    }

}
