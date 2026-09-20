// Copyright 2025 The Open Brush Authors
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
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{

    internal class TestPathFill
    {
        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        /// A closed loop sampled on a circle in the (u, v) plane, displaced along w by
        /// height(theta).
        private static List<Vector3> Loop(int count, float radius, Func<float, float> height,
                                          Vector3 u, Vector3 v, Vector3 w)
        {
            var points = new List<Vector3>(count);
            for (int i = 0; i < count; ++i)
            {
                float t = 2f * Mathf.PI * i / count;
                points.Add(u * (radius * Mathf.Cos(t)) +
                           v * (radius * Mathf.Sin(t)) +
                           w * height(t));
            }
            return points;
        }

        private static List<Vector3> Star(int points, float outerRadius, float innerRadius)
        {
            var result = new List<Vector3>(points * 2);
            for (int i = 0; i < points * 2; ++i)
            {
                float t = Mathf.PI * i / points;
                float r = (i % 2 == 0) ? outerRadius : innerRadius;
                result.Add(new Vector3(r * Mathf.Cos(t), r * Mathf.Sin(t), 0f));
            }
            return result;
        }

        private static float SurfaceArea(PathFill.Result result)
        {
            float total = 0f;
            for (int i = 0; i < result.Triangles.Length; i += 3)
            {
                Vector3 a = result.Vertices[result.Triangles[i]];
                Vector3 b = result.Vertices[result.Triangles[i + 1]];
                Vector3 c = result.Vertices[result.Triangles[i + 2]];
                total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }
            return total;
        }

        private static float WorstPlaneDeviation(PathFill.Result result)
        {
            float worst = 0f;
            for (int i = 0; i < result.Vertices.Length; ++i)
            {
                worst = Mathf.Max(worst, Mathf.Abs(
                    Vector3.Dot(result.Vertices[i] - result.PlaneOrigin, result.PlaneNormal)));
            }
            return worst;
        }

        private static bool PointInPolygon(Vector2 p, IList<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                    p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) /
                          (polygon[j].y - polygon[i].y) + polygon[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // ---------------------------------------------------------------------------
        // A planar path must fill exactly like a 2D fill
        // ---------------------------------------------------------------------------

        [Test]
        public void PlanarPathFillsInItsOwnPlane()
        {
            Vector3 normal = new Vector3(1f, 2f, 3f).normalized;
            Vector3 u, v;
            PathFillGeometry.BasisFromNormal(normal, out u, out v);
            var path = Loop(96, 2f, t => 0f, u, v, normal);

            PathFill.Result result = PathFill.Fill(path);
            Assert.IsNotNull(result);
            Assert.Less(WorstPlaneDeviation(result), 1e-4f,
                        "a planar path must produce a planar fill");
            Assert.Greater(Vector3.Dot(result.PlaneNormal, normal), 0.9999f,
                           "the fitted plane must match the path's plane, oriented by its winding");
            Assert.Less(result.Flatness, 1e-5f);
            Assert.AreEqual(0, result.ProjectedSelfIntersections);
            Assert.AreEqual(Mathf.PI * 4f, SurfaceArea(result), Mathf.PI * 4f * 0.05f,
                            "the filled area should be the area of the disc");
        }

        [Test]
        public void ShadingNormalsFollowTheSurfaceOrientation()
        {
            var path = Loop(64, 1f, t => 0f, Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Result result = PathFill.Fill(path);
            Assert.IsNotNull(result);
            for (int i = 0; i < result.Normals.Length; ++i)
            {
                Assert.Greater(Vector3.Dot(result.Normals[i], result.PlaneNormal), 0.999f);
            }
        }

        [Test]
        public void ConcavePathDoesNotFillItsHull()
        {
            var path = Star(5, 2f, 0.7f);
            PathFill.Result result = PathFill.Fill(path);
            Assert.IsNotNull(result);

            var outline = new List<Vector2>(result.Boundary.Length);
            foreach (Vector3 p in result.Boundary) { outline.Add(new Vector2(p.x, p.y)); }

            for (int i = 0; i < result.Triangles.Length; i += 3)
            {
                Vector3 centroid = (result.Vertices[result.Triangles[i]] +
                                    result.Vertices[result.Triangles[i + 1]] +
                                    result.Vertices[result.Triangles[i + 2]]) / 3f;
                Assert.IsTrue(PointInPolygon(new Vector2(centroid.x, centroid.y), outline),
                              "no triangle may lie outside a concave outline");
            }

            // Area of a 5-pointed star, not of its convex hull.
            float expected = 10f * 0.5f * 2f * 0.7f * Mathf.Sin(Mathf.PI / 5f);
            Assert.AreEqual(expected, SurfaceArea(result), expected * 0.02f);
        }

        // ---------------------------------------------------------------------------
        // A non-planar path must be spanned, not flattened
        // ---------------------------------------------------------------------------

        [Test]
        public void NonPlanarPathIsSpannedNotFlattened()
        {
            const float kAmplitude = 0.4f;
            var path = Loop(96, 1f, t => kAmplitude * Mathf.Sin(2f * t),
                            Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));

            PathFill.Result result = PathFill.Fill(path);
            Assert.IsNotNull(result);
            Assert.Greater(result.Flatness, 0.05f, "this saddle is not planar and should say so");

            float maxLift = 0f;
            float interiorLift = 0f;
            float centreLift = float.MaxValue;
            for (int i = 0; i < result.Vertices.Length; ++i)
            {
                Vector3 p = result.Vertices[i];
                maxLift = Mathf.Max(maxLift, Mathf.Abs(p.z));
                var radial = new Vector2(p.x, p.y);
                if (radial.magnitude > 0.3f && radial.magnitude < 0.6f && p.x > 0.2f && p.y > 0.2f)
                {
                    interiorLift = Mathf.Max(interiorLift, p.z);
                }
                if (radial.magnitude < 0.02f) { centreLift = Mathf.Abs(p.z); }
            }

            Assert.Greater(interiorLift, 0.05f,
                           "interior vertices must follow the boundary out of the plane");
            Assert.LessOrEqual(maxLift, kAmplitude + 1e-3f,
                               "the lift must not overshoot the boundary it interpolates");
            Assert.Less(centreLift, 0.02f,
                        "the centre of a symmetric saddle should sit on the plane");
        }

        [Test]
        public void BoundaryPointsSurviveIntoTheMesh()
        {
            var path = Loop(96, 1f, t => 0.4f * Mathf.Sin(2f * t),
                            Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Result result = PathFill.Fill(path);
            Assert.IsNotNull(result);

            foreach (Vector3 boundaryPoint in result.Boundary)
            {
                float nearest = float.MaxValue;
                for (int i = 0; i < result.Vertices.Length; ++i)
                {
                    nearest = Mathf.Min(nearest, (result.Vertices[i] - boundaryPoint).magnitude);
                }
                Assert.Less(nearest, 1e-4f,
                            "the fill must stay welded to the path it was built from");
            }
        }

        [Test]
        public void SkipLiftProducesAPlanarPatch()
        {
            var path = Loop(96, 1f, t => 0.4f * Mathf.Sin(2f * t),
                            Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Options options = PathFill.Options.Default;
            options.SkipLift = true;
            PathFill.Result result = PathFill.Fill(path, options);
            Assert.IsNotNull(result);
            Assert.Less(WorstPlaneDeviation(result), 1e-4f);
        }

        // ---------------------------------------------------------------------------
        // Contract
        // ---------------------------------------------------------------------------

        [Test]
        public void FillIsDeterministic()
        {
            // Strokes are rebuilt from their control points on load, so the same path must
            // always produce the same mesh.
            var path = Loop(96, 1f, t => 0.4f * Mathf.Sin(2f * t),
                            Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Result first = PathFill.Fill(path);
            PathFill.Result second = PathFill.Fill(path);

            Assert.AreEqual(first.Vertices.Length, second.Vertices.Length);
            Assert.AreEqual(first.Triangles, second.Triangles);
            for (int i = 0; i < first.Vertices.Length; ++i)
            {
                Assert.AreEqual(first.Vertices[i].x, second.Vertices[i].x);
                Assert.AreEqual(first.Vertices[i].y, second.Vertices[i].y);
                Assert.AreEqual(first.Vertices[i].z, second.Vertices[i].z);
            }
        }

        [Test]
        public void ExplicitlyClosedPathIsNotDoubleCounted()
        {
            var open = Loop(48, 1f, t => 0f, Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            var closed = new List<Vector3>(open);
            closed.Add(open[0]);

            PathFill.Result fromOpen = PathFill.Fill(open);
            PathFill.Result fromClosed = PathFill.Fill(closed);
            Assert.IsNotNull(fromOpen);
            Assert.IsNotNull(fromClosed);
            Assert.AreEqual(fromOpen.Boundary.Length, fromClosed.Boundary.Length);
        }

        [Test]
        public void SelfIntersectingOutlineIsReported()
        {
            var figureEight = new List<Vector3>
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(-1f, 1f, 0f),
            };
            PathFill.Options options = PathFill.Options.Default;
            options.SimplifyTolerance = 1e-6f;

            PathFill.Result result = PathFill.Fill(figureEight, options);
            Assert.IsNotNull(result);
            Assert.Greater(result.ProjectedSelfIntersections, 0,
                           "callers need to know when the fill rule, not the path, chose the shape");
        }

        [Test]
        public void FillRuleChangesWhichRegionsAreFilled()
        {
            // A square with a square sub-loop wound the same way: non-zero fills the middle,
            // even-odd leaves it as a hole. Both are driven through the real tessellator.
            var outer = new List<Vector2>
            {
                new Vector2(-2f, -2f), new Vector2(2f, -2f), new Vector2(2f, 2f), new Vector2(-2f, 2f),
            };
            var inner = new List<Vector2>
            {
                new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f),
            };
            var contours = new List<IList<Vector2>> { outer, inner };

            var nonZeroVerts = new List<Vector2>();
            var nonZeroTris = new List<int>();
            Assert.IsTrue(PathFillTessellator.Tessellate(
                contours, PathFillRule.NonZero, nonZeroVerts, nonZeroTris));

            var oddEvenVerts = new List<Vector2>();
            var oddEvenTris = new List<int>();
            Assert.IsTrue(PathFillTessellator.Tessellate(
                contours, PathFillRule.OddEven, oddEvenVerts, oddEvenTris));

            Assert.AreEqual(16f, Area2D(nonZeroVerts, nonZeroTris), 0.01f);
            Assert.AreEqual(12f, Area2D(oddEvenVerts, oddEvenTris), 0.01f);
        }

        private static float Area2D(List<Vector2> vertices, List<int> triangles)
        {
            float total = 0f;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = vertices[triangles[i]];
                Vector2 b = vertices[triangles[i + 1]];
                Vector2 c = vertices[triangles[i + 2]];
                total += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            }
            return total;
        }

        [Test]
        public void PathsThatCannotBeFilledReturnNull()
        {
            Assert.IsNull(PathFill.Fill(new List<Vector3>
            {
                Vector3.zero, Vector3.one, Vector3.one * 2f,
            }), "a collinear path has no fill");

            Assert.IsNull(PathFill.Fill(new List<Vector3> { Vector3.zero, Vector3.up }),
                          "two points have no fill");

            Assert.IsNull(PathFill.Fill(new List<Vector3>
            {
                Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero,
            }), "coincident points have no fill");

            Assert.IsNull(PathFill.Fill(null));
        }

        [Test]
        public void VertexBudgetIsRespected()
        {
            var path = Loop(96, 2f, t => 0f, Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Options options = PathFill.Options.Default;
            options.MaxVertices = 200;
            PathFill.Result result = PathFill.Fill(path, options);
            Assert.IsNotNull(result);
            Assert.LessOrEqual(result.Vertices.Length, options.MaxVertices * 4);
        }

        [Test]
        public void BoundaryIsSimplifiedWithinTheCap()
        {
            var path = Loop(2000, 1f, t => 0f, Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Options options = PathFill.Options.Default;
            options.MaxBoundaryPoints = 64;
            PathFill.Result result = PathFill.Fill(path, options);
            Assert.IsNotNull(result);
            Assert.LessOrEqual(result.Boundary.Length, 64);
            Assert.GreaterOrEqual(result.Boundary.Length, 3);
        }

        // ---------------------------------------------------------------------------
        // Building blocks
        // ---------------------------------------------------------------------------

        [Test]
        public void BasisFromNormalIsRightHanded()
        {
            var normals = new[]
            {
                Vector3.up, Vector3.right, new Vector3(0f, 0f, 1f),
                new Vector3(1f, 2f, 3f).normalized, new Vector3(-0.3f, 0.1f, 0.9f).normalized,
            };
            foreach (Vector3 n in normals)
            {
                Vector3 u, v;
                PathFillGeometry.BasisFromNormal(n, out u, out v);
                Assert.AreEqual(1f, u.magnitude, 1e-4f);
                Assert.AreEqual(1f, v.magnitude, 1e-4f);
                Assert.AreEqual(0f, Vector3.Dot(u, v), 1e-4f);
                Assert.Greater(Vector3.Dot(Vector3.Cross(u, v), n), 0.9999f);
            }
        }

        [Test]
        public void MeanValueCoordinatesInterpolateTheBoundary()
        {
            var square = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f), new Vector2(0f, 10f),
            };
            var values = new List<float> { 0f, 1f, 2f, 1f };

            // Exact at the vertices.
            for (int i = 0; i < square.Count; ++i)
            {
                Assert.AreEqual(values[i],
                                PathFillGeometry.MeanValueInterpolate(square[i], square, values), 1e-5f);
            }
            // Linear along an edge.
            Assert.AreEqual(0.5f,
                PathFillGeometry.MeanValueInterpolate(new Vector2(5f, 0f), square, values), 1e-3f);
            // The centre of this configuration averages to 1.
            Assert.AreEqual(1f,
                PathFillGeometry.MeanValueInterpolate(new Vector2(5f, 5f), square, values), 1e-3f);
        }

        [Test]
        public void NewellNormalHandlesNonPlanarLoops()
        {
            var loop = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0.3f),
                new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, -0.3f),
            };
            Vector3 normal = PathFillGeometry.NewellNormal(loop).normalized;
            Assert.Greater(Vector3.Dot(normal, new Vector3(0f, 0f, 1f)), 0.9f);
        }
    }
} // namespace TiltBrush
