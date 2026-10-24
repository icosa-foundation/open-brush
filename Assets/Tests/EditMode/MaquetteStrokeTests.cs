// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class MaquetteStrokeTests
    {
        private string m_Root;
        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), $"OpenBrush-StrokeTest-{Guid.NewGuid():N}");
            Directory.CreateDirectory(m_Root);
        }
        [TearDown]
        public void TearDown() { Directory.Delete(m_Root, true); }

        [Test]
        public void CurvedRibbonSamplesBothEdgesWithoutFittingTheirCenterline()
        {
            using (var document = Document("Curved", new JArray(
                Pair(Point(0, 0, 0), Point(0, 2, 0)), Pair(Point(10, 4, 0), Point(10, 8, 2)), Pair(Point(20, 0, 0), Point(20, 2, 0))),
                new JObject { ["position"] = Point(10, 4, 1) }))
            {
                var ribbon = MaquetteStrokeAdapter.Read(document, document.ReadStates(document.SelectScene())[0], 1);
                Assert.AreEqual(11, ribbon.FirstEdge.Length);
                AssertPoint(new[] { 4f, 1.984f, 0f }, ribbon.FirstEdge[2]);
                AssertPoint(new[] { 4f, 4.976f, .992f }, ribbon.SecondEdge[2]);
                AssertPoint(new[] { 20f, 0f, 0f }, ribbon.FirstEdge[10]);
                AssertPoint(new[] { 20f, 2f, 0f }, ribbon.SecondEdge[10]);
            }
        }

        [Test]
        public void StraightRibbonUsesEndpointOrientationAndFirstWidthInItsSavedFrame()
        {
            foreach (int version in new[] { 2, 3, 20 })
            {
                var rotation = version < 4
                    ? new JObject { ["x"] = 0, ["y"] = 0, ["z"] = Math.Sqrt(.5), ["w"] = Math.Sqrt(.5) }
                    : new JObject { ["x"] = -.5, ["y"] = -.5, ["z"] = .5, ["w"] = .5 };
                using (var document = Document("Straight", new JArray(
                    Pair(Point(-1, 0, 0), Point(1, 0, 0)), Pair(Point(100, 10, 0), Point(200, 20, 0)), Pair(Point(4, -2, 0), Point(4, 2, 0))),
                    new JObject { ["position"] = Point(2, 0, 0), ["rotation"] = rotation }, version: version))
                {
                    var ribbon = MaquetteStrokeAdapter.Read(document, document.ReadStates(document.SelectScene())[0], 1);
                    Assert.AreEqual(2, ribbon.FirstEdge.Length);
                    AssertPoint(new[] { 3f, -2f, 0f }, ribbon.FirstEdge[0]);
                    AssertPoint(new[] { 1f, -2f, 0f }, ribbon.SecondEdge[0]);
                    AssertPoint(new[] { 4f, 2f, 0f }, ribbon.FirstEdge[1]);
                    AssertPoint(new[] { 0f, 2f, 0f }, ribbon.SecondEdge[1]);
                }
            }
        }

        [Test]
        public void RibbonEdgesRetainShearFromNestedNonuniformTransformsAndUnitConversion()
        {
            var transform = new JObject { ["position"] = Point(1, 0, 0), ["scale"] = Point(2, 1, 1),
                ["rotation"] = new JObject { ["x"] = 0, ["y"] = 0, ["z"] = Math.Sqrt(.5), ["w"] = Math.Sqrt(.5) } };
            var parent = new JObject { ["position"] = Point(5, 0, 0), ["scale"] = Point(3, 1, 1) };
            using (var document = Document("Curved", new JArray(Pair(Point(-1, 0, 0), Point(1, 0, 0)), Pair(Point(-1, 2, 0), Point(1, 2, 0))), transform, parent))
            {
                var state = document.ReadStates(document.SelectScene())[1];
                var ribbon = MaquetteStrokeAdapter.Read(document, state, 10);
                AssertPoint(new[] { 110f, -20f, 0f }, ribbon.FirstEdge[0]);
                AssertPoint(new[] { 110f, 20f, 0f }, ribbon.SecondEdge[0]);
            }
        }

        private MaquetteDocument Document(string mode, JArray pairs, JObject transform, JObject parent = null, int version = 20)
        {
            Write("manifest.json.mq", new JObject { ["guid"] = "own-stroke-fixture" });
            Write("brushes.json", new JObject { ["version"] = version, ["strokes"] = new JArray(new JObject
                { ["guid"] = "ribbon", ["drawingMode"] = mode, ["controlPoints"] = pairs }) });
            JObject state = new JObject { ["entityStateGuid"] = "stroke-state", ["targetEntityGuid"] = "ribbon", ["cachedTransform"] = transform };
            if (parent != null) { state = new JObject { ["entityStateGuid"] = "group-state", ["targetEntityGuid"] = "group", ["cachedTransform"] = parent, ["children"] = new JArray(state) }; }
            Write("scenes.json", new JObject { ["version"] = version, ["slides"] = new JArray(new JObject
                { ["sceneGuid"] = "scene", ["treeRoots"] = new JArray(state) }) });
            return new MaquetteDocument(m_Root);
        }

        private void Write(string name, JObject data) => File.WriteAllText(Path.Combine(m_Root, name), data.ToString());
        private static JObject Point(float x, float y, float z) => new JObject { ["x"] = x, ["y"] = y, ["z"] = z };
        private static JObject Pair(JObject a, JObject b) => new JObject { ["point1"] = a, ["point2"] = b };
        private static void AssertPoint(float[] expected, float[] actual)
        {
            for (int i = 0; i < 3; ++i) { Assert.AreEqual(expected[i], actual[i], 1e-4); }
        }
    }
}
