// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestVoxRaycast
    {
        private static RuntimeVoxDocument.RuntimeModel CreateModel(params Vector3Int[] cells)
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("ray", new Vector3Int(3, 3, 3));
            foreach (var cell in cells) model.AddOrUpdateVoxel(cell, 1);
            return model;
        }

        [Test]
        public void RaycastFindsAllSixFacesAndHonorsInclusiveDistanceLimit()
        {
            var model = CreateModel(Vector3Int.one);
            var faces = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.up,
                Vector3Int.down, Vector3Int.forward, Vector3Int.back };
            foreach (var face in faces)
            {
                var ray = new Ray(Vector3.one + (Vector3)face * 2, -(Vector3)face * 10);
                Assert.IsFalse(VoxToolTarget.TryRaycast(model, TrTransform.identity, ray, 1.49f, out _));
                Assert.IsTrue(VoxToolTarget.TryRaycast(model, TrTransform.identity, ray, 1.5f, out var hit));
                Assert.AreEqual(Vector3Int.one, hit.Cell);
                Assert.AreEqual(face, hit.Face);
                Assert.AreEqual(1.5f, hit.Distance, 0.00001f);
                Assert.Less(Vector3.Distance(Vector3.one + (Vector3)face * 0.5f, hit.Point_GS), 0.00001f);
                Assert.AreEqual((Vector3)face, hit.Normal_GS);
            }
            Assert.AreEqual(1, model.Voxels.Count);
        }

        [Test]
        public void RaycastInsideOccupiedCellsFindsExposedExitInsteadOfInternalFace()
        {
            var model = CreateModel(new Vector3Int(0, 1, 1), Vector3Int.one);
            var ray = new Ray(new Vector3(0, 1, 1), Vector3.right);
            Assert.IsFalse(VoxToolTarget.TryRaycast(model, TrTransform.identity, ray, 1.49f, out _));
            Assert.IsTrue(VoxToolTarget.TryRaycast(model, TrTransform.identity, ray, 2, out var hit));
            Assert.AreEqual(Vector3Int.one, hit.Cell);
            Assert.AreEqual(Vector3Int.right, hit.Face);
            Assert.AreEqual(1.5f, hit.Distance, 0.00001f);
        }

        [Test]
        public void RaycastOnSurfaceCanHitAtZeroInEitherDirection()
        {
            var model = CreateModel(Vector3Int.one);
            foreach (var direction in new[] { Vector3.right, Vector3.left })
            {
                var ray = new Ray(new Vector3(1.5f, 1, 1), direction);
                Assert.IsTrue(VoxToolTarget.TryRaycast(model, TrTransform.identity, ray, 0, out var hit));
                Assert.AreEqual(Vector3Int.one, hit.Cell);
                Assert.AreEqual(Vector3Int.right, hit.Face);
                Assert.AreEqual(0, hit.Distance);
            }
        }

        [TestCase(2f)]
        [TestCase(-2f)]
        public void RaycastTransformsPointsAndNormalsForRotatedScaledAndMirroredGrids(float scale)
        {
            var model = CreateModel(Vector3Int.one);
            var rotation = new Quaternion(0, 0.70710677f, 0, 0.70710677f);
            var grid = TrTransform.TRS(new Vector3(3, 4, 5), rotation, scale);
            var ray = new Ray(grid * new Vector3(-1, 1, 1), grid.MultiplyVector(Vector3.right));
            Assert.IsTrue(VoxToolTarget.TryRaycast(model, grid, ray, 4, out var hit));
            Assert.AreEqual(Vector3Int.one, hit.Cell);
            Assert.AreEqual(Vector3Int.left, hit.Face);
            Assert.AreEqual(3, hit.Distance, 0.00001f);
            Assert.Less(Vector3.Distance(grid * new Vector3(0.5f, 1, 1), hit.Point_GS), 0.00001f);
            Vector3 expectedNormal = scale > 0 ? Vector3.forward : Vector3.back;
            Assert.Less(Vector3.Distance(expectedNormal, hit.Normal_GS), 0.00001f);
            Assert.AreEqual(1, hit.Normal_GS.magnitude, 0.00001f);
        }

        [Test]
        public void RaycastEdgeCrossingsAreDeterministicAndParallelMissesStayEmpty()
        {
            var model = CreateModel(new Vector3Int(1, 0, 1));
            var ray = new Ray(new Vector3(0, 0, 1), new Vector3(1, 1, 0));
            Assert.IsTrue(VoxToolTarget.TryRaycast(model, TrTransform.identity, ray, 2, out var hit));
            Assert.AreEqual(new Vector3Int(1, 0, 1), hit.Cell);
            Assert.AreEqual(Vector3Int.left, hit.Face);
            Assert.AreEqual(Mathf.Sqrt(0.5f), hit.Distance, 0.00001f);
            var cornerModel = CreateModel(Vector3Int.right);
            Assert.IsTrue(VoxToolTarget.TryRaycast(cornerModel, TrTransform.identity,
                new Ray(Vector3.zero, Vector3.one), 2, out var corner));
            Assert.AreEqual(Vector3Int.right, corner.Cell);
            Assert.AreEqual(Vector3Int.left, corner.Face);
            Assert.Less(Vector3.Distance(Vector3.one * 0.5f, corner.Point_GS), 0.00001f);
            Assert.IsFalse(VoxToolTarget.TryRaycast(model, TrTransform.identity,
                new Ray(new Vector3(-1, 4, 1), Vector3.right), 5, out _));
            Assert.IsFalse(VoxToolTarget.TryRaycast(model, TrTransform.identity,
                new Ray(Vector3.zero, Vector3.zero), 5, out _));
        }

        [Test]
        public void RaycastCrossesLongSparseGridsWithoutChangingCells()
        {
            var document = new RuntimeVoxDocument();
            var model = document.CreateModel("sparse", new Vector3Int(256, 3, 3));
            model.AddOrUpdateVoxel(new Vector3Int(255, 1, 1), 1);
            Assert.IsTrue(VoxToolTarget.TryRaycast(model, TrTransform.identity,
                new Ray(new Vector3(-0.5f, 1, 1), Vector3.right), 256, out var hit));
            Assert.AreEqual(new Vector3Int(255, 1, 1), hit.Cell);
            Assert.AreEqual(255, hit.Distance);
            Assert.AreEqual(1, model.Voxels.Count);
        }

        [Test]
        public void LuaRaycastRejectsInvalidInputsBeforeQueryingTheScene()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => VoxApiWrapper.Raycast(Vector3.zero, Vector3.right, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => VoxApiWrapper.Raycast(Vector3.zero, Vector3.right, float.NaN));
            Assert.Throws<ArgumentException>(() => VoxApiWrapper.Raycast(Vector3.zero, Vector3.zero));
            Assert.Throws<ArgumentException>(() => VoxApiWrapper.Raycast(new Vector3(float.NaN, 0, 0), Vector3.right));
        }
    }
}
