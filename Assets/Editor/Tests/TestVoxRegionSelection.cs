// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestVoxRegionSelection
    {
        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingMoveOrCloneUsesOriginalCellsAndOverwritesDestination(bool clone)
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("region", Vector3Int.one * 8);
            model.AddOrUpdateVoxel(Vector3Int.zero, 1);
            model.AddOrUpdateVoxel(Vector3Int.right, 2);
            model.AddOrUpdateVoxel(Vector3Int.right * 2, 3);
            model.AddOrUpdateVoxel(Vector3Int.up * 5, 4);
            var before = document.CaptureSnapshot();
            Assert.IsTrue(document.TryTransformRegion(model, Vector3Int.zero, Vector3Int.right,
                Vector3Int.zero, Quaternion.identity, Vector3Int.right, clone, out var shift));
            AssertCell(model, Vector3Int.right + shift, 1);
            AssertCell(model, Vector3Int.right * 2 + shift, 2);
            AssertCell(model, Vector3Int.up * 5 + shift, 4);
            Assert.AreEqual(clone, model.TryGetPaletteIndex(shift, out _));
            document.RestoreSnapshot(before);
            Assert.AreEqual(4, model.Voxels.Count);
            AssertCell(model, Vector3Int.zero, 1);
            AssertCell(model, Vector3Int.right, 2);
            AssertCell(model, Vector3Int.right * 2, 3);
        }

        [Test]
        public void QuarterTurnAndNegativeTranslationPreserveColorsAndGridPlacement()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("rotation", Vector3Int.one * 8);
            model.AddOrUpdateVoxel(Vector3Int.right, 1);
            model.AddOrUpdateVoxel(Vector3Int.right * 2, 2);
            var rotation = RuntimeVoxDocument.SnapRegionRotation(Quaternion.Euler(0, 0, 86));
            var translation = new Vector3Int(-4, -5, 0);
            Assert.IsTrue(document.TryTransformRegion(model, Vector3Int.zero, Vector3Int.one * 3,
                Vector3Int.zero, rotation, translation, false, out var shift));
            AssertCell(model, Vector3Int.up + translation + shift, 1);
            AssertCell(model, Vector3Int.up * 2 + translation + shift, 2);
            Assert.AreEqual(-(Vector3)shift, model.TransformOffset);
        }

        [Test]
        public void AllCubeOrientationsMapCellsExactlyWithoutDuplicates()
        {
            var rotations = (from x in Enumerable.Range(0, 4)
                             from y in Enumerable.Range(0, 4)
                             from z in Enumerable.Range(0, 4)
                             select RuntimeVoxDocument.SnapRegionRotation(Quaternion.Euler(x * 90, y * 90, z * 90)))
                .Select(q => RuntimeVoxDocument.TransformRegionCell(new Vector3Int(1, 2, 3),
                    Vector3Int.zero, q, Vector3Int.zero)).Distinct().ToArray();
            Assert.AreEqual(24, rotations.Length);
            Assert.IsTrue(rotations.All(cell => cell.sqrMagnitude == 14));
        }

        [Test]
        public void PreviewRestoresBaselineSoTraversedCellsSurvive()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("preview", Vector3Int.one * 8);
            model.AddOrUpdateVoxel(Vector3Int.zero, 1);
            model.AddOrUpdateVoxel(Vector3Int.right, 2);
            var before = document.CaptureSnapshot();
            Assert.IsTrue(document.TryTransformRegion(model, Vector3Int.zero, Vector3Int.zero,
                Vector3Int.zero, Quaternion.identity, Vector3Int.right, false, out _));
            document.RestoreSnapshot(before);
            Assert.IsTrue(document.TryTransformRegion(model, Vector3Int.zero, Vector3Int.zero,
                Vector3Int.zero, Quaternion.identity, Vector3Int.right * 2, false, out var shift));
            AssertCell(model, Vector3Int.right + shift, 2);
            AssertCell(model, Vector3Int.right * 2 + shift, 1);
        }

        [Test]
        public void FormatLimitAndIdentityLeaveDocumentUnchanged()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("limits", Vector3Int.one * 8);
            model.AddOrUpdateVoxel(Vector3Int.zero, 1);
            var definition = model.Definition;
            Assert.IsFalse(document.TryTransformRegion(model, Vector3Int.zero, Vector3Int.zero,
                Vector3Int.zero, Quaternion.identity, Vector3Int.zero, false, out _));
            Assert.IsFalse(document.TryTransformRegion(model, Vector3Int.zero, Vector3Int.zero,
                Vector3Int.zero, Quaternion.identity, Vector3Int.right * 256, true, out _));
            Assert.AreSame(definition, model.Definition);
            AssertCell(model, Vector3Int.zero, 1);
        }

        [TestCase(0)]
        [TestCase(90)]
        [TestCase(180)]
        public void GrabDeltaUsesRotatedScaledGridAndIgnoresResize(int degrees)
        {
            var widget = TrTransform.TRS(new Vector3(5, 7, -2), Quaternion.Euler(20, 35, 10), 2.5f);
            var grid = widget * TrTransform.TR(new Vector3(-3, 4, 1), Quaternion.Euler(0, 90, 0));
            var pivot = new Vector3Int(2, 3, 1);
            var move = new Vector3Int(-2, 1, 3);
            Quaternion turn = Quaternion.Euler(0, 0, degrees);
            var delta = TrTransform.TR((Vector3)pivot + move, turn) * TrTransform.TR(-pivot, Quaternion.identity);
            TrTransform desired = grid * delta * grid.inverse * widget;
            desired.scale *= 2;
            RuntimeVoxDocument.GetRegionGrabDelta(widget, grid, desired, pivot, out var rotation, out var translation);
            Assert.Less(Quaternion.Angle(turn, rotation), 0.01f);
            Assert.AreEqual(move, translation);
        }

        [Test]
        public void ImportedRegionMovePreservesPlacementThroughExportAndSnapshotUndoRedo()
        {
            var source = new RuntimeVoxDocument();
            var original = source.CreateModel("import", Vector3Int.one * 8);
            original.AddOrUpdateVoxel(Vector3Int.one, 1);
            original.AddOrUpdateVoxel(Vector3Int.one * 4, 2);
            var document = RuntimeVoxDocument.FromBytes(source.ToVoxBytes());
            var model = document.Models[0];
            var before = document.CaptureSnapshot();
            var beforeGrid = VoxMeshBuilder.GetModelTransform(model);
            Vector3Int move = new Vector3Int(-5, 2, 0);
            Assert.IsTrue(document.TryTransformRegion(model, Vector3Int.one, Vector3Int.one,
                Vector3Int.one, Quaternion.identity, move, false, out var shift));
            var after = document.CaptureSnapshot();
            var reopened = RuntimeVoxDocument.FromBytes(document.ToVoxBytes());
            var reopenedGrid = VoxMeshBuilder.GetModelTransform(reopened.Models[0]);
            AssertCell(reopened.Models[0], Vector3Int.one + move + shift, 1);
            AssertCell(reopened.Models[0], Vector3Int.one * 4 + shift, 2);
            Assert.Less((reopenedGrid * (Vector3)(Vector3Int.one + move + shift) -
                beforeGrid * (Vector3)(Vector3Int.one + move)).sqrMagnitude, 0.000001f);
            Assert.Less((reopenedGrid * (Vector3)(Vector3Int.one * 4 + shift) -
                beforeGrid * (Vector3)(Vector3Int.one * 4)).sqrMagnitude, 0.000001f);
            document.RestoreSnapshot(before);
            AssertCell(model, Vector3Int.one, 1);
            document.RestoreSnapshot(after);
            AssertCell(model, Vector3Int.one + move + shift, 1);
        }

        private static void AssertCell(RuntimeVoxDocument.RuntimeModel model, Vector3Int cell, byte expected)
        {
            Assert.IsTrue(model.TryGetPaletteIndex(cell, out byte actual), $"Missing cell {cell}");
            Assert.AreEqual(expected, actual);
        }
    }
}
