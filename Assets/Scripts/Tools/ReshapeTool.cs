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
    public class ReshapeTool : ToggleStrokeModificationTool
    {
        private sealed class SculptContactState
        {
            public float LastApplicationTime;
            public readonly List<int> ControlPointIndices = new();
            public PointerManager.ControlPoint[] TransformStartPoints;
            public float[] TransformWeights;
            public PointerManager.ControlPoint[][] TransformResultBuffers;
            public int NextTransformResultBuffer;
            public Vector3 LastTransformTranslation;
            public float LastTransformAngle;
            public bool HasAppliedTransformPose;
        }

        private const float k_ReferenceUpdatesPerSecond = 90f;
        private const float k_MaxContinuousStepSeconds = 0.1f;
        private const float k_ArcLengthFeatherRadiusRatio = 0.25f;
        private const float k_SmoothAmountPerReferenceUpdate = 0.1f;

        /// Keeps track of the first sculpting change made while the trigger is held.
        private bool m_AtLeastOneModificationMade = false;
        private bool m_OwnsUndoGroup;
        private readonly Dictionary<Stroke, ModifyStrokePointsCommand> m_ActiveSculptCommands = new();
        private readonly Dictionary<Stroke, SculptContactState> m_SculptContacts = new();
        private readonly List<Stroke> m_ExpiredSculptContacts = new();
        private float[] m_InfluenceWeights = new float[0];
        private Vector3 m_TransformStartToolPosition;
        private Quaternion m_TransformStartToolRotation;
        private Vector3 m_TwistAxis;
        private float m_TransformTwistAngle;
        private bool m_WaitingForTriggerRelease;
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
                // Hiding interrupts the current gesture and clears its captured contacts. If the
                // trigger remains held when the tool is shown again, GetCommandDown will not run
                // to establish a new Grab origin. Waiting for release prevents new contacts from
                // being transformed relative to the interrupted gesture's stale origin.
                if (InputManager.m_Instance != null &&
                    InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
                {
                    m_WaitingForTriggerRelease = true;
                }
            }
            m_ActiveSubTool.gameObject.SetActive(!bHide);
            base.HideTool(bHide);
        }

        protected override bool IsOn()
        {
            return m_bIsPushing;
        }

        public bool TryToggleFlattenInfluenceMode()
        {
            if (!(m_ActiveSubTool is FlattenSubTool flatten) ||
                m_OwnsUndoGroup || m_WaitingForTriggerRelease ||
                (InputManager.m_Instance != null && InputManager.m_Instance.GetCommand(
                    InputManager.SketchCommands.Activate)))
            {
                return false;
            }

            // Apply the mode and its preview immediately. Deferring the switch through the
            // standard toggle animation could allow a newly started gesture to change modes at
            // the animation midpoint.
            ResetToggleAnimation();
            flatten.ToggleInfluenceMode();
            m_SculptContacts.Clear();
            ResetDetection();
            UpdateMesh();
            OnAnimationSwitch();
            return true;
        }

        public void SetSubTool(BaseSculptSubTool subTool)
        {
            bool modeChanged = m_ActiveSubTool != subTool;
            if (InputManager.m_Instance != null &&
                InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                EndOwnedUndoGroup();
                m_WaitingForTriggerRelease = true;
            }

            // Disable old subtool
            m_ActiveSubTool.gameObject.SetActive(false);
            m_ActiveSubTool = subTool;
            m_ActiveSubTool.gameObject.SetActive(!m_ToolHidden);
            m_SculptContacts.Clear();
            if (modeChanged)
            {
                ResetDetection();
                // Signed modes should start in their primary state instead of inheriting the
                // previous mode's alternate state.
                m_bIsPushing = true;
                UpdateMesh();
            }
        }

        public void FinalizeSculptingBatch()
        {
            m_AtLeastOneModificationMade = false;
        }

        public override void OnUpdateDetection()
        {
            if (m_WaitingForTriggerRelease &&
                !InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                m_WaitingForTriggerRelease = false;
            }

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
                CanvasScript canvas = m_CurrentCanvas != null ? m_CurrentCanvas : App.ActiveCanvas;
                m_TransformStartToolPosition = canvas.Pose.inverse * m_ToolTransform.position;
                m_TransformStartToolRotation = m_ToolTransform.rotation;
                m_TwistAxis =
                    (Quaternion.Inverse(canvas.Pose.rotation) * m_ToolTransform.forward).normalized;
                m_TransformTwistAngle = 0f;
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

            if (IsCapturedTransformMode &&
                InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            {
                UpdateCapturedTransformAngle();
            }

            if (IsCapturedTransformMode && m_CurrentlyHot && m_SculptContacts.Count > 0)
            {
                UpdateCapturedTransformStrokes();
            }

            if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.ToggleReshape))
            {
                if (m_ActiveSubTool.SubToolIdentifier == SculptSubToolManager.SubTool.Flatten)
                {
                    TryToggleFlattenInfluenceMode();
                }
                else if (!IsCapturedTransformMode && !IsSmoothMode)
                {
                    m_bIsPushing = !m_bIsPushing;
                    StartToggleAnimation();
                }
            }

            if (!IsCapturedTransformMode && m_CurrentlyHot && m_SculptContacts.Count > 0)
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
            if (m_WaitingForTriggerRelease)
            {
                return false;
            }

            // Metadata of target stroke
            var stroke = rGroup.m_Stroke;
            if (IsCapturedTransformMode)
            {
                CaptureTransformContact(stroke);
                return false;
            }

            var newControlPoints = stroke.m_ControlPoints.ToArray();
            float now = Time.realtimeSinceStartup;
            m_SculptContacts.TryGetValue(stroke, out SculptContactState contactState);
            float continuousStrengthScale = 1f;
            float nextApplicationTime = now;
            if (m_ActiveSubTool.UsesContinuousStrength)
            {
                float previousApplicationTime = contactState != null
                    ? contactState.LastApplicationTime
                    : now - Time.unscaledDeltaTime;
                float elapsed = Mathf.Clamp(
                    now - previousApplicationTime, 0f, k_MaxContinuousStepSeconds);
                // Advance only by the time applied so any excess remains for later updates.
                nextApplicationTime = previousApplicationTime + elapsed;
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
            if (m_InfluenceWeights.Length < newControlPoints.Length)
            {
                m_InfluenceWeights = new float[newControlPoints.Length];
            }
            for (int i = 0; i < newControlPoints.Length; ++i)
            {
                m_InfluenceWeights[i] = m_ActiveSubTool.CalculateInfluence(
                    newControlPoints[i].m_Pos, toolPosition, radius, m_CurrentCanvas.Pose);
            }
            StrokeSculptInfluence.FeatherAlongStroke(
                newControlPoints, m_InfluenceWeights,
                radius * k_ArcLengthFeatherRadiusRatio, newControlPoints.Length);

            if (IsSmoothMode)
            {
                strokeIsModified = StrokeSculptInfluence.ApplySmooth(
                    stroke.m_ControlPoints, m_InfluenceWeights,
                    k_SmoothAmountPerReferenceUpdate * pressure, continuousStrengthScale,
                    newControlPoints);
                if (strokeIsModified)
                {
                    for (int i = 1; i < stroke.m_ControlPoints.Length - 1; ++i)
                    {
                        if (newControlPoints[i].m_Pos != stroke.m_ControlPoints[i].m_Pos)
                        {
                            contactState.ControlPointIndices.Add(i);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < stroke.m_ControlPoints.Length; i++)
                {
                    var newControlPoint = newControlPoints[i];
                    float distance = Vector3.Distance(newControlPoint.m_Pos, toolPosition);
                    float influence = m_InfluenceWeights[i];

                    if (influence > 0f && m_ActiveSubTool.IsInReach(
                        newControlPoint.m_Pos, m_CurrentCanvas.Pose))
                    {
                        float strength = m_ActiveSubTool.CalculateStrength(
                            newControlPoint.m_Pos, distance, radius,
                            m_CurrentCanvas.Pose, m_bIsPushing);
                        if (strength != 0f)
                        {
                            Vector3 direction = m_ActiveSubTool.CalculateDirection(
                                newControlPoint.m_Pos, m_ToolTransform, m_CurrentCanvas.Pose,
                                m_bIsPushing, rGroup);
                            float displacement = strength * influence * pressure;
                            displacement = m_ActiveSubTool.ScaleDisplacementForReferenceUpdates(
                                newControlPoint.m_Pos, displacement, continuousStrengthScale,
                                m_CurrentCanvas.Pose, m_bIsPushing);
                            displacement = m_ActiveSubTool.ConstrainDisplacement(
                                displacement, distance, m_bIsPushing);
                            newControlPoint.m_Pos += direction * displacement;
                            strokeIsModified = true;
                            newControlPoints[i] = newControlPoint;
                            contactState.ControlPointIndices.Add(i);
                        }
                    }
                }
            }

            if (strokeIsModified)
            {
                StrokeSculptInfluence.TransportOrientations(
                    stroke.m_ControlPoints, newControlPoints);
                ApplyStrokeModification(stroke, newControlPoints);
                contactState.LastApplicationTime = nextApplicationTime;
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
            if (m_ActiveSubTool is FlattenSubTool flatten)
            {
                InputManager.Brush.Geometry.ShowSculptToggle(
                    flatten.Mode == FlattenSubTool.InfluenceMode.Sphere);
            }
            else if (!IsCapturedTransformMode && !IsSmoothMode)
            {
                InputManager.Brush.Geometry.ShowSculptToggle(m_bIsPushing);
            }
        }

        protected override void UpdateMesh()
        {
            base.UpdateMesh();
            if (m_ActiveSubTool is FlattenSubTool flatten &&
                flatten.Mode == FlattenSubTool.InfluenceMode.PlaneProjected)
            {
                // Plane-projected influence extends through the target plane, so the sphere is
                // not its boundary. Leave the subtool's disc visible as the mode preview instead.
                m_OnMesh.gameObject.SetActive(false);
                m_OffMesh.gameObject.SetActive(false);
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

        private bool IsGrabMode =>
            m_ActiveSubTool.SubToolIdentifier == SculptSubToolManager.SubTool.Grab;

        private bool IsCapturedTransformMode => IsGrabMode;

        private bool IsSmoothMode =>
            m_ActiveSubTool.SubToolIdentifier == SculptSubToolManager.SubTool.Smooth;

        protected override bool UsesCustomStrokeIntersection()
        {
            return m_ActiveSubTool.SubToolIdentifier == SculptSubToolManager.SubTool.Flatten;
        }

        protected override bool StrokeIntersectsCustomDetectionVolume(Stroke stroke)
        {
            if (stroke?.m_ControlPoints == null || m_CurrentCanvas == null)
            {
                return false;
            }

            TrTransform canvasPose = m_CurrentCanvas.Pose;
            Vector3 toolPosition = canvasPose.inverse * m_ToolTransform.position;
            float radius = GetSize() / canvasPose.scale;
            foreach (PointerManager.ControlPoint controlPoint in stroke.m_ControlPoints)
            {
                if (m_ActiveSubTool.CalculateInfluence(
                        controlPoint.m_Pos, toolPosition, radius, canvasPose) > 0f &&
                    m_ActiveSubTool.IsInReach(controlPoint.m_Pos, canvasPose))
                {
                    return true;
                }
            }
            return false;
        }

        private void CaptureTransformContact(Stroke stroke)
        {
            if (stroke?.m_ControlPoints == null || m_SculptContacts.ContainsKey(stroke))
            {
                return;
            }

            if (m_InfluenceWeights.Length < stroke.m_ControlPoints.Length)
            {
                m_InfluenceWeights = new float[stroke.m_ControlPoints.Length];
            }
            float radius = GetSize() / m_CurrentCanvas.Pose.scale;
            bool hasInfluence = false;
            for (int i = 0; i < stroke.m_ControlPoints.Length; ++i)
            {
                float distance = Vector3.Distance(
                    stroke.m_ControlPoints[i].m_Pos, m_TransformStartToolPosition);
                float influence = StrokeSculptInfluence.CalculateRadialWeight(distance, radius);
                m_InfluenceWeights[i] = influence;
                hasInfluence |= influence > 0f;
            }

            if (!hasInfluence)
            {
                return;
            }

            var contactState = new SculptContactState
            {
                TransformStartPoints = stroke.m_ControlPoints.ToArray(),
                TransformWeights = new float[stroke.m_ControlPoints.Length],
                TransformResultBuffers = new[]
                {
                    new PointerManager.ControlPoint[stroke.m_ControlPoints.Length],
                    new PointerManager.ControlPoint[stroke.m_ControlPoints.Length],
                },
            };
            for (int i = 0; i < stroke.m_ControlPoints.Length; ++i)
            {
                contactState.TransformWeights[i] = m_InfluenceWeights[i];
            }
            StrokeSculptInfluence.FeatherAlongStroke(
                contactState.TransformStartPoints, contactState.TransformWeights,
                radius * k_ArcLengthFeatherRadiusRatio);
            for (int i = 0; i < contactState.TransformWeights.Length; ++i)
            {
                if (contactState.TransformWeights[i] > 0f)
                {
                    contactState.ControlPointIndices.Add(i);
                }
            }
            m_SculptContacts.Add(stroke, contactState);
        }

        private void UpdateCapturedTransformAngle()
        {
            float wrappedAngle = StrokeSculptInfluence.CalculateTwistAngle(
                m_TransformStartToolRotation, m_ToolTransform.rotation,
                m_TransformStartToolRotation * Vector3.forward);
            m_TransformTwistAngle = StrokeSculptInfluence.UnwrapAngle(
                m_TransformTwistAngle, wrappedAngle);
        }

        private void UpdateCapturedTransformStrokes()
        {
            Vector3 toolPosition = m_CurrentCanvas.Pose.inverse * m_ToolTransform.position;
            Vector3 translation = toolPosition - m_TransformStartToolPosition;
            bool anyStrokeModified = false;
            foreach (KeyValuePair<Stroke, SculptContactState> contact in m_SculptContacts)
            {
                SculptContactState state = contact.Value;
                bool poseIsUnchanged = state.HasAppliedTransformPose &&
                    state.LastTransformTranslation == translation &&
                    Mathf.Approximately(state.LastTransformAngle, m_TransformTwistAngle);
                bool poseIsAtStart = !state.HasAppliedTransformPose &&
                    translation == Vector3.zero && Mathf.Approximately(m_TransformTwistAngle, 0f);
                if (poseIsUnchanged || poseIsAtStart)
                {
                    continue;
                }

                PointerManager.ControlPoint[] newControlPoints =
                    state.TransformResultBuffers[state.NextTransformResultBuffer];
                state.NextTransformResultBuffer =
                    (state.NextTransformResultBuffer + 1) % state.TransformResultBuffers.Length;
                StrokeSculptInfluence.ApplyCapturedTransform(
                    state.TransformStartPoints, state.TransformWeights,
                    m_TransformStartToolPosition, translation, m_TwistAxis,
                    m_TransformTwistAngle,
                    newControlPoints);
                ApplyStrokeModification(contact.Key, newControlPoints);
                state.LastTransformTranslation = translation;
                state.LastTransformAngle = m_TransformTwistAngle;
                state.HasAppliedTransformPose = true;
                anyStrokeModified = true;
            }

            if (anyStrokeModified)
            {
                IntersectionHappenedThisFrame();
            }
        }

        private void ApplyStrokeModification(
            Stroke stroke, PointerManager.ControlPoint[] newControlPoints)
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
                if (m_ActiveSubTool.CalculateInfluence(
                        controlPoint.m_Pos, toolPosition, radius, canvasPose) > 0f &&
                    m_ActiveSubTool.CalculateStrength(
                        controlPoint.m_Pos, distance, radius, canvasPose, m_bIsPushing) != 0 &&
                    m_ActiveSubTool.IsInReach(controlPoint.m_Pos, canvasPose))
                {
                    return true;
                }
            }
            return false;
        }
    }
} // namespace TiltBrush
