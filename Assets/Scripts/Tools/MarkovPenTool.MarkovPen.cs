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
    /// <summary>
    /// Represents a Markov Pen, generating random sylization while drawing
    /// </summary>  
    /// 
    /// <remarks>
    /// Implementation of Lang K., Alexa M.: The Markov Pen: Online Synthesis of 
    /// Free-Hand Drawing Styles. In Non-Photorealistic Animation and Rendering (2015), 
    /// Eurographics Association.
    /// <br>
    /// Note: Currently supports only (approximately) straight example base paths.
    /// </remarks>
    public partial class MarkovPen 
    {
        /// <summary>
        /// Example mapping containing the user-given base path and style curve for training
        /// </summary>
        private readonly ExampleMapping m_ExampleMapping;
        /// <summary>
        /// The target mapping used for synthesis
        /// </summary>
        private TargetMapping m_TargetMapping;
        /// <summary>
        /// The synthesis engine creating new stylization
        /// </summary>
        private readonly SynthesisEngine m_SynthesisEngine = new();

        /// <summary>
        /// Construct a MarkovPen instance
        /// </summary>
        /// 
        /// <remarks>
        /// Trains a Markov pen from an example style curve and base path.
        /// </remarks>
        /// 
        /// <param name="basePathControlPoints">
        /// Control points of the base path
        /// </param>
        /// <param name="styleCurveControlPoints">
        /// Control points of the style curve
        /// </param>
        public MarkovPen(List<Vector3> basePathControlPoints, List<Vector3> styleCurveControlPoints)
        {
            BasePath basePath = new BasePath(basePathControlPoints);
            Debug.Log("MarkovPen: Arclength of example base path: " + basePath.ArcLength());
            Curve styleCurve = new Curve(styleCurveControlPoints);
            Debug.Log("MarkovPen: Arclength of example style curve: " + styleCurve.ArcLength());

            m_ExampleMapping = new ExampleMapping(basePath, styleCurve);
            m_TargetMapping = new TargetMapping(m_ExampleMapping.GetMaxOffsetAlongNormals());
        }

        /// <summary>
        /// Reconstruct an example mapping one-to-one, producing a new target mapping
        /// </summary>
        /// 
        /// <remarks>
        /// Exactly reproduces the example mapping along a target base path,
        /// repeating it as needed to cover the entire arc length.
        /// </remarks>
        /// 
        /// <param name="pointer">
        /// The position and rotation of the pointing device
        /// </param>
        /// 
        /// <returns>
        /// The generated knots for the target style curve
        /// </returns>
        public List<Tuple<Vector3, Quaternion>> Stylize((Vector3 position, Quaternion rotation) pointer)
        {
            m_TargetMapping.AddBasePoint(pointer.position, pointer.rotation * new Vector3(0.0f,1.0f,0.0f));
        
            return m_SynthesisEngine.Reconstruct(m_ExampleMapping, m_TargetMapping);
        }

        /// <summary>
        /// Discard the current target mapping and start a fresh one
        /// </summary>
        /// 
        /// <remarks>
        /// Called on trigger-down.
        /// </remarks>
        public void NewLine()
        {
            m_TargetMapping = new TargetMapping(m_ExampleMapping.GetMaxOffsetAlongNormals());
        }

        /// <summary>
        /// Check if the model has been trained
        /// </summary>
        /// 
        /// <returns>
        /// true if the model is trained, false otherwise
        /// </returns>
        public bool IsTrained()
        {
            return m_ExampleMapping != null;
        }
    }
}