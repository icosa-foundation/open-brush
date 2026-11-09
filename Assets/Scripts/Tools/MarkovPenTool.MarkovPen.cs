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
using UnityEngine;

namespace TiltBrush
{
    /// @class MarkovPen
    /// @brief The Markov Pen is a technique for generating character styles.
    ///
    /// This class serves as the base for other partial classes and derives from MarkovPenTool.
    public partial class MarkovPen 
    {
        private readonly ExampleMapping m_ExampleMapping;
        private TargetMapping m_TargetMapping;
        private readonly SynthesisEngine m_SynthesisEngine = new();

        /// @brief Constuct a MarkovPen instance
        /// 
        /// @param basePathControlPoints - Control Points of the Base Path
        /// @param styleCurveControlPoints - Control points of the Style Curve
        public MarkovPen(List<Vector3> basePathControlPoints, List<Vector3> styleCurveControlPoints)
        {
            BasePath basePath = new BasePath(basePathControlPoints);
            Debug.Log("MarkovPen: Arclength of example base path: " + basePath.ArcLength());
            Curve styleCurve = new Curve(styleCurveControlPoints);
            Debug.Log("MarkovPen: Arclength of example style curve: " + styleCurve.ArcLength());

            m_ExampleMapping = new ExampleMapping(basePath, styleCurve);
            m_TargetMapping = new TargetMapping(m_ExampleMapping.GetMaxOffsetAlongNormals());
        }

        /// @brief Reconstructs the target mapping using the Synthesizer and returns the reconstructed points.
        /// @param targetMapping A Mapping representing the growing target base path and an empty target style curve.
        /// @return A list of reconstructed point pairs on the target curve.
        public List<Tuple<Vector3, Quaternion>> Stylize((Vector3 position, Quaternion rotation) pointer)
        {
            m_TargetMapping.AddBasePoint(pointer.position, pointer.rotation * new Vector3(0.0f,1.0f,0.0f));
        
            return m_SynthesisEngine.Reconstruct(m_ExampleMapping, m_TargetMapping);
        }

        /// @brief Discard the current target mapping and start a fresh one
        /// @note Called on trigger-down
        public void NewLine()
        {
            m_TargetMapping = new TargetMapping(m_ExampleMapping.GetMaxOffsetAlongNormals());
        }

        public bool IsTrained()
        {
            return m_ExampleMapping != null;
        }

    }
}