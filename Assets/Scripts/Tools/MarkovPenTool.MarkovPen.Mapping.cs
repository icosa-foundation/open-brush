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
    partial class MarkovPen
    {
        /// @class Mapping
        /// @brief Represents a mapping between arc length positions and offsets from style curve to base path.
        ///
        /// Samples the style curve, projects samples onto the base path, and computes mapping and offsets.
        public abstract class Mapping
        {
            // Curves
            protected BasePath m_BasePath;
            protected Curve m_StyleCurve;

            //Mapping
            protected List<Vector2> m_Mapping = new();

            public int Size()
            {
                return m_Mapping.Count;
            }

            public Vector2 Last()
            {
                return m_Mapping.Last();
            }

            /// @brief Check if the mapping is empty.
            /// @return True if the mapping is empty; otherwise false.
            public bool IsEmpty()
            {
                return m_Mapping.Count == 0;
            }

            /// @brief Gets the association at the specified index.
            /// @param index The index of the association to retrieve.
            /// @return A Vector2 representing the association (x = arc length position, y = offset).
            public Vector2 GetAssociation(int index)
            {
                return m_Mapping[index];
            }
        }

        public class ExampleMapping : Mapping
        {
            // Sampling interval
            private const float k_SamplingInterval = 0.025f;
            private float m_SamplingInterval = k_SamplingInterval;

            // Offsets along base path
            private List<float> m_OffsetsAlongBasePath;

            /// @brief Constructor for the Mapping class.
            /// @param styleCurve The style curve for the mapping.
            /// @param basePath The base path for the mapping.
            /// @exception NullReferenceException Thrown if styleCurve or basePath is null.
            public ExampleMapping(BasePath basePath, Curve styleCurve)
            {
                Debug.Log("MarkovPen: compute Mapping");

                m_BasePath = basePath;
                m_StyleCurve = styleCurve;

                // Compute sampling interval
                float samplingInterval = ComputeSamplingInterval();
                Debug.Log("MarkovPen: Sampling interval on style curve: " + samplingInterval);

                // Sample style curve
                List<Vector3> samples = SampleStyleCurve(samplingInterval);

                // Project samples onto base path
                List<float> projections = Project(samples);

                // Compute mapping
                ComputeMapping(samples, projections);

                // Compute maximum offset
                float maxOffsetAlongNormals = ComputeMaxOffsetInNormalDirection();
                basePath.Smooth(maxOffsetAlongNormals);
                Debug.Log("MarkovPen: Filter tap for normal smoothing: " + maxOffsetAlongNormals);

                // Compute offsets along base path
                ComputeOffsetsAlongBasePath();

                Debug.Log("MarkovPen: Mapping size: " + m_Mapping.Count);
            }

            /// @brief Check if the mapping is configured as repetitive.
            /// @return True if the mapping is considered repetitive.
            public bool IsRepetitive()
            {
                return true;
            }

            /// @brief Get the offsets for a given index in the mapping.
            /// @param index The index for which offsets are requested.
            /// @return A Vector2 containing the offset values.
            public Vector2 GetOffsets(int index)
            {
                return new Vector2(
                    m_OffsetsAlongBasePath[index],
                    m_Mapping[index].y);
            }

            public float GetMaxOffsetAlongNormals()
            {
                return m_BasePath.Tap;
            }

            /// @brief Compute the sampling interval to evenly sample the arc length of the style curve.
            /// @return The computed sampling interval.
            public float ComputeSamplingInterval()
            {
                int numSamples =
                    Mathf.RoundToInt(m_StyleCurve.ArcLength() / m_SamplingInterval);

                numSamples = Math.Max(numSamples, 5);

                return m_StyleCurve.ArcLength() / numSamples;
            }

            /// @brief Sample the style curve uniformly based on the given sampling interval.
            /// @param samplingInterval The interval representing arc length for sampling.
            /// @return A list of sampled points on the style curve.
            public List<Vector3> SampleStyleCurve(float samplingInterval)
            {
                List<Vector3> samples = new();

                int numSamples =
                    Mathf.RoundToInt(m_StyleCurve.ArcLength() / m_SamplingInterval) + 1;

                for (int i = 0; i < numSamples; i++)
                {
                    samples.Add(m_StyleCurve.PositionAt(i * samplingInterval));
                }

                return samples;
            }

            /// @brief Projects a list of 3D samples onto the base path and returns their projections.
            /// @param samples A list of 3D vectors representing the samples to be projected.
            /// @return A list of float values representing the projections onto the base path.
            public List<float> Project(List<Vector3> samples)
            {
                List<float> projections = new();

                foreach (var sample in samples)
                {
                    float projection = m_BasePath.Project(sample)[0];
                    projections.Add(projection);
                }

                return projections;
            }

            /// @brief Associate arc length positions of projected points to the corresponding offsets.
            /// @param samples Samples aligned along the style curve.
            /// @param projections Projected arc length positions aligned along the base path.
            private void ComputeMapping(List<Vector3> samples, List<float> projections)
            {
                m_Mapping = new();

                for (int i = 0; i < samples.Count; ++i)
                {
                    Vector3 basePoint = m_BasePath.PositionAt(projections[i]);

                    Vector3 smoothNormal =
                        m_BasePath.SmoothNormalAt(projections[i]);

                    Vector3 toSample = samples[i] - basePoint;

                    float offsetAlongNormal =
                        Vector3.Distance(basePoint, samples[i]);

                    if (Vector3.Dot(smoothNormal, toSample) < 0)
                    {
                        offsetAlongNormal *= -1;
                    }

                    m_Mapping.Add(
                        new Vector2(projections[i], offsetAlongNormal));
                }
            }

            /// @brief Compute the maximal offset from the associations in the mapping.
            private float ComputeMaxOffsetInNormalDirection()
            {
                float maxOffset = 0;

                foreach (var association in m_Mapping)
                {
                    maxOffset =
                        Math.Max(maxOffset, Math.Abs(association.y));
                }

                return maxOffset;
            }

            /// @brief Compute the offsets by iterating through the mapping and provide repetition handling.
            /// Synchronizes start and end points in the mapping.
            private void ComputeOffsetsAlongBasePath()
            {
                m_OffsetsAlongBasePath = new List<float>(m_Mapping.Count);

                for (int i = 1; i < m_Mapping.Count; ++i)
                {
                    m_OffsetsAlongBasePath.Add(
                        m_Mapping[i].x - m_Mapping[i - 1].x);
                }

                // Start and end point in mapping match
                if (IsRepetitive())
                {
                    Debug.Log("MarkovPen: Mapping is repetitive");

                    // Offset of last point becomes that of first
                    m_OffsetsAlongBasePath.Insert(
                        0,
                        m_OffsetsAlongBasePath.Last());

                    // Remove last point and offset
                    m_OffsetsAlongBasePath.RemoveAt(
                        m_OffsetsAlongBasePath.Count - 1);
                    m_Mapping.RemoveAt(m_Mapping.Count - 1);
                }
            }
        }

        public class TargetMapping : Mapping
        {
            public int LastIndex { get; private set; }

            public TargetMapping(float maxOffsetAlongNormals)
            {
                LastIndex = -1;
                m_BasePath = new BasePath(maxOffsetAlongNormals);
                m_StyleCurve = new Curve();
            }

            public void AddBasePoint(Vector3 point, Vector3 upVector)
            {
                m_BasePath.AddControlPoint(point, upVector);
            }

            /// @brief Inflate an association of the mapping to obtain a tuple of 3D points (base point and inflated point).
            /// @param association The 2D association containing arc length position and offset.
            /// @return A tuple of two Vector3 points representing the inflated segment.
            public Tuple<Vector3, Vector3> Inflate(Vector2 association)
            {
                Vector3 basePoint =
                    m_BasePath.PositionAt(association.x);

                Vector3 normal =
                    m_BasePath.SmoothNormalAt(association.x);

                Vector3 toPoint =
                    Vector3.Scale(
                        normal.normalized,
                        new Vector3(
                            association.y,
                            association.y,
                            association.y));

                return new Tuple<Vector3, Vector3>(
                    basePoint,
                    basePoint + toPoint);
            }

            /// @brief Apply offsets to the mapping at a specific index and update the style curve.
            /// @param offsets A Vector2 containing the offsets to be applied.
            /// @param index The index at which the offsets are applied.
            /// @return True if the offsets are successfully applied; otherwise false.
            public bool Apply(Vector2 offsets, int index)
            {
                // Compute new arcLength position
                float l =
                    IsEmpty()
                        ? 0
                        : m_Mapping.Last().x + offsets.x;

                // Check if new arcLength position exceeds the arcLength of the base path
                if (l >= m_BasePath.ArcLength())
                {
                    return false;
                }

                // Add new association to the mapping
                m_Mapping.Add(new Vector2(l, offsets.y));

                m_StyleCurve.AddControlPoint(
                    Inflate(m_Mapping.Last()).Item2);

                LastIndex = index;

                return true;
            }
        }
    }
}