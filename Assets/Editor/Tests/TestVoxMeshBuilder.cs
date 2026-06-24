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

using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VoxReader;
using VoxReader.Interfaces;
using Vector3 = UnityEngine.Vector3;

namespace TiltBrush
{
    internal class TestVoxMeshBuilder
    {
        private struct TestVoxel
        {
            public readonly byte X;
            public readonly byte Y;
            public readonly byte Z;
            public readonly byte ColorIndex;

            public TestVoxel(byte x, byte y, byte z, byte colorIndex)
            {
                X = x;
                Y = y;
                Z = z;
                ColorIndex = colorIndex;
            }
        }

        [Test]
        public void SingleVoxel_OptimizedAndCubesMatchExpectedGeometry()
        {
            IModel model = ReadSingleModel(CreateVoxBytes(new[]
            {
                new TestVoxel(0, 0, 0, 1)
            }, sizeX: 2, sizeY: 2, sizeZ: 2));

            var builder = new VoxMeshBuilder();
            Mesh optimized = builder.GenerateOptimizedMesh(model);
            Mesh cubes = builder.GenerateSeparateCubesMesh(model);

            Assert.NotNull(optimized);
            Assert.NotNull(cubes);

            Assert.AreEqual(24, optimized.vertexCount);
            Assert.AreEqual(24, cubes.vertexCount);
            Assert.AreEqual(36, optimized.triangles.Length);
            Assert.AreEqual(36, cubes.triangles.Length);

            AssertVectorAlmostEqual(new Vector3(1f, 1f, 1f), optimized.bounds.size);
            AssertVectorAlmostEqual(new Vector3(1f, 1f, 1f), cubes.bounds.size);
        }

        [Test]
        public void TwoAdjacentVoxels_OptimizedReducesGeometryAndPreservesBounds()
        {
            IModel model = ReadSingleModel(CreateVoxBytes(new[]
            {
                new TestVoxel(0, 0, 0, 1),
                new TestVoxel(1, 0, 0, 1)
            }, sizeX: 3, sizeY: 2, sizeZ: 2));

            var builder = new VoxMeshBuilder();
            Mesh optimized = builder.GenerateOptimizedMesh(model);
            Mesh cubes = builder.GenerateSeparateCubesMesh(model);

            Assert.NotNull(optimized);
            Assert.NotNull(cubes);

            Assert.Less(optimized.vertexCount, cubes.vertexCount);
            Assert.Less(optimized.triangles.Length, cubes.triangles.Length);
            Assert.AreEqual(24, optimized.vertexCount);
            Assert.AreEqual(36, optimized.triangles.Length);
            Assert.AreEqual(48, cubes.vertexCount);
            Assert.AreEqual(72, cubes.triangles.Length);

            AssertVectorAlmostEqual(new Vector3(2f, 1f, 1f), optimized.bounds.size);
            AssertVectorAlmostEqual(cubes.bounds.size, optimized.bounds.size);
        }

        private static void AssertVectorAlmostEqual(Vector3 expected, Vector3 actual, float tolerance = 0.0001f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance);
            Assert.AreEqual(expected.y, actual.y, tolerance);
            Assert.AreEqual(expected.z, actual.z, tolerance);
        }

        private static IModel ReadSingleModel(byte[] bytes)
        {
            IVoxFile voxFile = VoxReader.VoxReader.Read(bytes);
            Assert.NotNull(voxFile);
            Assert.AreEqual(1, voxFile.Models.Length);
            return voxFile.Models[0];
        }

        private static byte[] CreateVoxBytes(IReadOnlyList<TestVoxel> voxels, int sizeX, int sizeY, int sizeZ)
        {
            byte[] sizeChunk = BuildSizeChunk(sizeX, sizeY, sizeZ);
            byte[] xyziChunk = BuildXyziChunk(voxels);
            byte[] rgbaChunk = BuildRgbaChunk();

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("VOX "));
                writer.Write(150);

                writer.Write(Encoding.ASCII.GetBytes("MAIN"));
                writer.Write(0);
                writer.Write(sizeChunk.Length + xyziChunk.Length + rgbaChunk.Length);
                writer.Write(sizeChunk);
                writer.Write(xyziChunk);
                writer.Write(rgbaChunk);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] BuildSizeChunk(int sizeX, int sizeY, int sizeZ)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(sizeX);
                writer.Write(sizeY);
                writer.Write(sizeZ);
                return WrapChunk("SIZE", stream.ToArray());
            }
        }

        private static byte[] BuildXyziChunk(IReadOnlyList<TestVoxel> voxels)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(voxels.Count);
                foreach (TestVoxel voxel in voxels)
                {
                    writer.Write(voxel.X);
                    writer.Write(voxel.Y);
                    writer.Write(voxel.Z);
                    writer.Write(voxel.ColorIndex);
                }

                return WrapChunk("XYZI", stream.ToArray());
            }
        }

        private static byte[] BuildRgbaChunk()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < 256; i++)
                {
                    byte v = (byte)i;
                    writer.Write(v);
                    writer.Write(v);
                    writer.Write(v);
                    writer.Write((byte)255);
                }

                return WrapChunk("RGBA", stream.ToArray());
            }
        }

        private static byte[] WrapChunk(string id, byte[] content)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes(id));
                writer.Write(content.Length);
                writer.Write(0);
                writer.Write(content);
                return stream.ToArray();
            }
        }
    }
}
