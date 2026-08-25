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
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    internal class TestStrokeSculptInfluence
    {
        [Test]
        public void ReshapePrefabRegistersEverySubToolIdentifierOnce()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/ReshapeTool.prefab");
            Assert.IsNotNull(prefab);

            BaseSculptSubTool[] subTools =
                prefab.GetComponentsInChildren<BaseSculptSubTool>(true);
            var identifiers = System.Array.ConvertAll(
                subTools, subTool => subTool.SubToolIdentifier);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    SculptSubToolManager.SubTool.Push,
                    SculptSubToolManager.SubTool.Pinch,
                    SculptSubToolManager.SubTool.Flatten,
                    SculptSubToolManager.SubTool.Grab,
                    SculptSubToolManager.SubTool.Smooth,
                },
                identifiers);
        }

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

        [Test]
        public void FeatherAlongStrokeUsesArcLength()
        {
            var controlPoints = new[]
            {
                ControlPointAt(-2f),
                ControlPointAt(-0.5f),
                ControlPointAt(0f),
                ControlPointAt(0.5f),
                ControlPointAt(2f),
            };
            var weights = new[] { 0f, 0f, 1f, 0f, 0f };

            StrokeSculptInfluence.FeatherAlongStroke(controlPoints, weights, 2f);

            Assert.That(weights, Is.EqualTo(new[] { 0f, 0.75f, 1f, 0.75f, 0f })
                .Within(0.00001f));
        }

        [Test]
        public void FeatherAlongStrokeCombinesInfluenceFromBothDirections()
        {
            var controlPoints = new[]
            {
                ControlPointAt(0f),
                ControlPointAt(1f),
                ControlPointAt(2f),
                ControlPointAt(3f),
            };
            var weights = new[] { 1f, 0f, 0f, 0.75f };

            StrokeSculptInfluence.FeatherAlongStroke(controlPoints, weights, 4f);

            Assert.That(weights, Is.EqualTo(new[] { 1f, 0.75f, 0.5f, 0.75f })
                .Within(0.00001f));
        }

        [Test]
        public void GrabTranslationAlwaysUsesCapturedPoints()
        {
            var startPoints = new[] { ControlPointAt(1f), ControlPointAt(3f) };
            startPoints[0].m_Orient = Quaternion.Euler(10f, 20f, 30f);
            var weights = new[] { 1f, 0.25f };
            var result = new PointerManager.ControlPoint[2];

            StrokeSculptInfluence.ApplyGrabTranslation(
                startPoints, weights, new Vector3(4f, 0f, 0f), result);
            Assert.AreEqual(5f, result[0].m_Pos.x);
            Assert.AreEqual(4f, result[1].m_Pos.x);

            StrokeSculptInfluence.ApplyGrabTranslation(
                startPoints, weights, new Vector3(2f, 0f, 0f), result);
            Assert.AreEqual(3f, result[0].m_Pos.x);
            Assert.AreEqual(3.5f, result[1].m_Pos.x);
            Assert.AreEqual(startPoints[0].m_Orient, result[0].m_Orient);
        }

        [Test]
        public void SmoothPreservesEndpointsAndStraightensInteriorPoint()
        {
            var startPoints = new[]
            {
                ControlPointAt(0f, 0f),
                ControlPointAt(1f, 1f),
                ControlPointAt(2f, 0f),
            };
            var result = new PointerManager.ControlPoint[3];

            bool modified = StrokeSculptInfluence.ApplySmooth(
                startPoints, new[] { 1f, 1f, 1f }, 0.5f, result);

            Assert.IsTrue(modified);
            Assert.AreEqual(startPoints[0].m_Pos, result[0].m_Pos);
            Assert.AreEqual(startPoints[2].m_Pos, result[2].m_Pos);
            Assert.AreEqual(new Vector3(1f, 0.5f, 0f), result[1].m_Pos);
        }

        [Test]
        public void SmoothAcceptsLargerReusableWeightBuffer()
        {
            var startPoints = new[]
            {
                ControlPointAt(0f, 0f),
                ControlPointAt(1f, 1f),
                ControlPointAt(2f, 0f),
            };
            var result = new PointerManager.ControlPoint[3];

            bool modified = StrokeSculptInfluence.ApplySmooth(
                startPoints, new[] { 1f, 1f, 1f, 0f, 0f }, 0.5f, result);

            Assert.IsTrue(modified);
            Assert.AreEqual(new Vector3(1f, 0.5f, 0f), result[1].m_Pos);
        }

        [Test]
        public void SmoothUsesSegmentLengthsForUnevenSpacing()
        {
            var startPoints = new[]
            {
                ControlPointAt(0f, 0f),
                ControlPointAt(1f, 1f),
                ControlPointAt(4f, 0f),
            };
            var result = new PointerManager.ControlPoint[3];

            StrokeSculptInfluence.ApplySmooth(
                startPoints, new[] { 1f, 1f, 1f }, 1f, result);

            float previousLength = Vector3.Distance(
                startPoints[0].m_Pos, startPoints[1].m_Pos);
            float nextLength = Vector3.Distance(
                startPoints[1].m_Pos, startPoints[2].m_Pos);
            Vector3 expected = Vector3.Lerp(
                startPoints[0].m_Pos, startPoints[2].m_Pos,
                previousLength / (previousLength + nextLength));
            Assert.AreEqual(expected, result[1].m_Pos);
        }

        [Test]
        public void ProportionalAmountMatchesRepeatedReferenceUpdates()
        {
            float scaledAmount = StrokeSculptInfluence.ScaleProportionalAmount(
                0.1f, 9f, towardTarget: true);

            Assert.AreEqual(1f - Mathf.Pow(0.9f, 9f), scaledAmount, 0.00001f);
            Assert.Less(scaledAmount, 0.9f);
        }

        [Test]
        public void SmoothUsesRepeatedReferenceUpdateScaling()
        {
            var startPoints = new[]
            {
                ControlPointAt(0f, 0f),
                ControlPointAt(1f, 1f),
                ControlPointAt(2f, 0f),
            };
            var result = new PointerManager.ControlPoint[3];

            StrokeSculptInfluence.ApplySmooth(
                startPoints, new[] { 1f, 1f, 1f }, 0.1f, 9f, result);

            Assert.AreEqual(Mathf.Pow(0.9f, 9f), result[1].m_Pos.y, 0.00001f);
        }

        [Test]
        public void ProportionalSpreadMatchesRepeatedReferenceUpdates()
        {
            float displacement = StrokeSculptInfluence.ScaleProportionalDisplacement(
                2f, 0.2f, 9f, towardTarget: false);

            Assert.AreEqual(2f * (Mathf.Pow(1.1f, 9f) - 1f), displacement, 0.00001f);
        }

        [TestCase(2f, -2f)]
        [TestCase(-3f, 3f)]
        [TestCase(0f, 0f)]
        public void PlaneOffsetMovesPointPerpendicularlyOntoPlane(float height, float expectedY)
        {
            Vector3 offset = StrokeSculptInfluence.CalculatePlaneOffset(
                new Vector3(4f, height, 7f), Vector3.zero, Vector3.up * 3f);

            Assert.AreEqual(new Vector3(0f, expectedY, 0f), offset);
        }

        [Test]
        public void PlaneWeightDoesNotFadeWithHeightAbovePlane()
        {
            float onPlane = StrokeSculptInfluence.CalculatePlaneWeight(
                new Vector3(1f, 0f, 0f), Vector3.zero, Vector3.up, 2f);
            float abovePlane = StrokeSculptInfluence.CalculatePlaneWeight(
                new Vector3(1f, 100f, 0f), Vector3.zero, Vector3.up, 2f);

            Assert.AreEqual(onPlane, abovePlane);
            Assert.AreEqual(0.5f, onPlane);
        }

        [Test]
        public void FlattenInfluenceStopsAtSphericalBoundary()
        {
            var gameObject = new GameObject("FlattenSubTool test");
            try
            {
                gameObject.AddComponent<BoxCollider>();
                var subTool = gameObject.AddComponent<FlattenSubTool>();

                float inside = subTool.CalculateInfluence(
                    new Vector3(0f, 0.5f, 0f), Vector3.zero, 1f, TrTransform.identity);
                float outside = subTool.CalculateInfluence(
                    new Vector3(0f, 2f, 0f), Vector3.zero, 1f, TrTransform.identity);

                Assert.That(inside, Is.GreaterThan(0f));
                Assert.AreEqual(0f, outside);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void FlattenPlaneProjectedInfluenceExtendsPerpendicularToPlane()
        {
            var gameObject = new GameObject("FlattenSubTool test");
            try
            {
                gameObject.AddComponent<BoxCollider>();
                var subTool = gameObject.AddComponent<FlattenSubTool>();
                var serializedSubTool = new SerializedObject(subTool);
                serializedSubTool.FindProperty("m_InfluenceMode").enumValueIndex =
                    (int)FlattenSubTool.InfluenceMode.PlaneProjected;
                serializedSubTool.ApplyModifiedPropertiesWithoutUndo();

                float influence = subTool.CalculateInfluence(
                    new Vector3(0f, 2f, 0f), Vector3.zero, 1f, TrTransform.identity);

                Assert.AreEqual(1f, influence);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void LineOffsetMovesPointPerpendicularlyOntoSharedLine()
        {
            Vector3 offset = StrokeSculptInfluence.CalculateLineOffset(
                new Vector3(4f, 6f, 3f), new Vector3(1f, 2f, 3f), Vector3.up * 2f);

            Assert.AreEqual(new Vector3(-3f, 0f, 0f), offset);
        }

        [Test]
        public void LineWeightDoesNotFadeAlongLineLength()
        {
            float nearOrigin = StrokeSculptInfluence.CalculateLineWeight(
                new Vector3(1f, 0f, 0f), Vector3.zero, Vector3.forward, 2f);
            float farAlongLine = StrokeSculptInfluence.CalculateLineWeight(
                new Vector3(1f, 0f, 100f), Vector3.zero, Vector3.forward, 2f);

            Assert.AreEqual(nearOrigin, farAlongLine);
            Assert.AreEqual(0.5f, nearOrigin);
        }

        [Test]
        public void TwistAngleExtractsControllerRoll()
        {
            Quaternion start = Quaternion.Euler(20f, 30f, 10f);
            Vector3 axis = start * Vector3.forward;
            Quaternion current = Quaternion.AngleAxis(45f, axis) * start;

            float angle = StrokeSculptInfluence.CalculateTwistAngle(start, current, axis);

            Assert.AreEqual(45f, angle, 0.0001f);
        }

        [TestCase(179f, -179f, 181f)]
        [TestCase(-179f, 179f, -181f)]
        [TestCase(539f, -179f, 541f)]
        public void UnwrapAnglePreservesContinuousRotation(
            float previousAngle, float wrappedAngle, float expected)
        {
            Assert.AreEqual(
                expected, StrokeSculptInfluence.UnwrapAngle(previousAngle, wrappedAngle));
        }

        [Test]
        public void GrabTwistPreservesRadiusAndRotatesOrientation()
        {
            var startPoints = new[] { ControlPointAt(2f, 0f) };
            startPoints[0].m_Orient = Quaternion.identity;
            var result = new PointerManager.ControlPoint[1];

            StrokeSculptInfluence.ApplyCapturedTransform(
                startPoints, new[] { 1f }, Vector3.zero, Vector3.zero, Vector3.forward, 90f,
                result);

            Assert.AreEqual(2f, result[0].m_Pos.magnitude, 0.0001f);
            Assert.Less(Vector3.Distance(new Vector3(0f, 2f, 0f), result[0].m_Pos), 0.0001f);
            Assert.AreEqual(
                Quaternion.AngleAxis(90f, Vector3.forward), result[0].m_Orient);
        }

        [Test]
        public void OrientationTransportFollowsChangedEndpointTangents()
        {
            var startPoints = new[]
            {
                ControlPointAt(0f, 0f),
                ControlPointAt(1f, 0f),
                ControlPointAt(2f, 0f),
            };
            Quaternion originalOrientation = Quaternion.Euler(20f, 30f, 40f);
            for (int i = 0; i < startPoints.Length; ++i)
            {
                startPoints[i].m_Orient = originalOrientation;
            }
            var result = (PointerManager.ControlPoint[])startPoints.Clone();
            result[1].m_Pos = new Vector3(1f, 1f, 0f);

            StrokeSculptInfluence.TransportOrientations(startPoints, result);

            Quaternion expectedStart = Quaternion.FromToRotation(
                Vector3.right, new Vector3(1f, 1f, 0f)) * originalOrientation;
            Quaternion expectedEnd = Quaternion.FromToRotation(
                Vector3.right, new Vector3(1f, -1f, 0f)) * originalOrientation;
            Assert.AreEqual(expectedStart, result[0].m_Orient);
            Assert.AreEqual(originalOrientation, result[1].m_Orient);
            Assert.AreEqual(expectedEnd, result[2].m_Orient);
        }

        [Test]
        public void OrientationTransportIgnoresDegenerateStroke()
        {
            var startPoints = new[] { ControlPointAt(1f), ControlPointAt(1f) };
            startPoints[0].m_Orient = Quaternion.Euler(10f, 20f, 30f);
            startPoints[1].m_Orient = Quaternion.Euler(40f, 50f, 60f);
            var result = (PointerManager.ControlPoint[])startPoints.Clone();

            StrokeSculptInfluence.TransportOrientations(startPoints, result);

            Assert.AreEqual(startPoints[0].m_Orient, result[0].m_Orient);
            Assert.AreEqual(startPoints[1].m_Orient, result[1].m_Orient);
        }

        private static PointerManager.ControlPoint ControlPointAt(float x)
        {
            return new PointerManager.ControlPoint { m_Pos = new Vector3(x, 0f, 0f) };
        }

        private static PointerManager.ControlPoint ControlPointAt(float x, float y)
        {
            return new PointerManager.ControlPoint { m_Pos = new Vector3(x, y, 0f) };
        }
    }
} // namespace TiltBrush
