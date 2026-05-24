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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vector3 = System.Numerics.Vector3;

namespace ThreeDimensionalMarkovPen._3DMP
{
    partial class MarkovPen
    {
         /// <summary>
         /// The Synthesizer class is responsible for reconstructing the targetMapping based on the exampleMapping.
         /// Part of the MarkovPen; it generates associations for the targetMapping by utilizing information from the exampleMapping.
         /// </summary>
         private class Synthesizer
        {
            private Mapping m_ExampleMapping;

                 /// <summary>
                 /// Empty constructor for the Synthesizer class. Initializes with a null example mapping.
                 /// </summary>
                 public Synthesizer()
            {
                m_ExampleMapping = null;
            }

                 /// <summary>
                 /// Constructor for the Synthesizer class.
                 /// </summary>
                 /// <param name="exampleMapping">The example mapping used for generating associations.</param>
                 public Synthesizer(Mapping exampleMapping)
            {
                m_ExampleMapping = exampleMapping;
            }

                 /// <summary>
                 /// Reconstructs the target mapping by generating associations based on the example mapping.
                 /// Iteratively applies offsets to the target mapping, generating associations and inflating points.
                 /// </summary>
                 /// <param name="targetMapping">The target mapping to be reconstructed.</param>
                 /// <returns>A list of associations representing the reconstructed relationship between curves.</returns>
                 public List<Tuple<UnityEngine.Vector3, UnityEngine.Vector3>> Reconstruct(Mapping targetMapping)
            {
                float offset = m_ExampleMapping.MaxOffset;

                if (targetMapping.IsEmpty())
                {
                    targetMapping.SetMaxOffset(offset);
                }

                int index =
                    (targetMapping.LastIndex + 1) %
                    m_ExampleMapping.GetMapping.Count;

                List<Tuple<UnityEngine.Vector3, UnityEngine.Vector3>> associations =
                    new List<Tuple<UnityEngine.Vector3, UnityEngine.Vector3>>();

                while (true)
                {
                    Vector2 offsets = m_ExampleMapping.GetOffsets(index);

                    if (!targetMapping.Apply(offsets, index))
                    {
                        break;
                    }

                    associations.Add(
                        targetMapping.Inflate(
                            targetMapping.GetAssociation(
                                targetMapping.GetMapping.Count - 1)));

                    // Increment the index in a circular manner to iterate through the example mapping
                    index = (index + 1) % m_ExampleMapping.GetMapping.Count;
                }

                return associations;
            }

            /// <summary>
            /// Checks if the Synthesizer is trained with an example mapping.
            /// </summary>
            /// <returns>True if the Synthesizer is trained (exampleMapping is not null); otherwise false.</returns>
            public bool IsTrained()
            {
                return m_ExampleMapping != null;
            }

            /// <summary>
            /// Clears the example mapping in the Synthesizer, removing training data.
            /// </summary>
            public void Clear()
            {
                m_ExampleMapping = null;
            }
        }
    }
}