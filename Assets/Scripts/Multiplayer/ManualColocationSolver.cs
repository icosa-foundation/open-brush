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
using TiltBrush;
using UnityEngine;

namespace OpenBrush.Multiplayer
{
    public enum ManualColocationValidationError
    {
        None,
        ReferenceUnavailable,
        NonFiniteInput,
        InvalidScale,
        LineTooShort,
        InsufficientHorizontalSpan,
    }

    [Flags]
    public enum ManualColocationWarning
    {
        None = 0,
        LengthMismatch = 1 << 0,
        HeightMismatch = 1 << 1,
        PossibleEndpointReversal = 1 << 2,
        EndpointResidual = 1 << 3,
    }

    public struct ManualColocationSolveResult
    {
        public bool Success;
        public TrTransform ScenePose;
        public ManualColocationValidationError Error;
        public ManualColocationWarning Warnings;
        public float ReferenceLengthMeters;
        public float LocalLengthMeters;
        public float LengthMismatchMeters;
        public float LengthMismatchRatio;
        public float HorizontalSpanMeters;
        public float YawDegrees;
        public float EndpointResidualMeters;
    }

    /// Computes the upright scene-to-room transform for a two-point manual
    /// colocation reference. This class deliberately has no scene, UI, or
    /// networking dependencies so the alignment math can be tested in isolation.
    public static class ManualColocationSolver
    {
        public const float MinLineLengthMeters = 0.1f;
        public const float MinHorizontalSpanMeters = 0.5f;
        public const float LengthMismatchMetersWarning = 0.05f;
        public const float LengthMismatchRatioWarning = 0.1f;
        public const float HeightMismatchMetersWarning = 0.05f;
        public const float EndpointResidualMetersWarning = 0.05f;
        public const float EndpointReversalDegreesWarning = 170f;
        public const float MinSceneScale = 1e-4f;
        public const float MaxSceneScale = 1e4f;

        public static ManualColocationValidationError ValidateMeasurement(
            Vector3 start_RS,
            Vector3 end_RS,
            float minimumHorizontalSpanMeters = MinHorizontalSpanMeters)
        {
            if (!IsFinite(start_RS) || !IsFinite(end_RS))
            {
                return ManualColocationValidationError.NonFiniteInput;
            }

            Vector3 delta = end_RS - start_RS;
            float lengthMeters = delta.magnitude * App.UNITS_TO_METERS;
            if (lengthMeters < MinLineLengthMeters)
            {
                return ManualColocationValidationError.LineTooShort;
            }

            float horizontalSpanMeters =
                Vector3.ProjectOnPlane(delta, Vector3.up).magnitude *
                App.UNITS_TO_METERS;
            if (horizontalSpanMeters < minimumHorizontalSpanMeters)
            {
                return ManualColocationValidationError.InsufficientHorizontalSpan;
            }

            return ManualColocationValidationError.None;
        }

        public static ManualColocationValidationError TryCreateReference(
            TrTransform ownerScenePose,
            Vector3 ownerStart_RS,
            Vector3 ownerEnd_RS,
            uint revision,
            int creatorPlayerId,
            out ManualColocationReference reference)
        {
            reference = default;

            if (!IsFinite(ownerScenePose.translation) ||
                !IsFinite(ownerScenePose.rotation) ||
                !IsFinite(ownerScenePose.scale))
            {
                return ManualColocationValidationError.NonFiniteInput;
            }

            if (ownerScenePose.scale < MinSceneScale || ownerScenePose.scale > MaxSceneScale)
            {
                return ManualColocationValidationError.InvalidScale;
            }

            ManualColocationValidationError measurementError =
                ValidateMeasurement(ownerStart_RS, ownerEnd_RS);
            if (measurementError != ManualColocationValidationError.None)
            {
                return measurementError;
            }

            reference = new ManualColocationReference
            {
                IsValid = true,
                Start_SS = ownerScenePose.inverse.MultiplyPoint(ownerStart_RS),
                End_SS = ownerScenePose.inverse.MultiplyPoint(ownerEnd_RS),
                SceneScale = ownerScenePose.scale,
                Revision = revision,
                CreatorPlayerId = creatorPlayerId,
            };
            return ManualColocationValidationError.None;
        }

