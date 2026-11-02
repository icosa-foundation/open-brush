// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestMembraneFill
    {
        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
                && !float.IsInfinity(value.x) && !float.IsInfinity(value.y)
                && !float.IsInfinity(value.z);
        }
        private static List<Vector3> TwistedLoop()
        {
            var path = new List<Vector3>();
            for (int i = 0; i < 96; ++i)
            {
                float t = i * Mathf.PI * 2 / 96;
                path.Add(new Vector3(Mathf.Sin(2 * t), Mathf.Cos(t), Mathf.Sin(3 * t)));
            }
            return path;
        }

        [Test]
        public void CrossingStrokeRemainsBoundedAndReplaysExactly()
        {
            var path = TwistedLoop();
            var options = MembraneFill.Options.Default;
            options.PathColors = new List<Color32>();
            foreach (var point in path) { options.PathColors.Add(new Color32(80, 120, 160, 255)); }
            var first = MembraneFill.Fill(path, options);
            var replay = MembraneFill.Fill(path, options);
            Assert.NotNull(first);
            CollectionAssert.AreEqual(first.Vertices, replay.Vertices);
            CollectionAssert.AreEqual(first.Triangles, replay.Triangles);
            CollectionAssert.AreEqual(first.Colors, replay.Colors);
            foreach (var vertex in first.Vertices)
            {
                Assert.IsTrue(IsFinite(vertex));
                Assert.LessOrEqual(Mathf.Abs(vertex.x), 1.00001f);
                Assert.LessOrEqual(Mathf.Abs(vertex.y), 1.00001f);
                Assert.LessOrEqual(Mathf.Abs(vertex.z), 1.00001f);
            }
            foreach (int index in first.Triangles)
            {
                Assert.That(index, Is.InRange(0, first.Vertices.Length - 1));
            }
            // Explicit closure and duplicate samples must not change the parameterization.
            path.Add(path[0]);
            path.Insert(12, path[12]);
            var closed = MembraneFill.Fill(path, MembraneFill.Options.Default);
            CollectionAssert.AreEqual(first.Vertices, closed.Vertices);
        }

        [Test]
        public void SmallMotionIsContinuousAndPrefixesKeepTheirTopology()
        {
            var path = TwistedLoop();
            var options = MembraneFill.Options.Default;
            var first = MembraneFill.Fill(path, options);
            path[40] += new Vector3(0.0001f, -0.0001f, 0.0001f);
            var moved = MembraneFill.Fill(path, options);
            CollectionAssert.AreEqual(first.Triangles, moved.Triangles);
            for (int i = 0; i < first.Vertices.Length; ++i)
            {
                Assert.Less(Vector3.Distance(first.Vertices[i], moved.Vertices[i]), 0.001f);
            }
            Assert.IsNull(MembraneFill.Fill(path.GetRange(0, 2), options));
            Assert.IsNull(MembraneFill.Fill(path.GetRange(0, 3), options));
            foreach (int length in new[] { 8, 24, 48, 95 })
            {
                var prefix = MembraneFill.Fill(path.GetRange(0, length), options);
                Assert.NotNull(prefix);
                CollectionAssert.AreEqual(first.Triangles, prefix.Triangles);
            }
        }

        [Test]
        public void NewAndNearlyStraightStrokesDoNotEmitCollapsedPreviewMeshes()
        {
            var path = new List<Vector3>
            {
                Vector3.zero,
                new Vector3(0.002f, 0, 0)
            };
            var options = MembraneFill.Options.Default;
            options.SimplifyToleranceAbsolute = 0.00125f;
            Assert.IsNull(MembraneFill.Fill(path, options));
            path.Add(new Vector3(0.002f, 0.00002f, 0));
            Assert.IsNull(MembraneFill.Fill(path, options));
            path[2] = new Vector3(0.002f, 0.00026f, 0);
            Assert.NotNull(MembraneFill.Fill(path, options));
            path[2] = new Vector3(0.002f, 0.002f, 0);
            Assert.NotNull(MembraneFill.Fill(path, options));
            // Winding or a change of drawing plane must not affect the width check.
            path.Reverse();
            Assert.NotNull(MembraneFill.Fill(path, options));
        }

        [Test]
        public void WorkspaceReuseDoesNotCarryGeometryOrColorsBetweenStrokes()
        {
            var workspace = new MembraneFill.Workspace();
            var path = TwistedLoop();
            var options = MembraneFill.Options.Default;
            foreach (bool faceted in new[] { false, true, false })
            {
                options.Faceted = faceted;
                foreach (int colorMode in new[] { 0, 1, 2, 1, 0 })
                {
                    options.PathColors = colorMode == 0 ? null : new List<Color32>();
                    for (int i = 0; i < path.Count && colorMode != 0; ++i)
                    {
                        options.PathColors.Add(new Color32((byte)(colorMode == 1 ? 80 : i * 2), 120, 160, 255));
                    }
                    var warm = MembraneFill.Fill(path, options, workspace);
                    var cold = MembraneFill.Fill(path, options);
                    CollectionAssert.AreEqual(cold.Vertices, warm.Vertices);
                    CollectionAssert.AreEqual(cold.Normals, warm.Normals);
                    CollectionAssert.AreEqual(cold.Colors, warm.Colors);
                    CollectionAssert.AreEqual(cold.Uvs, warm.Uvs);
                    CollectionAssert.AreEqual(cold.Triangles, warm.Triangles);
                    var vertices = warm.Vertices;
                    var triangles = warm.Triangles;
                    path[12] += new Vector3(0.01f, 0, 0);
                    var changed = MembraneFill.Fill(path, options, workspace);
                    Assert.AreSame(vertices, changed.Vertices);
                    Assert.AreSame(triangles, changed.Triangles);
                    if (colorMode == 1)
                    {
                        foreach (var color in changed.Colors)
                        {
                            Assert.AreEqual(new Color32(80, 120, 160, 255), color);
                        }
                    }
                }
            }
            // A smaller budget rebuilds storage; returning to the original budget must
            // still produce the same cold result rather than retaining old rim samples.
            options.MaxVertices = 64;
            MembraneFill.Fill(path, options, workspace);
            options.MaxVertices = 3000;
            CollectionAssert.AreEqual(MembraneFill.Fill(path, options).Vertices,
                MembraneFill.Fill(path, options, workspace).Vertices);
        }

        [Test]
        public void FinalBoundaryRecoversDetailWithoutMovingThePreviewGrid()
        {
            var path = new List<Vector3>();
            var options = MembraneFill.Options.Default;
            options.PathColors = new List<Color32>();
            for (int i = 0; i < 768; ++i)
            {
                float angle = 2 * Mathf.PI * i / 768;
                float radius = 1 + 0.15f * Mathf.Sin(18 * angle);
                path.Add(new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle),
                    0.1f * Mathf.Sin(3 * angle)));
                options.PathColors.Add(new Color32((byte)(i % 256), 100, 200, 255));
            }
            var workspace = new MembraneFill.Workspace();
            var preview = MembraneFill.Fill(path, options, workspace);
            var previewVertices = (Vector3[])preview.Vertices.Clone();
            var final = MembraneFill.FillFinal(path, options, workspace);
            Assert.AreEqual(384, final.Boundary.Length);
            Assert.AreEqual(1007, final.Vertices.Length);
            Assert.AreEqual(1628, final.Triangles.Length / 3);
            for (int i = 0; i < previewVertices.Length; ++i)
            {
                Assert.AreEqual(previewVertices[i], final.Vertices[i]);
            }
            Assert.Less(BoundaryError(path, final.Boundary), BoundaryError(path, preview.Boundary) * 0.3f);
            var replay = MembraneFill.FillFinal(path, options);
            CollectionAssert.AreEqual(final.Vertices, replay.Vertices);
            CollectionAssert.AreEqual(final.Colors, replay.Colors);
            CollectionAssert.AreEqual(final.Triangles, replay.Triangles);

            // Every internal edge is shared by exactly two triangles. The only open edges
            // must be the dense outer rim, including the two grid corners with two rim edges.
            var edges = new Dictionary<long, int>();
            for (int i = 0; i < final.Triangles.Length; i += 3)
            {
                for (int j = 0; j < 3; ++j)
                {
                    int a = final.Triangles[i + j], b = final.Triangles[i + (j + 1) % 3];
                    Assert.That(a, Is.InRange(0, final.Vertices.Length - 1));
                    long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                    edges.TryGetValue(key, out int count);
                    edges[key] = count + 1;
                }
            }
            int openEdges = 0;
            foreach (int count in edges.Values)
            {
                Assert.That(count, Is.InRange(1, 2));
                if (count == 1) { ++openEdges; }
            }
            Assert.AreEqual(final.Boundary.Length, openEdges);
        }

        private static float BoundaryError(IList<Vector3> path, Vector3[] boundary)
        {
            float worst = 0;
            foreach (var p in path)
            {
                float closest = float.MaxValue;
                for (int i = 0; i < boundary.Length; ++i)
                {
                    Vector3 a = boundary[i], edge = boundary[(i + 1) % boundary.Length] - a;
                    float t = edge.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(p - a, edge) / edge.sqrMagnitude) : 0;
                    closest = Mathf.Min(closest, (p - a - t * edge).magnitude);
                }
                worst = Mathf.Max(worst, closest);
            }
            return worst;
        }

        [Test]
        public void FinalBoundaryRespectsBudgetsAndDoesNotAffectLaterPreviews()
        {
            var path = TwistedLoop();
            var workspace = new MembraneFill.Workspace();
            foreach (bool faceted in new[] { false, true })
            {
                foreach (int budget in new[] { 6, 64, 3000, 6000 })
                {
                    var options = MembraneFill.Options.Default;
                    options.Faceted = faceted;
                    options.MaxVertices = budget;
                    var final = MembraneFill.FillFinal(path, options, workspace, 8);
                    Assert.NotNull(final);
                    Assert.LessOrEqual(final.Vertices.Length, budget);
                    foreach (var vertex in final.Vertices) { Assert.IsTrue(IsFinite(vertex)); }
                    CollectionAssert.AreEqual(MembraneFill.Fill(path, options).Vertices,
                        MembraneFill.Fill(path, options, workspace).Vertices);
                }
            }
        }

        [Test]
        public void PlanarRimIsPreservedAndBothLayoutsRespectBudgets()
        {
            var square = new List<Vector3>
            {
                Vector3.zero, Vector3.right, Vector3.right + Vector3.up, Vector3.up
            };
            foreach (bool faceted in new[] { false, true })
            {
                foreach (int budget in new[] { 64, 3000, 3600 })
                {
                    var options = MembraneFill.Options.Default;
                    options.Faceted = faceted;
                    options.MaxVertices = budget;
                    var fill = MembraneFill.Fill(square, options);
                    Assert.NotNull(fill);
                    Assert.LessOrEqual(fill.Vertices.Length, budget);
                    foreach (var vertex in fill.Vertices) { Assert.AreEqual(0, vertex.z); }
                    foreach (var rim in fill.Boundary)
                    {
                        Assert.IsTrue(rim.x == 0 || rim.x == 1 || rim.y == 0 || rim.y == 1);
                        CollectionAssert.Contains(fill.Vertices, rim);
                    }
                }
            }
        }
    }
}
