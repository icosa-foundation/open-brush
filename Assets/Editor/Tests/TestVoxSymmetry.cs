// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestVoxSymmetry
    {
        [Test]
        public void ReflectedBrushKeepsExactGridCellsAndExpandedFootprint()
        {
            var footprint = new VoxBrushFootprint(3);
            var reflection = Matrix4x4.identity;
            reflection.m00 = -1;
            Assert.IsTrue(VoxBrushRasterizer.TryGetBounds(footprint, reflection, out var min, out var max));
            var cells = new List<Vector3Int>();
            var center = new Vector3Int(10, 5, 4);
            VoxBrushRasterizer.GetCells(footprint, reflection, reflection, center, min, max, null, false, cells);
            Assert.AreEqual(81, cells.Count);
            Assert.AreEqual(cells.Count, new HashSet<Vector3Int>(cells).Count);
            Assert.Contains(center + Vector3Int.left * 2, cells);
            Assert.Contains(center + Vector3Int.right * 2, cells);
            Assert.IsFalse(cells.Contains(center + new Vector3Int(2, 2, 0)));
        }

        private static void Rotation45(out Matrix4x4 forward, out Matrix4x4 inverse)
        {
            const float c = 0.70710677f;
            forward = Matrix4x4.identity;
            forward.m00 = forward.m11 = c;
            forward.m01 = -c;
            forward.m10 = c;
            inverse = Matrix4x4.identity;
            inverse.m00 = inverse.m11 = c;
            inverse.m01 = c;
            inverse.m10 = -c;
        }

        [Test]
        public void ArbitraryRotationRasterizesTheWholeBrushVolume()
        {
            Rotation45(out var forward, out var inverse);
            var footprint = new VoxBrushFootprint(2);
            Assert.IsTrue(VoxBrushRasterizer.TryGetBounds(footprint, forward, out var min, out var max));
            var cells = new List<Vector3Int>();
            VoxBrushRasterizer.GetCells(footprint, forward, inverse, Vector3Int.zero, min, max, null, false, cells);
            Assert.Contains(new Vector3Int(2, 0, 0), cells);
            Assert.Contains(new Vector3Int(0, 2, 0), cells);
            Assert.IsFalse(cells.Contains(new Vector3Int(2, 1, 0)));
            Assert.AreEqual(cells.Count, new HashSet<Vector3Int>(cells).Count);
        }

        [Test]
        public void PaintAndEraseRasterizationOnlyReturnsOccupiedCells()
        {
            Rotation45(out var forward, out var inverse);
            var footprint = new VoxBrushFootprint(2);
            VoxBrushRasterizer.TryGetBounds(footprint, forward, out var min, out var max);
            var model = new RuntimeVoxDocument().CreateModel("target", new Vector3Int(8, 8, 8));
            var center = Vector3Int.one * 3;
            model.AddOrUpdateVoxel(center, 1);
            model.AddOrUpdateVoxel(center + new Vector3Int(2, 0, 0), 1);
            model.AddOrUpdateVoxel(center + new Vector3Int(2, 1, 0), 1);
            var cells = new List<Vector3Int>();
            VoxBrushRasterizer.GetCells(footprint, forward, inverse, center, min, max, model, true, cells);
            Assert.AreEqual(2, cells.Count);
            Assert.Contains(center, cells);
            Assert.Contains(center + new Vector3Int(2, 0, 0), cells);
            Assert.AreEqual(3, model.Voxels.Count);
        }

        [Test]
        public void MirrorPathsStaySeparateAndClipToTheTargetVolume()
        {
            var first = new List<Vector3Int>();
            var second = new List<Vector3Int>();
            VoxBrushRasterizer.GetPath(Vector3Int.zero, Vector3Int.right * 2,
                Vector3Int.zero, Vector3Int.one * 20, first);
            VoxBrushRasterizer.GetPath(Vector3Int.right * 10, Vector3Int.right * 12,
                Vector3Int.zero, Vector3Int.one * 20, second);
            CollectionAssert.AreEqual(new[] { Vector3Int.zero, Vector3Int.right, Vector3Int.right * 2 }, first);
            CollectionAssert.AreEqual(new[] { Vector3Int.right * 10, Vector3Int.right * 11, Vector3Int.right * 12 }, second);
            VoxBrushRasterizer.GetPath(Vector3Int.left * 100000, Vector3Int.right * 100000,
                Vector3Int.zero, new Vector3Int(3, 0, 0), first);
            Assert.AreEqual(4, first.Count);
            Assert.AreEqual(Vector3Int.zero, first[0]);
            Assert.AreEqual(Vector3Int.right * 3, first[3]);
        }
    }
}
