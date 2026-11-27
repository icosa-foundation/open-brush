// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using TiltBrush.DocumentImport;
using UnityEngine;

namespace TiltBrush.Tests
{
    public class EditableModelFoundationTests
    {
        private static Type AppType(string name) => Type.GetType($"TiltBrush.{name}, Assembly-CSharp", throwOnError: true);

        [Test]
        public void SharedRecipeBuildsAnEditableMeshAtAuthoredScaleWithoutJitter()
        {
            var manager = AppType("MeshEditing.EditableModelManager");
            var recipe = GeometryRecipeData.Parse("{\"Version\":1,\"Generator\":{\"Type\":\"panel\",\"Parameters\":{\"width\":2,\"height\":3,\"depth\":0.5}}}");
            object poly = manager.GetMethod("Evaluate").Invoke(null, new object[] { recipe });
            var build = manager.GetMethod("BuildMesh", BindingFlags.NonPublic | BindingFlags.Static);
            Mesh first = null, second = null;
            try
            {
                first = (Mesh)build.Invoke(null, new[] { poly });
                second = (Mesh)build.Invoke(null, new[] { poly });
                float units = (float)AppType("App").GetField("METERS_TO_UNITS").GetRawConstantValue();
                Assert.That(first.bounds.size.x, Is.EqualTo(2 * units).Within(.0001f));
                Assert.That(first.bounds.size.y, Is.EqualTo(3 * units).Within(.0001f));
                Assert.That(first.bounds.size.z, Is.EqualTo(.5f * units).Within(.0001f));
                CollectionAssert.AreEqual(first.vertices, second.vertices);
                CollectionAssert.AreEqual(first.triangles, second.triangles);
                Assert.AreEqual(first.vertexCount, first.normals.Length);
            }
            finally
            {
                if (first != null) { UnityEngine.Object.DestroyImmediate(first); }
                if (second != null) { UnityEngine.Object.DestroyImmediate(second); }
            }
        }

        [Test]
        public void UpdatingANewModelRootCreatesAndReusesItsMeshComponents()
        {
            var type = AppType("MeshEditing.EditableModelManager");
            var instanceField = type.GetField("m_Instance");
            var previous = instanceField.GetValue(null);
            var managerRoot = new GameObject("Editable mesh test manager");
            var modelRoot = new GameObject("Editable mesh test root");
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            mesh.RecalculateBounds();
            try
            {
                var manager = managerRoot.AddComponent(type);
                var update = type.GetMethod("UpdateMesh");
                update.Invoke(manager, new object[] { modelRoot, mesh, null });
                update.Invoke(manager, new object[] { modelRoot, mesh, null });
                Assert.AreSame(mesh, modelRoot.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreEqual(1, modelRoot.GetComponents<MeshFilter>().Length);
                Assert.AreEqual(1, modelRoot.GetComponents<MeshRenderer>().Length);
                Assert.AreEqual(1, modelRoot.GetComponents<BoxCollider>().Length);
                Assert.AreEqual(mesh.bounds.size, modelRoot.GetComponent<BoxCollider>().size);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(modelRoot);
                UnityEngine.Object.DestroyImmediate(managerRoot);
                UnityEngine.Object.DestroyImmediate(mesh);
                instanceField.SetValue(null, previous);
            }
        }

        [Test]
        public void EditableInstanceIndexUsesTheExistingSketchJsonConventions()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = (IContractResolver)Activator.CreateInstance(AppType("CustomJsonContractResolver"))
            };
            var type = AppType("TiltEditableModels");
            const string json = "{\"AssetId\":\"frame\",\"RawTransforms\":[[[1,2,3],[0,0,0,1],2]],\"PinStates\":[true],\"GroupIds\":[7],\"LayerIds\":[2],\"Colors\":[[1,0.5,0.25,1]],\"Future\":{\"value\":42}}";
            object index = JsonConvert.DeserializeObject(json, type, settings);
            Assert.AreEqual("frame", type.GetProperty("AssetId").GetValue(index));
            string Save(object value)
            {
                using (var text = new StringWriter())
                using (var writer = (JsonWriter)Activator.CreateInstance(AppType("CustomJsonWriter"), text))
                {
                    JsonSerializer.Create(settings).Serialize(writer, value);
                    writer.Flush();
                    return text.ToString();
                }
            }
            var restored = JsonConvert.DeserializeObject(Save(index), type, settings);
            Assert.AreEqual("frame", type.GetProperty("AssetId").GetValue(restored));
            Assert.AreEqual(1, ((IList)type.GetProperty("RawTransforms").GetValue(restored)).Count);
            Assert.AreEqual(42, (int)JObject.Parse(Save(restored))["Future"]["value"]);
        }

