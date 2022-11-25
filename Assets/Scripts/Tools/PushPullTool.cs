// Copyright 2022 Chingiz Dadashov-Khandan
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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class PushPullTool : ToggleStrokeModificationTool
    {
        /// Keeps track of the first sculpting change made while the trigger is held.
        private bool m_AtLeastOneModificationMade = false;
        /// Determines whether the tool is in push mode or pull mode.
        /// Corresponds to the On/Off state
        private bool m_bIsPushing = true;
        /// This holds a GameObject that represents the currently active sub-tool, inside
        /// the existing sculpting sphere. These can be used for further finetuning
        /// vertex interactions, and also just for visual representations for the
        /// user.
        [SerializeField]
        public BaseSculptSubTool m_ActiveSubTool;

        override public void EnableTool(bool bEnable)
        {
            // Call this after setting up our tool's state.
            base.EnableTool(bEnable);
            // CTODO: change the material of all strokes to some wireframe shader.
            HideTool(!bEnable);
        }

        override public void HideTool(bool bHide)
        {
            m_ActiveSubTool.gameObject.SetActive(!bHide);
            base.HideTool(bHide);
        }

        override protected bool IsOn()
        {
            return m_bIsPushing;
        }

        public void SetSubTool(BaseSculptSubTool subTool)
        {
            // Disable old subtool
            m_ActiveSubTool.gameObject.SetActive(false);
            m_ActiveSubTool = subTool;
        }

        public void FinalizeSculptingBatch()
        {
            m_AtLeastOneModificationMade = false;
        }


        override public void OnUpdateDetection()
        {
            if (!m_CurrentlyHot && m_ToolWasHot)
            {
                FinalizeSculptingBatch();
                ResetToolRotation();
                ClearGpuFutureLists();
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.TogglePushPull))
            {
                if (m_ActiveSubTool.m_SubToolIdentifier != SculptSubToolManager.SubTool.Flatten)
                {
                    m_bIsPushing = !m_bIsPushing;
                    StartToggleAnimation();
                }
                // CTODO: custom feature for Flattening?
            }
        }

        override protected void OnAnimationSwitch()
        {
            // AudioManager.m_Instance.PlayToggleSelect(m_ToolTransform.position, true);
            InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);
        }

        override protected bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup)
        {
            // Metadata of target stroke
            var stroke = rGroup.m_Stroke;
            var newControlPoints = stroke.m_ControlPoints.ToArray();

            // Tool position adjusted by canvas transformations
            bool strokeIsModified = false;
            for (int i = 0; i < stroke.m_ControlPoints.Length; i++)
            {
                var newControlPoint = newControlPoints[i];
                float distance = Vector3.Distance(newControlPoint.m_Pos, m_CurrentCanvas.Pose.inverse * m_ToolTransform.position);
                float strength = m_ActiveSubTool.CalculateStrength(newControlPoint.m_Pos, distance, m_CurrentCanvas.Pose, m_bIsPushing);

                if (distance <= GetSize() / m_CurrentCanvas.Pose.scale && strength != 0 && m_ActiveSubTool.IsInReach(newControlPoint.m_Pos, m_CurrentCanvas.Pose))
                {
                    Vector3 direction = m_ActiveSubTool.CalculateDirection(newControlPoint.m_Pos, m_ToolTransform, m_CurrentCanvas.Pose, m_bIsPushing, rGroup);
                    newControlPoint.m_Pos += direction * strength;
                    InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);
                    strokeIsModified = true;
                    newControlPoints[i] = newControlPoint;
                }
            }

            if (strokeIsModified)
            {
                PlayModifyStrokeSound();
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new ModifyStrokePointsCommand(stroke, newControlPoints)
                );
                m_AtLeastOneModificationMade = true;
            }
            return true;
        }

        override public void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            if (m_ActiveSubTool.m_SubToolIdentifier != SculptSubToolManager.SubTool.Flatten)
            {
                InputManager.Brush.Geometry.ShowSculptToggle(m_bIsPushing);
            }
        }

    }

} // namespace TiltBrush
