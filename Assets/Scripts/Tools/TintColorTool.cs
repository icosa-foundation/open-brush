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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Paints per-control-point color overrides inside the tool radius.
    /// Toggle mode uses the same interaction as PushPull:
    /// On = apply tint (Replace mode), Off = clear tint.
    public class TintColorTool : ToggleStrokeModificationTool
    {
        private bool m_IsApplyingTint = true;

        protected override bool IsOn()
        {
            return m_IsApplyingTint;
        }

        public override void OnUpdateDetection()
        {
            if (!m_CurrentlyHot && m_ToolWasHot)
            {
                ResetToolRotation();
                ClearGpuFutureLists();
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.TogglePushPull))
            {
                m_IsApplyingTint = !m_IsApplyingTint;
                StartToggleAnimation();
            }
        }

        protected override void OnAnimationSwitch()
        {
            InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);
        }

        protected override bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup)
        {
            var stroke = rGroup.m_Stroke;
            int controlPointCount = stroke.m_ControlPoints.Length;
            if (controlPointCount == 0)
            {
                return true;
            }

            List<Color32?> newOverrideColors = stroke.m_OverrideColors != null &&
                                               stroke.m_OverrideColors.Count == controlPointCount
                ? stroke.m_OverrideColors.ToList()
                : new List<Color32?>(new Color32?[controlPointCount]);
            bool applyingTint = m_IsApplyingTint;
            bool strokeIsModified = false;
            Color32 tintColor = PointerManager.m_Instance.PointerColor;
            float maxDistance = GetSize() / m_CurrentCanvas.Pose.scale;
            Vector3 toolPos = m_CurrentCanvas.Pose.inverse * m_ToolTransform.position;

            for (int i = 0; i < controlPointCount; i++)
            {
                float distance = Vector3.Distance(stroke.m_ControlPoints[i].m_Pos, toolPos);
                if (distance > maxDistance)
                {
                    continue;
                }

                if (applyingTint)
                {
                    if (!newOverrideColors[i].HasValue || !newOverrideColors[i].Value.Equals(tintColor))
                    {
                        newOverrideColors[i] = tintColor;
                        strokeIsModified = true;
                    }
                }
                else if (newOverrideColors[i].HasValue)
                {
                    newOverrideColors[i] = null;
                    strokeIsModified = true;
                }
            }

            ColorOverrideMode targetMode = stroke.m_ColorOverrideMode;
            if (applyingTint)
            {
                if (targetMode != ColorOverrideMode.Replace)
                {
                    targetMode = ColorOverrideMode.Replace;
                    strokeIsModified = true;
                }
            }
            else if (!newOverrideColors.Any(c => c.HasValue))
            {
                if (stroke.m_OverrideColors != null || targetMode != ColorOverrideMode.None)
                {
                    newOverrideColors = null;
                    targetMode = ColorOverrideMode.None;
                    strokeIsModified = true;
                }
            }

            if (strokeIsModified)
            {
                PlayModifyStrokeSound();
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new ModifyStrokePointColorsCommand(stroke, newOverrideColors, targetMode)
                );
                InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);
            }

            return true;
        }

        public override void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            if (controller == InputManager.ControllerName.Brush)
            {
                InputManager.Brush.Geometry.ShowSculptToggle(m_IsApplyingTint);
            }
        }
    }
} // namespace TiltBrush
