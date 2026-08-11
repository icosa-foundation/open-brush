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

using UnityEngine;

namespace TiltBrush
{
    public static class StrokeSculptInfluence
    {
        /// Returns a smooth radial weight which is one at the tool centre and zero at or
        /// beyond its boundary. The zero derivatives at both ends avoid visible changes
        /// in velocity as a control point crosses either end of the falloff.
        public static float CalculateRadialWeight(float distance, float radius)
        {
            if (radius <= 0f || distance >= radius)
            {
                return 0f;
            }

            float normalizedDistance = Mathf.Clamp01(distance / radius);
            return 1f - Mathf.SmoothStep(0f, 1f, normalizedDistance);
        }

        /// Uses analog trigger pressure when available while preserving full-strength input for
        /// desktop and other controllers which report a digital activation with a zero ratio.
        public static float CalculatePressure(float triggerRatio, bool isActive)
        {
            if (!isActive)
            {
                return 0f;
            }

            return triggerRatio > 0f ? Mathf.Clamp01(triggerRatio) : 1f;
        }

        /// Extends existing spatial weights along the stroke by arc length. The two linear passes
        /// produce a finite-support envelope in O(n), so neighboring samples can share influence
        /// without making the cost dependent on control-point density squared.
        public static void FeatherAlongStroke(
            PointerManager.ControlPoint[] controlPoints, float[] weights, float featherDistance,
            int count = -1)
        {
            if (controlPoints == null || weights == null || featherDistance <= 0f)
            {
                return;
            }

            count = count < 0 ? weights.Length : count;
            if (count > controlPoints.Length || count > weights.Length)
            {
                return;
            }

            for (int i = 1; i < count; ++i)
            {
                float segmentLength = Vector3.Distance(
                    controlPoints[i - 1].m_Pos, controlPoints[i].m_Pos);
                float propagatedWeight = weights[i - 1] - segmentLength / featherDistance;
                weights[i] = Mathf.Clamp01(Mathf.Max(weights[i], propagatedWeight));
            }

            for (int i = count - 2; i >= 0; --i)
            {
                float segmentLength = Vector3.Distance(
                    controlPoints[i].m_Pos, controlPoints[i + 1].m_Pos);
                float propagatedWeight = weights[i + 1] - segmentLength / featherDistance;
                weights[i] = Mathf.Clamp01(Mathf.Max(weights[i], propagatedWeight));
            }
        }

        /// Applies a captured translation to the original control points. Rebuilding from the
        /// captured points makes the result independent of frame rate and intersection scheduling.
        public static void ApplyGrabTranslation(
            PointerManager.ControlPoint[] startPoints, float[] weights, Vector3 translation,
            PointerManager.ControlPoint[] result)
        {
            if (startPoints == null || weights == null || result == null ||
                startPoints.Length != weights.Length || startPoints.Length != result.Length)
            {
                return;
            }

            for (int i = 0; i < startPoints.Length; ++i)
            {
                result[i] = startPoints[i];
                result[i].m_Pos += translation * weights[i];
            }
        }

        /// Relaxes interior control points toward the length-weighted line between their
        /// neighbors. Endpoints are copied unchanged.
        public static bool ApplySmooth(
            PointerManager.ControlPoint[] startPoints, float[] weights, float amount,
            PointerManager.ControlPoint[] result)
        {
            if (startPoints == null || weights == null || result == null ||
                startPoints.Length != weights.Length || startPoints.Length != result.Length)
            {
                return false;
            }

            for (int i = 0; i < startPoints.Length; ++i)
            {
                result[i] = startPoints[i];
            }

            bool modified = false;
            for (int i = 1; i < startPoints.Length - 1; ++i)
            {
                float previousLength = Vector3.Distance(
                    startPoints[i - 1].m_Pos, startPoints[i].m_Pos);
                float nextLength = Vector3.Distance(
                    startPoints[i].m_Pos, startPoints[i + 1].m_Pos);
                float combinedLength = previousLength + nextLength;
                float pointAmount = Mathf.Clamp01(amount * weights[i]);
                if (combinedLength <= Mathf.Epsilon || pointAmount <= 0f)
                {
                    continue;
                }

                Vector3 target = Vector3.Lerp(
                    startPoints[i - 1].m_Pos, startPoints[i + 1].m_Pos,
                    previousLength / combinedLength);
                Vector3 smoothedPosition = Vector3.Lerp(
                    startPoints[i].m_Pos, target, pointAmount);
                if (smoothedPosition != startPoints[i].m_Pos)
                {
                    result[i].m_Pos = smoothedPosition;
                    modified = true;
                }
            }
            return modified;
        }
    }
} // namespace TiltBrush
