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
using System.Threading.Tasks;
using TiltBrush;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace OpenBrush.Multiplayer
{
    public enum ManualColocationState
    {
        Unavailable,
        OwnerCanSetReference,
        CapturingStart,
        ReferenceAvailable,
        Aligned,
        AlignmentStale,
        Error,
    }

    public class ManualColocationManager : MonoBehaviour
    {
        public static ManualColocationManager m_Instance;

        public event Action<ManualColocationReference> ReferenceChanged;
        public event Action<ManualColocationState> LocalStateChanged;
        public event Action<ManualColocationSolveResult> AlignmentApplied;

        public ManualColocationReference CurrentReference => m_CurrentReference;
        public ManualColocationState State => m_State;
        public bool HasReference => m_CurrentReference.IsValid;

        private ManualColocationReference m_CurrentReference;
        private ManualColocationReference m_CaptureReference;
        private ManualColocationState m_State = ManualColocationState.Unavailable;
        private BaseTool.ToolType m_PreviousToolType;
        private ManualColocationTool m_CaptureTool;
        private bool m_IsApplyingPose;
        private bool m_IsCaptureActive;
        private bool m_IsCapturingForOwner;
        private uint m_LatestRevision;

        private void Awake()
        {
            m_Instance = this;
        }

        private void Start()
        {
            if (App.Scene != null)
            {
                App.Scene.PoseChanged += OnScenePoseChanged;
            }
            if (MultiplayerManager.m_Instance != null)
            {
                MultiplayerManager.m_Instance.StateUpdated += OnConnectionStateChanged;
                MultiplayerManager.m_Instance.RoomOwnershipUpdated += OnRoomOwnershipUpdated;
            }
            RefreshState();
        }

        private void OnDestroy()
        {
            if (App.Scene != null)
            {
                App.Scene.PoseChanged -= OnScenePoseChanged;
            }
            if (MultiplayerManager.m_Instance != null)
            {
                MultiplayerManager.m_Instance.StateUpdated -= OnConnectionStateChanged;
                MultiplayerManager.m_Instance.RoomOwnershipUpdated -= OnRoomOwnershipUpdated;
            }
            if (m_Instance == this)
            {
                m_Instance = null;
            }
        }

        public void BeginAlignmentWorkflow()
        {
            MultiplayerManager multiplayer = MultiplayerManager.m_Instance;
            if (multiplayer == null || multiplayer.State != ConnectionState.IN_ROOM)
            {
                SetState(ManualColocationState.Unavailable);
                return;
            }

            bool isOwner = multiplayer.IsUserRoomOwner();
            if (!isOwner && !m_CurrentReference.IsValid)
            {
                SetState(ManualColocationState.Unavailable);
                return;
            }

            SketchSurfacePanel surface = SketchSurfacePanel.m_Instance;
            if (surface == null)
            {
                SetError("Sketch surface panel is unavailable.");
                return;
            }

            m_CaptureTool =
                surface.GetToolOfType(BaseTool.ToolType.ManualColocationTool)
                as ManualColocationTool;
            if (m_CaptureTool == null)
            {
                GameObject toolObject = new GameObject("Manual Colocation Tool");
                toolObject.transform.SetParent(surface.transform, false);
                m_CaptureTool = toolObject.AddComponent<ManualColocationTool>();
                m_CaptureTool.m_Type = BaseTool.ToolType.ManualColocationTool;
                if (!surface.RegisterRuntimeTool(m_CaptureTool))
                {
                    Destroy(toolObject);
                    m_CaptureTool = null;
                    SetError("Manual colocation tool could not be registered.");
                    return;
                }
            }

            m_PreviousToolType = surface.ActiveToolType;
            m_IsCapturingForOwner = isOwner;
            m_IsCaptureActive = true;
            if (!isOwner)
            {
                m_CaptureReference = m_CurrentReference;
            }
            m_CaptureTool.BeginCapture(
                OnCaptureCompleted,
                CancelCapture,
                GetCaptureFeedback);
            surface.EnableSpecificTool(BaseTool.ToolType.ManualColocationTool);
            SetState(ManualColocationState.CapturingStart);
            Debug.Log(
                $"[ManualColocation] Capture started. Owner={m_IsCapturingForOwner}");
        }

        public void CancelCapture()
        {
            m_IsCaptureActive = false;
            RestorePreviousTool();
            RefreshState();
            Debug.Log("[ManualColocation] Capture cancelled.");
        }

        public async Task<bool> ClearReference()
        {
            MultiplayerManager multiplayer = MultiplayerManager.m_Instance;
            if (multiplayer == null ||
                multiplayer.State != ConnectionState.IN_ROOM ||
                !multiplayer.IsUserRoomOwner())
            {
                return false;
            }

            var clearedReference = new ManualColocationReference
            {
                IsValid = false,
                Revision = NextRevision(),
                CreatorPlayerId = multiplayer.LocalPlayerId,
            };
            ApplyReceivedReference(clearedReference);
            bool sent = await multiplayer.PublishManualColocationReference(
                clearedReference);
            if (!sent)
            {
                SetError("Failed to publish the cleared alignment reference.");
            }
            return sent;
        }

        public void ApplyReceivedReference(ManualColocationReference reference)
        {
            if (reference.Revision <= m_LatestRevision)
            {
                Debug.Log(
                    $"[ManualColocation] Ignored duplicate or old reference revision {reference.Revision}; current={m_LatestRevision}.");
                return;
            }

            if (reference.IsValid)
            {
                if (float.IsNaN(reference.SceneScale) ||
                    float.IsInfinity(reference.SceneScale) ||
                    reference.SceneScale <
                        ManualColocationSolver.MinSceneScale ||
                    reference.SceneScale >
                        ManualColocationSolver.MaxSceneScale)
                {
                    Debug.LogWarning(
                        $"[ManualColocation] Rejected revision {reference.Revision} with invalid scale.");
                    return;
                }

                ManualColocationValidationError referenceError =
                    ManualColocationSolver.ValidateMeasurement(
                        reference.Start_SS * reference.SceneScale,
                        reference.End_SS * reference.SceneScale);
                if (referenceError !=
                    ManualColocationValidationError.None)
                {
                    Debug.LogWarning(
                        $"[ManualColocation] Rejected revision {reference.Revision}: {referenceError}.");
                    return;
                }
            }

            bool wasLocallyAligned = m_State == ManualColocationState.Aligned;
            bool cancelParticipantCapture =
                m_IsCaptureActive &&
                !m_IsCapturingForOwner &&
                reference.Revision != m_CaptureReference.Revision;
            m_LatestRevision = reference.Revision;
            m_CurrentReference = reference;

            if (cancelParticipantCapture)
            {
                m_IsCaptureActive = false;
                RestorePreviousTool();
                Debug.Log(
                    $"[ManualColocation] Capture cancelled because the reference changed from revision {m_CaptureReference.Revision} to {reference.Revision}.");
            }

            ReferenceChanged?.Invoke(reference);

            if (!reference.IsValid)
            {
                SetState(IsLocalUserOwner()
                    ? ManualColocationState.OwnerCanSetReference
                    : ManualColocationState.Unavailable);
            }
            else if (wasLocallyAligned)
            {
                SetState(ManualColocationState.AlignmentStale);
            }
            else
            {
                SetState(ManualColocationState.ReferenceAvailable);
            }

            Debug.Log(
                $"[ManualColocation] Reference revision {reference.Revision} received. Valid={reference.IsValid}");
        }

        public void ResetForRoomLifecycle()
        {
            m_IsCaptureActive = false;
            RestorePreviousTool();
            m_CurrentReference = default;
            m_CaptureReference = default;
            m_LatestRevision = 0;
            m_IsCapturingForOwner = false;
            ReferenceChanged?.Invoke(m_CurrentReference);
            SetState(ManualColocationState.Unavailable);
            Debug.Log("[ManualColocation] Room state reset.");
        }

        public void OnLocalPlayerJoinedRoom()
        {
            RefreshState();
        }

        public void OnBecameRoomOwner()
        {
            RefreshState();
        }

        private async void OnCaptureCompleted(Vector3 start_RS, Vector3 end_RS)
        {
            bool wasCaptureActive = m_IsCaptureActive;
            ManualColocationReference captureReference = m_CaptureReference;
            m_IsCaptureActive = false;
            RestorePreviousTool();

            if (!wasCaptureActive)
            {
                return;
            }

            MultiplayerManager multiplayer = MultiplayerManager.m_Instance;
            if (multiplayer == null || multiplayer.State != ConnectionState.IN_ROOM)
            {
                SetError("The multiplayer room changed during capture.");
                return;
            }

            if (m_IsCapturingForOwner)
            {
                if (!multiplayer.IsUserRoomOwner())
                {
                    SetError("Room ownership changed during capture.");
                    return;
                }

                ManualColocationValidationError error =
                    ManualColocationSolver.TryCreateReference(
                        App.Scene.Pose,
                        start_RS,
                        end_RS,
                        NextRevision(),
                        multiplayer.LocalPlayerId,
                        out ManualColocationReference reference);
                if (error != ManualColocationValidationError.None)
                {
                    SetError($"Owner reference validation failed: {error}");
                    return;
                }

                m_CurrentReference = reference;
                m_LatestRevision = reference.Revision;
                ReferenceChanged?.Invoke(reference);
                SetState(ManualColocationState.Aligned);
                bool sent =
                    await multiplayer.PublishManualColocationReference(reference);
                if (!sent)
                {
                    SetError("Failed to publish the alignment reference.");
                    return;
                }
                Debug.Log(
                    $"[ManualColocation] Owner published reference revision {reference.Revision}.");
                return;
            }

            if (!m_CurrentReference.IsValid ||
                m_CurrentReference.Revision != captureReference.Revision)
            {
                SetError(
                    $"The room alignment reference changed during capture (started={captureReference.Revision}, current={m_CurrentReference.Revision}).");
                return;
            }

            ManualColocationSolveResult result =
                ManualColocationSolver.TrySolve(
                    captureReference, start_RS, end_RS);
            if (!result.Success)
            {
                SetError($"Alignment solve failed: {result.Error}");
                return;
            }

            TrTransform solvedPose = result.ScenePose;
            TrTransform sanitizedPose = App.Scene.SanitizePose(result.ScenePose);
            result.ScenePose = sanitizedPose;

            Vector3 solvedStart_RS =
                solvedPose.MultiplyPoint(captureReference.Start_SS);
            Vector3 solvedEnd_RS =
                solvedPose.MultiplyPoint(captureReference.End_SS);
            Vector3 mappedStart_RS =
                sanitizedPose.MultiplyPoint(captureReference.Start_SS);
            Vector3 mappedEnd_RS =
                sanitizedPose.MultiplyPoint(captureReference.End_SS);
            float sanitizationDisplacementMeters = Mathf.Max(
                Vector3.Distance(solvedStart_RS, mappedStart_RS),
                Vector3.Distance(solvedEnd_RS, mappedEnd_RS)) *
                App.UNITS_TO_METERS;
            if (sanitizationDisplacementMeters >
                ManualColocationSolver.EndpointResidualMetersWarning)
            {
                result.Warnings |= ManualColocationWarning.EndpointResidual;
                SetError(
                    $"Alignment exceeds the scene bounds ({sanitizationDisplacementMeters:F3} m adjustment).");
                Debug.LogWarning(
                    $"[ManualColocationBounds] Rejected pose for revision {captureReference.Revision}; adjustment={sanitizationDisplacementMeters:F3} m.");
                return;
            }

            result.EndpointResidualMeters = Mathf.Max(
                Vector3.Distance(mappedStart_RS, start_RS),
                Vector3.Distance(mappedEnd_RS, end_RS)) *
                App.UNITS_TO_METERS;

            m_IsApplyingPose = true;
            try
            {
                App.Scene.Pose = sanitizedPose;
                result.ScenePose = App.Scene.Pose;
            }
            finally
            {
                m_IsApplyingPose = false;
            }

            SetState(ManualColocationState.Aligned);
            AlignmentApplied?.Invoke(result);
            Debug.Log(
                $"[ManualColocation] Pose applied. Revision={captureReference.Revision}, yaw={result.YawDegrees:F2}, residual={result.EndpointResidualMeters:F3}, warnings={result.Warnings}");
        }

        private ManualColocationCaptureFeedback GetCaptureFeedback(
            Vector3 start_RS,
            Vector3 end_RS)
        {
            string controls = $" {Localize("MP_MANUAL_COLOCATION_CONFIRM_CONTROLS")}";

            if (m_IsCapturingForOwner)
            {
                ManualColocationValidationError error =
                    ManualColocationSolver.TryCreateReference(
                        App.Scene.Pose,
                        start_RS,
                        end_RS,
                        NextRevision(),
                        MultiplayerManager.m_Instance?.LocalPlayerId ?? -1,
                        out _);
                if (error != ManualColocationValidationError.None)
                {
                    return new ManualColocationCaptureFeedback
                    {
                        CanConfirm = false,
                        Message = string.Format(
                            Localize(
                                "MP_MANUAL_COLOCATION_REFERENCE_INVALID"),
                            error),
                    };
                }

                return new ManualColocationCaptureFeedback
                {
                    CanConfirm = true,
                    Message = $"{string.Format(Localize("MP_MANUAL_COLOCATION_REFERENCE_LENGTH"), Vector3.Distance(start_RS, end_RS) * App.UNITS_TO_METERS)}{controls}",
                };
            }

            ManualColocationSolveResult result =
                ManualColocationSolver.TrySolve(
                    m_CaptureReference, start_RS, end_RS);
            if (!result.Success)
            {
                return new ManualColocationCaptureFeedback
                {
                    CanConfirm = false,
                    Message = string.Format(
                        Localize(
                            "MP_MANUAL_COLOCATION_ALIGNMENT_INVALID"),
                        result.Error),
                };
            }

            string warning = result.Warnings == ManualColocationWarning.None
                ? string.Empty
                : $" {string.Format(Localize("MP_MANUAL_COLOCATION_WARNING"), result.Warnings)}";
            return new ManualColocationCaptureFeedback
            {
                CanConfirm = true,
                Message = $"{string.Format(Localize("MP_MANUAL_COLOCATION_MEASURED_LENGTH"), result.LocalLengthMeters)}{warning}{controls}",
            };
        }

        private void RestorePreviousTool()
        {
            if (m_CaptureTool == null)
            {
                return;
            }

            SketchSurfacePanel surface = SketchSurfacePanel.m_Instance;
            m_CaptureTool = null;
            if (surface != null)
            {
                surface.EnableSpecificTool(m_PreviousToolType);
            }
        }

        private void OnScenePoseChanged(TrTransform previous, TrTransform current)
        {
            if (m_IsApplyingPose || m_State != ManualColocationState.Aligned)
            {
                return;
            }

            if (IsLocalUserOwner() && m_CurrentReference.IsValid)
            {
                _ = ClearReference();
                Debug.Log(
                    "[ManualColocation] Owner scene pose changed; the shared reference was invalidated.");
            }
            else
            {
                SetState(ManualColocationState.AlignmentStale);
                Debug.Log(
                    "[ManualColocation] Local scene pose changed; alignment is stale.");
            }
        }

        private void OnConnectionStateChanged(ConnectionState state)
        {
            if (state != ConnectionState.IN_ROOM)
            {
                ResetForRoomLifecycle();
            }
            else
            {
                RefreshState();
            }
        }

        private void OnRoomOwnershipUpdated(bool isOwner)
        {
            if (isOwner)
            {
                OnBecameRoomOwner();
            }
            else
            {
                RefreshState();
            }
        }

        private void RefreshState()
        {
            MultiplayerManager multiplayer = MultiplayerManager.m_Instance;
            if (multiplayer == null || multiplayer.State != ConnectionState.IN_ROOM)
            {
                SetState(ManualColocationState.Unavailable);
            }
            else if (m_CurrentReference.IsValid)
            {
                if (m_State != ManualColocationState.Aligned &&
                    m_State != ManualColocationState.AlignmentStale)
                {
                    SetState(ManualColocationState.ReferenceAvailable);
                }
            }
            else if (multiplayer.IsUserRoomOwner())
            {
                SetState(ManualColocationState.OwnerCanSetReference);
            }
            else
            {
                SetState(ManualColocationState.Unavailable);
            }
        }

        private void SetState(ManualColocationState state)
        {
            if (m_State == state)
            {
                return;
            }
            m_State = state;
            LocalStateChanged?.Invoke(state);
        }

        private void SetError(string message)
        {
            Debug.LogError($"[ManualColocation] {message}");
            SetState(ManualColocationState.Error);
        }

        private uint NextRevision()
        {
            return m_LatestRevision == uint.MaxValue ? 1 : m_LatestRevision + 1;
        }

        private bool IsLocalUserOwner()
        {
            return MultiplayerManager.m_Instance != null &&
                   MultiplayerManager.m_Instance.IsUserRoomOwner();
        }

        private static string Localize(string key)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "Strings", key);
        }

    }
}
