// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace TiltBrush
{
    internal class TestRuntimeVoxDocument
    {
        [Test]
        public void RuntimeModel_AddMoveSetRemoveVoxel_Works()
        {
            var document = new RuntimeVoxDocument();
            RuntimeVoxDocument.RuntimeModel model = document.CreateModel("test", new Vector3Int(4, 4, 4));

            Assert.IsTrue(model.AddOrUpdateVoxel(new Vector3Int(0, 0, 0), 1));
            Assert.IsTrue(model.TryGetPaletteIndex(new Vector3Int(0, 0, 0), out byte paletteIndex));
            Assert.AreEqual(1, paletteIndex);

            Assert.IsTrue(model.MoveVoxel(new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0)));
            Assert.IsFalse(model.TryGetPaletteIndex(new Vector3Int(0, 0, 0), out _));

            Assert.IsTrue(model.SetVoxelColor(new Vector3Int(1, 0, 0), 2));
            Assert.IsTrue(model.TryGetPaletteIndex(new Vector3Int(1, 0, 0), out byte movedPaletteIndex));
            Assert.AreEqual(2, movedPaletteIndex);

            Assert.IsTrue(model.RemoveVoxel(new Vector3Int(1, 0, 0)));
            Assert.IsFalse(model.TryGetPaletteIndex(new Vector3Int(1, 0, 0), out _));
        }

        [Test]
        public void RuntimeModel_RejectsOutOfBoundsAndZeroPaletteIndex()
        {
            var document = new RuntimeVoxDocument();
            RuntimeVoxDocument.RuntimeModel model = document.CreateModel("test", new Vector3Int(2, 2, 2));

            Assert.IsFalse(model.AddOrUpdateVoxel(new Vector3Int(-1, 0, 0), 1));
            Assert.IsFalse(model.AddOrUpdateVoxel(new Vector3Int(2, 0, 0), 1));
            Assert.IsFalse(model.AddOrUpdateVoxel(new Vector3Int(0, 0, 0), 0));
        }

        [Test]
        public void RuntimeModel_AcceptsMaximumVoxDimensions()
        {
            var document = new RuntimeVoxDocument();
            RuntimeVoxDocument.RuntimeModel model = document.CreateModel(
                "maximum",
                new Vector3Int(
                    RuntimeVoxDocument.MaxModelDimension,
                    RuntimeVoxDocument.MaxModelDimension,
                    RuntimeVoxDocument.MaxModelDimension));

            Assert.IsTrue(model.AddOrUpdateVoxel(new Vector3Int(255, 255, 255), 1));

            RuntimeVoxDocument reloaded = RuntimeVoxDocument.FromBytes(document.ToVoxBytes());
            Assert.AreEqual(model.Size, reloaded.Models[0].Size);
            Assert.IsTrue(reloaded.Models[0].TryGetPaletteIndex(
                new Vector3Int(255, 255, 255),
                out byte paletteIndex));
            Assert.AreEqual(1, paletteIndex);
        }

        [TestCase(257, 1, 1)]
        [TestCase(1, 257, 1)]
        [TestCase(1, 1, 257)]
        public void RuntimeModel_RejectsDimensionsLargerThanVoxCoordinates(int x, int y, int z)
        {
            var document = new RuntimeVoxDocument();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                document.CreateModel("too-large", new Vector3Int(x, y, z)));
        }

        [Test]
        public void VoxMeshBuilder_BuildsFromRuntimeModel()
        {
            var document = new RuntimeVoxDocument();
            RuntimeVoxDocument.RuntimeModel model = document.CreateModel("mesh", new Vector3Int(4, 4, 4));
            document.ReplacePaletteEntry(1, new Color32(255, 0, 0, 255));

            model.AddOrUpdateVoxel(new Vector3Int(0, 0, 0), 1);
            model.AddOrUpdateVoxel(new Vector3Int(1, 0, 0), 1);

            var builder = new VoxMeshBuilder();
            Mesh optimized = builder.GenerateOptimizedMesh(model, document.Palette);
            Mesh cubes = builder.GenerateSeparateCubesMesh(model, document.Palette);

            Assert.NotNull(optimized);
            Assert.NotNull(cubes);
            Assert.AreEqual(24, optimized.vertexCount);
            Assert.AreEqual(36, optimized.triangles.Length);
            Assert.AreEqual(48, cubes.vertexCount);
            Assert.AreEqual(72, cubes.triangles.Length);
        }

        [TestCase(10, 0, 0)]
        [TestCase(0, 7, 0)]
        [TestCase(0, 0, 5)]
        [TestCase(10, 7, 5)]
        public void VoxMeshBuilder_OptimizedMeshPreservesVoxelTranslation(int x, int y, int z)
        {
            var document = new RuntimeVoxDocument();
            var shift = new Vector3Int(x, y, z);
            var size = new Vector3Int(16, 16, 16);
            var original = document.CreateModel("original", size);
            var shifted = document.CreateModel("shifted", size);
            foreach (var position in new[] { Vector3Int.zero, Vector3Int.right })
            {
                Assert.IsTrue(original.AddOrUpdateVoxel(position, 1));
                Assert.IsTrue(shifted.AddOrUpdateVoxel(position + shift, 1));
            }

            var builder = new VoxMeshBuilder();
            Mesh originalMesh = builder.GenerateOptimizedMesh(original, document.Palette);
            Mesh shiftedMesh = builder.GenerateOptimizedMesh(shifted, document.Palette);
            try
            {
                Assert.AreEqual(originalMesh.bounds.min + (Vector3)shift, shiftedMesh.bounds.min);
                Assert.AreEqual(originalMesh.bounds.max + (Vector3)shift, shiftedMesh.bounds.max);
                Assert.AreEqual(originalMesh.bounds.size, shiftedMesh.bounds.size);
                CollectionAssert.AreEqual(originalMesh.triangles, shiftedMesh.triangles);
                Vector3[] originalVertices = originalMesh.vertices;
                Vector3[] shiftedVertices = shiftedMesh.vertices;
                Assert.AreEqual(originalVertices.Length, shiftedVertices.Length);
                for (int i = 0; i < originalVertices.Length; i++)
                {
                    Assert.AreEqual(originalVertices[i] + (Vector3)shift, shiftedVertices[i]);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(originalMesh);
                UnityEngine.Object.DestroyImmediate(shiftedMesh);
            }
        }

        [Test]
        public void RuntimeVoxDocument_RoundTripsThroughVoxBytes()
        {
            var source = new RuntimeVoxDocument();
            source.ReplacePaletteEntry(1, new Color32(255, 0, 0, 255));
            source.ReplacePaletteEntry(2, new Color32(0, 255, 0, 255));

            RuntimeVoxDocument.RuntimeModel model = source.CreateModel("roundtrip", new Vector3Int(8, 8, 8));
            model.AddOrUpdateVoxel(new Vector3Int(0, 0, 0), 1);
            model.AddOrUpdateVoxel(new Vector3Int(1, 2, 3), 2);

            byte[] bytes = source.ToVoxBytes();
            RuntimeVoxDocument reloaded = RuntimeVoxDocument.FromBytes(bytes);

            Assert.AreEqual(1, reloaded.Models.Count);
            RuntimeVoxDocument.RuntimeModel reloadedModel = reloaded.Models[0];
            Assert.AreEqual(2, reloadedModel.Voxels.Count);
            Assert.IsTrue(reloadedModel.TryGetPaletteIndex(new Vector3Int(0, 0, 0), out byte firstColor));
            Assert.IsTrue(reloadedModel.TryGetPaletteIndex(new Vector3Int(1, 2, 3), out byte secondColor));
            Assert.AreEqual(1, firstColor);
            Assert.AreEqual(2, secondColor);
            Assert.AreEqual(new Color32(255, 0, 0, 255), reloaded.Palette[0]);
            Assert.AreEqual(new Color32(0, 255, 0, 255), reloaded.Palette[1]);
        }

        [Test]
        public void RuntimeVoxDocument_GeneratedBytesRemainExtensibleWhenRequested()
        {
            var source = new RuntimeVoxDocument();
            source.CreateModel("generated", new Vector3Int(4, 4, 4));

            RuntimeVoxDocument restored = RuntimeVoxDocument.FromBytes(
                source.ToVoxBytes(), preserveSourceData: false);

            Assert.IsFalse(restored.HasPreservedSourceData);
            Assert.DoesNotThrow(() => restored.CreateModel("second", new Vector3Int(2, 2, 2)));
            Assert.AreEqual(2, restored.Models.Count);
        }

        [Test]
        public void RuntimeVoxDocument_RoundTripsMultipleModelsThroughVoxBytes()
        {
            var source = new RuntimeVoxDocument();
            source.ReplacePaletteEntry(1, new Color32(255, 0, 0, 255));
            source.ReplacePaletteEntry(2, new Color32(0, 255, 0, 255));

            RuntimeVoxDocument.RuntimeModel a = source.CreateModel("a", new Vector3Int(8, 8, 8));
            RuntimeVoxDocument.RuntimeModel b = source.CreateModel("b", new Vector3Int(8, 8, 8));
            a.AddOrUpdateVoxel(new Vector3Int(0, 0, 0), 1);
            b.AddOrUpdateVoxel(new Vector3Int(1, 2, 3), 2);

            byte[] bytes = source.ToVoxBytes();
            RuntimeVoxDocument reloaded = RuntimeVoxDocument.FromBytes(bytes);

            Assert.AreEqual(2, reloaded.Models.Count);
            Assert.AreEqual(1, reloaded.Models[0].Voxels.Count);
            Assert.AreEqual(1, reloaded.Models[1].Voxels.Count);
        }

        [TestCase(1.8f, -2.8f, 3.2f, 2, -3, 3)]
        [TestCase(-1.2f, 2.2f, -3.8f, -1, 2, -4)]
        [TestCase(1.5f, -2.5f, 3.5f, 2, -2, 4)]
        [TestCase(2f, -3f, 4f, 2, -3, 4)]
        public void RuntimeVoxDocument_ExportRoundsTransformOffsets(
            float x, float y, float z, int expectedX, int expectedY, int expectedZ)
        {
            for (int modelCount = 1; modelCount <= 2; modelCount++)
            {
                var source = new RuntimeVoxDocument();
                var model = source.CreateModel("translated", new Vector3Int(8, 8, 8));
                model.AddOrUpdateVoxel(Vector3Int.zero, 1);
                model.TransformOffset = new Vector3(x, y, z);
                if (modelCount == 2)
                {
                    source.CreateModel("other", new Vector3Int(8, 8, 8))
                        .AddOrUpdateVoxel(Vector3Int.zero, 1);
                }

                RuntimeVoxDocument reloaded = RuntimeVoxDocument.FromBytes(source.ToVoxBytes());

                Assert.AreEqual(modelCount, reloaded.Models.Count);
                Assert.AreEqual(new Vector3(expectedX, expectedY, expectedZ),
                    reloaded.Models[0].TransformOffset, $"Model count: {modelCount}");
                Assert.AreEqual(new Vector3(x, y, z), model.TransformOffset);
            }
        }

        [Test]
        public void RuntimeVoxDocument_LoadsFromStreamAndReadOnlyMemory()
        {
            var source = new RuntimeVoxDocument();
            RuntimeVoxDocument.RuntimeModel model = source.CreateModel("stream", new Vector3Int(8, 8, 8));
            model.AddOrUpdateVoxel(new Vector3Int(2, 2, 2), 1);
            byte[] bytes = source.ToVoxBytes();

            RuntimeVoxDocument fromMemory = RuntimeVoxDocument.FromBytes(new System.ReadOnlyMemory<byte>(bytes));
            RuntimeVoxDocument fromStream;
            using (var stream = new MemoryStream(bytes))
            {
                fromStream = RuntimeVoxDocument.FromStream(stream);
            }

            Assert.AreEqual(1, fromMemory.Models.Count);
            Assert.AreEqual(1, fromStream.Models.Count);
            Assert.AreEqual(1, fromMemory.Models[0].Voxels.Count);
            Assert.AreEqual(1, fromStream.Models[0].Voxels.Count);
        }

        [Test]
        public void RuntimeVoxDocument_PreservesSourceChunksWhileEditingVoxels()
        {
            var source = new RuntimeVoxDocument();
            RuntimeVoxDocument.RuntimeModel sourceModel = source.CreateModel(
                "preserved",
                new Vector3Int(8, 8, 8));
            sourceModel.TransformOffset = new Vector3(2, 3, 4);
            sourceModel.AddOrUpdateVoxel(Vector3Int.zero, 1);
            source.ReplacePaletteEntry(1, new Color32(200, 10, 20, 255));
            source.ReplacePaletteEntry(2, new Color32(20, 30, 200, 255));

            byte[] metadata = { 9, 8, 7, 6, 5 };
            byte[] sourceBytes = AppendMainChild(source.ToVoxBytes(), "META", metadata);
            var indexMap = new byte[256];
            for (int i = 0; i < byte.MaxValue; i++)
            {
                indexMap[i] = (byte)(i + 1);
            }
            indexMap[0] = 2;
            indexMap[1] = 3;
            indexMap[2] = 1;
            sourceBytes = AppendMainChild(sourceBytes, "IMAP", indexMap);
            byte[] originalRootTransform = FindMainChildContent(sourceBytes, "nTRN", 0);
            byte[] originalModelTransform = FindMainChildContent(sourceBytes, "nTRN", 1);

            RuntimeVoxDocument loaded = RuntimeVoxDocument.FromBytes(sourceBytes);
            Assert.IsTrue(loaded.HasPreservedSourceData);
            Assert.IsTrue(loaded.Models[0].TryGetPaletteIndex(Vector3Int.zero, out byte originalColor));
            Assert.AreEqual(2, originalColor);
            Assert.IsTrue(loaded.Models[0].AddOrUpdateVoxel(new Vector3Int(1, 2, 3), 2));
            loaded.ReplacePaletteEntry(2, new Color32(12, 34, 56, 255));

            byte[] editedBytes = loaded.ToVoxBytes();
            CollectionAssert.AreEqual(metadata, FindMainChildContent(editedBytes, "META", 0));
            CollectionAssert.AreEqual(indexMap, FindMainChildContent(editedBytes, "IMAP", 0));
            CollectionAssert.AreEqual(
                originalRootTransform,
                FindMainChildContent(editedBytes, "nTRN", 0));
            CollectionAssert.AreEqual(
                originalModelTransform,
                FindMainChildContent(editedBytes, "nTRN", 1));

            RuntimeVoxDocument reloaded = RuntimeVoxDocument.FromBytes(editedBytes);
            Assert.IsTrue(reloaded.Models[0].TryGetPaletteIndex(new Vector3Int(1, 2, 3), out byte color));
            Assert.AreEqual(2, color);
            Assert.AreEqual(new Color32(12, 34, 56, 255), reloaded.Palette[1]);
        }

        [Test]
        public void Model_CreatesIndependentEditableVoxDocuments()
        {
            var source = new RuntimeVoxDocument();
            RuntimeVoxDocument.RuntimeModel sourceModel = source.CreateModel(
                "source",
                new Vector3Int(8, 8, 8));
            sourceModel.AddOrUpdateVoxel(Vector3Int.zero, 1);
            byte[] metadata = { 3, 1, 4, 1, 5 };
            byte[] sourceBytes = AppendMainChild(source.ToVoxBytes(), "META", metadata);

            var model = new Model("editable-source.vox");
            MethodInfo setSource = typeof(Model).GetMethod(
                "SetEditableVoxSource",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo createDocument = typeof(Model).GetMethod(
                "CreateEditableVoxDocument",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(setSource);
            Assert.IsNotNull(createDocument);
            setSource.Invoke(model, new object[] { sourceBytes });

            var first = (RuntimeVoxDocument)createDocument.Invoke(model, null);
            var second = (RuntimeVoxDocument)createDocument.Invoke(model, null);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            CollectionAssert.AreEqual(
                metadata,
                FindMainChildContent(first.ToVoxBytes(), "META", 0));
            CollectionAssert.AreEqual(
                metadata,
                FindMainChildContent(second.ToVoxBytes(), "META", 0));

            Assert.IsTrue(first.Models[0].AddOrUpdateVoxel(new Vector3Int(1, 2, 3), 2));
            Assert.IsFalse(second.Models[0].TryGetPaletteIndex(new Vector3Int(1, 2, 3), out _));

            sourceBytes[0] = 0;
            var third = (RuntimeVoxDocument)createDocument.Invoke(model, null);
            Assert.IsNotNull(third);
            Assert.IsTrue(third.Models[0].TryGetPaletteIndex(Vector3Int.zero, out byte paletteIndex));
            Assert.AreEqual(1, paletteIndex);
        }

        [Test]
        public void Model_WithoutVoxSourceHasNoEditableDocument()
        {
            var model = new Model("ordinary.obj");
            MethodInfo createDocument = typeof(Model).GetMethod(
                "CreateEditableVoxDocument",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(createDocument);
            Assert.IsNull(createDocument.Invoke(model, null));
        }

        private static byte[] AppendMainChild(byte[] source, string id, byte[] content)
        {
            int childrenLength = BitConverter.ToInt32(source, 16);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(source);
                writer.Write(Encoding.ASCII.GetBytes(id));
                writer.Write(content.Length);
                writer.Write(0);
                writer.Write(content);
                writer.Flush();

                byte[] result = stream.ToArray();
                byte[] newChildrenLength = BitConverter.GetBytes(childrenLength + 12 + content.Length);
                Buffer.BlockCopy(newChildrenLength, 0, result, 16, sizeof(int));
                return result;
            }
        }

        private static byte[] FindMainChildContent(byte[] source, string wantedId, int occurrence)
        {
            int mainContentLength = BitConverter.ToInt32(source, 12);
            int mainChildrenLength = BitConverter.ToInt32(source, 16);
            int offset = 20 + mainContentLength;
            int end = offset + mainChildrenLength;
            while (offset < end)
            {
                string id = Encoding.ASCII.GetString(source, offset, 4);
                int contentLength = BitConverter.ToInt32(source, offset + 4);
                int childrenLength = BitConverter.ToInt32(source, offset + 8);
                if (id == wantedId)
                {
                    if (occurrence == 0)
                    {
                        var content = new byte[contentLength];
                        Buffer.BlockCopy(source, offset + 12, content, 0, contentLength);
                        return content;
                    }
                    occurrence--;
                }
                offset += 12 + contentLength + childrenLength;
            }
            Assert.Fail($"Could not find VOX chunk '{wantedId}'.");
            return null;
        }
    }
}
