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
    //@class MarkovPen
    //@brief The Markov Pen is a technique for generating character styles."
    /*
     *This class serves as the base for other partial classes. It is an GameObject in Unity and needs therefore derive from MonoBehaviour.
    */
    public partial class MarkovPen : MonoBehaviour
    {
        private Mapping m_ExampleMapping;
        private Mapping m_TargetMapping;
        private Synthesizer m_Synthesizer;

        private Curve m_ExampleStyleCurve;
        private Curve m_TargetStyleCurve;

        private BaseCurve m_ExampleBaseCurve;
        private BaseCurve m_TargetBaseCurve;

        /**
 * @brief Initializes the MarkovPen base class.
 * 
 * This method is responsible for setting up the MarkovPen by providing it with an example mapping, which represents the relation
 * between arc length positions and offsets from style to base curve.
 * 
 * @param exampleMapping A Mapping computed from the example curves, containing the association between arc length positions and offsets.
 */
        public void Initialize(Mapping exampleMapping)
        {
            m_ExampleMapping = exampleMapping;

            Debug.Log("mapping " + m_ExampleMapping.IsEmpty());

            m_Synthesizer = new Synthesizer(m_ExampleMapping);
        }

        /**
 * @brief Reconstructs the targetMapping using the Synthesizer class.
 * 
 * This method calls the Synthesizer class to reconstruct the targetMapping in the same way as the exampleMapping. 
 * It takes a targetMapping, which represents the association between arc length positions and offsets for a growing targetBaseCurve 
 * and an empty targetStyleCurve.
 * 
 * @param targetMapping A Mapping representing the growing targetBaseCurve and an empty targetStyleCurve.
 * @return A list of Tuple<Vector3, Vector3> representing the reconstructed points on the target curve.
 */
        public List<Tuple<Vector3, Vector3>> Reconstruct(Mapping targetMapping)
        {
            List<Tuple<Vector3, Vector3>> result =
                m_Synthesizer.Reconstruct(targetMapping);

            return result;
        }

        /**
 * @brief Checks if the MarkovPen is trained.
 * 
 * This method returns a boolean value indicating whether the MarkovPen is considered trained. 
 * It checks if the _exampleMapping is not null.
 * 
 * @return True if the MarkovPen is trained (exampleMapping is not null), false otherwise.
 */
        public bool IsTrained()
        {
            return m_ExampleMapping != null;
        }

        /**
 * @brief Clears the MarkovPen by setting the synthesizer and exampleMapping to null.
 * 
 * This method resets the MarkovPen by nullifying both the synthesizer and exampleMapping attributes.
 * It is used to clean up the MarkovPen's state, making it ready for new training or operations.
 */
        public void Clear()
        {
            m_Synthesizer = null;
            m_ExampleMapping = null;
        }
    }
}