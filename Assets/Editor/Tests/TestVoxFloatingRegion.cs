// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestVoxFloatingRegion
    {
        private static VoxFloatingRegion Create(out RuntimeVoxDocument document)
        {
            document = new RuntimeVoxDocument();
            var model = document.CreateModel("floating", Vector3Int.one * 8);
            model.AddOrUpdateVoxel(Vector3Int.zero, 1);
            model.AddOrUpdateVoxel(Vector3Int.right, 2);
            return new VoxFloatingRegion(model, Vector3Int.zero, Vector3Int.zero);
        }

        private static VoxFloatingRegion Move(VoxFloatingRegion current, VoxFloatingRegion grabStart,
            int translation, RuntimeVoxDocument document)
        {
            var result = current.TransformFrom(grabStart, Vector3Int.zero, Quaternion.identity,
                Vector3Int.right * translation);
            Assert.IsTrue(result.TryCompose(document));
            return result;
        }

        private static void AssertCell(VoxFloatingRegion floating, Vector3Int anchor, byte color)
        {
            Vector3Int index = anchor + floating.Model.SourceCellOffset - floating.IndexOrigin;
            Assert.IsTrue(floating.Model.TryGetPaletteIndex(index, out byte actual), $"Missing cell {anchor}");
            Assert.AreEqual(color, actual);
        }

        [Test]
        public void RegrabbingOverlappingRegionRestoresCoveredBackgroundAndKeepsSourceEmpty()
        {
            var start = Create(out var document);
            var first = Move(start, start, 1, document);
            AssertCell(first, Vector3Int.right, 1);
            var second = Move(first, first, 1, document);
            AssertCell(second, Vector3Int.right, 2);
            AssertCell(second, Vector3Int.right * 2, 1);
            Vector3Int original = second.Model.SourceCellOffset - second.IndexOrigin;
            Assert.IsFalse(second.Model.TryGetPaletteIndex(original, out _));
            Assert.AreEqual(1, second.Cells.Count);
        }

        [Test]
        public void StampPermanentlyOverwritesBackgroundWhileRegionKeepsFloating()
        {
            var start = Create(out var document);
            var moved = Move(start, start, 1, document);
            var stamped = moved.Stamp();
            Assert.IsTrue(stamped.SamePose(moved));
            Assert.AreSame(stamped, stamped.Stamp(), "Identical repeated stamps are no-ops");
            var next = Move(stamped, stamped, 1, document);
            AssertCell(next, Vector3Int.right, 1);
            AssertCell(next, Vector3Int.right * 2, 1);
            // The immutable pre-stamp state is what Undo restores. Its covered
            // destination must still contain the original background color.
            var undone = Move(moved, moved, 1, document);
            AssertCell(undone, Vector3Int.right, 2);
            AssertCell(undone, Vector3Int.right * 2, 1);
        }

        [Test]
        public void MultipleStampsDuringOneGrabKeepInitialPayloadAndEarlierStamps()
        {
            var start = Create(out var document);
            var first = Move(start, start, 1, document).Stamp();
            var second = Move(first, start, 2, document).Stamp();
            var third = Move(second, start, 3, document);
            AssertCell(third, Vector3Int.right, 1);
            AssertCell(third, Vector3Int.right * 2, 1);
            AssertCell(third, Vector3Int.right * 3, 1);
            Assert.AreEqual(1, third.Cells.Count);
        }

        [Test]
        public void BackgroundInsideFloatingCuboidHolesIsNeverPickedUp()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("holes", Vector3Int.one * 12);
            model.AddOrUpdateVoxel(Vector3Int.zero, 1);
            model.AddOrUpdateVoxel(Vector3Int.right * 2, 1);
            model.AddOrUpdateVoxel(Vector3Int.right * 4, 2);
            var start = new VoxFloatingRegion(model, Vector3Int.zero, Vector3Int.right * 2);
            var first = Move(start, start, 3, document);
            AssertCell(first, Vector3Int.right * 4, 2);
            var second = Move(first, first, 1, document);
            AssertCell(second, Vector3Int.right * 4, 1);
            var third = Move(second, second, 1, document);
            AssertCell(third, Vector3Int.right * 4, 2);
            Assert.AreEqual(2, third.Cells.Count);
        }

        [Test]
        public void SavingFinalizesOnlySavedDocumentAndLeavesRuntimeUnderlayIntact()
        {
            _ = Create(out var source);
            var document = RuntimeVoxDocument.FromBytes(source.ToVoxBytes());
            var model = document.Models[0];
            var start = new VoxFloatingRegion(model, Vector3Int.zero, Vector3Int.zero);
            var moved = Move(start, start, 1, document);
            var saved = RuntimeVoxDocument.FromBytes(document.ToVoxBytes());
            Assert.AreEqual(1, saved.Models[0].Voxels.Count);
            Vector3 expected = VoxMeshBuilder.GetModelTransform(model) *
                (Vector3)(Vector3Int.right + model.SourceCellOffset - moved.IndexOrigin);
            foreach (var cell in saved.Models[0].Voxels)
            {
                Assert.AreEqual(1, cell.Value);
                Assert.Less((VoxMeshBuilder.GetModelTransform(saved.Models[0]) * (Vector3)cell.Key - expected)
                    .sqrMagnitude, 0.000001f);
            }
            var afterSave = Move(moved, moved, 1, document);
            AssertCell(afterSave, Vector3Int.right, 2);
            AssertCell(afterSave, Vector3Int.right * 2, 1);
        }

        [Test]
        public void QuarterTurnsAndNegativeMovesKeepFloatingPayloadOnItsOriginalLattice()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("rotation", Vector3Int.one * 8);
            model.AddOrUpdateVoxel(Vector3Int.zero, 1);
            model.AddOrUpdateVoxel(Vector3Int.right, 2);
            model.AddOrUpdateVoxel(Vector3Int.right * 4, 3);
            var before = document.CaptureSnapshot();
            var start = new VoxFloatingRegion(model, Vector3Int.zero, Vector3Int.right);
            var rotated = start.TransformFrom(start, Vector3Int.zero, Quaternion.Euler(0, 0, 90),
                new Vector3Int(-3, -2, 0));
            Assert.IsTrue(rotated.TryCompose(document));
            var after = document.CaptureSnapshot();
            AssertCell(rotated, new Vector3Int(-3, -2, 0), 1);
            AssertCell(rotated, new Vector3Int(-3, -1, 0), 2);
            document.RestoreSnapshot(before);
            AssertCell(start, Vector3Int.zero, 1);
            AssertCell(start, Vector3Int.right, 2);
            document.RestoreSnapshot(after);
            var next = Move(rotated, rotated, 1, document);
            AssertCell(next, new Vector3Int(-2, -2, 0), 1);
            AssertCell(next, new Vector3Int(-2, -1, 0), 2);
            AssertCell(next, Vector3Int.right * 4, 3);
        }

        [Test]
        public void RejectedOversizedMoveLeavesVisibleCompositionUntouched()
        {
            var start = Create(out var document);
            var model = start.Model;
            var definition = model.Definition;
            var invalid = start.TransformFrom(start, Vector3Int.zero, Quaternion.identity, Vector3Int.right * 300);
            Assert.IsFalse(invalid.TryCompose(document));
            Assert.AreSame(definition, model.Definition);
            AssertCell(start, Vector3Int.zero, 1);
            AssertCell(start, Vector3Int.right, 2);
        }
    }
}
