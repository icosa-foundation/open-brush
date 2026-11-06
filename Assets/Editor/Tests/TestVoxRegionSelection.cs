// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestVoxRegionSelection
    {
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
    }
}