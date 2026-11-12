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
            new VoxRegionLift(document, region).Redo();
            return region;
        }

        private static VoxFloatingRegion Move(VoxFloatingRegion region, int distance) =>
            region.TransformFrom(region, Vector3Int.zero, Quaternion.identity, Vector3Int.right * distance);

        [TestCase(false)]
        [TestCase(true)]
        public void ExportGeometryIncludesMovedCellsAndRestoresLiveHierarchy(bool failExport)
        {
            var start = Lift(out var document);
            var moved = Move(start, 4);
            document.FloatingSelection = new VoxFloatingSaveState(moved, moved.Pose);
            var definition = start.Model.Definition;
            var parent = new GameObject("export widget");
            var live = new GameObject("live model");
            var material = new Material(Shader.Find("Standard"));
            Mesh temporaryMesh = null;
            GameObject temporaryRoot = null;
            try
            {
                parent.transform.SetPositionAndRotation(new Vector3(3, 7, 2), Quaternion.Euler(12, 34, 56));
                parent.transform.localScale = Vector3.one * 2;
                live.transform.SetParent(parent.transform, false);
                live.transform.localPosition = Vector3.one;
                live.transform.localRotation = Quaternion.Euler(0, 20, 0);
                live.transform.localScale = Vector3.one * 3;
                live.layer = 5;
                Matrix4x4 liveMatrix = live.transform.localToWorldMatrix;
                try
                {
                    using (new ModelWidget.VoxExportGeometry(live.transform, document, material))
                    {
                        Assert.IsFalse(live.activeSelf);
                        Assert.AreEqual("EditorOnly", live.tag);
                        temporaryRoot = parent.transform.GetChild(1).gameObject;
                        Assert.AreEqual(liveMatrix, temporaryRoot.transform.localToWorldMatrix);
                        var filter = temporaryRoot.GetComponentInChildren<MeshFilter>();
                        temporaryMesh = filter.sharedMesh;
                        Assert.AreEqual(5, filter.gameObject.layer);
                        Assert.AreEqual(72, temporaryMesh.triangles.Length, "Both separated voxels must be exported");
                        var rotation = VoxMeshBuilder.ModelRotation;
                        var worldBounds = new Bounds(filter.transform.TransformPoint(temporaryMesh.vertices[0]), Vector3.zero);
                        foreach (var vertex in temporaryMesh.vertices)
                            worldBounds.Encapsulate(filter.transform.TransformPoint(vertex));
                        Assert.IsTrue(worldBounds.Contains(liveMatrix.MultiplyPoint3x4(rotation * Vector3.right)));
                        Assert.IsTrue(worldBounds.Contains(liveMatrix.MultiplyPoint3x4(rotation * (Vector3.right * 4))));
                        if (failExport) throw new System.InvalidOperationException("Simulated export failure");
                    }
                }
                catch (System.InvalidOperationException) when (failExport) { }
                Assert.IsTrue(live.activeSelf);
                Assert.AreEqual("Untagged", live.tag);
                Assert.AreEqual(liveMatrix, live.transform.localToWorldMatrix);
                Assert.AreEqual(1, parent.transform.childCount);
                Assert.IsTrue(temporaryRoot == null);
                Assert.IsTrue(temporaryMesh == null);
                Assert.AreSame(moved, document.FloatingSelection.Value.Region);
                Assert.AreSame(definition, start.Model.Definition);
                Assert.IsFalse(start.Model.Voxels.ContainsKey(Vector3Int.right * 4));
                Assert.AreEqual(2, start.Model.Voxels[Vector3Int.right]);
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(material);
            }
        }

        private static VoxFloatingRegion SharedRegion(out RuntimeVoxDocument document,
            out RuntimeVoxDocument.RuntimeModel other)
        {
            document = new RuntimeVoxDocument();
            var model = document.CreateModel("shared", Vector3Int.one * 32);
            model.SourceCellOffset = new Vector3Int(3, 2, 1);
            model.TransformOffset = new Vector3(10, 20, 30);
            model.AddOrUpdateVoxel(model.SourceCellOffset, 1);
            model.AddOrUpdateVoxel(model.SourceCellOffset + Vector3Int.right, 2);
            other = document.CreateModel("other placement", model.Size);
            other.Definition = model.Definition;
            return new VoxFloatingRegion(model, model.SourceCellOffset, model.SourceCellOffset).Lift();
        }

        [Test]
        public void SharedLiftUndoRestoresLinkAndRedoReusesDetachedDefinition()
        {
            var region = SharedRegion(out var document, out var other);
            var model = region.Model;
            var original = model.Definition;
            var cell = model.SourceCellOffset;
            var lift = new VoxRegionLift(document, region);
            Assert.AreSame(original, model.Definition, "Constructing a lift must not detach the placement");
            lift.Redo();
            var detached = model.Definition;
            Assert.AreNotSame(original, detached);
            Assert.IsFalse(model.Voxels.ContainsKey(cell));
            Assert.AreEqual(1, other.Voxels[cell]);
            lift.Undo();
            Assert.AreSame(other.Definition, model.Definition);
            other.SetVoxelColor(cell, 7);
            Assert.AreEqual(7, model.Voxels[cell], "Edits must propagate again after undo");
            other.SetVoxelColor(cell, 1);
            lift.Redo();
            Assert.AreSame(detached, model.Definition, "Redo must reuse the existing detached grid");
            Assert.IsFalse(model.Voxels.ContainsKey(cell));
            Assert.AreSame(original, other.Definition);
            lift.Undo();
            Assert.AreSame(original, model.Definition);
        }

        [Test]
        public void RejectedSharedGrabRestoresDefinitionAndPlacementWithoutAnUndoEntry()
        {
            var region = SharedRegion(out var document, out var other);
            var model = region.Model;
            var offset = model.TransformOffset;
            var cellOffset = model.SourceCellOffset;
            var lift = new VoxRegionLift(document, region);
            lift.Redo();
            Assert.IsFalse(region.CanTransformFrom(region, Vector3Int.zero, Quaternion.identity, Vector3Int.right * 300));
            lift.Undo(); // The same rollback used when the unrecorded grab is cancelled.
            Assert.AreSame(other.Definition, model.Definition);
            Assert.AreEqual(offset, model.TransformOffset);
            Assert.AreEqual(cellOffset, model.SourceCellOffset);
            Assert.AreEqual(1, model.Voxels[cellOffset]);
        }

        [Test]
        public void SharedLiftRestoresBothCoordinateFramesAcrossRebasingAndRedo()
        {
            var region = SharedRegion(out var document, out var other);
            var model = region.Model;
            var original = model.Definition;
            var offset = model.TransformOffset;
            var cellOffset = model.SourceCellOffset;
            var lift = new VoxRegionLift(document, region);
            lift.Redo();
            var moved = Move(region, -8);
            var stamp = VoxCellChange.Write(moved);
            VoxCellChange.Apply(model, stamp, true);
            Assert.IsTrue(document.NormalizeDrawingBounds());
            var rebased = model.Definition;
            var rebasedOffset = model.TransformOffset;
            var rebasedCellOffset = model.SourceCellOffset;
            VoxCellChange.Apply(model, stamp, false);
            lift.Undo();
            Assert.AreSame(original, model.Definition);
            Assert.AreEqual(offset, model.TransformOffset);
            Assert.AreEqual(cellOffset, model.SourceCellOffset);
            lift.Redo();
            Assert.AreSame(rebased, model.Definition);
            Assert.AreEqual(rebasedOffset, model.TransformOffset);
            Assert.AreEqual(rebasedCellOffset, model.SourceCellOffset);
            VoxCellChange.Apply(model, stamp, true);
            Assert.AreEqual(1, model.Voxels[Vector3Int.left * 8 + model.SourceCellOffset]);
            Assert.IsFalse(document.NormalizeDrawingBounds(), "Redo must retain the already fitted bounds");
            Assert.AreSame(original, other.Definition);
            Assert.AreEqual(2, other.Voxels.Count);
        }

        [Test]
        public void UnsharedLiftKeepsItsDefinitionAcrossUndoAndRedo()
        {
            var region = SharedRegion(out var document, out var other);
            document.RemoveModel(other);
            var model = region.Model;
            var original = model.Definition;
            var lift = new VoxRegionLift(document, region);
            lift.Redo();
            Assert.AreSame(original, model.Definition);
            lift.Undo();
            Assert.AreSame(original, model.Definition);
            Assert.AreEqual(1, model.Voxels[model.SourceCellOffset]);
            lift.Redo();
            Assert.AreSame(original, model.Definition);
            Assert.IsFalse(model.Voxels.ContainsKey(model.SourceCellOffset));
        }

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
        public void PaletteChangesAreSavedWithoutFinalizingTheFloatingRegion()
        {
            var start = Lift(out var document);
            var moved = Move(start, 1);
            document.FloatingSelection = new VoxFloatingSaveState(moved, moved.Pose);
            var definition = start.Model.Definition;
            var color = new Color32(12, 34, 56, 200);
            document.ReplacePaletteEntry(1, color);
            var saved = RuntimeVoxDocument.FromBytes(document.ToVoxBytes());
            byte index = saved.Models[0].Voxels.Single().Value;
            Assert.AreEqual(color, saved.Palette[index - 1]);
            Assert.AreSame(moved, document.FloatingSelection.Value.Region);
            Assert.AreSame(definition, start.Model.Definition);
            Assert.AreEqual(2, start.Model.Voxels[Vector3Int.right]);
            Assert.IsFalse(start.Model.Voxels.ContainsKey(Vector3Int.zero));
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
