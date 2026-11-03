// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestVoxFloatingRegion
    {
        private static VoxFloatingRegion Lift(out RuntimeVoxDocument document)
        {
            document = new RuntimeVoxDocument();
            var model = document.CreateModel("floating", Vector3Int.one * 32);
            model.AddOrUpdateVoxel(Vector3Int.zero, 1);
            model.AddOrUpdateVoxel(Vector3Int.right, 2);
            var region = new VoxFloatingRegion(model, Vector3Int.zero, Vector3Int.zero).Lift();
            VoxCellChange.Apply(model, VoxCellChange.Remove(region), true);
            return region;
        }

        private static VoxFloatingRegion Move(VoxFloatingRegion region, int distance) =>
            region.TransformFrom(region, Vector3Int.zero, Quaternion.identity, Vector3Int.right * distance);

        [Test]
        public void MovingOverBackgroundKeepsOnePayloadAndDoesNotEditTheGrid()
        {
            var start = Lift(out var document);
            var definition = start.Model.Definition;
            var moved = Move(start, 1);
            var next = Move(moved, 1);
            Assert.AreSame(start.Payload, next.Payload);
            Assert.AreSame(definition, next.Model.Definition);
            Assert.AreEqual(1, next.Model.Voxels.Count);
            Assert.AreEqual(2, next.Model.Voxels[Vector3Int.right]);
            Assert.IsFalse(next.Model.Voxels.ContainsKey(Vector3Int.zero));
            Assert.AreEqual(Vector3Int.right * 2, next.CellsAt(next.Pose).Single().Key);
        }

        [Test]
        public void OverlappingStampsOverwriteOnlyDestinationCellsAndUndoRestoresTheirValues()
        {
            var start = Lift(out _);
            var moved = Move(start, 1);
            var stamp = VoxCellChange.Write(moved);
            VoxCellChange.Apply(start.Model, stamp, true);
            Assert.AreEqual(1, stamp.Count);
            Assert.AreEqual(1, start.Model.Voxels[Vector3Int.right]);
            Assert.IsEmpty(VoxCellChange.Write(moved));
            var next = Move(moved, 1);
            VoxCellChange.Apply(start.Model, VoxCellChange.Write(next), true);
            Assert.AreEqual(1, start.Model.Voxels[Vector3Int.right * 2]);
            VoxCellChange.Apply(start.Model, stamp, false);
            Assert.AreEqual(2, start.Model.Voxels[Vector3Int.right]);
            Assert.AreEqual(1, start.Model.Voxels[Vector3Int.right * 2]);
            Assert.AreSame(start.Payload, next.Payload);
        }

        [Test]
        public void SaveIncludesPendingDragPoseWithoutChangingLiveCellsOrFloatingState()
        {
            var start = Lift(out var document);
            var pending = Move(start, -4);
            document.FloatingSelection = new VoxFloatingSaveState(start, pending.Pose);
            var definition = start.Model.Definition;
            var saved = RuntimeVoxDocument.FromBytes(document.ToVoxBytes());
            Assert.AreSame(definition, start.Model.Definition);
            Assert.AreEqual(Vector3Int.zero, start.Model.SourceCellOffset);
            Assert.AreEqual(1, start.Model.Voxels.Count);
            Assert.AreEqual(2, start.Model.Voxels[Vector3Int.right]);
            Assert.AreSame(start, document.FloatingSelection.Value.Region);
            Assert.AreEqual(2, saved.Models[0].Voxels.Count);
            Vector3 expected = VoxMeshBuilder.GetModelTransform(start.AnchorOffset, start.Model.GlobalRotation) *
                (pending.Pose * Vector3.zero);
            var copy = saved.Models[0];
            var savedCell = copy.Voxels.Single(cell => cell.Value == 1).Key;
            Assert.Less((VoxMeshBuilder.GetModelTransform(copy) * (Vector3)savedCell - expected).sqrMagnitude, .000001f);
        }

        [Test]
        public void StampUndoUsesStableCoordinatesAfterBoundsAreNormalized()
        {
            var start = Lift(out var document);
            var moved = Move(start, -4);
            var changes = VoxCellChange.Write(moved);
            VoxCellChange.Apply(start.Model, changes, true);
            document.NormalizeDrawingBounds();
            VoxCellChange.Apply(start.Model, changes, false);
            Assert.AreEqual(1, start.Model.Voxels.Count);
            Assert.AreEqual(2, start.Model.Voxels[Vector3Int.right + start.Model.SourceCellOffset]);
            VoxCellChange.Apply(start.Model, changes, true);
            Assert.AreEqual(1, start.Model.Voxels[Vector3Int.left * 4 + start.Model.SourceCellOffset]);
        }

        [Test]
        public void RotationKeepsCellsOnTheLatticeAndRejectsOversizedComposition()
        {
            var start = Lift(out _);
            Assert.IsFalse(start.CanTransformFrom(start, Vector3Int.zero, Quaternion.identity, Vector3Int.right * 300));
            var rotated = start.TransformFrom(start, Vector3Int.zero, Quaternion.Euler(0, 0, 90), Vector3Int.left);
            Assert.AreEqual(Vector3Int.left, rotated.CellsAt(rotated.Pose).Single().Key);
            Assert.AreEqual(2, start.Model.Voxels[Vector3Int.right]);
        }

        [Test]
        public void BoundaryEditsInvalidateBothChunksButLeaveDistantChunksAlone()
        {
            var grid = new VoxChunkGrid();
            grid.Set(new Vector3Int(-1, 0, 0), 1);
            grid.Set(Vector3Int.zero, 1);
            grid.Set(new Vector3Int(64, 0, 0), 1);
            long neighbor = grid.Chunks[new Vector3Int(-1, 0, 0)].Version;
            long distant = grid.Chunks[new Vector3Int(4, 0, 0)].Version;
            grid.Set(Vector3Int.zero, 2);
            Assert.Greater(grid.Chunks[new Vector3Int(-1, 0, 0)].Version, neighbor);
            Assert.AreEqual(distant, grid.Chunks[new Vector3Int(4, 0, 0)].Version);
            Assert.AreEqual(1, grid.Region(new Vector3Int(-1, 0, 0), Vector3Int.zero).Count(cell => cell.Value == 2));
            grid.Set(new Vector3Int(-1, 0, 0), 0);
            Assert.AreEqual(Vector3Int.zero, grid.Min);
        }

        [Test]
        public void ChunkMeshesCullSharedFacesAndExposeThemAfterErasingANeighbor()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("seam", Vector3Int.one * 32);
            model.AddOrUpdateVoxel(new Vector3Int(15, 0, 0), 1);
            model.AddOrUpdateVoxel(new Vector3Int(16, 0, 0), 1);
            var builder = new VoxMeshBuilder();
            Mesh left = builder.GenerateChunkMesh(model, Vector3Int.zero, document.Palette, null, 1, null);
            Mesh right = builder.GenerateChunkMesh(model, Vector3Int.right, document.Palette, null, 1, null);
            Mesh exposed = null;
            try
            {
                Assert.AreEqual(20, left.vertexCount);
                Assert.AreEqual(20, right.vertexCount);
                model.RemoveVoxel(new Vector3Int(16, 0, 0));
                exposed = builder.GenerateChunkMesh(model, Vector3Int.zero, document.Palette, null, 1, null);
                Assert.AreEqual(24, exposed.vertexCount);
            }
            finally
            {
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
                Object.DestroyImmediate(exposed);
            }
        }
    }
}