        [Test]
        public void EditableLinksResolveAndRetainedRecordsUseTheCurrentSaveTable()
        {
            var metadataType = AppType("SketchMetadata");
            var recordType = AppType("TiltEditableModels");
            var mirrorType = AppType("SymmetryMirror");
            var contextType = AppType("WidgetLinkSaveContext");
            const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
            var mirrorId = Guid.NewGuid();
            string json = $"{{\"EditableModelIndex\":[{{\"AssetId\":\"future\",\"GroupIds\":[9],\"WidgetLinks\":[{{\"GroupId\":17,\"PointerIndex\":1}}],\"Future\":42}}],\"WidgetLinks\":{{\"Version\":1,\"Groups\":[{{\"Id\":17,\"MirrorId\":\"{mirrorId}\"}}]}}}}";
            var metadata = JsonConvert.DeserializeObject(json, metadataType);
            var table = metadataType.GetProperty("WidgetLinks").GetValue(metadata);
            var records = (IList)metadataType.GetProperty("EditableModelIndex").GetValue(metadata);
            var mirrors = (IDictionary)Activator.CreateInstance(typeof(System.Collections.Generic.Dictionary<,>)
                .MakeGenericType(typeof(Guid), mirrorType));
            mirrors.Add(mirrorId, Activator.CreateInstance(mirrorType, new object[] { mirrorId, null }));
            table.GetType().GetMethod("Resolve", hidden).Invoke(table, new[] { metadata, mirrors });
            var links = (IList)recordType.GetProperty("WidgetLinks").GetValue(records[0]);
            Assert.NotNull(links[0].GetType().GetField("RuntimeGroup", hidden).GetValue(links[0]));
            var context = Activator.CreateInstance(contextType, hidden, null,
                new object[] { Array.CreateInstance(AppType("GrabWidget"), 0), false }, null);
            var copy = contextType.GetMethod("CaptureMissing", hidden, null, new[] { recordType }, null)
                .Invoke(context, new[] { records[0] });
            var saved = JObject.Parse(JsonConvert.SerializeObject(copy));
            Assert.AreEqual(1, (int)saved["WidgetLinks"][0]["GroupId"]);
            Assert.AreEqual(1, (int)saved["WidgetLinks"][0]["PointerIndex"]);
            Assert.AreEqual(42, (int)saved["Future"]);
            Assert.AreEqual(17u, links[0].GetType().GetField("GroupId").GetValue(links[0]));
            Assert.NotNull(contextType.GetProperty("Table", hidden).GetValue(context));
            var legacy = JsonConvert.DeserializeObject("{\"AssetId\":\"old\",\"GroupIds\":[9]}", recordType);
            Assert.IsNull(recordType.GetProperty("WidgetLinks").GetValue(legacy));
        }

        [Test]
        public void PortedPrefabUsesTheEditableModelWidgetScript()
        {
            var editable = AppType("EditableModelWidget");
            Assert.IsTrue(editable.IsSubclassOf(AppType("ModelWidget")));
            Assert.NotNull(Resources.Load("EditableModelWidget", editable));
        }

        [Test]
        public void AdditiveEditableImportsKeepRetainedRecordsAndRemapSharedDefinitionsAndPayloads()
        {
            var manager = AppType("MeshEditing.EditableModelManager");
            var definitionType = AppType("EditableModelDefinitionData");
            var instanceType = AppType("TiltEditableModels");
            var retainedDefinitions = (IDictionary)manager.GetField("RetainedDefinitions").GetValue(null);
            var retainedInstances = (IList)manager.GetField("RetainedInstances").GetValue(null);
            var originalDefinitions = new Hashtable(retainedDefinitions);
            var originalInstances = new ArrayList(retainedInstances);
            var definitions = (IDictionary)Activator.CreateInstance(retainedDefinitions.GetType());
            const string definitionJson = "{\"Recipe\":{\"Version\":1,\"Generator\":{\"Type\":\"future-shape\"}},\"Geometry\":{\"GeometryFile\":\"editable-model-source.bin\"}}";
            var definition = JsonConvert.DeserializeObject(definitionJson, definitionType);
            var sourceFiles = (IDictionary)definitionType.GetProperty("SourceFiles").GetValue(definition);
            sourceFiles.Add("editable-model-source.bin", new byte[] { 1, 2, 3 });
            definitions.Add("shared", definition);
            var instances = Array.CreateInstance(instanceType, 2);
            for (int i = 0; i < instances.Length; ++i)
            {
                instances.SetValue(JsonConvert.DeserializeObject("{\"AssetId\":\"shared\",\"RawTransforms\":[],\"Future\":42}", instanceType), i);
            }
            try
            {
                retainedDefinitions.Clear();
                retainedInstances.Clear();
                retainedDefinitions.Add("shared", definition);
                retainedInstances.Add(instances.GetValue(0));
                var load = manager.GetMethod("LoadFromTilt");
                load.Invoke(null, new object[] { definitions, instances, true });
                load.Invoke(null, new object[] { definitions, instances, true });
                Assert.AreEqual(3, retainedDefinitions.Count);
                Assert.AreEqual(5, retainedInstances.Count);
                string Id(object record) => (string)instanceType.GetProperty("AssetId").GetValue(record);
                string first = Id(retainedInstances[1]);
                string second = Id(retainedInstances[3]);
                Assert.AreEqual(first, Id(retainedInstances[2]));
                Assert.AreEqual(second, Id(retainedInstances[4]));
                Assert.AreNotEqual(first, second);
                Assert.AreNotEqual("shared", first);
                Assert.AreSame(definition, retainedDefinitions["shared"]);
                Assert.AreEqual("shared", Id(instances.GetValue(0)));
                foreach (string id in new[] { first, second })
                {
                    var imported = retainedDefinitions[id];
                    string file = (string)definitionType.GetProperty("GeometryFile").GetValue(imported);
                    Assert.AreEqual($"editable-model-{id}.bin", file);
                    var files = (IDictionary)definitionType.GetProperty("SourceFiles").GetValue(imported);
                    CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, (byte[])files[file]);
                }
                Assert.AreEqual("editable-model-source.bin", definitionType.GetProperty("GeometryFile").GetValue(definition));
                var saved = JsonConvert.SerializeObject(retainedInstances);
                var restored = (IList)JsonConvert.DeserializeObject(saved, instances.GetType());
                Assert.AreEqual(5, restored.Count);
                Assert.AreEqual(42, (int)JObject.FromObject(restored[1])["Future"]);
            }
            finally
            {
                retainedDefinitions.Clear();
                foreach (DictionaryEntry entry in originalDefinitions) { retainedDefinitions.Add(entry.Key, entry.Value); }
                retainedInstances.Clear();
                foreach (var record in originalInstances) { retainedInstances.Add(record); }
            }
        }
    }
}
