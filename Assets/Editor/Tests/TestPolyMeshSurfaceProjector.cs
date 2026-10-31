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

using System.Collections.Generic;
using NUnit.Framework;
using Polyhydra.Core;
using UnityEngine;

namespace TiltBrush
{
    internal class TestPolyMeshSurfaceProjector
    {
        [Test]
        public void ClosestPointStaysInsidePolygonFace()
        {
            var poly = new PolyMesh(
                new[]
                {
                    new Vector3(-1, -1, 0),
                    new Vector3(1, -1, 0),
                    new Vector3(1, 1, 0),
                    new Vector3(-1, 1, 0)
                },
                new[] { new[] { 0, 1, 2, 3 } });
            var projector = new PolyMeshSurfaceProjector(poly);

            MathTestUtils.AssertAlmostEqual(
                new Vector3(0.25f, 0.5f, 0),
                projector.ClosestPointOnFace(0, new Vector3(0.25f, 0.5f, 2)));
            MathTestUtils.AssertAlmostEqual(
                new Vector3(1, 0.5f, 0),
                projector.ClosestPointOnFace(0, new Vector3(2, 0.5f, 1)));
        }

        [Test]
        public void ClosestFaceUsesPolygonSurfaceRatherThanCentroid()
        {
            var poly = new PolyMesh(
                new[]
                {
                    new Vector3(-5, -1, 0),
                    new Vector3(0, -1, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(-5, 1, 0),
                    new Vector3(0, -1, 0),
                    new Vector3(1, -1, 0),
                    new Vector3(1, 1, 0),
                    new Vector3(0, 1, 0)
                },
                new[]
                {
                    new[] { 0, 1, 2, 3 },
                    new[] { 4, 5, 6, 7 }
                });
            var projector = new PolyMeshSurfaceProjector(poly);

            int faceIndex = projector.FindClosestFace(
                new Vector3(-0.1f, 0, 0.25f), out Vector3 closestPoint);

            Assert.That(faceIndex, Is.EqualTo(0));
            MathTestUtils.AssertAlmostEqual(new Vector3(-0.1f, 0, 0), closestPoint);
        }

        [Test]
        public void TransitionUsesHalfedgeDihedralAngle()
        {
            var poly = new PolyMesh(
                new[]
                {
                    new Vector3(-1, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(0, 0, 1)
                },
                new[]
                {
                    new[] { 0, 1, 2 },
                    new[] { 1, 0, 3 }
                });
            var projector = new PolyMeshSurfaceProjector(poly);
            float dihedralAngle = poly.Faces[0].Halfedge.Pair != null
                ? poly.Faces[0].Halfedge.DihedralAngle
                : FindPairedEdgeAngle(poly.Faces[0]);

            Assert.That(dihedralAngle, Is.GreaterThan(1f));
            Assert.That(projector.CanTransition(0, 1, dihedralAngle - 0.5f), Is.False);
            Assert.That(projector.CanTransition(0, 1, dihedralAngle + 0.5f), Is.True);
        }

        [Test]
        public void TransitionCanCrossSkippedEligibleFace()
        {
            var poly = new PolyMesh(
                new[]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(2, 0, 0),
                    new Vector3(3, 0, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(1, 1, 0),
                    new Vector3(2, 1, 0),
                    new Vector3(3, 1, 0)
                },
                new[]
                {
                    new[] { 0, 1, 5, 4 },
                    new[] { 1, 2, 6, 5 },
                    new[] { 2, 3, 7, 6 }
                });
            var projector = new PolyMeshSurfaceProjector(poly);

            Assert.That(projector.CanTransition(0, 2, 0.5f), Is.True);
        }

        private static float FindPairedEdgeAngle(Face face)
        {
            foreach (Halfedge edge in face.GetHalfedges())
            {
                if (edge.Pair != null)
                {
                    return edge.DihedralAngle;
                }
            }
            return float.PositiveInfinity;
        }
    }
} // namespace TiltBrush
