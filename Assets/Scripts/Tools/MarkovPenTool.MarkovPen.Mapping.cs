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
using VRSketchingGeometry.Splines;

namespace TiltBrush
{
    partial class MarkovPen
    {
        /// @class Mapping
        /// @brief Represents a mapping between arc length positions and offsets from style to base curve.
        ///
        /// Samples the style curve, projects samples onto the base curve, and computes mapping and offsets.
        public class Mapping
        {
            //Curves
            public BaseCurve BaseCurve { get; private set; }

            private Curve m_StyleCurve;

            //SamplingInterval
            private const float k_SamplingInterval = 0.025f;

            private float m_SamplingInterval = k_SamplingInterval;

            //Mapping
            private List<Vector2> m_Mapping = new List<Vector2>();

            //Offsets
            private List<float> m_OffsetsAlongCurve;

            private float m_MaxOffset = 0f;

            public int LastIndex { get; private set; }

            /// @brief Constructor for the Mapping class.
            /// @param styleCurve The style curve for the mapping.
            /// @param baseCurve The base curve for the mapping.
            /// @exception NullReferenceException Thrown if styleCurve or baseCurve is null.
            public Mapping(Curve styleCurve, BaseCurve baseCurve)
            {
                Debug.Log("ENter");

                LastIndex = -1;

                if (styleCurve == null)
                {
                    throw new NullReferenceException("StyleCurve must not be null");
                }

                if (baseCurve == null)
                {
                    throw new NullReferenceException("BaseCurve must not be null");
                }

                Debug.Log("ENter2");

                BaseCurve = baseCurve;
                m_StyleCurve = styleCurve;

                if (m_StyleCurve.ArcLength() < m_SamplingInterval)
                {
                    return;
                }

                Debug.Log("ENter3");

                //compute sampling interval
                float samplingInterval = ComputeSamplingInterval();

                //sample style curve
                List<Vector3> samples = SampleStyleCurveUniformly(samplingInterval);

                //project samples to base curve
                List<float> projections = Project(samples);

                //compute mapping
                ComputeMapping(samples, projections);

                //compute maximum offset
                ComputeMaxOffset();

                //compute offsets
                ComputeOffsets();
            }

            /// @brief Sample the style curve uniformly based on the given sampling interval.
            /// @param samplingInterval The interval representing arc length for sampling.
            /// @return A list of sampled points on the style curve.
            public List<Vector3> SampleStyleCurveUniformly(float samplingInterval)
            {
                List<Vector3> samples = new List<Vector3>();

                int numSamples =
                    Mathf.RoundToInt(m_StyleCurve.ArcLength() / m_SamplingInterval) + 1;

                for (int i = 0; i < numSamples; i++)
                {
                    samples.Add(m_StyleCurve.PositionAt(i * samplingInterval));
                }

                return samples;
            }

            /// @brief Projects a list of 3D samples onto the base curve and returns their projections.
            /// @param samples A list of 3D vectors representing the samples to be projected.
            /// @return A list of float values representing the projections onto the base curve.
            public List<float> Project(List<Vector3> samples)
            {
                List<float> projections = new List<float>();

                foreach (var sample in samples)
                {
                    float projection = BaseCurve.Project(sample)[0];
                    projections.Add(projection);
                }

                return projections;
            }

            /// @brief Compute the sampling interval to evenly sample the arc length of the style curve.
            /// @return The computed sampling interval.
            public float ComputeSamplingInterval()
            {
                int numSamples =
                    Mathf.RoundToInt(m_StyleCurve.ArcLength() / m_SamplingInterval);

                return m_StyleCurve.ArcLength() / numSamples;
            }

            /// @brief Associate arc length positions of projected points to the corresponding offsets.
            /// @param samples Samples aligned along the style curve.
            /// @param projections Projected arc length positions aligned along the base curve.
            private void ComputeMapping(List<Vector3> samples, List<float> projections)
            {
                m_Mapping = new List<Vector2>();

                for (int i = 0; i < samples.Count; ++i)
                {
                    Vector3 basePoint = BaseCurve.PositionAt(projections[i]);

                    Vector3 smoothNormal =
                        BaseCurve.SmoothNormalAt(projections[i]);

                    Vector3 toSample = samples[i] - basePoint;

                    float offsetAlongNormal =
                        Vector3.Distance(basePoint, samples[i]);

                    if (Vector3.Dot(smoothNormal, toSample) < 0)
                    {
                        offsetAlongNormal *= -1;
                    }

                    // Debug.Log("association: " + new Vector2(projections[i], offset));

                    m_Mapping.Add(
                        new Vector2(projections[i], offsetAlongNormal));
                }
            }

