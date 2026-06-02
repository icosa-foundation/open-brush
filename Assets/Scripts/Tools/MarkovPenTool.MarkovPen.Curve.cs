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
using Debug = UnityEngine.Debug;

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
            protected List<Vector3> m_ControlPoints = new List<Vector3>();

            public float Tension = 0f;
            public float Continuity = 0f;
            public float Bias = 0f;

            private Vector3? m_LastInput = null;

            private const float k_Responsiveness = 1f;

            private float m_Responsiveness = k_Responsiveness;

            /// @brief Constructor for the style curve; initializes the first arc length position.
            /// @param responsiveness Responsiveness parameter controlling smoothing (default: 1).
            public Curve(float responsiveness = k_Responsiveness)
            {
                m_Responsiveness = responsiveness;
                m_ArcLengthPositions = new List<float> { 0f };
            }

            /// @brief This public virtual method adds a control point to the curve and performs necessary updates.
            /// If the curve is empty, the control point is directly added. If it's the first control point,
            /// it is stored as the last input for future interpolation. For subsequent points, the method uses
            /// cubic Hermite interpolation (elasticurve implementation) to add a new point to the curve.
            /// The method also updates arc length information when the curve has at least three control points.
            /// @param controlPoint The new control point to be added.
            /// @param upVector The up vector associated with the control point.
            public virtual void AddControlPoint(Vector3 controlPoint, Vector3 upVector)
            {
                if (m_ControlPoints.Count == 0)
                {
                    m_ControlPoints.Add(controlPoint);
                    return;
                }

                if (m_LastInput == null)
                {
                    m_LastInput = controlPoint;
                    return;
                }

                //elasticurve implementation
                m_ControlPoints.Add(Interpolate(
                    m_ControlPoints[m_ControlPoints.Count > 1 ? ^2 : ^1],
                    m_ControlPoints[^1],
                    (Vector3)m_LastInput,
                    controlPoint,
                    0,
                    0,
                    0,
                    m_Responsiveness));

                m_LastInput = controlPoint;

                if (m_ControlPoints.Count >= 3)
                {
                    m_ArcLengthPositions.Add(
                        m_ArcLengthPositions.Last() +
                        ComputeArcLength(m_ControlPoints.Count - 2));
                }
            }

            /// @brief Calculate the tangent vector at a given point using cubic Hermite interpolation factors.
            /// @param point1 The previous control point.
            /// @param point2 The current control point.
            /// @param point3 The next control point.
            /// @param tension The tension factor for interpolation.
            /// @param continuity The continuity factor for interpolation.
            /// @param bias The bias factor for interpolation.
            /// @return The computed tangent vector at the given point.
            protected static Vector3 ComputeTangent(
                Vector3 point1,
                Vector3 point2,
                Vector3 point3,
                float tension,
                float continuity,
                float bias)
            {
                var factor1 = (1 - tension) * (1 + continuity) * (1 + bias) / 2;
                var factor2 = (1 - tension) * (1 - continuity) * (1 - bias) / 2;

                return factor1 * (point2 - point1) + factor2 * (point3 - point2);
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

            /// @brief Append a control point to the curve and update related information.
            /// @param point The control point to be appended to the curve.
            public void Append(Vector3 point)
            {
                if (m_ControlPoints.Count == 0)
                {
                    m_ControlPoints.Add(point);
                }

                m_ControlPoints.Add(point);

                if (m_ControlPoints.Count > 3)
                {
                    ComputeArcLength(m_ControlPoints.Count - 2);
                }
            }

            /// @brief Calculates the position along the curve at a given arc length parameter.
            /// Returns extrapolated positions when l is outside the curve range.
            /// @param l The arc length parameter at which to calculate the position.
            /// @return The Vector3 position on the curve at the specified arc length parameter.
            public Vector3 PositionAt(float l)
            {
                if (l < 0)
                {
                    //throw new Exception("(l <= 0)");
                    Vector3 tangent = ComputeTangent(
                        m_ControlPoints[0],
                        m_ControlPoints[0],
                        m_ControlPoints[1],
                        0,
                        0,
                        0).normalized;

                    return m_ControlPoints[0] + l * tangent;
                }

                if (l >= m_ArcLengthPositions.Last())
                {
                    //throw new Exception("l >= _arcLengthPositions.Last()");
                    Vector3 tangent = ComputeTangent(
                        m_ControlPoints[^2],
                        m_ControlPoints[^1],
                        m_ControlPoints[^1],
                        0,
                        0,
                        0).normalized;

                    return m_ControlPoints[^2] +
                           (l - m_ArcLengthPositions.Last()) * tangent;
                }

                float t = TimeAt(l);

                Vector3[] segment = GetSegmentPositions(SegmentIndex(t));

                return Interpolate(
                    segment[0],
                    segment[1],
                    segment[2],
                    segment[3],
                    0,
                    0,
                    0,
                    SegmentT(t));
            }

            /// @brief Computes the local parameter within a curve segment based on the given time parameter.
            /// @param t The time parameter for the curve segment.
            /// @return The local parameter within the curve segment (value between 0 and 1).
            protected float SegmentT(float t)
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

            /// @brief Perform cubic Hermite interpolation to calculate the position on the curve.
            /// @param point1 First control point.
            /// @param point2 Second control point.
            /// @param point3 Third control point.
            /// @param point4 Fourth control point.
            /// @param tension Tension factor for interpolation.
            /// @param continuity Continuity factor for interpolation.
            /// @param bias Bias factor for interpolation.
            /// @param t The parameter value for interpolation.
            /// @return The interpolated Vector3 position on the curve.
            public Vector3 Interpolate(
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

                var tangent2 =
                    ComputeTangent(point1, point2, point3, tension, continuity, bias);

                var tangent3 =
                    ComputeTangent(point2, point3, point4, tension, continuity, bias);

                float h1 = (float)(2 * Math.Pow(t, 3) - 3 * Math.Pow(t, 2) + 1);
                float h2 = (float)((-2) * Math.Pow(t, 3) + 3 * Math.Pow(t, 2));
                float h3 = (float)(Math.Pow(t, 3) - 2 * Math.Pow(t, 2) + t);
                float h4 = (float)(Math.Pow(t, 3) - Math.Pow(t, 2));

                Vector3 newPoint =
                    h1 * point2 +
                    h2 * point3 +
                    h3 * tangent2 +
                    h4 * tangent3;

                return newPoint;
            }

            /// @brief Calculate the arc length between two points on the interpolated curve.
            /// Recursively subdivides the segment until distances fall below the threshold.
            /// @param p1 The first control point.
            /// @param p2 The second control point.
            /// @param p3 The third control point.
            /// @param p4 The fourth control point.
            /// @param t1 The parameter value for the first interpolated point.
            /// @param t2 The parameter value for the second interpolated point.
            /// @param threshold The maximum distance threshold for recursive calculation (default: 0.1).
            /// @return The computed arc length between the two interpolated points.
            public float ArcLength(
                Vector3 p1,
                Vector3 p2,
                Vector3 p3,
                Vector3 p4,
                float t1,
                float t2,
                float threshold = 0.1f)
            {
                Vector3 interpolatedPoint1 =
                    Interpolate(p1, p2, p3, p4, Tension, Continuity, Bias, t1);

                Vector3 interpolatedPoint2 =
                    Interpolate(p1, p2, p3, p4, Tension, Continuity, Bias, t2);

                float distance =
                    Vector3.Distance(interpolatedPoint1, interpolatedPoint2);

                if (distance < threshold)
                {
                    return distance;
                }
                else
                {
                    float tMid = t1 + (t2 - t1) / 2;

                    return ArcLength(p1, p2, p3, p4, t1, tMid, threshold) +
                           ArcLength(p1, p2, p3, p4, tMid, t2, threshold);
                }
            }

            /// @brief Computes the arc length of a cubic Bezier curve segment at a specific parameter value.
            /// @param i The index of the curve segment.
            /// @return The arc length of the cubic Bezier curve segment at the given parameter value.
            private float ComputeArcLength(int i)
            {
                Vector3[] segment = GetSegmentPositions(i);

                return ArcLength(
                    segment[0],
                    segment[1],
                    segment[2],
                    segment[3],
                    0f,
                    1f);
            }

            /// @brief Retrieve the total arc length of the entire curve.
            /// @return The total arc length of the curve.
            public virtual float ArcLength()
            {
                return m_ArcLengthPositions.Last();
            }

            /// @brief Get the list of control points for the curve.
            public List<Vector3> ControlPoints => m_ControlPoints;

            /// @brief Check if the curve has any control points.
            /// @return True if the curve has no control points, otherwise false.
            public bool IsEmpty()
            {
                return m_ControlPoints.Count == 0;
            }

            /// @brief Finalize the curve, updating arc length information by computing the last segment's length.
            public virtual void Finish()
            {
                if (m_ControlPoints.Count < 2)
                {
                    return;
                }

                m_ArcLengthPositions.Add(
                    m_ArcLengthPositions.Last() +
                    ComputeArcLength(m_ControlPoints.Count - 2));
            }

            /// @brief Check if the curve has been fully processed and finalized.
            /// @return True if the curve is finished, otherwise false.
            public bool IsFinished()
            {
                if (this is BaseCurve)
                {
                    Debug.Log(
                        "BaseCurve is finished" +
                        (m_ControlPoints.Count >= 2 &&
                         m_ArcLengthPositions.Count == m_ControlPoints.Count));
                }

                Debug.Log(
                    "Curve is finished" +
                    (m_ControlPoints.Count >= 2 &&
                     m_ArcLengthPositions.Count == m_ControlPoints.Count) +
                    "Differenz: " +
                    (m_ArcLengthPositions.Count - m_ControlPoints.Count));

                return m_ControlPoints.Count >= 2 &&
                       m_ArcLengthPositions.Count == m_ControlPoints.Count;
            }
        }
    }
}