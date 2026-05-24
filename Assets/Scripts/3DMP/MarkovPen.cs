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
using System.Data;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// The Markov Pen is a technique for generating character styles.
    /// This class serves as the base for other partial classes and derives from MonoBehaviour.
    /// </summary>
    public partial class MarkovPen : MonoBehaviour
    {
        private Mapping m_ExampleMapping;
        private Mapping m_TargetMapping;
        private Synthesizer m_Synthesizer;

        private Curve m_ExampleStyleCurve;
        private Curve m_TargetStyleCurve;

        private BaseCurve m_ExampleBaseCurve;
        private BaseCurve m_TargetBaseCurve;

         /// <summary>
         /// Initializes the MarkovPen with an example mapping used for synthesis.
         /// </summary>
         /// <param name="exampleMapping">A Mapping computed from the example curves.</param>
         public void Initialize(Mapping exampleMapping)
        {
            m_ExampleMapping = exampleMapping;

            Debug.Log("mapping " + m_ExampleMapping.IsEmpty());

            m_Synthesizer = new Synthesizer(m_ExampleMapping);
        }

         /// <summary>
         /// Reconstructs the target mapping using the Synthesizer and returns the reconstructed points.
         /// </summary>
         /// <param name="targetMapping">A Mapping representing the growing target base curve and an empty target style curve.</param>
         /// <returns>A list of reconstructed point pairs on the target curve.</returns>
         public List<Tuple<Vector3, Vector3>> Reconstruct(Mapping targetMapping)
        {
            List<Tuple<Vector3, Vector3>> result =
                m_Synthesizer.Reconstruct(targetMapping);

            return result;
        }

         /// <summary>
         /// Checks whether the MarkovPen is trained (example mapping present).
         /// </summary>
         /// <returns>True if the MarkovPen is trained; otherwise false.</returns>
         public bool IsTrained()
        {
            return m_ExampleMapping != null;
        }

         /// <summary>
         /// Clears the MarkovPen state by nullifying the synthesizer and example mapping.
         /// </summary>
         public void Clear()
        {
            m_Synthesizer = null;
            m_ExampleMapping = null;
        }
    }
}