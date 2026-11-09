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

namespace TiltBrush
{
    public partial class MarkovPen
    {
        /// @class Curve
        /// @brief Partial class representative for the style curve of the MarkovPen.
        ///
        /// The Curve class provides basic spline functionalities: adding control points, computing arc length positions, and interpolation on the curve.
        public class Curve
        {
            protected List<float> m_ArcLengthPositions;
            protected List<Vector3> m_ControlPoints = new();

            private Vector3 m_LastInput = Vector3.zero;

            private const float k_Responsiveness = 1f;

            private readonly float m_Responsiveness;

            /// @brief Constructor for the style curve; initializes the first arc length position.
            /// @param responsiveness Responsiveness parameter controlling smoothing (default: 1).
            public Curve(float responsiveness = k_Responsiveness)
            {
                m_Responsiveness = responsiveness;
                m_ArcLengthPositions = new List<float> { 0f };
            }

            /// @brief Construct Curve 
            /// 
            /// @params controlPoints - Constrol points of the curve from OpenBrush
            public Curve(List<Vector3> controlPoints) : this()
            {
                foreach (var point in controlPoints)
                {
                    AddControlPoint(point);
                }
                Finish();
            }

            /// @brief Check if the curve has been fully processed and finalized.
            /// @return True if the curve is finished, otherwise false.
            public virtual bool IsFinished()
            {
                return m_ControlPoints.Count >= 2 &&
                    m_ArcLengthPositions.Count == m_ControlPoints.Count;
            }

            /// @brief This public virtual method adds a control point to the curve and performs necessary updates.
            /// If the curve is empty, the control point is directly added. If it's the first control point,
            /// it is stored as the last input for future interpolation. For subsequent points, the method uses
            /// cubic Hermite interpolation (elasticurve implementation) to add a new point to the curve.
            /// The method also updates arc length information when the curve has at least three control points.
            /// @param controlPoint The new control point to be added.
            public void AddControlPoint(Vector3 controlPoint)
            {
                if (m_ControlPoints.Count == 0)
                {
                    m_ControlPoints.Add(controlPoint);
                    return;
                }

                // If we have one control point already and another is added,
                // just record the added point
                if (m_LastInput == Vector3.zero)
                {
                    m_LastInput = controlPoint;
                    return;
                }

                Vector3[] segment =
                {
                    m_ControlPoints[m_ControlPoints.Count > 1 ? ^2 : ^1],
                    m_ControlPoints[^1],
                    m_LastInput,
                    controlPoint
                };

                // Elasticurve implementation
                Vector3 interpolatedPoint = Interpolate(segment, m_Responsiveness);

                m_LastInput = controlPoint;

                if (Vector3.Distance(segment[1], interpolatedPoint) < 0.1)
                {
                    return;
                }

                m_ControlPoints.Add(interpolatedPoint);

                if (m_ControlPoints.Count >= 3)
                {
                    m_ArcLengthPositions.Add(
                        m_ArcLengthPositions.Last() +
                        ComputeArcLength(m_ControlPoints.Count - 3));
                }
            }

            /// @brief Retrieve the total arc length of the entire curve.
            /// @return The total arc length of the curve.
            public virtual float ArcLength()
            {
                return m_ArcLengthPositions.Last();
            }

            /// @brief Calculates the position along the curve at a given arc length parameter.
            /// Returns extrapolated positions when l is outside the curve range.
            /// @param l The arc length parameter at which to calculate the position.
            /// @return The Vector3 position on the curve at the specified arc length parameter.
            public Vector3 PositionAt(float l)
            {
                if (l < 0)
                {
                    Vector3[] firstSegment = GetSegmentPositions(0);
                    Vector3 tangent = ComputeTangentsAtEndpoints(firstSegment).Item1;

                    return m_ControlPoints[0] + l * tangent.normalized;
                }

                if (l >= m_ArcLengthPositions.Last())
                {
                    Vector3[] lastSegment = GetSegmentPositions(m_ControlPoints.Count - 2);
                    Vector3 tangent = ComputeTangentsAtEndpoints(lastSegment).Item2;

                    return m_ControlPoints[^1] +
                           (l - m_ArcLengthPositions.Last()) * tangent.normalized;
                }

                float t = TimeAt(l);

                Vector3[] segment = GetSegmentPositions(SegmentIndex(t));

                return Interpolate(segment, SegmentTime(t));
            }

