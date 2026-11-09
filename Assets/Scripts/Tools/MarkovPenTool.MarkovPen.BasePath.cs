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
        /// <summary>
        /// Represents the base path of a Markov Pen
        /// </summary>
        /// 
        /// <remarks>
        /// Extends the functionality of the Curve class and provides
        /// additional features such as projection and smoothing functionalities.
        /// </remarks>
        public class BasePath : Curve
        {
            /// <summary>
            /// The tap (half-window size) used for normal smoothing
            /// </summary>
            public float Tap { get; private set; }

            /// <summary> 
            /// The list of up vectors for each knot
            /// </summary>
            private readonly List<Vector3> m_UpVectors = new();

            /// <summary> 
            /// The list of smooth normals for each knot
            /// <summary>
            private readonly List<Vector3> m_SmoothNormals = new();

            /// <summary> 
            /// Construct an empty BasePath instance
            /// </summary>
            /// 
            /// <remarks>
            /// Creates a new target base path with initially no knots and zero arc length.
            /// </remarks>
            /// 
            /// <param name="smoothingTap">  
            /// The tap (half window size) for normal smoothing
            /// </param>
            /// <param name="responsiveness">
            /// Responsiveness parameter controlling Elasticurve smoothing 
            /// (default: 0.75).
            /// </param>
            public BasePath(float smoothingTap, float responsiveness = 0.75f) : base(responsiveness)
            {
                Tap = smoothingTap;
            }

            /// <summary> 
            /// Construct a BasePath instance from a list of control points
            /// </summary>
            ///
            /// <remarks>
            /// Creates a fully initialized example base path.
            /// </remarks>
            ///
            /// <param name="controlPoints">
            /// 3D Points forming the base path in OpenBrush
            /// </param>
            public BasePath(List<Vector3> controlPoints)
            {
                Vector3 upVector = Orthogonal(controlPoints.Last() - controlPoints.First()).normalized;

                AddControlPoint(controlPoints.First(), upVector);
                AddControlPoint(controlPoints.Last(), upVector);
                Finish();
            }

            /// <summary>
            /// Add a control point to a target base path 
            /// and perform all necessary updates
            /// </summary>
            ///
            /// <remarks>
            /// Extends the base class method to incorporate smoothing of up vector and normals.
            /// </remarks>
            ///
            /// <param name="controlPoint">
            /// The new control point to be added
            /// </param>
            /// <param name="upVector">
            /// The up vector associated with the control point
            /// </param>
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

            /// <summary> 
            /// Request the total arc length of the base path
            /// </summary>
            /// 
            /// <remarks>
            /// Returns the last arc length position
            /// for which smooth normals have already been computed.
            /// </remarks>
            /// 
            /// <returns>
            /// The total arc length of the base path
            /// </returns>
            public override float ArcLength()
            {
                if (Tap == 0)
                {
                    return base.ArcLength();
                }

                return m_ArcLengthPositions[Math.Max(m_SmoothNormals.Count - 1, 0)];
            }

            /// <summary>
            /// Set the tap (half-window size) used for normal smoothing
            /// </summary>
            /// 
            /// <param name="tap">
            /// The smoothing tap
            /// </param>
            public void Smooth(float tap)
            {
                Tap = tap;
            }

            /// <summary>
            /// Evaluate smoothed normal vector at given arc length position
            /// </summary>
            /// 
            /// <param name="l"> 
            /// Arc length position (may be negative or beyond the arc length of the base path)
            /// </param>
            /// 
            /// <returns>
            /// Smoothed normal vector
            /// </returns>
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

            /// <summary>
            /// Compute smoothed tangent vector at given index
            /// </summary>
            /// 
            /// <param name="l">
            /// The knot index starting from 0
            /// </param>
            /// 
            /// <returns> 
            /// Smoothed tangent vector
            /// </returns>
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

            /// <summary>
            /// Compute smoothed normal vector at given index
            /// </summary>
            /// 
            /// <param name="index"> 
            /// The knot index starting from 0
            /// </param>
            /// 
            /// <returns>
            /// Smoothed normal vector
            /// </returns>
            private Vector3 ComputeSmoothNormal(int index)
            {
                Vector3 upVector = m_UpVectors[index].normalized * 100;
                Vector3 smoothTangentDirection = ComputeSmoothTangent(index).normalized;

                // Project prologenged up vector onto smooth tangent
                float projection = Vector3.Dot(upVector, smoothTangentDirection);

                // make up vector perpendicular to smoot tangent
                return (upVector - smoothTangentDirection * projection).normalized;
            }

            /// <summary>
            /// Project a point onto the base path
            /// </summary>
            /// 
            /// <remarks>
            /// Determines the arc length positions 
            /// where the prolonged smooth normal 
            /// (approximately) runs through the given 2D point.<br>
            /// Note that there may be multiple approximate projection points.
            /// </remarks>
            ///
            /// <param name="toProject">  
            /// Point to project
            /// </param>
            /// 
            /// <returns> 
            /// List of arc length positions the point projects to
            /// </returns>
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

            /// <summary>
            /// Project a point onto a portion of the base path
            /// </summary>
            /// 
            /// <remarks>
            /// Determines the arc length positions within a specified interval 
            /// where the prolonged smooth normal 
            /// (approximately) runs through the given 2D point.<br>
            /// Note that there may be multiple approximate projection points.
            /// </remarks>
            /// 
            /// <param name="point">
            /// Point to project
            /// </param>
            /// <param name="l1">
            /// Beginning of arc length interval
            /// </param>
            /// <param name="l2">
            /// End of arc length interval
            /// </param>
            /// <param name="projections">  
            /// List of arc length positions the point projects to
            /// </param>
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

            /// <summary>
            /// Compute the distance of a 2D point 
            /// to the prolonged smooth normal 
            /// at the given arc length position
            /// </summary>
            /// 
            /// <remarks>
            /// Projects a point onto the smoothed normal vector 
            /// at the specified arc length position
            /// and computes the signed distance of the point to the projected point.
            /// </remarks>
            /// 
            /// <param name="point"> 
            /// The point to project
            /// </param>
            /// <param name="l">
            /// Arc length position at which the smooth normal is to be evaluated
            /// </param>
            /// 
            /// <returns> 
            /// Signed distance to the normal line at l
            /// </returns>
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

            /// <summary>
            /// Finalize the base path
            /// </summary>
            /// 
            /// <remarks>
            /// Computes remaining smooth normals.
            /// </remarks>
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
