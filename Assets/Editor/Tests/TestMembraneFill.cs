// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestMembraneFill
    {
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
            var options = PathFill.Options.Default;
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
                Assert.IsTrue(PathFillGeometry.IsFinite(vertex));
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
            var closed = MembraneFill.Fill(path, PathFill.Options.Default);
            CollectionAssert.AreEqual(first.Vertices, closed.Vertices);
        }

        [Test]
        public void SmallMotionIsContinuousAndPrefixesKeepTheirTopology()
        {
            var path = TwistedLoop();
            var options = PathFill.Options.Default;
            var first = MembraneFill.Fill(path, options);
            path[40] += new Vector3(0.0001f, -0.0001f, 0.0001f);
            var moved = MembraneFill.Fill(path, options);
            CollectionAssert.AreEqual(first.Triangles, moved.Triangles);
            for (int i = 0; i < first.Vertices.Length; ++i)
            {
                Assert.Less(Vector3.Distance(first.Vertices[i], moved.Vertices[i]), 0.001f);
            }
            foreach (int length in new[] { 2, 3, 8, 24, 48, 95 })
            {
                var prefix = MembraneFill.Fill(path.GetRange(0, length), options);
                Assert.NotNull(prefix);
                CollectionAssert.AreEqual(first.Triangles, prefix.Triangles);
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
                    var options = PathFill.Options.Default;
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
