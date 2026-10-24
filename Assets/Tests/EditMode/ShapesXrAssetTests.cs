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
        public void ModelOverridesFollowOriginalGroupsWithoutChangingOtherMeshInstances()
        {
            var gltf = JObject.Parse(@"{'asset':{'version':'2.0'},'scene':0,'scenes':[{'nodes':[0,1,2]}],
                'nodes':[{'mesh':0,'extras':{'original_index':7}},{'mesh':0,'extras':{'original_index':2}},
                    {'mesh':0,'extras':{'original_index':9}}],
                'meshes':[{'primitives':[{'attributes':{'POSITION':0},'material':0}]}],
                'materials':[{'alphaMode':'BLEND','pbrMetallicRoughness':{'baseColorFactor':[0.2,0.3,0.4,0.8],
                    'baseColorTexture':{'index':0},'metallicFactor':0.4,'roughnessFactor':0.7},
                    'extensions':{'KHR_materials_unlit':{}}}],'extensionsUsed':['KHR_materials_unlit']}");
            string unchanged = gltf.ToString();
            var properties = new System.Collections.Generic.Dictionary<int, JObject>
            {
                [0] = JObject.Parse(@"{'m-c':[128,64,32,255],'m-o':0.5,'m-met':0.8,'m-rou':0.2,'m-st':0}"),
                [1] = JObject.Parse(@"{'m-c':[0,0,255,255],'m-o':1,'m-st':1,'m-e':[0,0,0,255]}"),
                [9] = new JObject()
            };
            var overrides = ShapesXrModelAdapter.MaterialOverrides(gltf, properties, out var unmapped);
            Assert.AreEqual(2, overrides.Count);
            Assert.AreEqual(1, overrides[0].NodeIndex);
            Assert.AreEqual(0, overrides[1].NodeIndex);
            CollectionAssert.AreEquivalent(new[] { "m-e", "material slot 9 has no mesh group" }, unmapped);
            Assert.IsNull(overrides[0].Material["extensions"]["KHR_materials_unlit"]);
            Assert.IsNotNull(overrides[1].Material["extensions"]["KHR_materials_unlit"]);
            Assert.AreEqual(.8f, (float)overrides[0].Material["pbrMetallicRoughness"]["metallicFactor"], 1e-6);
            var factor = overrides[0].Material["pbrMetallicRoughness"]["baseColorFactor"].ToObject<float[]>();
            Assert.AreEqual(.2158605f, factor[0], 1e-6);
            Assert.AreEqual(.05126946f, factor[1], 1e-6);
            Assert.AreEqual(.01444384f, factor[2], 1e-6);
            string root = Path.Combine(Path.GetTempPath(), $"OpenBrush-MaterialOverride-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            try
            {
                string source = Path.Combine(root, "source.gltf"), variant = Path.Combine(root, "variant.gltf");
                File.WriteAllText(source, unchanged);
                new GltfAssetDocument(source).WriteVariant(variant, Array.Empty<GeometryStepData>(), null,
                    opacity: .5f, materialOverrides: overrides);
                var output = JObject.Parse(File.ReadAllText(variant));
                var nodes = (JArray)output["nodes"];
                Assert.AreEqual(0, (int)nodes[2]["mesh"]);
                Assert.AreNotEqual((int)nodes[0]["mesh"], (int)nodes[1]["mesh"]);
                foreach (var entry in overrides)
                {
                    int materialIndex = (int)output["meshes"][(int)nodes[entry.NodeIndex]["mesh"]]["primitives"][0]["material"];
                    var material = output["materials"][materialIndex];
                    Assert.AreEqual(entry.NodeIndex == 1 ? .25 : .5,
                        (double)material["pbrMetallicRoughness"]["baseColorFactor"][3], 1e-6);
                    Assert.IsTrue(JToken.DeepEquals(gltf["materials"][0]["pbrMetallicRoughness"]["baseColorTexture"],
                        material["pbrMetallicRoughness"]["baseColorTexture"]));
                }
                Assert.AreEqual(unchanged, gltf.ToString());
                Assert.AreEqual(unchanged, File.ReadAllText(source));
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
