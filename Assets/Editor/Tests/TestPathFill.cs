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
        // Per-point attributes, which FillBrush uses to carry stroke colour into the fill
        // ---------------------------------------------------------------------------

        [Test]
        public void BoundaryIndicesAddressTheOriginalPath()
        {
            var path = Loop(96, 1f, t => 0f, Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Result result = PathFill.Fill(path);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Boundary.Length, result.BoundaryIndices.Length);
            for (int i = 0; i < result.BoundaryIndices.Length; ++i)
            {
                Assert.Less((path[result.BoundaryIndices[i]] - result.Boundary[i]).magnitude, 1e-6f,
                            "simplification must only drop points, never move them");
            }
        }

        [Test]
        public void PathColorsAreInterpolatedAcrossTheFill()
        {
            var path = Loop(64, 1f, t => 0f, Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            var pathColors = new List<Color32>(path.Count);
            foreach (Vector3 p in path)
            {
                pathColors.Add(p.x >= 0f ? new Color32(255, 0, 0, 255) : new Color32(0, 0, 255, 255));
            }

            PathFill.Options options = PathFill.Options.Default;
            options.PathColors = pathColors;
            PathFill.Result result = PathFill.Fill(path, options);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Colors);
            Assert.AreEqual(result.Vertices.Length, result.Colors.Length);

            bool exactAtBoundary = false;
            bool blendedInside = false;
            for (int i = 0; i < result.Vertices.Length; ++i)
            {
                Vector3 v = result.Vertices[i];
                Color32 c = result.Colors[i];
                if (v.x > 0.9f && Mathf.Abs(v.y) < 0.2f)
                {
                    exactAtBoundary |= c.r == 255 && c.b == 0;
                }
                if (new Vector2(v.x, v.y).magnitude < 0.05f)
                {
                    blendedInside |= c.r > 40 && c.b > 40;
                }
            }
            Assert.IsTrue(exactAtBoundary, "colour must be exact where the fill meets the stroke");
            Assert.IsTrue(blendedInside, "colour must blend across the interior");
        }

        [Test]
        public void ColorsAreOmittedOrIgnoredWhenNotUsable()
        {
            var path = Loop(64, 1f, t => 0f, Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));

            Assert.IsNull(PathFill.Fill(path).Colors,
                          "no colours in, no colours out");

            PathFill.Options mismatched = PathFill.Options.Default;
            mismatched.PathColors = new List<Color32> { new Color32(1, 2, 3, 4) };
            PathFill.Result result = PathFill.Fill(path, mismatched);
            Assert.IsNotNull(result, "a mismatched colour list must not fail the fill");
            Assert.IsNull(result.Colors);
        }

        // ---------------------------------------------------------------------------
        // Self-overlapping outlines, which is what the tool script spirals produce
        // ---------------------------------------------------------------------------

        /// ToolScript.Spiral.lua
        private static List<Vector3> ConicalSpiral(float turns, int stepsPerTurn)
        {
            var points = new List<Vector3>();
            float totalSteps = turns * stepsPerTurn;
            for (float i = 0f; i <= 1f; i += 1f / totalSteps)
            {
                float angle = Mathf.PI * 2f * turns * i;
                points.Add(new Vector3(Mathf.Cos(angle) * i, Mathf.Sin(angle) * i, -(i * 2f) + 1f));
            }
            return points;
        }

        /// ToolScript.SpiralSphere.lua
        private static List<Vector3> SphericalSpiral(float turns, int steps)
        {
            var points = new List<Vector3>();
            for (int i = 0; i <= steps; ++i)
            {
                float z = 2.0f * i / steps - 1f;
                float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
                float angle = (Mathf.PI * 2f * turns * i) / steps;
                points.Add(new Vector3(radius * Mathf.Sin(angle), radius * Mathf.Cos(angle), z));
            }
            return points;
        }

        /// A centroid fan triangulates any outline, however self-overlapping, so the lift
        /// can be exercised on these paths without depending on the real tessellator.
        private static bool FanTessellate(IList<Vector2> outline, PathFillRule rule,
                                          List<Vector2> verts, List<int> tris)
        {
            verts.Clear();
            tris.Clear();
            Vector2 centre = Vector2.zero;
            for (int i = 0; i < outline.Count; ++i)
            {
                verts.Add(outline[i]);
                centre += outline[i];
            }
            int hub = verts.Count;
            verts.Add(centre / outline.Count);
            for (int i = 0; i < outline.Count; ++i)
            {
                tris.Add(i);
                tris.Add((i + 1) % outline.Count);
                tris.Add(hub);
            }
            return true;
        }

        [Test]
        public void SpiralPathsProduceNoSpikes()
        {
            // Mean value coordinates are only well behaved on a simple polygon. A spiral
            // projects to an outline wound many times over, where the weights take large
            // values of both signs; before this was handled, the lift overshot the
            // boundary's own range by a factor of 80 to 150, which draws as spikes.
            var paths = new List<List<Vector3>>
            {
                ConicalSpiral(6f, 12),
                ConicalSpiral(20f, 32),
                SphericalSpiral(10f, 200),
                SphericalSpiral(40f, 500),
            };

            foreach (List<Vector3> path in paths)
            {
                PathFill.Options options = PathFill.Options.Default;
                options.Tessellator = FanTessellate;
                PathFill.Result result = PathFill.Fill(path, options);
                Assert.IsNotNull(result);

                float lo = float.MaxValue, hi = -float.MaxValue;
                foreach (Vector3 b in result.Boundary)
                {
                    float h = Vector3.Dot(b - result.PlaneOrigin, result.PlaneNormal);
                    lo = Mathf.Min(lo, h);
                    hi = Mathf.Max(hi, h);
                }
                foreach (Vector3 v in result.Vertices)
                {
                    float h = Vector3.Dot(v - result.PlaneOrigin, result.PlaneNormal);
                    Assert.LessOrEqual(h, hi + 1e-4f,
                                       "the surface may not reach further off the plane than the path does");
                    Assert.GreaterOrEqual(h, lo - 1e-4f);
                }

                // And nothing may fly away from the stroke in any direction.
                Vector3 min = path[0], max = path[0];
                foreach (Vector3 p in path)
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                Vector3 centre = (min + max) * 0.5f;
                float diagonal = (max - min).magnitude;
                foreach (Vector3 v in result.Vertices)
                {
                    Assert.LessOrEqual((v - centre).magnitude, diagonal);
                }
            }
        }

        [Test]
        public void MeanValueWeightsStayAConvexCombinationOrFallBack()
        {
            // The sum of the weight magnitudes is exactly the factor by which the
            // interpolation can overshoot: one while the weights form a convex combination,
            // and hundreds on a spiral. Whatever the outline, it must stay bounded.
            var outlines = new List<List<Vector2>>
            {
                new List<Vector2> { new Vector2(-50f, -50f), new Vector2(50f, -50f),
                                    new Vector2(50f, 50f), new Vector2(-50f, 50f) },
                new List<Vector2> { new Vector2(-50f, -50f), new Vector2(50f, -50f),
                                    new Vector2(50f, 50f), new Vector2(0f, -10f),
                                    new Vector2(-50f, 50f) },
                new List<Vector2> { new Vector2(-50f, -50f), new Vector2(50f, 50f),
                                    new Vector2(50f, -50f), new Vector2(-50f, 50f) },
            };

            // A spiral outline, wound six times.
            var spiral = new List<Vector2>();
            for (int i = 0; i < 120; ++i)
            {
                float t = i / 119f;
                float angle = Mathf.PI * 2f * 6f * t;
                spiral.Add(new Vector2(Mathf.Cos(angle) * t * 50f, Mathf.Sin(angle) * t * 50f));
            }
            outlines.Add(spiral);

            foreach (List<Vector2> outline in outlines)
            {
                var weights = new float[outline.Count];
                for (int gx = 1; gx < 12; ++gx)
                {
                    for (int gy = 1; gy < 12; ++gy)
                    {
                        var p = new Vector2(-50f + 100f * gx / 12f, -50f + 100f * gy / 12f);
                        PathFillGeometry.MeanValueWeights(p, outline, weights);

                        float sum = 0f, magnitude = 0f;
                        foreach (float w in weights)
                        {
                            sum += w;
                            magnitude += Mathf.Abs(w);
                        }
                        Assert.AreEqual(1f, sum, 1e-3f, "normalized weights must sum to one");
                        Assert.LessOrEqual(magnitude, 4.5f,
                                           "beyond this the weights extrapolate instead of interpolating");
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Faceted output, which the Flat Fill variant uses
        // ---------------------------------------------------------------------------

        [Test]
        public void FacetedFillGivesEachTriangleItsOwnNormal()
        {
            var path = Loop(48, 1f, t => 0.25f * Mathf.Sin(2f * t),
                            Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Options options = PathFill.Options.Default;
            options.Faceted = true;

            PathFill.Result result = PathFill.Fill(path, options);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Triangles.Length, result.Vertices.Length,
                            "faceting gives every triangle its own corners");
            Assert.AreEqual(result.Vertices.Length, result.Uvs.Length);

            for (int i = 0; i + 2 < result.Triangles.Length; i += 3)
            {
                Assert.AreEqual(i, result.Triangles[i]);
                Assert.AreEqual(i + 1, result.Triangles[i + 1]);
                Assert.AreEqual(i + 2, result.Triangles[i + 2]);

                Vector3 n0 = result.Normals[i];
                Assert.Greater(Vector3.Dot(n0, result.Normals[i + 1]), 0.9999f);
                Assert.Greater(Vector3.Dot(n0, result.Normals[i + 2]), 0.9999f);

                Vector3 a = result.Vertices[i];
                Vector3 b = result.Vertices[i + 1];
                Vector3 c = result.Vertices[i + 2];
                Vector3 face = Vector3.Cross(b - a, c - a);
                if (face.sqrMagnitude > 1e-18f)
                {
                    Assert.Greater(Vector3.Dot(face.normalized, n0), 0.999f,
                                   "the shared normal is the outward face normal");
                }
            }
        }

        [Test]
        public void FacetingSplitsTheSurfaceWithoutMovingIt()
        {
            var path = Loop(48, 1f, t => 0.25f * Mathf.Sin(2f * t),
                            Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Options faceted = PathFill.Options.Default;
            faceted.Faceted = true;

            PathFill.Result flat = PathFill.Fill(path, faceted);
            PathFill.Result smooth = PathFill.Fill(path, PathFill.Options.Default);
            Assert.IsNotNull(flat);
            Assert.IsNotNull(smooth);

            foreach (Vector3 v in flat.Vertices)
            {
                float nearest = float.MaxValue;
                foreach (Vector3 s in smooth.Vertices)
                {
                    nearest = Mathf.Min(nearest, (v - s).magnitude);
                }
                Assert.Less(nearest, 1e-5f, "faceting must split the surface, not move it");
            }
        }

        [Test]
        public void FacetedFillRespectsTheVertexBudget()
        {
            var path = Loop(48, 1f, t => 0.25f * Mathf.Sin(2f * t),
                            Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            foreach (int cap in new[] { 600, 3000 })
            {
                PathFill.Options options = PathFill.Options.Default;
                options.Faceted = true;
                options.MaxVertices = cap;
                PathFill.Result result = PathFill.Fill(path, options);
                if (result == null) { continue; }
                Assert.LessOrEqual(result.Vertices.Length, cap,
                                   "the budget must account for the split, not just the patch");
            }
        }

        [Test]
        public void FacetedFillCarriesColorsToEveryCorner()
        {
            var path = Loop(48, 1f, t => 0f, Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            var pathColors = new List<Color32>(path.Count);
            foreach (Vector3 p in path)
            {
                pathColors.Add(p.x >= 0f ? new Color32(255, 0, 0, 255) : new Color32(0, 0, 255, 255));
            }

            PathFill.Options options = PathFill.Options.Default;
            options.Faceted = true;
            options.PathColors = pathColors;

            PathFill.Result result = PathFill.Fill(path, options);
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Colors);
            Assert.AreEqual(result.Vertices.Length, result.Colors.Length);
        }

        // ---------------------------------------------------------------------------
        // Hardening against what the tessellator can actually hand back
        // ---------------------------------------------------------------------------

        [Test]
        public void MalformedTessellationIsSurvived()
        {
            // LibTess pads unfilled polygon slots with Undef (-1), and the vector graphics
            // package casts its indices to UInt16 unchecked, so that padding arrives as
            // 65535. It also runs LibTess with NoEmptyPolygons off, so degenerate faces come
            // through. None of it may reach the mesh.
            PathFillTessellateFn hostile = (outline, rule, verts, tris) =>
            {
                verts.Clear();
                tris.Clear();
                for (int i = 0; i < outline.Count; ++i) { verts.Add(outline[i]); }
                Vector2 centre = Vector2.zero;
                for (int i = 0; i < outline.Count; ++i) { centre += outline[i]; }
                int centreIndex = verts.Count;
                verts.Add(centre / outline.Count);
                verts.Add(new Vector2(float.NaN, float.NaN));
                int nanIndex = verts.Count - 1;
                for (int i = 0; i < outline.Count; ++i)
                {
                    tris.Add(i);
                    tris.Add((i + 1) % outline.Count);
                    tris.Add(centreIndex);
                }
                tris.Add(0); tris.Add(1); tris.Add(65535);        // Undef through the cast
                tris.Add(2); tris.Add(2); tris.Add(centreIndex);  // degenerate face
                tris.Add(3); tris.Add(4); tris.Add(nanIndex);     // non-finite corner
                return true;
            };

            var path = Loop(24, 1f, t => 0.2f * Mathf.Sin(2f * t),
                            Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));
            PathFill.Options options = PathFill.Options.Default;
            options.Tessellator = hostile;

            PathFill.Result result = PathFill.Fill(path, options);
            Assert.IsNotNull(result, "a few bad triangles must not lose the whole fill");
            Assert.AreEqual(3, result.DroppedTriangles);
            Assert.IsTrue(result.HasAnomalies);

            for (int i = 0; i < result.Triangles.Length; ++i)
            {
                Assert.Less(result.Triangles[i], result.Vertices.Length);
                Assert.GreaterOrEqual(result.Triangles[i], 0);
            }
            foreach (Vector3 v in result.Vertices)
            {
                Assert.IsTrue(PathFillGeometry.IsFinite(v), "no vertex may be NaN or infinite");
            }
        }

        [Test]
        public void VertexBudgetIsAHardCapNotAnEstimate()
        {
            // A self-crossing outline makes the tessellator emit a vertex per intersection,
            // so the raw tessellation can overrun the budget before refinement is even
            // considered. Callers store geometry counts in 16-bit fields, so the cap has to
            // hold exactly.
            PathFillTessellateFn explosive = (outline, rule, verts, tris) =>
            {
                verts.Clear();
                tris.Clear();
                for (int i = 0; i < outline.Count; ++i) { verts.Add(outline[i]); }
                Vector2 centre = Vector2.zero;
                for (int i = 0; i < outline.Count; ++i) { centre += outline[i]; }
                centre /= outline.Count;
                int extra = outline.Count * outline.Count / 4;
                for (int i = 0; i < extra; ++i)
                {
                    float a = 2f * Mathf.PI * i / extra;
                    verts.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.1f + 0.001f * i));
                }
                for (int i = 1; i + 1 < verts.Count; ++i)
                {
                    tris.Add(0); tris.Add(i); tris.Add(i + 1);
                }
                return true;
            };

            var path = Loop(400, 1f, t => 0.1f * Mathf.Sin(3f * t),
                            Vector3.right, Vector3.up, new Vector3(0f, 0f, 1f));

            foreach (int cap in new[] { 500, 2000, 6000 })
            {
                PathFill.Options options = PathFill.Options.Default;
                options.Tessellator = explosive;
                options.MaxVertices = cap;
                options.MaxBoundaryPoints = 128;

                PathFill.Result result = PathFill.Fill(path, options);
                if (result == null) { continue; }   // refusing is an acceptable outcome
                Assert.LessOrEqual(result.Vertices.Length, cap,
                                   "MaxVertices must hold exactly, not approximately");
            }
        }

        [Test]
        public void CountUniqueEdgesMatchesWhatSubdivisionAdds()
        {
            var vertices = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(10f, 0f), new Vector2(10f, 10f), new Vector2(0f, 10f),
            };
            var triangles = new List<int> { 0, 1, 2, 0, 2, 3 };

            int expected = PathFillGeometry.CountUniqueEdges(triangles);
            int before = vertices.Count;
            PathFillGeometry.Subdivide(vertices, triangles, 0.001f, int.MaxValue, 1);
            Assert.AreEqual(before + expected, vertices.Count,
                            "a 1->4 pass adds exactly one vertex per unique edge");
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
