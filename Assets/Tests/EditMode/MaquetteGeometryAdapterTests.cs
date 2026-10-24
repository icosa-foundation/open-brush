// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class MaquetteGeometryAdapterTests
    {
        private static JObject Entity(string type, params string[] values)
        {
            var parameters = new JArray();
            for (int i = 0; i < values.Length; i += 2)
            {
                parameters.Add(new JObject { ["Key"] = values[i], ["Value"] = values[i + 1] });
            }
            return new JObject { ["prefabPath"] = $"Procedural/MqProceduralMesh_{type}", ["parameters"] = parameters };
        }

        [Test]
        public void CylinderMapsIndependentEllipsesAndEulerRotation()
        {
            var entity = Entity("CylinderCone", "radius_bottom", "0.6", "radius_top", "0.4",
                "bottom_scale", "{\"x\":2,\"y\":3}", "top_scale", "{\"x\":1,\"y\":2}",
                "top_rotation", "{\"x\":0,\"y\":0,\"z\":90}", "invert", "true");
            MaquetteGeometryAdapter.Convert(entity, new float[] { 1, 1, 1 }).GetSteps(out var generator, out var operations);
            Assert.AreEqual("cylinder", generator.Type);
            Assert.AreEqual(1.2f, generator.Parameters["bottomRadius"]);
            Assert.AreEqual(1.5f, generator.Parameters["bottomAspect"]);
            Assert.AreEqual(2f, generator.Parameters["topAspect"]);
            var rotation = (float[])generator.Parameters["topRotation"];
            Assert.AreEqual(Math.Sqrt(.5), rotation[2], 1e-6);
            Assert.AreEqual(Math.Sqrt(.5), rotation[3], 1e-6);
            Assert.AreEqual("flip", operations[0].Type);
        }

        [Test]
        public void RectangleKeepsRadiusInWorkingDimensionsAndCompensatesScaleOnce()
        {
            var entity = Entity("RoundedCornerQuad", "width", "2", "height", "3", "thickness", "0.2", "radius", "0.1");
            MaquetteGeometryAdapter.Convert(entity, new float[] { 2, 3, 4 }).GetSteps(out var generator, out var operations);
            Assert.AreEqual(4f, generator.Parameters["width"]);
            Assert.AreEqual(9f, generator.Parameters["height"]);
            Assert.AreEqual(.1f, generator.Parameters["radius"]);
            Assert.AreEqual(.8f, operations[0].Parameters["thickness"]);
            CollectionAssert.AreEqual(new float[] { .5f, 1f / 3, .25f }, (float[])operations[2].Parameters["scale"]);
        }

        [Test]
        public void SquareFrameCanCollapseToFilledOutlineWithoutInventingAHole()
        {
            var entity = Entity("RoundedCornerRim", "border_size", "1", "radius", "0", "scale_border", "true");
            MaquetteGeometryAdapter.Convert(entity, new float[] { 1, 1, 1 }).GetSteps(out var generator, out _);
            Assert.AreEqual("frame", generator.Type);
            Assert.AreEqual(.15f, generator.Parameters["borderWidth"]);
            Assert.AreEqual(0f, generator.Parameters["radius"]);
        }

        [Test]
        public void LegacyFrameUsesOutwardBorderAndTheOldScaleBorderMeaning()
        {
            var entity = Entity("RoundedCornerRim", "border_size", "0.02", "radius", "0", "scale_border", "true");
            MaquetteGeometryAdapter.Convert(entity, new float[] { 2, 3, 1 }, 20).GetSteps(out var legacy, out _);
            Assert.AreEqual(1.04f, (float)legacy.Parameters["width"], 1e-6);
            Assert.AreEqual(.94f, (float)legacy.Parameters["height"], 1e-6);
            Assert.AreEqual(.02f, legacy.Parameters["borderWidth"]);
            MaquetteGeometryAdapter.Convert(entity, new float[] { 2, 3, 1 }, 21).GetSteps(out var current, out _);
            Assert.AreEqual(1f, current.Parameters["width"]);
            Assert.AreEqual(.06f, current.Parameters["borderWidth"]);
            ((JArray)entity["parameters"])[2]["Value"] = "false";
            MaquetteGeometryAdapter.Convert(entity, new float[] { 2, 3, 1 }, 20).GetSteps(out legacy, out _);
            Assert.AreEqual(1.12f, (float)legacy.Parameters["width"], 1e-6);
            Assert.AreEqual(.06f, legacy.Parameters["borderWidth"]);
        }

        [Test]
        public void CapsuleRoundProfileControlsScaleCompensationAndZeroSweepUsesUpperCap()
        {
            var entity = Entity("Capsule", "round_profile", "true", "sweep_angle", "0");
            MaquetteGeometryAdapter.Convert(entity, new float[] { 2, 3, 4 }).GetSteps(out var generator, out var operations);
            Assert.AreEqual("conic", generator.Type);
            Assert.AreEqual(.5f, generator.Parameters["bottomClip"]);
            Assert.AreEqual("doubleSidedProfile", generator.Parameters["zeroSweep"]);
            Assert.AreEqual(1f, generator.Parameters["radius"]);
            CollectionAssert.AreEqual(new float[] { 0, .5f, 0 }, (float[])operations[0].Parameters["translation"]);
            CollectionAssert.AreEqual(new float[] { .5f, 1f / 3, .25f }, (float[])operations[1].Parameters["scale"]);
        }
    }
}
