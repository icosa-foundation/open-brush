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
    /// Thumbstick press toggles between applying and clearing overrides.
    public class TintColorTool : ToggleStrokeModificationTool
    {
        [SerializeField] private Texture2D m_IconReplace;
        [SerializeField] private Texture2D m_IconClear;

        private bool m_ClearMode;
        private bool m_OwnsUndoGroup;
        private readonly Dictionary<Stroke, ModifyStrokePointColorsCommand> m_ActiveTintCommands = new();
        public float EffectAmount { get; set; } = 1f;

        protected override bool IsOn()
        {
            return !m_ClearMode;
        }

        public override void OnUpdateDetection()
        {
            if (!m_CurrentlyHot && m_ToolWasHot)
            {
                ResetToolRotation();
                ClearGpuFutureLists();
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                m_ActiveTintCommands.Clear();
                if (ApiManager.Instance.ActiveUndo == null)
                {
                    ApiManager.Instance.StartUndo();
                    m_OwnsUndoGroup = true;
                }
            }
            else if (m_OwnsUndoGroup && !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                EndOwnedUndoGroup();
            }
            else if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                m_ActiveTintCommands.Clear();
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleReshape))
            {
                m_ClearMode = !m_ClearMode;
                StartToggleAnimation();
            }
        }

        public override void EnableTool(bool bEnable)
        {
            if (!bEnable)
            {
                EndOwnedUndoGroup();
            }
            base.EnableTool(bEnable);
        }

        public override void HideTool(bool bHide)
        {
            if (bHide)
            {
                EndOwnedUndoGroup();
            }
            base.HideTool(bHide);
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
                return false;
            }

            List<Color32?> newOverrideColors = stroke.m_OverrideColors != null &&
                                               stroke.m_OverrideColors.Count == controlPointCount
                ? stroke.m_OverrideColors.ToList()
                : new List<Color32?>(new Color32?[controlPointCount]);
            bool applyingTint = !m_ClearMode;
            bool strokeIsModified = false;
            Color tintColor = PointerManager.m_Instance.PointerColor;
            Color baseColor = stroke.m_Color;
            float pressure = InputManager.Brush.GetTriggerRatio();
            float amount = pressure * EffectAmount * 0.5f;
            float maxDistance = GetSize() / m_CurrentCanvas.Pose.scale;
            Vector3 toolPos = m_CurrentCanvas.Pose.inverse * m_ToolTransform.position;
            ColorOverrideMode targetMode = stroke.m_ColorOverrideMode;

            for (int i = 0; i < controlPointCount; i++)
            {
                float distance = Vector3.Distance(stroke.m_ControlPoints[i].m_Pos, toolPos);
                if (distance > maxDistance)
                {
                    continue;
                }

                if (applyingTint)
                {
                    if (targetMode != ColorOverrideMode.Replace)
                    {
                        // Preserve the visible colors of untouched points before changing the
                        // stroke-wide interpretation of overrides created by another source.
                        for (int j = 0; j < newOverrideColors.Count; j++)
                        {
                            if (newOverrideColors[j].HasValue)
                            {
                                newOverrideColors[j] = stroke.GetColor(j);
                            }
                        }
                        targetMode = ColorOverrideMode.Replace;
                        strokeIsModified = true;
                    }
                    Color existing = newOverrideColors[i].HasValue
                        ? (Color)newOverrideColors[i].Value
                        : baseColor;
                    // Lerp RGB only — preserve alpha (used by QuillFlatBrush for per-vertex opacity)
                    Color32 blended = Color.Lerp(existing, tintColor, amount);
                    blended.a = newOverrideColors[i].HasValue
                        ? newOverrideColors[i].Value.a
                        : ((Color32)baseColor).a;
                    if (!newOverrideColors[i].HasValue || !newOverrideColors[i].Value.Equals(blended))
                    {
                        newOverrideColors[i] = blended;
                        strokeIsModified = true;
                    }
                }
                else if (newOverrideColors[i].HasValue)
                {
                    newOverrideColors[i] = null;
                    strokeIsModified = true;
                }
            }

            if (!applyingTint && !newOverrideColors.Any(c => c.HasValue))
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
                var undoParent = ApiManager.Instance.ActiveUndo;
                ModifyStrokePointColorsCommand cmd;
                if (undoParent == null)
                {
                    cmd = new ModifyStrokePointColorsCommand(stroke, newOverrideColors, targetMode);
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
                }
                else
                {
                    if (!m_ActiveTintCommands.TryGetValue(stroke, out cmd))
                    {
                        cmd = new ModifyStrokePointColorsCommand(
                            stroke, newOverrideColors, targetMode, undoParent);
                        m_ActiveTintCommands.Add(stroke, cmd);
                    }
                    else
                    {
                        cmd.UpdateEndState(newOverrideColors, targetMode);
                    }
                    // Apply changes immediately while keeping this command inside the active undo group.
                    cmd.Redo();
                }
                InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);
            }

            return strokeIsModified;
        }

        public override void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            if (controller == InputManager.ControllerName.Brush)
            {
                InputManager.Brush.Geometry.ShowTintMode(!m_ClearMode,
                    m_ClearMode ? m_IconClear : m_IconReplace);
            }
        }

        private void EndOwnedUndoGroup()
        {
            if (m_OwnsUndoGroup)
            {
                BaseCommand undoGroup = ApiManager.Instance.ActiveUndo;
                ApiManager.Instance.ActiveUndo = null;
                if (undoGroup != null && undoGroup.HasChildren)
                {
                    SketchSurfacePanel.m_Instance.m_LastCommand = undoGroup;
                    SketchMemoryScript.m_Instance.RecordCommand(undoGroup);
                }
                m_OwnsUndoGroup = false;
            }
            m_ActiveTintCommands.Clear();
        }
    }
} // namespace TiltBrush
