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

namespace TiltBrush
{
    partial class MarkovPen
    {
        /// @class Synthesizer
        /// @brief The Synthesizer class is responsible for reconstructing the targetMapping based on the exampleMapping.
        ///
        /// Part of the MarkovPen; it generates associations for the targetMapping by utilizing information from the exampleMapping.
        private class Synthesizer
        {
            private Mapping m_ExampleMapping;

            /// @brief Empty constructor for the Synthesizer class. Initializes with a null example mapping.
            public Synthesizer()
            {
                m_ExampleMapping = null;
            }

            /// @brief Constructor for the Synthesizer class.
            /// @param exampleMapping The example mapping used for generating associations.
            public Synthesizer(Mapping exampleMapping)
            {
                m_ExampleMapping = exampleMapping;
            }

            /// @brief Reconstructs the target mapping by generating associations based on the example mapping.
            /// Iteratively applies offsets to the target mapping, generating associations and inflating points.
            /// @param targetMapping The target mapping to be reconstructed.
            /// @return A list of associations representing the reconstructed relationship between curves.
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

            /// @brief Checks if the Synthesizer is trained with an example mapping.
            /// @return True if the Synthesizer is trained (exampleMapping is not null); otherwise false.
            public bool IsTrained()
            {
                return m_ExampleMapping != null;
            }

            /// @brief Clears the example mapping in the Synthesizer, removing training data.
            public void Clear()
            {
                m_ExampleMapping = null;
            }
        }
    }
}