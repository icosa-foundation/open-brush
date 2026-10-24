// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class MaquetteDocumentTests
    {
        private string m_Root;
        private string m_Project;

        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), $"OpenBrush-ImportTest-{Guid.NewGuid():N}");
            m_Project = Path.Combine(m_Root, "project");
            Directory.CreateDirectory(m_Project);
            Write("manifest.json.mq", new JObject { ["guid"] = "project", ["version"] = "V020_ImageSupportsMaterial" });
            Write("primitives.json", new JObject { ["version"] = "20", ["primitives"] = new JArray(
                new JObject { ["guid"] = "box", ["primitiveType"] = "ModelFile", ["future"] = 42 }) });
            Write("scenes.json", new JObject
            {
                ["version"] = "20", ["lastSelectedSceneGuid"] = "second",
                ["slides"] = new JArray(Scene("first", 0), Scene("second", 1))
            });
        }

        [TearDown]
        public void TearDown() { Directory.Delete(m_Root, true); }

        private static JObject Scene(string id, int order) => new JObject
        {
            ["sceneGuid"] = id, ["sceneName"] = id, ["sequenceIndex"] = order,
            ["treeRoots"] = new JArray(new JObject
            {
                ["entityStateGuid"] = $"{id}-group", ["targetEntityGuid"] = "implicit-group", ["visible"] = false,
                ["cachedTransform"] = new JObject { ["position"] = new JObject { ["x"] = 10 } },
                ["children"] = new JArray(new JObject
                {
                    ["entityStateGuid"] = $"{id}-box", ["targetEntityGuid"] = "box", ["visible"] = true,
                    ["cachedTransform"] = new JObject { ["scale"] = new JObject { ["x"] = 2, ["y"] = 3, ["z"] = -1 } }
                })
            })
        };

        private void Write(string name, JObject data) => File.WriteAllText(Path.Combine(m_Project, name), data.ToString());

        [Test]
        public void ResolvesSelectedSceneAndPreservesHierarchyAndUnknownValues()
        {
            using (var document = new MaquetteDocument(m_Project))
            {
                Assert.AreEqual(20, document.Version);
                Assert.AreEqual("second", document.SelectScene().Id);
                var states = document.ReadStates(document.SelectScene());
                Assert.AreEqual(2, states.Count);
                Assert.IsNull(states[0].Entity); // Implicit groups need not have a named-group definition.
                Assert.AreSame(states[0], states[1].Parent);
                Assert.IsFalse(states[1].Visible);
                Assert.AreEqual("box", states[1].Entity.Id);
                Assert.AreEqual(42, (int)states[1].Entity.Data["future"]);
                Assert.AreEqual(-1, (int)states[1].Data["cachedTransform"]["scale"]["z"]);
                Assert.Throws<InvalidDataException>(() => document.SelectScene("absent"));
            }
        }

        [Test]
        public void DirectoryAndRootLayoutArchiveReadTheSameData()
        {
            string archive = Path.Combine(m_Root, "project.mqp");
            ZipFile.CreateFromDirectory(m_Project, archive);
            using (var directory = new MaquetteDocument(m_Project))
            using (var zipped = new MaquetteDocument(archive))
            {
                CollectionAssert.AreEquivalent(directory.Source.Files, zipped.Source.Files);
                Assert.IsTrue(JToken.DeepEquals(directory.SelectScene().Data, zipped.SelectScene().Data));
                Assert.AreEqual(2, zipped.ReadStates(zipped.SelectScene()).Count);
            }
        }

        [Test]
        public void RejectsDuplicateEntityIdentities()
        {
            Write("brushes.json", new JObject { ["strokes"] = new JArray(new JObject { ["guid"] = "box" }) });
            Assert.Throws<InvalidDataException>(() => { using (new MaquetteDocument(m_Project)) { } });
        }

        [Test]
        public void BuilderScaleMultipliesComponentsEvenWhenTheParentIsRotated()
        {
            using (var document = new MaquetteDocument(m_Project))
            {
                var states = document.ReadStates(document.SelectScene());
                states[0].Data["cachedTransform"]["scale"] = JObject.Parse("{\"x\":2,\"y\":3,\"z\":4}");
                states[0].Data["cachedTransform"]["rotation"] = JObject.Parse("{\"x\":0,\"y\":0,\"z\":0.70710678,\"w\":0.70710678}");
                CollectionAssert.AreEqual(new float[] { 4, 9, -4 }, states[1].GetBuilderScale());
            }
        }

        [Test]
        public void RejectsDuplicateJsonMembers()
        {
            File.WriteAllText(Path.Combine(m_Project, "manifest.json.mq"), "{\"guid\":\"a\",\"guid\":\"b\"}");
            Assert.Throws<JsonReaderException>(() => { using (new MaquetteDocument(m_Project)) { } });
        }

        [Test]
        public void RejectsResourceTraversal()
        {
            using (var source = new NativeProjectSource(m_Project))
            {
                Assert.Throws<InvalidDataException>(() => source.OpenRead("../outside"));
                Assert.Throws<InvalidDataException>(() => source.OpenRead("C:/outside"));
            }
            string archive = Path.Combine(m_Root, "unsafe.mqp");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) { zip.CreateEntry("../outside"); }
            Assert.Throws<InvalidDataException>(() => { using (new NativeProjectSource(archive)) { } });
        }

        [Test]
        public void RejectsAmbiguousArchiveResourceNames()
        {
            string archive = Path.Combine(m_Root, "ambiguous.mqp");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                zip.CreateEntry("Objects/model.glb");
                zip.CreateEntry("objects/model.glb");
            }
            Assert.Throws<InvalidDataException>(() => { using (new NativeProjectSource(archive)) { } });
        }
    }
}
