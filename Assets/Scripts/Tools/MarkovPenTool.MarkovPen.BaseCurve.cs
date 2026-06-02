// Copyright 2026 Marvin Link, Katrin Lang, Artur Meshalkin
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
        /// @class BaseCurve
        /// @brief Represents the base curve of the MarkovPen.
        ///
        /// The BaseCurve class extends the functionality of the Curve class and provides
        /// additional features such as projection, spline functionalities, and smoothing functionalities.
        public class BaseCurve : Curve
        {
            private float m_Tap = 0f;

            private List<Vector3> m_UpVectors = new List<Vector3>();

            private List<Vector3> m_SmoothNormals = new List<Vector3>();


            public BaseCurve() : base(0.25f)
            {
            }

            /// @brief Set the tap value for smoothing. A non-zero tap initiates the smoothing
            /// process during the addition of control points.
            /// @param tap The tap value for smoothing.
            public void SetTap(float tap)
            {
                m_Tap = tap;
            }

            /// @brief Add a control point to the base curve and update related information.
            /// Extends the base class method to incorporate smoothing functionalities based on the tap value.
            /// @param controlPoint The control point to be added to the base curve.
            /// @param upVector The up vector associated with the control point.
            public override void AddControlPoint(Vector3 controlPoint, Vector3 upVector)
            {
                base.AddControlPoint(controlPoint, upVector);

                if (m_Tap == 0)
                {
                    return;
                }

                m_UpVectors.Add(
                    m_UpVectors.Count > 0
                        ? (0.25f * upVector + 0.75f * m_UpVectors.Last()).normalized
                        : upVector);

                if (_controlPoints.Count < 4)
                {
                    return;
                }

                while (m_Tap <= _arcLengthPositions.Last() - _arcLengthPositions[m_SmoothNormals.Count])
                {
                    int index = m_SmoothNormals.Count;

                    m_SmoothNormals.Add(
                        ComputeSmoothNormal(
                            m_UpVectors[index],
                            ComputeSmoothTangent(_arcLengthPositions[index])));

                    if (m_SmoothNormals.Count > 1 &&
                        Vector3.Dot(m_SmoothNormals[^1], m_SmoothNormals[^2]) < 0)
                    {
                        m_SmoothNormals[^1] *= -1;
                    }
                }
            }

            /// @brief Computes a smoothed tangent vector based on the specified center position.
            /// Averages the normalized first derivatives at positions within a window around the given center.
            /// @param center The center position around which the smoothed tangent is calculated.
            /// @return The computed smoothed tangent vector.
            private Vector3 ComputeSmoothTangent(float center)
            {
                float windowSize = 2 * m_Tap + 1;

                Vector3 smoothTangent = Vector3.zero;

                for (
                    float length = center - m_Tap;
                    length <= center + m_Tap;
                    length += (1.0f / windowSize))
                {
                    smoothTangent += FirstDerivativeAt(length).normalized;
                }

                return smoothTangent / windowSize;
            }

            /// @brief Computes a smoothed normal vector based on the provided up vector and smooth tangent.
            /// Projects the up vector onto the smooth tangent and subtracts it, then normalizes the result.
            /// @param upVector The original up vector to be smoothed.
            /// @param smoothTangent The smooth tangent vector to influence the smoothing.
            /// @return A normalized vector representing the computed smoothed normal.
            private Vector3 ComputeSmoothNormal(Vector3 upVector, Vector3 smoothTangent)
            {
                upVector = upVector.normalized * 100;
                smoothTangent = smoothTangent.normalized;

                float projection = Vector3.Dot(upVector, smoothTangent);

                return (upVector - smoothTangent * projection).normalized;
            }

            /// @brief Evaluate the first derivative of a cubic Hermite spline at a specified parameter t.
            /// @param point1 The first control point of the spline.
            /// @param point2 The second control point of the spline.
            /// @param point3 The third control point of the spline.
            /// @param point4 The fourth control point of the spline.
            /// @param tension Tension parameter affecting the shape of the spline.
            /// @param continuity Continuity parameter affecting the smoothness of the spline.
            /// @param bias Bias parameter affecting the directionality of the spline.
            /// @param t The parameter at which to evaluate the first derivative (range [0,1]).
            /// @return The first derivative of the spline at parameter t.
            public static Vector3 EvaluateFirstDerivative(
                Vector3 point1,
                Vector3 point2,
                Vector3 point3,
                Vector3 point4,
                float tension,
                float continuity,
                float bias,
                float t)
            {
                if (t <= 0f)
                {
                    return point2;
                }
                else if (t >= 1f)
                {
                    return point3;
                }

                Vector3 tangent2 =
                    ComputeTangent(point1, point2, point3, tension, continuity, bias);

                Vector3 tangent3 =
                    ComputeTangent(point2, point3, point4, tension, continuity, bias);

                float h1 = (float)(6 * Math.Pow(t, 2) - 6 * Math.Pow(t, 1));
                float h2 = (float)((-6) * Math.Pow(t, 2) + 6 * Math.Pow(t, 1));
                float h3 = (float)(3 * Math.Pow(t, 2) - 4 * Math.Pow(t, 1) + 1);
                float h4 = (float)(3 * Math.Pow(t, 2) - 2 * Math.Pow(t, 1));

                Vector3 newPoint =
                    h1 * point2 +
                    h2 * point3 +
                    h3 * tangent2 +
                    h4 * tangent3;

                return newPoint;
            }

            /// @brief Retrieve the smoothed normal vector at a specified arc length along the curve.
            /// Uses cubic Hermite interpolation between precomputed smooth normals.
            /// @param l The arc length at which to retrieve the smoothed normal vector.
            /// @return The computed smoothed normal vector at the specified arc length.
            public Vector3 SmoothNormalAt(float l)
            {
                if (m_Tap == 0)
                {
                    Vector3 line =
                        Vector3.Normalize(_controlPoints.Last() - _controlPoints.First());

                    return new Vector3(-line.y, line.x, 0f);
                }

                Vector3 normal;

                if (l <= 0)
                {
                    return m_SmoothNormals[0];
                }
                else if (l >= _arcLengthPositions.Last())
                {
                    return m_SmoothNormals.Last();
                }
                else
                {
                    float t = TimeAt(l);

                    int index = SegmentIndex(t);

                    Debug.Log("index: " + (_controlPoints.Count - index));

                    if (index >= 0 && index + 2 <= m_SmoothNormals.Count)
                    {
                        normal = Interpolate(
                            index == 0
                                ? m_SmoothNormals[0]
                                : m_SmoothNormals[index - 1].normalized,

                            m_SmoothNormals[index].normalized,

                            m_SmoothNormals[index + 1].normalized,

                            index == m_SmoothNormals.Count - 2
                                ? m_SmoothNormals[index + 1].normalized
                                : m_SmoothNormals[index + 2].normalized,

                            0,
                            0,
                            0,
                            SegmentT(t));
                    }
                    else if (index < 0)
                    {
                        throw new ArgumentOutOfRangeException("The Argument is out of Range, due index to small.");
                    }
                    else if (index + 2 > m_SmoothNormals.Count)
                    {
                        throw new ArgumentOutOfRangeException("The Argument is out of Range, due index to big.");
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("The Argument is out of Range, but its not clear why.");
                    }
                }

                return normal.normalized;
            }

            /// @brief Compute the total arc length of the curve.
            /// Returns the last precomputed arc length position if enough data exists.
            /// @return The total arc length of the curve.
            public override float ArcLength()
            {
                if (m_UpVectors.Count < 4)
                {
                    return 0;
                }

                return _arcLengthPositions[Math.Max(m_SmoothNormals.Count - 1, 0)];
            }

            /// @brief Retrieve the tangent vector at a specified arc length along the curve.
            /// Uses cubic Hermite interpolation to compute the tangent for the segment.
            /// @param l The arc length at which to retrieve the tangent vector.
            /// @return The computed tangent vector at the specified arc length.
            public Vector3 FirstDerivativeAt(float l)
            {
                Vector3 firstDerivative;

                if (l <= 0)
                {
                    firstDerivative =
                        ComputeTangent(
                            _controlPoints[0],
                            _controlPoints[0],
                            _controlPoints[1],
                            0,
                            0,
                            0);
                }
                else if (l >= _arcLengthPositions.Last())
                {
                    firstDerivative =
                        ComputeTangent(
                            _controlPoints[_controlPoints.Count - 2],
                            _controlPoints[_controlPoints.Count - 1],
                            _controlPoints[_controlPoints.Count - 1],
                            0,
                            0,
                            0);
                }
                else
                {
                    float t = TimeAt(l);

                    Vector3[] segment = GetSegmentPositions(SegmentIndex(t));

                    firstDerivative = EvaluateFirstDerivative(
                        segment[0],
                        segment[1],
                        segment[2],
                        segment[3],
                        0,
                        0,
                        0,
                        SegmentT(t));
                }

                return firstDerivative;
            }

            /// @brief Project a point onto the curve and return the arc length positions of the projections.
            /// @param toProject The point to be projected onto the curve.
            /// @return A list of arc length positions corresponding to the projections.
            public List<float> Project(Vector3 toProject)
            {
                List<float> projections = new List<float>();

                Vector3 line =
                    Vector3.Normalize(_controlPoints.Last() - _controlPoints.First());

                Vector3 normal = new Vector3(-line.y, line.x, 0f);

                for (int index = 1; index < _arcLengthPositions.Count; ++index)
                {
                    Project(
                        toProject,
                        _arcLengthPositions[index - 1],
                        _arcLengthPositions[index],
                        projections,
                        normal);
                }

                if (projections.Count == 0)
                {
                    if (Vector3.Distance(toProject, _controlPoints[0]) <
                        Vector3.Distance(toProject, _controlPoints[^1]))
                    {
                        Vector3 toPoint = toProject - _controlPoints[0];

                        Vector3 tangentFirst =
                            -ComputeTangent(
                                    _controlPoints[0],
                                    _controlPoints[0],
                                    _controlPoints[1],
                                    0,
                                    0,
                                    0)
                                .normalized;

                        float l1 = Vector3.Dot(toPoint, tangentFirst);

                        projections.Add(-l1);
                    }
                    else
                    {
                        Vector3 toPoint = toProject - _controlPoints[^2];

                        Vector3 tangentLast =
                            ComputeTangent(
                                    _controlPoints[^2],
                                    _controlPoints[^1],
                                    _controlPoints[^1],
                                    0,
                                    0,
                                    0)
                                .normalized;

                        float l2 = Vector3.Dot(toPoint, tangentLast);

                        projections.Add(_arcLengthPositions.Last() + l2);
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
                List<float> projections,
                Vector3 normal)
            {
                double d1 = Shoot(point, normal, l1);
                double d2 = Shoot(point, normal, l2);

                if (d1 < 0.0 && d2 < 0.0)
                {
                    return;
                }

                if (d1 > 0.0 && d2 > 0.0)
                {
                    return;
                }

                float middle = l1 + (l2 - l1) / 2.0f;

                if (Math.Abs(d1) < 0.25 && Math.Abs(d2) < 0.25)
                {
                    projections.Add(middle);
                    return;
                }

                Project(point, l1, middle, projections, normal);
                Project(point, middle, l2, projections, normal);
            }

            /// @brief Perform a shooting method to calculate the signed distance from a point to the curve.
            /// @param point The point from which to calculate the distance to the curve.
            /// @param normal The normal vector used for the shooting method.
            /// @param l The arc length position on the curve.
            /// @return The signed distance from the point to the curve.
            private float Shoot(Vector3 point, Vector3 normal, float l)
            {
                Vector3 basePoint = PositionAt(l);
                Vector3 toPoint = point - basePoint;

                normal.Normalize();

                float projection = Vector3.Dot(normal, toPoint);
                Vector3 offset = normal * projection;
                Vector3 closestPoint = basePoint + offset;

                float dist = Vector3.Distance(point, closestPoint);

                double determinant = toPoint.x * normal.y - toPoint.y * normal.x;

                if (determinant < 0)
                {
                    dist *= -1;
                }

                return dist;
            }

            /// @brief Finalize the curve by computing smoothed tangents and normals for the remaining control points.
            /// Ensures that the smoothing process is completed for all control points.
            public override void Finish()
            {
                base.Finish();

                if (_controlPoints.Count < 2 || m_Tap == 0)
                {
                    return;
                }

                for (
                    int index = m_SmoothNormals.Count;
                    index < _controlPoints.Count;
                    index++)
                {
                    m_SmoothNormals.Add(
                        ComputeSmoothNormal(
                            m_UpVectors[index],
                            ComputeSmoothTangent(_arcLengthPositions[index])));
                }
            }
        }
    }
}