            /// @brief Retrieve the tangent vector at a specified arc length along the curve.
            /// Uses cubic Hermite interpolation to compute the tangent for the segment.
            /// @param l The arc length at which to retrieve the tangent vector.
            /// @return The computed tangent vector at the specified arc length.
            public Vector3 FirstDerivativeAt(float l)
            {
                if (l <= 0)
                {
                    Vector3[] firstSegment = GetSegmentPositions(0);
                    return ComputeTangentsAtEndpoints(firstSegment).Item1;
                }

                if (l >= m_ArcLengthPositions.Last())
                {
                    Vector3[] lastSegment = GetSegmentPositions(m_ControlPoints.Count - 2);
                    return ComputeTangentsAtEndpoints(lastSegment).Item2;
                }

                float t = TimeAt(l);

                Vector3[] segment = GetSegmentPositions(SegmentIndex(t));

                return EvaluateFirstDerivative(segment, SegmentTime(t));
            }

            /// @brief Get an array of four Vector3 positions for the segment at the given index.
            /// @param index The segment index used to retrieve control points.
            /// @return An array of four Vector3 positions.
            protected Vector3[] GetSegmentPositions(int index)
            {
                Vector3 point1 = m_ControlPoints[index == 0 ? index : index - 1];
                Vector3 point2 = m_ControlPoints[index];
                Vector3 point3 = m_ControlPoints[index + 1];
                Vector3 point4 = m_ControlPoints[index == m_ControlPoints.Count - 2 ? index + 1 : index + 2];

                return new[] { point1, point2, point3, point4 };
            }

            /// @brief Perform cubic Hermite interpolation to calculate the position on the curve.
            /// @param segment The segment consisting of four control points
            /// @param t The parameter value for interpolation.
            /// @return The interpolated Vector3 position on the curve.
            public Vector3 Interpolate(Vector3[] segment, float t)
            {
                (Vector3, Vector3) tangents = ComputeTangentsAtEndpoints(segment);

                // Hermite basis functions
                float h1 = (float)(2 * Math.Pow(t, 3) - 3 * Math.Pow(t, 2) + 1);
                float h2 = (float)((-2) * Math.Pow(t, 3) + 3 * Math.Pow(t, 2));
                float h3 = (float)(Math.Pow(t, 3) - 2 * Math.Pow(t, 2) + t);
                float h4 = (float)(Math.Pow(t, 3) - Math.Pow(t, 2));

                return
                    h1 * segment[1] +
                    h2 * segment[2] +
                    h3 * tangents.Item1 +
                    h4 * tangents.Item2;
            }

            /// @brief Evaluate the first derivative of a cubic Hermite spline at a specified parameter t.
            /// @param point1 The first control point of the spline.
            /// @param point2 The second control point of the spline.
            /// @param point3 The third control point of the spline.
            /// @param point4 The fourth control point of the spline.
            /// @param t The parameter at which to evaluate the first derivative (range [0,1]).
            /// @return The first derivative of the spline at parameter t.
            public static Vector3 EvaluateFirstDerivative(Vector3[] segment, float t)
            {

                (Vector3, Vector3) tangents = ComputeTangentsAtEndpoints(segment);

                // First derivatives of hermnite basis functions
                float h1 = (float)(6 * Math.Pow(t, 2) - 6 * Math.Pow(t, 1));
                float h2 = (float)((-6) * Math.Pow(t, 2) + 6 * Math.Pow(t, 1));
                float h3 = (float)(3 * Math.Pow(t, 2) - 4 * Math.Pow(t, 1) + 1);
                float h4 = (float)(3 * Math.Pow(t, 2) - 2 * Math.Pow(t, 1));

                Vector3 newPoint =
                    h1 * segment[1] +
                    h2 * segment[2] +
                    h3 * tangents.Item1 +
                    h4 * tangents.Item2;

                return newPoint;
            }

            /// @brief Calculate the tangent vector at a given point using cubic Hermite interpolation factors.
            /// @param segment The segment consisting of four control points
            /// @param tension The tension factor for interpolation.
            /// @param continuity The continuity factor for interpolation.
            /// @param bias The bias factor for interpolation.
            /// @return The computed tangent vector at the given point.
            protected static (Vector3, Vector3) ComputeTangentsAtEndpoints(Vector3[] segment)
            {
                return ((segment[2] - segment[0]) / 2, (segment[3] - segment[1]) / 2);
            }

