// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class ShapesXrAssetTests
    {
        private static readonly Guid kId = Guid.Parse("01010101-0101-0101-0101-010101010101");
        private static JObject Manifest() => JObject.Parse(@"{'succeeded':true,'data':{
            'id':'01010101-0101-0101-0101-010101010101','format':'png','name':'Example',
            'files':[[{'filename':'image.png','url':'https://example.invalid/image','size':12}],
                     [{'filename':'preview.png','url':'https://example.invalid/preview','size':2}]]}}");

        [Test]
        public void SelectsOriginalImagesAndConvertedRenderableModels()
        {
            var manifest = Manifest();
            var image = new ShapesXrResourceData(manifest, kId);
            Assert.AreEqual(ShapesXrResourceKind.Image, image.Kind);
            Assert.AreEqual("image.png", image.MainFile.Name);
            manifest["data"]["format"] = "zip";
            var files = (JArray)manifest["data"]["files"];
            files.Add(JValue.CreateNull());
            files.Add(JArray.Parse(@"[{'filename':'model.glb','url':'https://example.invalid/model','size':100}]"));
            var model = new ShapesXrResourceData(manifest, kId);
            Assert.AreEqual(ShapesXrResourceKind.Model, model.Kind);
            Assert.AreEqual("model.glb", model.MainFile.Name);
        }

        [Test]
        public void RejectsMismatchedAndUnsafeResourceManifests()
        {
            var manifest = Manifest();
            Assert.Throws<InvalidDataException>(() => new ShapesXrResourceData(manifest, Guid.Empty));
            manifest["data"]["files"][0][0]["filename"] = "../escape.png";
            Assert.Throws<InvalidDataException>(() => new ShapesXrResourceData(manifest, kId));
            manifest = Manifest();
            ((JArray)manifest["data"]["files"][0]).Add(manifest["data"]["files"][0][0].DeepClone());
            Assert.Throws<InvalidDataException>(() => new ShapesXrResourceData(manifest, kId));
        }

        [Test]
        public void KeepsBestLodPerGroupAndPreservesOrdinaryNodes()
        {
            var gltf = JObject.Parse(@"{'asset':{'version':'2.0'},'scene':0,'scenes':[{'nodes':[0,4]}],
                'nodes':[{'name':'__LOD_GROUP__sample','children':[1,2,3,5]},
                {'name':'__LOD_10__mesh','mesh':0},{'name':'__LOD_2__mesh','mesh':0},
                {'name':'__LOD_2__secondMaterial','mesh':0},{'name':'ordinary','mesh':0},
                {'name':'attachment','mesh':0}]}");
            var hidden = ShapesXrModelAdapter.HiddenLodNodes(gltf);
            CollectionAssert.AreEquivalent(new[] { 1 }, hidden);
            string root = Path.Combine(Path.GetTempPath(), $"OpenBrush-ShapesXR-LOD-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            try
            {
                string source = Path.Combine(root, "source.gltf"), variant = Path.Combine(root, "variant.gltf");
                File.WriteAllText(source, gltf.ToString());
                new GltfAssetDocument(source).WriteVariant(variant, Array.Empty<GeometryStepData>(), null, excludedNodes: hidden);
                var output = JObject.Parse(File.ReadAllText(variant));
                CollectionAssert.AreEqual(new[] { 2, 3, 5 }, ((JArray)output["nodes"][0]["children"]).Select(value => (int)value));
                CollectionAssert.AreEqual(new[] { 0, 4 }, ((JArray)output["scenes"][0]["nodes"]).Select(value => (int)value));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void ResolvesLocalSidecarsWithinTheirResourceRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), $"OpenBrush-LocalResource-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(root, "meshes"));
            try
            {
                File.WriteAllText(Path.Combine(root, "meshes", "model.gltf"), "{}");
                File.WriteAllText(Path.Combine(root, "buffer.bin"), "data");
                var resource = ImportResource.FromFile(Path.Combine(root, "meshes", "model.gltf"), root);
                Assert.AreEqual("data", resource.ResolveRelative("../buffer.bin").ReadText());
                Assert.Throws<InvalidDataException>(() => resource.ResolveRelative("../../outside.bin"));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
