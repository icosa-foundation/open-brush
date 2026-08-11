// Copyright 2026 The Open Brush Authors
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

using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestStrokeSculptInfluence
    {
        [Test]
        public void RadialWeightIsFullAtCentre()
        {
            Assert.AreEqual(1f, StrokeSculptInfluence.CalculateRadialWeight(0f, 2f));
        }

        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        [TestCase(100f, 1f)]
        public void RadialWeightIsZeroAtAndBeyondBoundary(float distance, float radius)
        {
            Assert.AreEqual(0f, StrokeSculptInfluence.CalculateRadialWeight(distance, radius));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void RadialWeightIsZeroForInvalidRadius(float radius)
        {
            Assert.AreEqual(0f, StrokeSculptInfluence.CalculateRadialWeight(0f, radius));
        }

        [Test]
        public void RadialWeightFallsSmoothlyWithDistance()
        {
            const float radius = 4f;
            float inner = StrokeSculptInfluence.CalculateRadialWeight(1f, radius);
            float middle = StrokeSculptInfluence.CalculateRadialWeight(2f, radius);
            float outer = StrokeSculptInfluence.CalculateRadialWeight(3f, radius);

            Assert.That(inner, Is.GreaterThan(middle));
            Assert.That(middle, Is.GreaterThan(outer));
            Assert.That(middle, Is.EqualTo(0.5f).Within(0.00001f));
        }

        [TestCase(0.25f, true, 0.25f)]
        [TestCase(2f, true, 1f)]
        [TestCase(0f, true, 1f)]
        [TestCase(0.75f, false, 0f)]
        public void PressureUsesAnalogValueWithDigitalFallback(
            float triggerRatio, bool isActive, float expected)
        {
            Assert.AreEqual(
                expected, StrokeSculptInfluence.CalculatePressure(triggerRatio, isActive));
        }

        [Test]
        public void PushStrengthScalesWithRadiusAndNotDirection()
        {
            var gameObject = new GameObject("PushSubTool test");
            try
            {
                var subTool = gameObject.AddComponent<PushSubTool>();
                float pushStrength = subTool.CalculateStrength(
                    Vector3.zero, 0.5f, 2f, TrTransform.identity, true);
                float pullStrength = subTool.CalculateStrength(
                    Vector3.zero, 0.5f, 2f, TrTransform.identity, false);

                Assert.AreEqual(0.2f, pushStrength);
                Assert.AreEqual(pushStrength, pullStrength);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(0.2f, 0.5f, true, 0.2f)]
        [TestCase(0.2f, 0.5f, false, 0.2f)]
        [TestCase(0.8f, 0.5f, false, 0.5f)]
        public void PullDisplacementCannotCrossToolCentre(
            float displacement, float distance, bool pushing, float expected)
        {
            var gameObject = new GameObject("PushSubTool test");
            try
            {
                var subTool = gameObject.AddComponent<PushSubTool>();
                Assert.AreEqual(
                    expected, subTool.ConstrainDisplacement(displacement, distance, pushing));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
} // namespace TiltBrush