            /// @brief Compute the maximal offset from the associations in the mapping.
            private void ComputeMaxOffset()
            {
                m_MaxOffset = 0;

                foreach (var association in m_Mapping)
                {
                    m_MaxOffset =
                        Math.Max(m_MaxOffset, Math.Abs(association.y));
                }
            }

            /// @brief Compute the offsets by iterating through the mapping and provide repetition handling.
            /// Synchronizes start and end points in the mapping.
            private void ComputeOffsets()
            {
                // Synchronize start and end point in mapping
                m_OffsetsAlongCurve = new List<float>(m_Mapping.Count);

                for (int i = 1; i < m_Mapping.Count; ++i)
                {
                    m_OffsetsAlongCurve.Add(
                        m_Mapping[i].x - m_Mapping[i - 1].x);
                }

                if (IsRepetitive())
                {
                    m_OffsetsAlongCurve.Insert(
                        0,
                        m_OffsetsAlongCurve.Last());

                    m_OffsetsAlongCurve.RemoveAt(
                        m_OffsetsAlongCurve.Count - 1);

                    m_Mapping.RemoveAt(m_Mapping.Count - 1);
                }
            }

            /// @brief Sets the tap of the BaseCurve to the computed max offset.
            /// @param offset The max offset to set as tap.
            public void SetMaxOffset(float offset)
            {
                if (IsEmpty()) ;

                BaseCurve.SetTap(offset);

                //_maxOffset = offset;
            }

            /// @brief Check if the mapping is configured as repetitive.
            /// @return True if the mapping is considered repetitive.
            public bool IsRepetitive()
            {
                return true;
            }

            /// @brief Inflate an association of the mapping to obtain a tuple of 3D points (base point and inflated point).
            /// @param association The 2D association containing arc length position and offset.
            /// @return A tuple of two Vector3 points representing the inflated segment.
            public Tuple<Vector3, Vector3> Inflate(Vector2 association)
            {
                Vector3 basePoint =
                    BaseCurve.PositionAt(association.x);

                Vector3 normal =
                    BaseCurve.SmoothNormalAt(association.x);

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

            /// @brief Get the offsets for a given index in the mapping.
            /// @param index The index for which offsets are requested.
            /// @return A Vector2 containing the offset values.
            public Vector2 GetOffsets(int index)
            {
                return new Vector2(
                    m_OffsetsAlongCurve[index],
                    m_Mapping[index].y);
            }

            /// @brief Apply offsets to the mapping at a specific index and update the style curve.
            /// @param offsets A Vector2 containing the offsets to be applied.
            /// @param index The index at which the offsets are applied.
            /// @return True if the offsets are successfully applied; otherwise false.
            public bool Apply(Vector2 offsets, int index)
            {
                // Calculate the current arcLength
                float l =
                    IsEmpty()
                        ? 0
                        : m_Mapping.Last().x + offsets.x;

                if (l >= BaseCurve.ArcLength())
                {
                    // Check if the current arcLength exceeds the arcLength of the base curve
                    return false;
                }

                // Update _mapping by adding a point out of current arcLength and its offset
                m_Mapping.Add(new Vector2(l, offsets.y));

                m_StyleCurve.Append(
                    Inflate(m_Mapping.Last()).Item2);

                LastIndex = index;

                return true;
            }

            /// @brief Check if the mapping is empty.
            /// @return True if the mapping is empty; otherwise false.
            public bool IsEmpty()
            {
                return m_Mapping.Count == 0;
            }

            /// @brief Clears attributes related to the mapping and resets state.
            public void Clear()
            {
                BaseCurve = null;

                m_StyleCurve = null;

                m_Mapping.Clear();

                m_OffsetsAlongCurve.Clear();

                m_MaxOffset = 0.0f;

                m_SamplingInterval = k_SamplingInterval;
            }

            /// @brief Gets the mapping associated with the instance.
            public List<Vector2> GetMapping => m_Mapping;

            /// @brief Gets the maximum offset associated with the mapping instance.
            public float MaxOffset => m_MaxOffset;

            /// @brief Gets the association at the specified index.
            /// @param index The index of the association to retrieve.
            /// @return A Vector2 representing the association (x = arc length position, y = offset).
            public Vector2 GetAssociation(int index)
            {
                return m_Mapping[index];
            }
        }
    }
}