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
    /// Thumbstick press cycles through modes: Replace → Multiply → Add → Clear.
    public class TintColorTool : ToggleStrokeModificationTool
    {
        private enum TintMode
        {
            Replace,
            Multiply,
            Add,
            Clear
        }

        private static readonly TintMode[] k_ModeOrder =
        {
            TintMode.Replace,
            // TintMode.Multiply,
            // TintMode.Add,
            TintMode.Clear
        };

        [SerializeField] private Texture2D m_IconReplace;
        [SerializeField] private Texture2D m_IconMultiply;
        [SerializeField] private Texture2D m_IconAdd;
        [SerializeField] private Texture2D m_IconClear;

        private TintMode m_CurrentMode = TintMode.Replace;

        protected override bool IsOn()
        {
            return m_CurrentMode != TintMode.Clear;
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
                int idx = System.Array.IndexOf(k_ModeOrder, m_CurrentMode);
                m_CurrentMode = k_ModeOrder[(idx + 1) % k_ModeOrder.Length];
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
            bool applyingTint = m_CurrentMode != TintMode.Clear;
            bool strokeIsModified = false;
            Color tintColor = PointerManager.m_Instance.PointerColor;
            Color baseColor = stroke.m_Color;
            float pressure = InputManager.Brush.GetTriggerRatio();
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
                    // Identity depends on mode: Replace=baseColor, Multiply=white, Add=black
                    Color identity = m_CurrentMode == TintMode.Multiply ? Color.white
                        : m_CurrentMode == TintMode.Add ? Color.black
                        : baseColor;
                    Color existing = newOverrideColors[i].HasValue
                        ? (Color)newOverrideColors[i].Value
                        : identity;
                    // Lerp RGB only — preserve alpha (used by QuillFlatBrush for per-vertex opacity)
                    Color32 blended = Color.Lerp(existing, tintColor, pressure);
                    blended.a = newOverrideColors[i].HasValue
                        ? newOverrideColors[i].Value.a
                        : (byte)255;
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

            ColorOverrideMode targetMode = stroke.m_ColorOverrideMode;
            if (applyingTint)
            {
                ColorOverrideMode desiredMode = ModeToColorOverrideMode(m_CurrentMode);
                if (targetMode != desiredMode)
                {
                    targetMode = desiredMode;
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
                InputManager.Brush.Geometry.ShowTintMode(
                    m_CurrentMode != TintMode.Clear, GetIconForMode(m_CurrentMode));
            }
        }

        private Texture2D GetIconForMode(TintMode mode)
        {
            switch (mode)
            {
                case TintMode.Replace: return m_IconReplace;
                case TintMode.Multiply: return m_IconMultiply;
                case TintMode.Add: return m_IconAdd;
                case TintMode.Clear: return m_IconClear;
                default: return null;
            }
        }

        private static ColorOverrideMode ModeToColorOverrideMode(TintMode mode)
        {
            switch (mode)
            {
                case TintMode.Replace: return ColorOverrideMode.Replace;
                case TintMode.Multiply: return ColorOverrideMode.Multiply;
                case TintMode.Add: return ColorOverrideMode.Add;
                default: return ColorOverrideMode.None;
            }
        }
    }
} // namespace TiltBrush
