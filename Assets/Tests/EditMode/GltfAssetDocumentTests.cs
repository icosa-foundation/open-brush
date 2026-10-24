// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class GltfAssetDocumentTests
    {
        private string m_Root;
        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), $"OpenBrush-GltfImport-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(m_Root, "project/models"));
        }
        [TearDown]
        public void TearDown() { Directory.Delete(m_Root, true); }

        private static JObject Model() => JObject.Parse(@"{'asset':{'version':'2.0'},'scene':0,
            'scenes':[{'nodes':[0]}],'nodes':[{'mesh':0,'extras':{'name':'2026-10-24'}}],
            'buffers':[{'byteLength':12}],'bufferViews':[{'buffer':0,'byteOffset':0,'byteLength':12}],
            'accessors':[{'bufferView':0,'componentType':5126,'type':'VEC3','count':1}],
            'meshes':[{'primitives':[{'mode':0,'attributes':{'POSITION':0},'material':0}]}],
            'materials':[{'extensions':{'KHR_materials_transmission':{'transmissionFactor':0.5}}}],
            'extensionsUsed':['KHR_materials_transmission'],'extras':{'future':42}}");

        [Test]
        public void VariantsShareExactBuffersAndTexturesAndRetainSourceMetadata()
        {
            string project = Path.Combine(m_Root, "project");
            Directory.CreateDirectory(Path.Combine(project, "textures"));
            byte[] mesh = new byte[12], image = new byte[] { 4, 3, 2, 1 };
            File.WriteAllBytes(Path.Combine(project, "textures/mesh.bin"), mesh);
            File.WriteAllBytes(Path.Combine(project, "textures/color space.png"), image);
            var data = Model();
            data["buffers"][0]["uri"] = "../textures/mesh.bin";
            data["images"] = new JArray(new JObject { ["uri"] = "../textures/color%20space.png" });
            string input = Path.Combine(project, "models/model.gltf");
            File.WriteAllText(input, data.ToString());
            using (var source = new NativeProjectSource(project))
            {
                var resource = new MaquetteAssetResolver(source).Resolve("models/model.gltf");
                var cached = GltfAssetDocument.CopySource(resource, input, Path.Combine(m_Root, "cache/sources"));
                GeometryRecipeData.Parse(@"{'Version':1,'Generator':{'Type':'rectangle','Parameters':{}},
                    'Operations':[{'Type':'transform','Parameters':{'scale':[2,3,4],'rotation':[0.5,0.5,0.5,0.5]}}]}")
                    .GetSteps(out _, out var transforms);
                string a = Path.Combine(m_Root, "cache/variants/a.gltf"), b = Path.Combine(m_Root, "cache/variants/b.gltf");
                cached.WriteVariant(a, transforms, new float[] { -1, -2, -3 });
                cached.WriteVariant(b, transforms, null, new float[] { 1, .5f, .25f, .5f });
                var first = JObject.Parse(File.ReadAllText(a));
                var second = JObject.Parse(File.ReadAllText(b));
                Assert.AreEqual((string)first["buffers"][0]["uri"], (string)second["buffers"][0]["uri"]);
                Assert.AreEqual(mesh, File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(a), (string)first["buffers"][0]["uri"])));
                Assert.AreEqual(image, File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(a), (string)first["images"][0]["uri"])));
                Assert.AreEqual(.5, (double)first["materials"][0]["extensions"]["KHR_materials_transmission"]["transmissionFactor"]);
                Assert.AreEqual(42, (int)first["extras"]["future"]);
                Assert.AreEqual("2026-10-24", (string)first["nodes"][0]["extras"]["name"]);
                Assert.AreEqual(new[] { 1f, -2f, -3f }, first["nodes"][1]["translation"].ToObject<float[]>());
                Assert.AreEqual(new[] { .5f, -.5f, -.5f, .5f }, first["nodes"][2]["rotation"].ToObject<float[]>());
                Assert.AreEqual(1, (int)second["meshes"][0]["primitives"][0]["material"]);
                Assert.AreEqual("BLEND", (string)second["materials"][1]["alphaMode"]);
                Assert.AreEqual(2, Directory.GetFiles(Path.GetDirectoryName(a)).Length);
                Assert.AreEqual(data.ToString(), File.ReadAllText(input));
            }
        }

        [Test]
        public void GlbBinaryPayloadIsCopiedWithoutRepackingAndInvalidHeadersAreRejected()
        {
            string project = Path.Combine(m_Root, "project");
            string input = Path.Combine(project, "model.glb");
            string json = Model().ToString(Newtonsoft.Json.Formatting.None);
            byte[] text = Encoding.UTF8.GetBytes(json.PadRight((Encoding.UTF8.GetByteCount(json) + 3) / 4 * 4));
            byte[] binary = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            using (var file = File.Create(input))
            using (var writer = new BinaryWriter(file))
            {
                writer.Write(0x46546C67u); writer.Write(2u); writer.Write((uint)(28 + text.Length + binary.Length));
                writer.Write((uint)text.Length); writer.Write(0x4E4F534Au); writer.Write(text);
                writer.Write((uint)binary.Length); writer.Write(0x004E4942u); writer.Write(binary);
            }
            using (var source = new NativeProjectSource(project))
            {
                var resource = new MaquetteAssetResolver(source).Resolve("model.glb");
                var result = GltfAssetDocument.CopySource(resource, input, Path.Combine(m_Root, "cache"));
                Assert.AreEqual(binary, File.ReadAllBytes(Path.Combine(m_Root, "cache/source.bin")));
                Assert.AreEqual("source.bin", (string)JObject.Parse(File.ReadAllText(result.Path))["buffers"][0]["uri"]);
                File.WriteAllBytes(input, new byte[12]);
                Assert.Throws<InvalidDataException>(() => GltfAssetDocument.CopySource(resource, input, Path.Combine(m_Root, "invalid")));
            }
        }
    }
}
