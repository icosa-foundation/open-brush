// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under
// the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
// ANY KIND, either express or implied. See the License for permissions and limitations.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestSymmetryMirrorMove : MathTestUtils
    {
        [TestCase(0, 4f, 2f)]
        [TestCase(1, -6f, 5f)]
        [TestCase(2, 6f, -4f)]
        public void SceneSpaceEditKeepsPeersMirroredOnRotatedScaledLayer(int edit, float expectedX, float expectedY)
        {
            var canvas = TrTransform.TRS(new Vector3(3, 4, 0), Quaternion.Euler(0, 0, 90), 2f);
            var delta = edit == 0 ? TrTransform.T(Vector3.right)
                : edit == 1 ? TrTransform.R(Quaternion.Euler(0, 0, 90)) : TrTransform.S(2f);
            var local = TransformItemsCommand.SceneDeltaInCanvas(delta, canvas);
            var reflection = new Plane(Vector3.right, Vector3.zero).ToTrTransform();
            var peerMove = reflection * local * reflection.inverse;
            var after = peerMove.MultiplyPoint(Vector3.left);

            // The scene-space mirror plane is y=4. Mirror the edited driver across it.
            AssertAlmostEqual(new Vector3(expectedX, expectedY, 0), canvas.MultiplyPoint(after));
            AssertAlmostEqual(new Vector3(3, 2, 0), canvas.MultiplyPoint(peerMove.inverse.MultiplyPoint(after)));
        }

        [Test]
        public void SceneSpaceWidgetEditSurvivesConversionThroughWidgetAndMirrorCanvases()
        {
            var mirrorCanvas = TrTransform.TRS(new Vector3(3, 4, 0), Quaternion.Euler(0, 0, 90), 2f);
            var widgetCanvas = TrTransform.TRS(new Vector3(-2, 1, 0), Quaternion.Euler(0, 0, 60), 0.5f);
            var local = TransformItemsCommand.SceneDeltaInCanvas(TrTransform.T(Vector3.right), widgetCanvas);
            var basis = mirrorCanvas.inverse * widgetCanvas;
            var mirrorDelta = basis * local * basis.inverse;
            var reflection = new Plane(Vector3.right, Vector3.zero).ToTrTransform();
            var peerMove = reflection * mirrorDelta * reflection.inverse;

            AssertAlmostEqual(new Vector3(4, 2, 0), mirrorCanvas.MultiplyPoint(peerMove.MultiplyPoint(Vector3.left)));
        }

        [Test]
        public void MovingReflectionHasSameWorldResultOnTransformedLayer()
        {
            var before = new Plane(Vector3.right, Vector3.zero).ToTrTransform();
            var after = new Plane(Vector3.right, new Vector3(2, 0, 0)).ToTrTransform();
            var canvas = TrTransform.TRS(new Vector3(8, -3, 1),
                Quaternion.Euler(20, 70, 10), 2.5f);
            var point = new Vector3(3, 1, 0);
            var delta = SymmetryMirrorMove.PointerDelta(before, after, canvas);

            // Moving the reflecting plane two units translates its reflection four units.
            var result = canvas.MultiplyPoint(delta.MultiplyPoint(canvas.inverse.MultiplyPoint(point)));
            AssertAlmostEqual(new Vector3(7, 1, 0), result);
            AssertAlmostEqual(point, canvas.MultiplyPoint(
                delta.inverse.MultiplyPoint(canvas.inverse.MultiplyPoint(result))));
        }

        [Test]
        public void PlacementCopyPreservesRecordedPoseAndDoesNotAliasTransformLists()
        {
            var original = new SymmetrySettingsSnapshot
            {
                WidgetTransform = TrTransform.T(new Vector3(9, 2, 1)),
                PointerTransforms = new List<TrTransform> { TrTransform.identity }
            };
            var transforms = new List<TrTransform> { TrTransform.T(Vector3.right) };
            var copy = original.WithPointerTransforms(transforms);
            transforms[0] = TrTransform.identity;

            Assert.AreEqual(original.WidgetTransform, copy.WidgetTransform);
            Assert.AreEqual(TrTransform.identity, original.PointerTransforms[0]);
            Assert.AreEqual(TrTransform.T(Vector3.right), copy.PointerTransforms[0]);
        }

        [Test]
        public void EqualPointerCountsDoNotMakeDifferentModesCompatible()
        {
            var plane = new SymmetrySettingsSnapshot
            {
                Mode = PointerManager.SymmetryMode.SinglePlane,
                PointerTransforms = new List<TrTransform> { TrTransform.identity, TrTransform.identity }
            };
            var other = plane.WithPointerTransforms(plane.PointerTransforms);
            Assert.IsTrue(plane.HasCompatibleTopology(other));
            other.Mode = PointerManager.SymmetryMode.MultiMirror;
            Assert.IsFalse(plane.HasCompatibleTopology(other));
        }
    }
}
