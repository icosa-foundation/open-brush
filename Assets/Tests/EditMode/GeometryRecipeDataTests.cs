// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class GeometryRecipeDataTests
    {
        [Test]
        public void ReadsNativeJsonTypesAndIntegralFloatWithoutStringCoercion()
        {
            var recipe = GeometryRecipeData.Parse("{\"Version\":1,\"Generator\":{\"Type\":\"cylinder\",\"Parameters\":{\"sides\":4.0,\"height\":2,\"smoothSides\":true,\"topOffset\":[1,2],\"topRotation\":[0,0,0,1]}}}");
            recipe.GetSteps(out var generator, out var operations);
            Assert.AreEqual(4, generator.Parameters["sides"]);
            Assert.IsInstanceOf<float>(generator.Parameters["height"]);
            Assert.AreEqual(true, generator.Parameters["smoothSides"]);
            CollectionAssert.AreEqual(new float[] { 1, 2 }, (float[])generator.Parameters["topOffset"]);
            Assert.IsEmpty(operations);
        }

        [Test]
        public void PreservesFutureDataWhileRefusingEvaluation()
        {
            string json = "{\"Version\":2,\"Future\":{\"a\":[1,\"two\",true]}}";
            var recipe = GeometryRecipeData.Parse(json);
            string saved = JsonConvert.SerializeObject(recipe);
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse(json), JObject.Parse(saved)));
            var restored = JsonConvert.DeserializeObject<GeometryRecipeData>(saved);
            Assert.Throws<NotSupportedException>(() => restored.GetSteps(out _, out _));
        }

        [Test]
        public void PreservesUnknownCurrentParametersWithoutSilentlyApplyingDefaults()
        {
            string json = "{\"Version\":1,\"Generator\":{\"Type\":\"rectangle\",\"Parameters\":{\"widht\":2}}}";
            var recipe = GeometryRecipeData.Parse(json);
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse(json), recipe.ToJson()));
            Assert.Throws<NotSupportedException>(() => recipe.GetSteps(out _, out _));
        }

        [TestCase("{\"sides\":3.5}")]
        [TestCase("{\"sides\":\"4\"}")]
        [TestCase("{\"smoothSides\":1}")]
        [TestCase("{\"height\":null}")]
        [TestCase("{\"height\":1e100}")]
        [TestCase("{\"topRotation\":[0,0,0,0]}")]
        [TestCase("{\"topOffset\":[0,0,0]}")]
        public void RejectsMalformedKnownValues(string parameters)
        {
            var recipe = GeometryRecipeData.Parse($"{{\"Version\":1,\"Generator\":{{\"Type\":\"cylinder\",\"Parameters\":{parameters}}}}}");
            Assert.Throws<InvalidDataException>(() => recipe.GetSteps(out _, out _));
        }

        [Test]
        public void RejectsDuplicateMembersAndNullOperations()
        {
            Assert.Throws<JsonReaderException>(() => GeometryRecipeData.Parse("{\"Version\":1,\"Version\":2}"));
            var recipe = GeometryRecipeData.Parse("{\"Version\":1,\"Generator\":{\"Type\":\"rectangle\"},\"Operations\":null}");
            Assert.Throws<InvalidDataException>(() => recipe.GetSteps(out _, out _));
        }

        [Test]
        public void ShapesXrPanelUsesLogicalCornersAndOriginalBoundsForBothBends()
        {
            var properties = JObject.Parse("{\"obj-pmugs\":[0.4,0.3,0.05],\"obj-pmugcor\":0.08,\"obj-pmugcors\":9,\"obj-pmugcos\":false,\"obj-pmugchr\":0.01,\"obj-pmugchs\":true,\"obj-pmuggb\":false,\"obj-pmbh\":13,\"obj-pmbv\":20}");
            var recipe = ShapesXrGeometryAdapter.Panel(properties);
            recipe.GetSteps(out var generator, out var operations);
            CollectionAssert.AreEqual(new[] { "topLeft", "topRight" }, (string[])generator.Parameters["roundedCorners"]);
            Assert.AreEqual(8, generator.Parameters["cornerSegments"]);
            Assert.AreEqual(1, generator.Parameters["edgeSegments"]);
            Assert.AreEqual(3, operations.Count);
            Assert.AreEqual("faceRemove", operations[0].Type);
            CollectionAssert.AreEqual(new float[] { 0, 0, 1 }, (float[])operations[0].Parameters["direction"]);
            Assert.AreEqual("x", operations[1].Parameters["axis"]);
            Assert.AreEqual("y", operations[2].Parameters["axis"]);
            CollectionAssert.AreEqual((float[])operations[1].Parameters["referenceSize"], (float[])operations[2].Parameters["referenceSize"]);
            string saved = JsonConvert.SerializeObject(recipe);
            Assert.AreEqual(saved, JsonConvert.SerializeObject(JsonConvert.DeserializeObject<GeometryRecipeData>(saved)));
        }

        [Test]
        public void ThinPanelKeepsBothPlanesWhenGenerateBackIsEnabled()
        {
            var properties = JObject.Parse("{\"obj-pmugs\":[1,1,0.0001],\"obj-pmugcor\":0,\"obj-pmugcors\":15,\"obj-pmugcos\":false,\"obj-pmugchr\":0.01,\"obj-pmugchs\":false,\"obj-pmuggb\":true,\"obj-pmbh\":0,\"obj-pmbv\":0}");
            ShapesXrGeometryAdapter.Panel(properties).GetSteps(out var generator, out var operations);
            Assert.AreEqual(0f, generator.Parameters["edgeRadius"]);
            Assert.AreEqual(4, operations.Count);
            foreach (var operation in operations)
            {
                Assert.AreEqual(0f, ((float[])operation.Parameters["direction"])[2]);
                Assert.AreEqual(false, operation.Parameters["invert"]);
            }
            properties["obj-pmuggb"] = false;
            ShapesXrGeometryAdapter.Panel(properties).GetSteps(out _, out operations);
            Assert.AreEqual(1, operations.Count);
            Assert.AreEqual(true, operations[0].Parameters["invert"]);
            CollectionAssert.AreEqual(new float[] { 0, 0, -1 }, (float[])operations[0].Parameters["direction"]);
        }
    }
}
