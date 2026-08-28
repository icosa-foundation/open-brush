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
            return ApplySmooth(startPoints, weights, amount, 1f, result);
        }

        public static bool ApplySmooth(
            PointerManager.ControlPoint[] startPoints, float[] weights,
            float amountPerReferenceUpdate, float referenceUpdates,
            PointerManager.ControlPoint[] result)
        {
            if (startPoints == null || weights == null || result == null ||
                weights.Length < startPoints.Length || startPoints.Length != result.Length)
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
                float pointAmount = ScaleProportionalAmount(
                    amountPerReferenceUpdate * weights[i], referenceUpdates,
                    towardTarget: true);
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

        /// Converts a per-reference-update proportional amount to the equivalent amount after
        /// referenceUpdates repeated applications.
        public static float ScaleProportionalAmount(
            float amountPerReferenceUpdate, float referenceUpdates, bool towardTarget)
        {
            if (amountPerReferenceUpdate <= 0f || referenceUpdates <= 0f)
            {
                return 0f;
            }

            float amount = Mathf.Clamp01(amountPerReferenceUpdate);
            return towardTarget
                ? 1f - Mathf.Pow(1f - amount, referenceUpdates)
                : Mathf.Pow(1f + amount, referenceUpdates) - 1f;
        }

        public static float ScaleProportionalDisplacement(
            float distanceToTarget, float displacementPerReferenceUpdate,
            float referenceUpdates, bool towardTarget)
        {
            if (distanceToTarget <= Mathf.Epsilon)
            {
                return 0f;
            }

            float amountPerReferenceUpdate =
                displacementPerReferenceUpdate / distanceToTarget;
            return distanceToTarget * ScaleProportionalAmount(
                amountPerReferenceUpdate, referenceUpdates, towardTarget);
        }

        /// Returns the shortest vector from a point to a plane. The plane normal need not be
        /// normalized.
        public static Vector3 CalculatePlaneOffset(
            Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            if (planeNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            Vector3 normal = planeNormal.normalized;
            float signedDistance = Vector3.Dot(point - planePoint, normal);
            return -signedDistance * normal;
        }

        public static float CalculatePlaneWeight(
            Vector3 point, Vector3 planePoint, Vector3 planeNormal, float radius)
        {
            if (planeNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                return 0f;
            }

            Vector3 normal = planeNormal.normalized;
            Vector3 fromPlanePoint = point - planePoint;
            Vector3 inPlaneOffset = fromPlanePoint -
                Vector3.Dot(fromPlanePoint, normal) * normal;
            return CalculateRadialWeight(inPlaneOffset.magnitude, radius);
        }

        /// Returns the shortest vector from a point to an infinite line. The line direction need
        /// not be normalized; finite reach is handled separately by the subtool volume.
        public static Vector3 CalculateLineOffset(
            Vector3 point, Vector3 linePoint, Vector3 lineDirection)
        {
            if (lineDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            Vector3 direction = lineDirection.normalized;
            Vector3 fromLinePoint = point - linePoint;
            Vector3 closestPoint = linePoint + Vector3.Dot(fromLinePoint, direction) * direction;
            return closestPoint - point;
        }

        public static float CalculateLineWeight(
            Vector3 point, Vector3 linePoint, Vector3 lineDirection, float radius)
        {
            return CalculateRadialWeight(
                CalculateLineOffset(point, linePoint, lineDirection).magnitude, radius);
        }

        /// Extracts the signed twist around a world-space axis from the controller rotation delta.
        public static float CalculateTwistAngle(
            Quaternion startRotation, Quaternion currentRotation, Vector3 axis)
        {
            if (axis.sqrMagnitude <= Mathf.Epsilon)
            {
                return 0f;
            }

            Vector3 normalizedAxis = axis.normalized;
            Quaternion delta = currentRotation * Quaternion.Inverse(startRotation);
            Vector3 deltaVector = new Vector3(delta.x, delta.y, delta.z);
            Vector3 projectedVector = Vector3.Project(deltaVector, normalizedAxis);
            Quaternion twist = new Quaternion(
                projectedVector.x, projectedVector.y, projectedVector.z, delta.w);
            if (Mathf.Abs(twist.x) + Mathf.Abs(twist.y) + Mathf.Abs(twist.z) +
                Mathf.Abs(twist.w) <= Mathf.Epsilon)
            {
                return 0f;
            }

            twist.Normalize();
            twist.ToAngleAxis(out float angle, out Vector3 twistAxis);
            if (angle > 180f)
            {
                angle -= 360f;
            }
            return Vector3.Dot(twistAxis, normalizedAxis) < 0f ? -angle : angle;
        }

        /// Returns the equivalent of wrappedAngle nearest to previousAngle, allowing callers
        /// sampling a wrapped angle over time to preserve complete rotations.
        public static float UnwrapAngle(float previousAngle, float wrappedAngle)
        {
            return previousAngle + Mathf.DeltaAngle(previousAngle, wrappedAngle);
        }

        /// Applies a captured soft transform. Translation and rotation both fade by the captured
        /// control-point weight, and orientation follows the same rotation as position.
        public static void ApplyCapturedTransform(
            PointerManager.ControlPoint[] startPoints, float[] weights, Vector3 pivot,
            Vector3 translation, Vector3 rotationAxis, float angle,
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
                float weight = weights[i];
                Quaternion rotation = Quaternion.AngleAxis(angle * weight, rotationAxis);
                result[i].m_Pos = pivot + rotation * (startPoints[i].m_Pos - pivot) +
                    translation * weight;
                result[i].m_Orient = rotation * startPoints[i].m_Orient;
            }
        }

        /// Parallel-transports each control point orientation through a non-rigid deformation.
        /// The minimal rotation from the old curve tangent to the new one preserves existing roll.
        public static void TransportOrientations(
            PointerManager.ControlPoint[] startPoints,
            PointerManager.ControlPoint[] result)
        {
            if (startPoints == null || result == null ||
                startPoints.Length != result.Length || startPoints.Length < 2)
            {
                return;
            }

            for (int i = 0; i < startPoints.Length; ++i)
            {
                if (!TryCalculateTangent(startPoints, i, out Vector3 oldTangent) ||
                    !TryCalculateTangent(result, i, out Vector3 newTangent))
                {
                    continue;
                }

                Quaternion transport = Quaternion.FromToRotation(oldTangent, newTangent);
                result[i].m_Orient = transport * startPoints[i].m_Orient;
            }
        }

        private static bool TryCalculateTangent(
            PointerManager.ControlPoint[] points, int index, out Vector3 tangent)
        {
            int previous = index - 1;
            while (previous >= 0 &&
                (points[index].m_Pos - points[previous].m_Pos).sqrMagnitude <= Mathf.Epsilon)
            {
                --previous;
            }

            int next = index + 1;
            while (next < points.Length &&
                (points[next].m_Pos - points[index].m_Pos).sqrMagnitude <= Mathf.Epsilon)
            {
                ++next;
            }

            tangent = Vector3.zero;
            if (previous >= 0 && next < points.Length)
            {
                tangent = points[next].m_Pos - points[previous].m_Pos;
            }
            if (tangent.sqrMagnitude <= Mathf.Epsilon && next < points.Length)
            {
                tangent = points[next].m_Pos - points[index].m_Pos;
            }
            if (tangent.sqrMagnitude <= Mathf.Epsilon && previous >= 0)
            {
                tangent = points[index].m_Pos - points[previous].m_Pos;
            }

            if (tangent.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }
            tangent.Normalize();
            return true;
        }
    }
} // namespace TiltBrush
