// Copyright 2026 Marvin Link, Katrin Lang
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
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace TiltBrush
{
    public partial class MarkovPen
    {
        /// @class BasePath
        /// @brief Represents the base path of the MarkovPen.
        ///
        /// The BasePath class extends the functionality of the Curve class and provides
        /// additional features such as projection, spline functionalities, and smoothing functionalities.
        public class BasePath : Curve
        {
            public float Tap { get; private set; }

            private readonly List<Vector3> m_UpVectors = new();

            private readonly List<Vector3> m_SmoothNormals = new();

            /// @brief Construct a target base path
            public BasePath(float maxOffsetAlongNormals, float responsiveness = 0.75f) : base(responsiveness)
            {
                Tap = maxOffsetAlongNormals;
            }

            /// @brief Construct an exmple base path
            ///
            /// @params List<Vector3> controlPoints - control points forming base path in OpenBrush
            public BasePath(List<Vector3> controlPoints)
            {
                Vector3 upVector = Orthogonal(controlPoints.Last() - controlPoints.First()).normalized;

                AddControlPoint(controlPoints.First(), upVector);
                AddControlPoint(controlPoints.Last(), upVector);
                Finish();
            }

            /// @brief Add a control point to the base path and update related information.
            /// Extends the base class method to incorporate smoothing functionalities based on the tap value.
            /// @param controlPoint The control point to be added to the base path.
            /// @param upVector The up vector associated with the control point.
            public void AddControlPoint(Vector3 controlPoint, Vector3 upVector)
            {
                base.AddControlPoint(controlPoint);

                m_UpVectors.Add(
                    m_UpVectors.Count > 0
                        ? (0.25f * upVector + 0.75f * m_UpVectors.Last()).normalized
                        : upVector);

                if (m_ControlPoints.Count < 4)
                {
                    return;
                }

                while (Tap <= m_ArcLengthPositions.Last() - m_ArcLengthPositions[m_SmoothNormals.Count])
                {
                    int index = m_SmoothNormals.Count;

                    m_SmoothNormals.Add(ComputeSmoothNormal(index));

                    if (m_SmoothNormals.Count > 1 &&
                        Vector3.Dot(m_SmoothNormals[^1], m_SmoothNormals[^2]) < 0)
                    {
                        m_SmoothNormals[^1] *= -1;
                    }
                }
            }

            /// @brief Compute the total arc length of the curve.
            /// Returns the last precomputed arc length position if enough data exists.
            /// @return The total arc length of the curve.
            public override float ArcLength()
            {
                if (Tap == 0)
                {
                    return base.ArcLength();
                }

                if (m_UpVectors.Count < 4)
                {
                    return 0;
                }

                return m_ArcLengthPositions[Math.Max(m_SmoothNormals.Count - 1, 0)];
            }

            public void Smooth(float tap)
            {
                Tap = tap;
            }

            /// @brief Retrieve the smoothed normal vector at a specified arc length along the curve.
            /// Uses cubic Hermite interpolation between precomputed smooth normals.
            /// @param l The arc length at which to retrieve the smoothed normal vector.
            /// @return The computed smoothed normal vector at the specified arc length.
            public Vector3 SmoothNormalAt(float l)
            {
                if (l <= 0)
                {
                    return m_SmoothNormals[0];
                }

                if (l >= m_ArcLengthPositions.Last())
                {
                    return m_SmoothNormals.Last();
                }

                float t = TimeAt(l);
                int index = SegmentIndex(t);

                Vector3[] smoothNormals =
                {
                    index == 0 ?
                        m_SmoothNormals[0] :
                        m_SmoothNormals[index - 1].normalized,
                    m_SmoothNormals[index].normalized,
                    m_SmoothNormals[index + 1].normalized,
                    index == m_SmoothNormals.Count - 2 ?
                        m_SmoothNormals[index + 1].normalized :
                        m_SmoothNormals[index + 2].normalized,
                };

                return Interpolate(smoothNormals, SegmentTime(t));
            }

            /// @brief Computes a smoothed tangent vector based on the specified center position.
            /// Averages the normalized first derivatives at positions within a window around the given center.
            /// @param center The center position around which the smoothed tangent is calculated.
            /// @return The computed smoothed tangent vector.
            private Vector3 ComputeSmoothTangent(int index)
            {
                float center = m_ArcLengthPositions[index];
                float windowSize = 2 * Tap + 1;

                Vector3 smoothTangentVector = Vector3.zero;
                // Add up tangent vectors
                for (float l = center - Tap; l <= center + Tap; l += (1.0f / windowSize))
                {
                    smoothTangentVector += FirstDerivativeAt(l).normalized;
                }

                return smoothTangentVector / windowSize;
            }

            /// @brief Computes a smoothed normal vector based on the provided up vector and smooth tangent.
            /// Projects the up vector onto the smooth tangent and subtracts it, then normalizes the result.
            /// @param upVector The original up vector to be smoothed.
            /// @param smoothTangent The smooth tangent vector to influence the smoothing.
            /// @return A normalized vector representing the computed smoothed normal.
            private Vector3 ComputeSmoothNormal(int index)
            {
                Vector3 upVector = m_UpVectors[index].normalized * 100;
                Vector3 smoothTangentDirection = ComputeSmoothTangent(index).normalized;

                // Project prologenged up vector onto smooth tangent
                float projection = Vector3.Dot(upVector, smoothTangentDirection);

                // make up vector perpendicular to smoot tangent
                return (upVector - smoothTangentDirection * projection).normalized;
            }

            /// @brief Project a point onto the curve and return the arc length positions of the projections.
            /// @param toProject The point to be projected onto the curve.
            /// @return A list of arc length positions corresponding to the projections.
            public List<float> Project(Vector3 toProject)
            {
                List<float> projections = new();

                // If the curve is a straight line, just project on that
                if (m_ControlPoints.Count == 2)
                {
                    Vector3 tangentDirection =
                        Vector3.Normalize(m_ControlPoints.Last() - m_ControlPoints.First());

                    Vector3 toPoint = toProject - m_ControlPoints[0];

                    projections.Add(Vector3.Dot(toPoint, tangentDirection));

                    return projections;
                }

                // Try to project on each segment
                for (int index = 1; index < m_ArcLengthPositions.Count; ++index)
                {

                    Project(
                        toProject,
                        m_ArcLengthPositions[index - 1],
                        m_ArcLengthPositions[index],
                        projections);
                }

                // If no projection has been found, project on tangent of first or last point
                if (projections.Count == 0)
                {
                    if (Vector3.Distance(toProject, m_ControlPoints[0]) <
                        Vector3.Distance(toProject, m_ControlPoints[^1]))
                    {
                        Vector3 toPoint = toProject - m_ControlPoints[0];

                        Vector3[] firstSegment = GetSegmentPositions(0);
                        Vector3 tangent = ComputeTangentsAtEndpoints(firstSegment).Item1;

                        float l = Vector3.Dot(toPoint, tangent.normalized);

                        projections.Add(l);
                    }
                    else
                    {
                        Vector3 toPoint = toProject - m_ControlPoints[^1];

                        Vector3[] lastSegment = GetSegmentPositions(m_ControlPoints.Count - 2);
                        Vector3 tangent = ComputeTangentsAtEndpoints(lastSegment).Item2;

                        float l = Vector3.Dot(toPoint, tangent.normalized);

                        projections.Add(m_ArcLengthPositions.Last() + l);
                    }
                }

                return projections;
            }

            /// @brief Recursively project a point onto a curve segment and update the arc length positions.
            /// Uses a recursive shooting method between arc length positions l1 and l2.
            /// @param point The point to be projected onto the curve segment.
            /// @param l1 The starting arc length position of the curve segment.
            /// @param l2 The ending arc length position of the curve segment.
            /// @param projections The list to store the resulting arc length positions.
            /// @param normal The normal vector used for the shooting method.
            private void Project(
                Vector3 point,
                float l1,
                float l2,
                List<float> projections)
            {
                double d1 = Shoot(point, l1);
                double d2 = Shoot(point, l2);

                if (d1 < 0.0 && d2 < 0.0)
                {
                    return;
                }

                if (d1 > 0.0 && d2 > 0.0)
                {
                    return;
                }

                float middle = l1 + (l2 - l1) / 2.0f;

                if (Math.Abs(d1) < 0.001 && Math.Abs(d2) < 0.001)
                {
                    projections.Add(middle);
                    return;
                }

                Project(point, l1, middle, projections);
                Project(point, middle, l2, projections);
            }

            /// @brief Perform a shooting method to calculate the signed distance from a point to the curve.
            /// @param point The point from which to calculate the distance to the curve.
            /// @param normal The normal vector used for the shooting method.
            /// @param l The arc length position on the curve.
            /// @return The signed distance from the point to the curve.
            private float Shoot(Vector3 point, float l)
            {
                Vector3 basePoint = PositionAt(l);
                Vector3 toPoint = point - basePoint;

                Vector3 normal = SmoothNormalAt(l).normalized;

                float projection = Vector3.Dot(normal, toPoint);
                Vector3 offset = normal * projection;
                Vector3 closestPoint = basePoint + offset;

                float distance = Vector3.Distance(point, closestPoint);

                double determinant = toPoint.x * normal.y - toPoint.y * normal.x;

                if (determinant < 0)
                {
                    distance *= -1;
                }

                return distance;
            }

            /// @brief Finalize the curve by computing smoothed tangents and normals for the remaining control points.
            /// Ensures that the smoothing process is completed for all control points.
            public override void Finish()
            {
                base.Finish();

                for (int index = m_SmoothNormals.Count;
                    index < m_ControlPoints.Count;
                    index++)
                {
                    m_SmoothNormals.Add(ComputeSmoothNormal(index));
                }
            }
        }
    }
}