        public static ManualColocationSolveResult TrySolve(
            ManualColocationReference reference,
            Vector3 localStart_RS,
            Vector3 localEnd_RS)
        {
            var result = new ManualColocationSolveResult
            {
                ScenePose = TrTransform.identity,
                Error = ManualColocationValidationError.None,
            };

            if (!reference.IsValid)
            {
                result.Error = ManualColocationValidationError.ReferenceUnavailable;
                return result;
            }

            if (!IsFinite(reference.Start_SS) ||
                !IsFinite(reference.End_SS) ||
                !IsFinite(reference.SceneScale) ||
                !IsFinite(localStart_RS) ||
                !IsFinite(localEnd_RS))
            {
                result.Error = ManualColocationValidationError.NonFiniteInput;
                return result;
            }

            if (reference.SceneScale < MinSceneScale ||
                reference.SceneScale > MaxSceneScale)
            {
                result.Error = ManualColocationValidationError.InvalidScale;
                return result;
            }

            ManualColocationValidationError localError =
                ValidateMeasurement(localStart_RS, localEnd_RS);
            if (localError != ManualColocationValidationError.None)
            {
                result.Error = localError;
                return result;
            }

            Vector3 referenceDelta_SS = reference.End_SS - reference.Start_SS;
            Vector3 referenceDelta_RS = referenceDelta_SS * reference.SceneScale;
            Vector3 referenceHorizontal = Vector3.ProjectOnPlane(referenceDelta_RS, Vector3.up);
            if (referenceDelta_RS.magnitude * App.UNITS_TO_METERS <
                MinLineLengthMeters)
            {
                result.Error = ManualColocationValidationError.LineTooShort;
                return result;
            }
            if (referenceHorizontal.magnitude * App.UNITS_TO_METERS <
                MinHorizontalSpanMeters)
            {
                result.Error = ManualColocationValidationError.InsufficientHorizontalSpan;
                return result;
            }

            Vector3 localDelta_RS = localEnd_RS - localStart_RS;
            Vector3 localHorizontal = Vector3.ProjectOnPlane(localDelta_RS, Vector3.up);
            float yaw = Vector3.SignedAngle(referenceHorizontal, localHorizontal, Vector3.up);
            Quaternion rotation = Quaternion.AngleAxis(yaw, Vector3.up);

            Vector3 referenceMidpoint_SS = (reference.Start_SS + reference.End_SS) * 0.5f;
            Vector3 localMidpoint_RS = (localStart_RS + localEnd_RS) * 0.5f;
            Vector3 translation =
                localMidpoint_RS - rotation * (referenceMidpoint_SS * reference.SceneScale);

            result.ScenePose =
                TrTransform.TRS(translation, rotation, reference.SceneScale);
            result.ReferenceLengthMeters =
                referenceDelta_RS.magnitude * App.UNITS_TO_METERS;
            result.LocalLengthMeters =
                localDelta_RS.magnitude * App.UNITS_TO_METERS;
            result.LengthMismatchMeters =
                Mathf.Abs(result.LocalLengthMeters - result.ReferenceLengthMeters);
            result.LengthMismatchRatio = result.ReferenceLengthMeters > Mathf.Epsilon
                ? result.LengthMismatchMeters / result.ReferenceLengthMeters
                : 0f;
            result.HorizontalSpanMeters =
                Mathf.Min(referenceHorizontal.magnitude, localHorizontal.magnitude) *
                App.UNITS_TO_METERS;
            result.YawDegrees = yaw;

            Vector3 mappedStart_RS = result.ScenePose.MultiplyPoint(reference.Start_SS);
            Vector3 mappedEnd_RS = result.ScenePose.MultiplyPoint(reference.End_SS);
            result.EndpointResidualMeters = Mathf.Max(
                Vector3.Distance(mappedStart_RS, localStart_RS),
                Vector3.Distance(mappedEnd_RS, localEnd_RS)) *
                App.UNITS_TO_METERS;

            if (result.LengthMismatchMeters > LengthMismatchMetersWarning ||
                result.LengthMismatchRatio > LengthMismatchRatioWarning)
            {
                result.Warnings |= ManualColocationWarning.LengthMismatch;
            }

            float referenceHeightDeltaMeters =
                Mathf.Abs(referenceDelta_SS.y * reference.SceneScale) *
                App.UNITS_TO_METERS;
            float localHeightDeltaMeters =
                Mathf.Abs(localDelta_RS.y) * App.UNITS_TO_METERS;
            if (referenceHeightDeltaMeters > HeightMismatchMetersWarning ||
                localHeightDeltaMeters > HeightMismatchMetersWarning)
            {
                result.Warnings |= ManualColocationWarning.HeightMismatch;
            }

            if (Mathf.Abs(yaw) > EndpointReversalDegreesWarning)
            {
                result.Warnings |= ManualColocationWarning.PossibleEndpointReversal;
            }

            if (result.EndpointResidualMeters > EndpointResidualMetersWarning)
            {
                result.Warnings |= ManualColocationWarning.EndpointResidual;
            }

            result.Success = true;
            return result;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                   IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
