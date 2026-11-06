// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Numerics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class ShapesXrStrokeTests
    {
        [Test]
        public void TapeWidthsUseCentimetresWhilePositionsAndHierarchyUseMetres()
        {
            var placement = new NativePlacement(new[]
            {
                (new Vector3(1, 0, 0), Quaternion.Identity, Vector3.One),
                (new Vector3(10, 0, 0), Quaternion.Identity, new Vector3(2, 1, 1))
            });
            var ribbon = ShapesXrStrokeAdapter.ReadRibbon(Properties(), placement, 10);
            AssertPoint(new[] { 119.6f, 0f, 0f }, ribbon.FirstEdge[0]);
            AssertPoint(new[] { 120.4f, 0f, 0f }, ribbon.SecondEdge[0]);
            AssertPoint(new[] { 120f, .6f, 0f }, ribbon.FirstEdge[1]);
            AssertPoint(new[] { 120f, 1.4f, 0f }, ribbon.SecondEdge[1]);
        }

        [Test]
        public void TapeRetainsExplicitFramesAndShearFromRotatedNonuniformHierarchy()
        {
            var placement = new NativePlacement(new[]
            {
                (new Vector3(1, 0, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)Math.PI / 2), new Vector3(2, 1, 1)),
                (new Vector3(5, 0, 0), Quaternion.Identity, new Vector3(3, 1, 1))
            });
            var properties = Properties();
            var ribbon = ShapesXrStrokeAdapter.ReadRibbon(properties, placement, 10);
            AssertPoint(new[] { 80f, -.4f, 0f }, ribbon.FirstEdge[0]);
            AssertPoint(new[] { 80f, .4f, 0f }, ribbon.SecondEdge[0]);
            AssertPoint(new[] { 78.2f, 0f, 0f }, ribbon.FirstEdge[1]);
            AssertPoint(new[] { 75.8f, 0f, 0f }, ribbon.SecondEdge[1]);
            properties["w"][1] = 0;
            ribbon = ShapesXrStrokeAdapter.ReadRibbon(properties, placement, 10);
            AssertPoint(ribbon.FirstEdge[1], ribbon.SecondEdge[1]);
        }

        [Test]
        public void UnsupportedProfileDoesNotSilentlyBecomeTape()
        {
            var properties = Properties();
            properties["a"] = new JValue(new Guid("2411fc78-4742-4586-b8a3-7b5266ba3731").ToByteArray());
            var error = Assert.Throws<NotSupportedException>(() => ShapesXrStrokeAdapter.ReadRibbon(properties, Identity(), 10));
            StringAssert.Contains("Marker", error.Message);
        }

        [Test]
        public void TapeRejectsMismatchedSamplesAndInvalidRotationsOrWidths()
        {
            var properties = Properties();
            ((JArray)properties["r"]).RemoveAt(1);
            Assert.Throws<InvalidDataException>(() => ShapesXrStrokeAdapter.ReadRibbon(properties, Identity(), 10));
            properties = Properties();
            properties["r"][0] = new JArray(0, 0, 0, 0);
            Assert.Throws<InvalidDataException>(() => ShapesXrStrokeAdapter.ReadRibbon(properties, Identity(), 10));
            properties = Properties();
            properties["w"][0] = double.NaN;
            Assert.Throws<InvalidDataException>(() => ShapesXrStrokeAdapter.ReadRibbon(properties, Identity(), 10));
        }

        private static NativePlacement Identity() => new NativePlacement(new[] { (Vector3.Zero, Quaternion.Identity, Vector3.One) });
        private static JObject Properties() => new JObject
        {
            ["a"] = new JValue(new Guid("3a0fc5ad-c934-4306-8063-60f5940cae06").ToByteArray()),
            ["p"] = new JArray(new JArray(0, 0, 0), new JArray(0, .1, 0)),
            ["r"] = new JArray(new JArray(0, 0, 0, 1), new JArray(0, 0, Math.Sqrt(.5), Math.Sqrt(.5))),
            ["w"] = new JArray(2, 4)
        };

        private static void AssertPoint(float[] expected, float[] actual)
        {
            for (int i = 0; i < 3; ++i) { Assert.AreEqual(expected[i], actual[i], 1e-4); }
        }
    }
}
