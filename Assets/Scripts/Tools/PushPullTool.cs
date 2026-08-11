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
        private sealed class SculptContactState
        {
            public float LastApplicationTime;
            public readonly List<int> ControlPointIndices = new();
        }

        private const float k_ReferenceUpdatesPerSecond = 90f;
        private const float k_MaxContinuousStepSeconds = 0.1f;

        /// Keeps track of the first sculpting change made while the trigger is held.
        private bool m_AtLeastOneModificationMade = false;
        private bool m_OwnsUndoGroup;
        private readonly Dictionary<Stroke, ModifyStrokePointsCommand> m_ActiveSculptCommands = new();
        private readonly Dictionary<Stroke, SculptContactState> m_SculptContacts = new();
        private readonly List<Stroke> m_ExpiredSculptContacts = new();
        /// Determines whether the tool is in push mode or pull mode.
        /// Corresponds to the On/Off state
        private bool m_bIsPushing = true;
        /// This holds a GameObject that represents the currently active sub-tool, inside
        /// the existing sculpting sphere. These can be used for further finetuning
        /// vertex interactions, and also just for visual representations for the
        /// user.
        [SerializeField]
        public BaseSculptSubTool m_ActiveSubTool;

        public override void EnableTool(bool bEnable)
        {
            if (!bEnable)
            {
                EndOwnedUndoGroup();
            }
            // Call this after setting up our tool's state.
            base.EnableTool(bEnable);
            // CTODO: change the material of all strokes to some wireframe shader.
            HideTool(!bEnable);
        }

        public override void HideTool(bool bHide)
        {
            if (bHide)
            {
                EndOwnedUndoGroup();
            }
            m_ActiveSubTool.gameObject.SetActive(!bHide);
            base.HideTool(bHide);
        }

        protected override bool IsOn()
        {
            return m_bIsPushing;
        }

        public void SetSubTool(BaseSculptSubTool subTool)
        {
            // Disable old subtool
            m_ActiveSubTool.gameObject.SetActive(false);
            m_ActiveSubTool = subTool;
            m_ActiveSubTool.gameObject.SetActive(!m_ToolHidden);
            m_SculptContacts.Clear();
        }

        public void FinalizeSculptingBatch()
        {
            m_AtLeastOneModificationMade = false;
        }

        public override void OnUpdateDetection()
        {
            if (!m_CurrentlyHot && m_ToolWasHot)
            {
                FinalizeSculptingBatch();
                ResetToolRotation();
                ClearGpuFutureLists();
                m_SculptContacts.Clear();
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            {
                m_ActiveSculptCommands.Clear();
                m_SculptContacts.Clear();
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
                m_ActiveSculptCommands.Clear();
                m_SculptContacts.Clear();
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

            if (m_CurrentlyHot && m_SculptContacts.Count > 0)
            {
                ExpireSculptContacts();
            }
        }

        protected override void OnAnimationSwitch()
        {
            // AudioManager.m_Instance.PlayToggleSelect(m_ToolTransform.position, true);
            InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, m_HapticsToggleOn);
        }

        protected override bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup)
        {
            // Metadata of target stroke
            var stroke = rGroup.m_Stroke;
            var newControlPoints = stroke.m_ControlPoints.ToArray();
            float now = Time.realtimeSinceStartup;
            m_SculptContacts.TryGetValue(stroke, out SculptContactState contactState);
            float continuousStrengthScale = 1f;
            if (m_ActiveSubTool.UsesContinuousStrength)
            {
                float elapsed = contactState != null
                    ? Mathf.Clamp(
                        now - contactState.LastApplicationTime, 0f, k_MaxContinuousStepSeconds)
                    : Mathf.Min(Time.unscaledDeltaTime, k_MaxContinuousStepSeconds);
                continuousStrengthScale = elapsed * k_ReferenceUpdatesPerSecond;
            }
            contactState ??= new SculptContactState();
            contactState.ControlPointIndices.Clear();

            // Tool position adjusted by canvas transformations
            bool strokeIsModified = false;
            float radius = GetSize() / m_CurrentCanvas.Pose.scale;
            Vector3 toolPosition = m_CurrentCanvas.Pose.inverse * m_ToolTransform.position;
            bool isActive = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
            float pressure = StrokeSculptInfluence.CalculatePressure(
                InputManager.Brush.GetTriggerRatio(), isActive);
            for (int i = 0; i < stroke.m_ControlPoints.Length; i++)
            {
                var newControlPoint = newControlPoints[i];
                float distance = Vector3.Distance(newControlPoint.m_Pos, toolPosition);
                float influence = StrokeSculptInfluence.CalculateRadialWeight(distance, radius);

                if (influence > 0f && m_ActiveSubTool.IsInReach(newControlPoint.m_Pos, m_CurrentCanvas.Pose))
                {
                    float strength = m_ActiveSubTool.CalculateStrength(
                        newControlPoint.m_Pos, distance, m_CurrentCanvas.Pose, m_bIsPushing);
                    if (strength != 0f)
                    {
                        Vector3 direction = m_ActiveSubTool.CalculateDirection(
                            newControlPoint.m_Pos, m_ToolTransform, m_CurrentCanvas.Pose,
                            m_bIsPushing, rGroup);
                        newControlPoint.m_Pos +=
                            direction * strength * influence * pressure * continuousStrengthScale;
                        strokeIsModified = true;
                        newControlPoints[i] = newControlPoint;
                        contactState.ControlPointIndices.Add(i);
                    }
                }
            }

            if (strokeIsModified)
            {
                PlayModifyStrokeSound();
                var undoParent = ApiManager.Instance.ActiveUndo;
                ModifyStrokePointsCommand cmd;
                if (undoParent == null)
                {
                    cmd = new ModifyStrokePointsCommand(stroke, newControlPoints);
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
                }
                else
                {
                    if (!m_ActiveSculptCommands.TryGetValue(stroke, out cmd))
                    {
                        cmd = new ModifyStrokePointsCommand(stroke, newControlPoints, undoParent);
                        m_ActiveSculptCommands.Add(stroke, cmd);
                    }
                    else
                    {
                        cmd.UpdateEndPoints(newControlPoints);
                    }
                    // Apply immediately while keeping this command in the active undo group.
                    cmd.Redo();
                }
                m_AtLeastOneModificationMade = true;
                contactState.LastApplicationTime = now;
                m_SculptContacts[stroke] = contactState;
            }
            else
            {
                m_SculptContacts.Remove(stroke);
            }

            return strokeIsModified;
        }

        public override void AssignControllerMaterials(InputManager.ControllerName controller)
        {
            if (m_ActiveSubTool.m_SubToolIdentifier != SculptSubToolManager.SubTool.Flatten)
            {
                InputManager.Brush.Geometry.ShowSculptToggle(m_bIsPushing);
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
            m_ActiveSculptCommands.Clear();
            m_SculptContacts.Clear();
        }

        private void ExpireSculptContacts()
        {
            m_ExpiredSculptContacts.Clear();
            foreach (KeyValuePair<Stroke, SculptContactState> contact in m_SculptContacts)
            {
                if (!HasSculptContact(contact.Key, contact.Value.ControlPointIndices))
                {
                    m_ExpiredSculptContacts.Add(contact.Key);
                }
            }
            foreach (Stroke stroke in m_ExpiredSculptContacts)
            {
                m_SculptContacts.Remove(stroke);
            }
            m_ExpiredSculptContacts.Clear();
        }

        private bool HasSculptContact(Stroke stroke, List<int> controlPointIndices)
        {
            if (stroke?.m_ControlPoints == null || m_CurrentCanvas == null)
            {
                return false;
            }

            TrTransform canvasPose = m_CurrentCanvas.Pose;
            Vector3 toolPosition = canvasPose.inverse * m_ToolTransform.position;
            float radius = GetSize() / canvasPose.scale;
            foreach (int index in controlPointIndices)
            {
                if (index < 0 || index >= stroke.m_ControlPoints.Length)
                {
                    continue;
                }
                PointerManager.ControlPoint controlPoint = stroke.m_ControlPoints[index];
                float distance = Vector3.Distance(controlPoint.m_Pos, toolPosition);
                if (distance <= radius &&
                    m_ActiveSubTool.CalculateStrength(
                        controlPoint.m_Pos, distance, canvasPose, m_bIsPushing) != 0 &&
                    m_ActiveSubTool.IsInReach(controlPoint.m_Pos, canvasPose))
                {
                    return true;
                }
            }
            return false;
        }
    }
} // namespace TiltBrush
