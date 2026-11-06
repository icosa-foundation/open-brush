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
        public void PortedPrefabUsesTheEditableModelWidgetScript()
        {
            var editable = AppType("EditableModelWidget");
            Assert.IsTrue(editable.IsSubclassOf(AppType("ModelWidget")));
            Assert.NotNull(Resources.Load("EditableModelWidget", editable));
        }
    }
}
