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
    partial class MarkovPen
    {
        
        /// <summary>
        /// Represents the synthesis engine of a Markov Pen 
        /// that generates associations of the target mapping
        /// </summary>
        private class SynthesisEngine
        {
            /// <summary>
            /// Reconstruct an example mapping one-to-one
            /// </summary>
            /// 
            /// <remarks>
            /// Exactly reproduces the example mapping along a target base path
            /// by iteratively applying offsets to the target mapping
            /// </remarks>
            /// 
            /// <param name="exampleMapping">
            /// The original mapping to reproduce
            /// </param>
            /// 
            /// <param name="targetMapping">
            /// The target mapping to populate
            /// </param>
            /// <returns>
            /// A list of control points on the target style curve
            /// and the associated rotations of the controller
            public List<Tuple<Vector3, Quaternion>> Reconstruct(ExampleMapping exampleMapping, TargetMapping targetMapping)
            {

                int index = (targetMapping.LastIndex + 1) % exampleMapping.Size();

                List<Tuple<Vector3, Quaternion>> generatedPoints = new List<Tuple<Vector3, Quaternion>>();

                while (true)
                {
                    Vector2 offsets = exampleMapping.GetOffsets(index);

                    if (!targetMapping.Apply(offsets, index))
                    {
                        break;
                    }

                    Vector2 association = targetMapping.Last();
                    Tuple<Vector3, Vector3> endPoints = targetMapping.Inflate(association);
                    generatedPoints.Add(Tuple.Create(endPoints.Item2, Quaternion.identity));

                    index = (index + 1) % exampleMapping.Size();
                }

                return generatedPoints;
            }

            ///<summary>
            ///Generates a pair of variates independently drawn from the standard normal distribution
            ///</summary>   
            ///<returns>
            ///A pair of variates independently drawn from the standard normal distribution
            ///</returns>

            public (double x, double y) GenerateStandardNormalVariates()
            {

                // Variate from the uniform distribution on the interval [0...1)   
                double u = Random.Shared.NextDouble();

                // Angle of random vector given in polar coordinates
                double angle = 2.0 * Math.PI * u;

                // Another variate from the uniform distribution on the interval [0...1)
                //Must not be zero
                do {
                u = Random.Shared.NextDouble();
                } while (u == 0.0);

                // Magnitude of random vector given in polar coordinates
                double magnitude = Math.Sqrt(-2.0 * Math.Log(u));

                // x any y coordinate of random vector are standard normal variates
                double x = Math.Cos(angle) * magnitude;
                double y = Math.Sin(angle) * magnitude;

                return (x , y);
            }
            /// <summary>
            /// Produces a pair of random values by introducing random variation around the provided mean values
            /// </summary>
            /// <returns>
            /// A pair of randomly adjusted values
            /// </returns>

            public (double ending1, double ending2) Randomize((double mean1, double mean2) meanValue)
            {
                // Standard deviations are asumed to be half of the mean values
                (double std1, double std2) standardDeviations;
                standardDeviations.std1 = meanValue.mean1 / 2.0;
                standardDeviations.std2 = meanValue.mean2 / 2.0;

                // Retrieve standard normal variates
                (double variate1, double variate2) variates = GenerateStandardNormalVariates();
                (double ending1, double ending2) endings;

                //Apply mean values and standard deviations
                endings.ending1 = standardDeviations.std1 * variates.variate1 + meanValue.mean1;
                endings.ending2 = standardDeviations.std2 * variates.variate2 + meanValue.mean2;

                return endings;
            
            }

            public double Randomize(double meanValue)
            {
                return Randomize((meanValue, meanValue)).ending1;
            }
    }
}
