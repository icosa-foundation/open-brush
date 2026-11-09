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
        /// <summary> 
        /// Represents a Catmull-Rom spline
        /// </summary>
        /// 
        /// <remarks>
        /// Provides basic spline functionalities: 
        /// adding control points, computing arc length positions, 
        /// and evaluating positions and derivatives.
        /// </remarks>
        public class Curve
        {
            /// <summary>
            /// The arc length positions of the knots
            /// </summary>
            protected List<float> m_ArcLengthPositions;
            
            /// <summary>
            /// The 3D positions of the knot points
            /// </summary>
            protected List<Vector3> m_ControlPoints = new();
            
            /// <summary>
            /// The previous input cached for Elasticurve smoothing 
            /// </summary>
            private Vector3 m_LastInput = Vector3.zero;

            /// <summary>
            /// The default responsiveness of the curve for Elasticurve smoothing
            /// </summary>            
            ///
            /// <remarks>
            /// 1 means no smoothing
            /// </remarks>
            private const float k_Responsiveness = 1f;

            /// <summary>
            /// The responsiveness of the curve for Elasticurve smoothing
            /// </summary>            
            private readonly float m_Responsiveness;

            /// <summary> 
            /// Construct an empty Curve instance
            /// </summary>
            /// 
            /// <remarks>
            /// Creates a target curve with initially no knots 
            /// and zero arc length.
            /// </remarks>
            /// 
            /// <param name="responsiveness">  
            /// Responsiveness parameter controlling Elasticurve smoothing 
            /// (default: 1 = no smoothing)
            /// </param>
            public Curve(float responsiveness = k_Responsiveness)
            {
                m_Responsiveness = responsiveness;
                m_ArcLengthPositions = new List<float> { 0f };
            }
            
            /// <summary> 
            /// Construct a Curve instance from a set of control points
            /// </summary>
            /// 
            /// <remarks>
            /// Creates a fully initialized example curve
            /// </remarks>
            ///
            /// <param name="controlPoints">
            /// Points forming the curve in OpenBrush
            /// </param>
            public Curve(List<Vector3> controlPoints): this()
            {
                foreach (var point in controlPoints)
                {
                    AddControlPoint(point);
                }
                Finish();
            }

            /// <summary>
            /// Check whether the curve has been finalized
            /// </summary>
            ///
            /// <returns>
            /// True if the curve is finished, false otherwise
            /// </returns>
            public virtual bool IsFinished()
            {
                return m_ControlPoints.Count >= 2 &&
                    m_ArcLengthPositions.Count == m_ControlPoints.Count;
            }

            /// <summary>
            /// Add a control point to a target curve 
            /// and perform all necessary updates
            /// </summary>
            /// 
            /// <param name="controlPoint">
            /// The new control point to be added
            /// </param>
            public void AddControlPoint(Vector3 controlPoint)
            {
                // If the curve is empty, the control point is directly added
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

                // Elasticurve (Yannick Thiel, Karan Singh, and Ravin Balakrishnan. 2011) 
                // implementation adapted for Catmull-Rom splines
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

            /// <summary> 
            /// Request the total arc length of the curve
            /// </summary>
            /// 
            /// <remarks>
            /// Returns the distance to walk along the curve 
            /// from the beginning to the end.
            /// </remarks>
            /// 
            /// <returns> 
            /// The total arc length of the curve
            /// </returns>
            public virtual float ArcLength()
            {
                return m_ArcLengthPositions.Last();
            }

            /// <summary> 
            /// Evaluate 3D point at given arc length position
            /// </summary>
            /// 
            /// <remarks>
            /// Retrieves the curve point corresponding to the given arc length position.
            /// Returns extrapolated positions when l is outside the curve range.
            /// </remarks>
            /// 
            /// <param name="l">  
            /// Arc length position (may be negative or beyond the arc length of the curve)
            /// </param>
            /// 
            /// <returns>
            /// 3D Point at the specified arc length position
            /// </returns>
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

            /// <summary> 
            /// Evaluate first derivative at given arc length position
            /// </summary>
            /// 
            /// <remarks>
            /// Returns the tangent vector at the specified arc length position.
            /// </remarks>
            /// 
            /// <param name="l">
            /// Arc length position (may be negative or beyond the arc length of the base path)
            /// </param>
            /// 
            /// <returns> 
            /// Derivative vector at the specified position
            /// </returns>
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

            /// <summary> 
            /// Get an array of four 3D positions making up the segment at the given index
            /// </summary>
            /// 
            /// <param name="index"> 
            /// The segment index (index of the second control point)
            /// </param>
            /// 
            /// <returns>
            /// An array of four 3D positions
            /// </returns>
            protected Vector3[] GetSegmentPositions(int index)
            {
                Vector3 point1 = m_ControlPoints[index == 0 ? index : index - 1];
                Vector3 point2 = m_ControlPoints[index];
                Vector3 point3 = m_ControlPoints[index + 1];
                Vector3 point4 = m_ControlPoints[index == m_ControlPoints.Count - 2 ? index + 1 : index + 2];

                return new[] { point1, point2, point3, point4 };
            }

            /// <summary> 
            /// Perform cubic Hermite interpolation 
            /// to compute the position at the specified time.
            /// </summary>
            /// 
            /// <param name="segment"> 
            /// The segment consisting of four control points
            /// </param>
            /// <param name="t">
            /// The time value for interpolation ranging between 0 and 1
            /// </param>
            /// 
            /// <returns>
            /// The interpolated 3D point
            /// </returns>
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

            /// <summary> 
            /// Perform cubic Hermite interpolation 
            /// to estimate the first derivative at the specified time.
            /// </summary>
            /// 
            /// <param name="segment"> 
            /// The segment consisting of four control points
            /// </param>
            /// <param name="t"> 
            /// The time value for interpolation ranging between 0 and 1
            /// </param>
            /// 
            /// <returns>
            /// The interpolated first derivative
            /// </returns>
            public Vector3 EvaluateFirstDerivative(Vector3[] segment, float t)
            {

                (Vector3, Vector3) tangents = ComputeTangentsAtEndpoints(segment);

                // First derivatives of hermite basis functions
                float h1 = (float)(6 * Math.Pow(t, 2) - 6 * Math.Pow(t, 1));
                float h2 = (float)((-6) * Math.Pow(t, 2) + 6 * Math.Pow(t, 1));
                float h3 = (float)(3 * Math.Pow(t, 2) - 4 * Math.Pow(t, 1) + 1);
                float h4 = (float)(3 * Math.Pow(t, 2) - 2 * Math.Pow(t, 1));

                return
                    h1 * segment[1] +
                    h2 * segment[2] +
                    h3 * tangents.Item1 +
                    h4 * tangents.Item2;
            }

            /// <summary> 
            /// Compute the tangent vectors at the endpoints of a segment
            /// </summary>
            /// 
            /// <param name="segment"> 
            /// The segment consisting of four control points
            /// </param>
            /// 
            /// <returns> 
            /// The computed tangent vectors
            /// </returns>
            protected static (Vector3, Vector3) ComputeTangentsAtEndpoints(Vector3[] segment)
            {
                return ((segment[2] - segment[0]) / 2, (segment[3] - segment[1]) / 2);
            }

            /// <summary> 
            /// Compute the local time value within a curve segment based on the global one
            /// </summary>
            /// 
            /// <param name="t"> 
            /// The global time value.
            /// </param>
            /// 
            /// <returns>
            /// The local time value within the curve segment 
            /// (ranging between 0 and 1).
            /// </returns>
            protected float SegmentTime(float t)
            {
                return t % 1;
            }

            /// <summary> 
            /// Compute the integer segment index from the given global time value
            /// </summary>
            /// 
            /// <param name="t">
            /// The global time value
            /// </param>
            /// 
            /// <returns>
            /// The rounded down integer value of 't'
            /// </returns>
            protected int SegmentIndex(float t)
            {
                return Mathf.FloorToInt(t);
            }

            /// <summary> 
            /// Convert arc length position to time value
            /// </summary>
            /// 
            /// <remarks>
            /// Computes the time value corresponding to a given arc length position
            /// </remarks>
            /// 
            /// <param name="l"> 
            /// Specified arc length position
            /// </param>
            /// 
            /// <returns> 
            /// Time value corresponding to arc length position
            /// </returns>
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

            /// <summary> 
            /// Computes the arc length of a Catmull-Rom curve segment
            /// </summary>
            /// 
            /// <param name="index"> 
            /// The index of the segment.
            /// </param>
            /// 
            /// <returns>
            /// The arc length of the curve segment at the given index
            /// </returns>
            private float ComputeArcLength(int index)
            {
                Vector3[] segment = GetSegmentPositions(index);

                // If the segment is a straight line, just return distance between endpoints
                if (Vector3.Distance(segment[0], segment[1]) == 0 &&
                    Vector3.Distance(segment[2], segment[3]) == 0)
                {
                    return Vector3.Distance(segment[1], segment[2]);
                }

                return ComputeArcLength(segment, 0f, 1f);
            }

            /// <summary>
            /// Compute the arc length distance between two points within a segment
            /// </summary>
            /// 
            /// <param name="segment"> 
            /// The segment consisting of four control points
            /// </param>
            /// <param name="t1"> 
            /// The time parameter for the first interpolated point
            /// </param>
            /// <param name="t2"> 
            /// The time parameter for the second interpolated point
            /// </param>
            /// <param name="threshold"> 
            /// The distance threshold for recursion to end (default: 0.001)
            /// </param>
            /// 
            /// <returns> 
            /// The arc length distance between the two interpolated points
            /// </returns>
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

            /// <summary> 
            /// Check if the curve has any control points
            /// </summary>
            /// 
            /// <returns>
            /// True if the curve has no control points, false otherwise
            /// </returns>
            public bool IsEmpty()
            {
                return m_ControlPoints.Count == 0;
            }

            /// <summary> 
            /// Finalize the curve
            /// </summary>
            /// 
            /// <remarks>
            /// Adds cached control point and complements the last two segments' arc length.
            /// </remarks>
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

            /// <summary>
            /// Compute a vector that is orthogonal to the given one
            /// </summary>
            ///     
            /// <remarks>
            /// Rotates the given vector by ninety degrees clockwise.
            /// </remarks>
            /// 
            /// <param name="v"> 
            /// The original vector
            /// <param>
            /// 
            /// <returns>
            /// A vector that corresponds to the original vector 
            /// rotated by ninety degrees clockwise
            /// </returns>
            public static Vector3 Orthogonal(Vector3 v)
            {
                return new Vector3(-v.y, v.x);
            }
        }
    }
}
