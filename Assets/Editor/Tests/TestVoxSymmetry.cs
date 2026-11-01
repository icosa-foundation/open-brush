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
        public void StraightEdgePreviewReplacesEndpointWithoutLeavingACurvedTrail()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("line", Vector3Int.one * 16);
            var copy = Copy(new Vector3Int(5, 5, 1), new Color32(220, 10, 20, 255));
            copy.Last = Vector3Int.one;
            copy.HasLast = true;
            var edits = new VoxSymmetryEdits();
            var preview = new Dictionary<Vector3Int, Color32>();
            byte[] before = document.ToVoxBytes();
            edits.CollectPreview(model, new[] { copy }, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true, preview);
            Assert.IsTrue(preview.ContainsKey(new Vector3Int(2, 2, 1)));
            copy.Center = new Vector3Int(5, 1, 1);
            edits.CollectPreview(model, new[] { copy }, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true, preview);
            Assert.IsFalse(preview.ContainsKey(new Vector3Int(2, 2, 1)));
            CollectionAssert.AreEqual(before, document.ToVoxBytes());
            Assert.IsTrue(edits.Apply(document, model, new[] { copy }, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true).Changed);
            Assert.AreEqual(5, model.Voxels.Count);
            for (int x = 1; x <= 5; ++x) Assert.IsTrue(model.Voxels.ContainsKey(new Vector3Int(x, 1, 1)));
        }

        [Test]
        public void StraightEdgeMirrorCopiesKeepTheirOwnAnchors()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("mirrored lines", Vector3Int.one * 32);
            var copies = new[] { Copy(new Vector3Int(5, 1, 1), new Color32(220, 10, 20, 255)),
                Copy(new Vector3Int(15, 1, 1), new Color32(10, 20, 220, 255)) };
            copies[0].Last = Vector3Int.one;
            copies[1].Last = new Vector3Int(11, 1, 1);
            foreach (var copy in copies) copy.HasLast = true;
            var edits = new VoxSymmetryEdits();
            var preview = new Dictionary<Vector3Int, Color32>();
            edits.CollectPreview(model, copies, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true, preview);
            Assert.AreEqual(10, preview.Count);
            Assert.AreEqual(0, model.Voxels.Count);
            edits.Apply(document, model, copies, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true);
            Assert.AreEqual(10, model.Voxels.Count);
            for (int x = 6; x <= 10; ++x) Assert.IsFalse(model.Voxels.ContainsKey(new Vector3Int(x, 1, 1)));
        }

        [Test]
        public void StraightEdgePaintAndErasePreviewDoNotModifyDiscardedPositions()
        {
            foreach (var mode in new[] { VoxelTool.EditMode.Paint, VoxelTool.EditMode.Erase })
            {
                var document = new RuntimeVoxDocument();
                var model = document.CreateModel("line", Vector3Int.one * 16);
                for (int x = 1; x <= 5; ++x) model.AddOrUpdateVoxel(new Vector3Int(x, 1, 1), 1);
                var untouched = new Vector3Int(2, 2, 1);
                model.AddOrUpdateVoxel(untouched, 1);
                var copy = Copy(new Vector3Int(5, 5, 1), new Color32(10, 220, 20, 255));
                copy.Last = Vector3Int.one;
                copy.HasLast = true;
                var edits = new VoxSymmetryEdits();
                var preview = new Dictionary<Vector3Int, Color32>();
                byte[] before = document.ToVoxBytes();
                edits.CollectPreview(model, new[] { copy }, new VoxBrushFootprint(0), mode, true, preview);
                copy.Center = new Vector3Int(5, 1, 1);
                edits.CollectPreview(model, new[] { copy }, new VoxBrushFootprint(0), mode, true, preview);
                CollectionAssert.AreEqual(before, document.ToVoxBytes());
                Assert.IsFalse(preview.ContainsKey(untouched));
                Assert.AreEqual(5, preview.Count);
                edits.Apply(document, model, new[] { copy }, new VoxBrushFootprint(0), mode, false);
                Assert.AreEqual(mode == VoxelTool.EditMode.Erase ? 1 : 6, model.Voxels.Count);
                Assert.AreEqual(1, model.Voxels[untouched]);
            }
        }

        [Test]
        public void StraightEdgePreviewChecksTheWholeLineAgainstFormatLimits()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("limits", Vector3Int.one * 16);
            var copy = Copy(new Vector3Int(400, 1, 1), new Color32(220, 10, 20, 255));
            copy.Last = Vector3Int.one;
            copy.HasLast = true;
            var edits = new VoxSymmetryEdits();
            var preview = new Dictionary<Vector3Int, Color32>();
            edits.CollectPreview(model, new[] { copy }, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true, preview);
            Assert.IsTrue(copy.PreviewInvalid);
            Assert.AreEqual(0, preview.Count);
            Assert.IsFalse(edits.Apply(document, model, new[] { copy }, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true).Changed);
            Assert.AreEqual(0, model.Voxels.Count);
            Assert.AreEqual(Vector3Int.one * 16, model.Size);
        }

        private static VoxSymmetryEdits.Copy Copy(Vector3Int center, Color32 color)
        {
            var copy = new VoxSymmetryEdits.Copy { Center = center, Color = color, Valid = true,
                Linear = Matrix4x4.identity, Inverse = Matrix4x4.identity };
            VoxBrushRasterizer.TryGetBounds(new VoxBrushFootprint(0), copy.Linear, out copy.Min, out copy.Max);
            return copy;
        }

        [Test]
        public void MirroredAddKeepsPrimaryColorAndPlacementAcrossBoundsRebasing()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("mirror", Vector3Int.one * 3);
            var red = new Color32(220, 10, 20, 255);
            var blue = new Color32(10, 20, 220, 255);
            var copies = new[] { Copy(Vector3Int.one, red), Copy(new Vector3Int(-3, 1, 1), blue),
                Copy(Vector3Int.one, new Color32(10, 220, 20, 255)) };
            var before = document.CaptureSnapshot();
            var result = new VoxSymmetryEdits().Apply(document, model, copies, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true);
            Assert.IsTrue(result.Changed);
            Assert.AreEqual(2, model.Voxels.Count);
            Assert.AreEqual(red, document.Palette[model.Voxels[copies[0].Center] - 1]);
            Assert.AreEqual(blue, document.Palette[model.Voxels[copies[1].Center] - 1]);
            Assert.AreEqual(Vector3.one, (Vector3)copies[0].Center + model.TransformOffset);
            Assert.AreEqual(copies[0].Center, copies[0].Last);
            var after = document.CaptureSnapshot();
            document.RestoreSnapshot(before);
            Assert.AreEqual(0, model.Voxels.Count);
            document.RestoreSnapshot(after);
            Assert.AreEqual(2, model.Voxels.Count);
            Assert.AreEqual(red, document.Palette[model.Voxels[copies[0].Center] - 1]);
        }

        [Test]
        public void MirroredGesturesDoNotConnectDifferentCopies()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("paths", Vector3Int.one * 16);
            var copies = new[] { Copy(new Vector3Int(1, 1, 1), new Color32(200, 10, 20, 255)),
                Copy(new Vector3Int(10, 1, 1), new Color32(20, 10, 200, 255)) };
            var edits = new VoxSymmetryEdits();
            edits.Apply(document, model, copies, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true);
            copies[0].Center += Vector3Int.right * 2;
            copies[1].Center += Vector3Int.right * 2;
            edits.Apply(document, model, copies, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true);
            Assert.AreEqual(6, model.Voxels.Count);
            for (int x = 4; x < 10; ++x) Assert.IsFalse(model.Voxels.ContainsKey(new Vector3Int(x, 1, 1)));
        }

        [Test]
        public void MirroredPaintAndEraseOnlyAffectExistingTargetCells()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("paint", Vector3Int.one * 16);
            model.AddOrUpdateVoxel(new Vector3Int(1, 1, 1), 1);
            model.AddOrUpdateVoxel(new Vector3Int(10, 1, 1), 2);
            var unrelated = new Vector3Int(5, 1, 1);
            model.AddOrUpdateVoxel(unrelated, 3);
            var green = new Color32(10, 220, 20, 255);
            var copies = new[] { Copy(new Vector3Int(1, 1, 1), green), Copy(new Vector3Int(10, 1, 1), green),
                Copy(new Vector3Int(13, 1, 1), green) };
            var edits = new VoxSymmetryEdits();
            Assert.IsTrue(edits.Apply(document, model, copies, new VoxBrushFootprint(0), VoxelTool.EditMode.Paint, false).Changed);
            Assert.AreEqual(3, model.Voxels.Count);
            Assert.AreEqual(green, document.Palette[model.Voxels[copies[0].Center] - 1]);
            Assert.AreEqual(green, document.Palette[model.Voxels[copies[1].Center] - 1]);
            Assert.AreEqual(3, model.Voxels[unrelated]);
            foreach (var copy in copies) copy.HasLast = false;
            Assert.IsTrue(edits.Apply(document, model, copies, new VoxBrushFootprint(0), VoxelTool.EditMode.Erase, false).Changed);
            Assert.AreEqual(1, model.Voxels.Count);
            Assert.IsTrue(model.Voxels.ContainsKey(unrelated));
        }

        [Test]
        public void MirroredAddRespectsFormatAndPaletteLimitsWithoutGrowingOnFailure()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("limits", new Vector3Int(256, 3, 3));
            for (int i = 1; i <= byte.MaxValue; ++i) model.AddOrUpdateVoxel(new Vector3Int(i - 1, 0, 0), (byte)i);
            var copies = new[] { Copy(new Vector3Int(400, 1, 1), document.Palette[0]),
                Copy(Vector3Int.one, new Color32(12, 34, 56, 255)) };
            byte[] before = document.ToVoxBytes();
            var result = new VoxSymmetryEdits().Apply(document, model, copies, new VoxBrushFootprint(0), VoxelTool.EditMode.Add, true);
            Assert.IsFalse(result.Changed);
            Assert.IsTrue(result.PaletteFull);
            CollectionAssert.AreEqual(before, document.ToVoxBytes());
            Assert.IsFalse(copies[0].HasLast);
            Assert.IsFalse(copies[1].HasLast);
        }

        [Test]
        public void PreviewBoxesPreserveHolesColorsAndShaderBufferLayout()
        {
            var cells = new Dictionary<Vector3Int, Color32>();
            var red = new Color32(200, 10, 20, 255);
            var blue = new Color32(10, 20, 200, 255);
            for (int x = 0; x < 3; ++x)
                for (int y = 0; y < 3; ++y)
                    for (int z = 0; z < 3; ++z)
                    {
                        var cell = new Vector3Int(x, y, z);
                        if (cell != Vector3Int.one) cells.Add(cell, cell == Vector3Int.one * 2 ? blue : red);
                    }
            var boxes = new List<VoxPreviewBox>();
            VoxPreviewBoxes.Append(cells, new HashSet<Vector3Int>(), boxes);
            var reconstructed = new HashSet<Vector3Int>();
            foreach (var box in boxes)
            {
                var min = Vector3Int.RoundToInt((Vector3)box.Min + Vector3.one * 0.5f);
                var max = Vector3Int.RoundToInt((Vector3)box.Max - Vector3.one * 0.5f);
                for (int x = min.x; x <= max.x; ++x)
                    for (int y = min.y; y <= max.y; ++y)
                        for (int z = min.z; z <= max.z; ++z)
                        {
                            var cell = new Vector3Int(x, y, z);
                            Assert.IsTrue(reconstructed.Add(cell));
                            Assert.IsTrue(cells.ContainsKey(cell));
                            Color32 color = box.Color;
                            Assert.AreEqual(cells[cell].r, color.r);
                            Assert.AreEqual(cells[cell].g, color.g);
                            Assert.AreEqual(cells[cell].b, color.b);
                        }
            }
            CollectionAssert.AreEquivalent(cells.Keys, reconstructed);
            Assert.AreEqual(48, System.Runtime.InteropServices.Marshal.SizeOf<VoxPreviewBox>());
        }

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
