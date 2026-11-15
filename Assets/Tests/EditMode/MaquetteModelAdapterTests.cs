// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class MaquetteModelAdapterTests
    {
        private static JObject Model() => JObject.Parse(@"{
            'asset': {'version': '2.0'}, 'scene': 0, 'scenes': [{'nodes': [0]}],
            'nodes': [
                {'scale': [0.1,0.1,0.1], 'children': [1]},
                {'translation': [3,8,5], 'rotation': [0.7071068,0,0,0.7071068],
                 'scale': [0.1,0.2,0.3], 'mesh': 0}],
            'meshes': [{'primitives': [{'attributes': {'POSITION': 0}}]}],
            'accessors': [{'type': 'VEC3', 'min': [0,-5,-10], 'max': [20,5,10]}],
            'buffers': [{'uri': 'source.bin', 'byteLength': 24}]
        }");

        [Test]
        public void MeshLocalPrimitiveDiscardsAuthoredPoseButKeepsUnitsAndSharedGeometry()
        {
            var source = Model();
            var before = source.DeepClone();
            Assert.IsTrue(MaquetteModelAdapter.TryCreateMeshLocalSource(source, .1f,
                out var result, out var center));
            var node = result["nodes"][(int)result["scenes"][0]["nodes"][0]];
            Assert.IsNull(node["rotation"]);
            Assert.IsNull(node["translation"]);
            Assert.AreEqual(0, (int)node["mesh"]);
            CollectionAssert.AreEqual(new[] { .1f, .1f, .1f }, node["scale"].ToObject<float[]>());
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f }, center);
            Assert.IsTrue(JToken.DeepEquals(source["meshes"], result["meshes"]));
            Assert.IsTrue(JToken.DeepEquals(source["buffers"], result["buffers"]));
            Assert.IsTrue(JToken.DeepEquals(before, source));
        }

        [Test]
        public void MultipleRendererHierarchyIsNotCollapsedIntoOnePrimitive()
        {
            var source = Model();
            ((JArray)source["nodes"]).Add(new JObject { ["mesh"] = 0 });
            ((JArray)source["nodes"][0]["children"]).Add(2);
            Assert.IsFalse(MaquetteModelAdapter.TryCreateMeshLocalSource(source, 1, out _, out _));
        }

        [Test]
        public void DeformedMeshKeepsExistingImportPath()
        {
            var source = Model();
            source["nodes"][1]["skin"] = 0;
            Assert.IsFalse(MaquetteModelAdapter.TryCreateMeshLocalSource(source, 1, out _, out _));
            source["nodes"][1]["skin"].Parent.Remove();
            source["meshes"][0]["primitives"][0]["targets"] = new JArray();
            Assert.IsFalse(MaquetteModelAdapter.TryCreateMeshLocalSource(source, 1, out _, out _));
        }
    }
}