            /// @brief Computes the local parameter within a curve segment based on the given time parameter.
            /// @param t The time parameter for the curve segment.
            /// @return The local parameter within the curve segment (value between 0 and 1).
            protected float SegmentTime(float t)
            {
                return t % 1;
            }

            /// @brief Round down the given time parameter 't' and return the corresponding integer segment index.
            /// @param t The input time parameter as a float.
            /// @return The rounded down integer value of 't'.
            protected int SegmentIndex(float t)
            {
                return Mathf.FloorToInt(t);
            }

            /// @brief Calculate the time parameter 't' based on the given arc length.
            /// @param l The desired arc length.
            /// @return The calculated time parameter 't'.
            protected float TimeAt(float l)
            {
                int i = 0;

                while (i < m_ArcLengthPositions.Count - 2 &&
                       l >= m_ArcLengthPositions[i + 1])
                {
                    i++;
                }

                float t =
                    (l - m_ArcLengthPositions[i]) /
                    (m_ArcLengthPositions[i + 1] - m_ArcLengthPositions[i]);

                return i + t;
            }

            /// @brief Computes the arc length of a cubic Bezier curve segment at a specific parameter value.
            /// @param i The index of the curve segment.
            /// @return The arc length of the cubic Bezier curve segment at the given parameter value.
            private float ComputeArcLength(int i)
            {
                Vector3[] segment = GetSegmentPositions(i);

                // If the segment is a straight line, just return distance between endpoints
                if (Vector3.Distance(segment[0], segment[1]) == 0 &&
                    Vector3.Distance(segment[2], segment[3]) == 0)
                {
                    return Vector3.Distance(segment[1], segment[2]);
                }

                return ComputeArcLength(segment, 0f, 1f);
            }

            /// @brief Calculate the arc length between two points on the interpolated curve.
            /// Recursively subdivides the segment until distances fall below the threshold.
            /// @param segment The segment consisting of four control points
            /// @param t1 The parameter value for the first interpolated point.
            /// @param t2 The parameter value for the second interpolated point.
            /// @param threshold The maximum distance threshold for recursive calculation (default: 0.1).
            /// @return The computed arc length between the two interpolated points.
            public float ComputeArcLength(
                Vector3[] segment,
                float t1,
                float t2,
                float threshold = 0.001f)
            {
                Vector3 point1 = Interpolate(segment, t1);
                Vector3 point2 = Interpolate(segment, t2);

                float distance = Vector3.Distance(point1, point2);

                if (distance < threshold)
                {
                    return distance;
                }
                else
                {
                    float tMid = t1 + (t2 - t1) / 2;

                    return ComputeArcLength(segment, t1, tMid, threshold) +
                           ComputeArcLength(segment, tMid, t2, threshold);
                }
            }

            /// @brief Check if the curve has any control points.
            /// @return True if the curve has no control points, otherwise false.
            public bool IsEmpty()
            {
                return m_ControlPoints.Count == 0;
            }

            /// @brief Finalize the curve, updating arc length information by computing the last segment's length.
            public virtual void Finish()
            {
                Vector3[] segment =
                {
                    m_ControlPoints[m_ControlPoints.Count > 1 ? ^2 : ^1],
                    m_ControlPoints[^1],
                    m_LastInput,
                    m_LastInput
                };

                // Elasticurve implementation
                m_ControlPoints.Add(Interpolate(segment, m_Responsiveness));

                // Compute arcLength for segment before last
                if (m_ControlPoints.Count >= 3)
                {
                    m_ArcLengthPositions.Add(
                        m_ArcLengthPositions.Last() +
                        ComputeArcLength(m_ControlPoints.Count - 3));
                }

                // Compute arcLength for last segment
                m_ArcLengthPositions.Add(
                    m_ArcLengthPositions.Last() +
                    ComputeArcLength(m_ControlPoints.Count - 2));
            }

            public static Vector3 Orthogonal(Vector3 v)
            {
                return new Vector3(-v.y, v.x);
            }
        }
    }
}