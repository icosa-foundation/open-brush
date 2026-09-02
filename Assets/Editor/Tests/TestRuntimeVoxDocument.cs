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
using System.IO;
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
    }
}
