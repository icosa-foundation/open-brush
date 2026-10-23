// Copyright 2024 The Open Brush Authors
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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxReader;
using VoxReader.Interfaces;
using Vector3 = UnityEngine.Vector3;

namespace TiltBrush
{
    public class VoxMeshBuilder
    {
        // VOX uses Z-up; runtime scene objects use Unity's Y-up coordinates.
        public static readonly Quaternion ModelRotation = Quaternion.Euler(-90f, 0f, 0f);

        internal static TrTransform GetModelTransform(RuntimeVoxDocument.RuntimeModel model)
        {
            return GetModelTransform(model.TransformOffset, model.GlobalRotation);
        }

        internal static TrTransform GetModelTransform(IModel model)
        {
            return GetModelTransform(
                RuntimeVoxDocument.GetTransformOffset(model),
                RuntimeVoxDocument.ToUnityMatrix(model.GlobalRotation));
        }

        private static TrTransform GetModelTransform(Vector3 offset, Matrix4x4 globalRotation)
        {
            Quaternion sceneRotation = Quaternion.LookRotation(
                globalRotation.MultiplyVector(Vector3.forward),
                globalRotation.MultiplyVector(Vector3.up));
            return TrTransform.TR(
                ModelRotation * offset,
                ModelRotation * sceneRotation);
        }

        // Only use for runtime VOX objects, which own their procedurally generated meshes.
        internal static void DestroyRuntimeSceneObject(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var meshes = new HashSet<Mesh>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                // Clear references so repeated cleanup before deferred destruction is harmless.
                filter.sharedMesh = null;
                if (mesh != null && meshes.Add(mesh))
                {
                    UnityEngine.Object.Destroy(mesh);
                }
            }

            UnityEngine.Object.Destroy(root);
        }

        public Mesh GenerateOptimizedMesh(RuntimeVoxDocument.RuntimeModel model, Color32[] palette)
        {
            return GenerateOptimizedMesh(model, palette, null, 1);
        }

        internal Mesh GenerateOptimizedMesh(
            RuntimeVoxDocument.RuntimeModel model,
            Color32[] palette,
            IReadOnlyList<int> paletteSubmeshIndices,
            int submeshCount)
        {
            RuntimeVoxelGrid grid = new RuntimeVoxelGrid(model, palette, paletteSubmeshIndices);
            MeshData meshData = GreedyMesh(grid, submeshCount);
            return CreateMesh($"{model.Name}_Optimized", meshData);
        }

        public Mesh GenerateSeparateCubesMesh(RuntimeVoxDocument.RuntimeModel model, Color32[] palette)
        {
            return GenerateSeparateCubesMesh(model, palette, null, 1);
        }

        internal Mesh GenerateSeparateCubesMesh(
            RuntimeVoxDocument.RuntimeModel model,
            Color32[] palette,
            IReadOnlyList<int> paletteSubmeshIndices,
            int submeshCount)
        {
            MeshData meshData = new MeshData(submeshCount);

            foreach (RuntimeVoxDocument.RuntimeVoxel voxel in model.EnumerateVoxels(palette))
            {
                AddCube(
                    meshData,
                    voxel.Position,
                    voxel.Color,
                    GetSubmeshIndex(voxel.PaletteIndex, paletteSubmeshIndices));
            }

            return CreateMesh($"{model.Name}_Cubes", meshData);
        }

        public Mesh GenerateOptimizedMesh(IModel model)
        {
            VoxelGrid grid = new VoxelGrid(model);
            MeshData meshData = GreedyMesh(grid);
            return CreateMesh($"{model.Name}_Optimized", meshData);
        }

        public Mesh GenerateSeparateCubesMesh(IModel model)
        {
            MeshData meshData = new MeshData(1);

            foreach (Voxel voxel in model.Voxels)
            {
                Vector3 position = new Vector3(
                    voxel.LocalPosition.X,
                    voxel.LocalPosition.Y,
                    voxel.LocalPosition.Z
                );

                Color32 color = new Color32(
                    voxel.Color.R,
                    voxel.Color.G,
                    voxel.Color.B,
                    voxel.Color.A
                );

                AddCube(meshData, position, color, 0);
            }

            return CreateMesh($"{model.Name}_Cubes", meshData);
        }

        private Mesh CreateMesh(string name, MeshData meshData)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;

            mesh.indexFormat = meshData.vertices.Count > 65535
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            mesh.SetVertices(meshData.vertices);
            mesh.SetColors(meshData.colors);
            mesh.subMeshCount = meshData.triangles.Count;
            for (int i = 0; i < meshData.triangles.Count; i++)
            {
                mesh.SetTriangles(meshData.triangles[i], i);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            return mesh;
        }

        private void AddCube(MeshData meshData, Vector3 center, Color32 color, int submeshIndex)
        {
            int baseIndex = meshData.vertices.Count;
            float size = 1.0f;
            float half = size * 0.5f;

            Vector3[] corners = new Vector3[8]
            {
                center + new Vector3(-half, -half, -half),
                center + new Vector3( half, -half, -half),
                center + new Vector3( half,  half, -half),
                center + new Vector3(-half,  half, -half),
                center + new Vector3(-half, -half,  half),
                center + new Vector3( half, -half,  half),
                center + new Vector3( half,  half,  half),
                center + new Vector3(-half,  half,  half)
            };

            meshData.vertices.Add(corners[4]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[5]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[6]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[7]); meshData.colors.Add(color);

            meshData.vertices.Add(corners[1]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[0]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[3]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[2]); meshData.colors.Add(color);

            meshData.vertices.Add(corners[7]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[6]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[2]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[3]); meshData.colors.Add(color);

            meshData.vertices.Add(corners[0]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[1]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[5]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[4]); meshData.colors.Add(color);

            meshData.vertices.Add(corners[5]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[1]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[2]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[6]); meshData.colors.Add(color);

            meshData.vertices.Add(corners[0]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[4]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[7]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[3]); meshData.colors.Add(color);

            for (int i = 0; i < 6; i++)
            {
                int vertexOffset = baseIndex + i * 4;

                List<int> triangles = meshData.triangles[submeshIndex];
                triangles.Add(vertexOffset + 0);
                triangles.Add(vertexOffset + 1);
                triangles.Add(vertexOffset + 2);

                triangles.Add(vertexOffset + 0);
                triangles.Add(vertexOffset + 2);
                triangles.Add(vertexOffset + 3);
            }
        }

        private MeshData GreedyMesh(VoxelGrid grid)
        {
            MeshData meshData = new MeshData(1);

            GreedyMeshAxis(grid, meshData, 0);
            GreedyMeshAxis(grid, meshData, 1);
            GreedyMeshAxis(grid, meshData, 2);

            return meshData;
        }

        private MeshData GreedyMesh(RuntimeVoxelGrid grid)
        {
            return GreedyMesh(grid, 1);
        }

        private MeshData GreedyMesh(RuntimeVoxelGrid grid, int submeshCount)
        {
            MeshData meshData = new MeshData(submeshCount);

            GreedyMeshAxis(grid, meshData, 0);
            GreedyMeshAxis(grid, meshData, 1);
            GreedyMeshAxis(grid, meshData, 2);

            return meshData;
        }

        private void GreedyMeshAxis(VoxelGrid grid, MeshData meshData, int axis)
        {
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            Vector3Int size = grid.Size;
            int[] dims = { size.x, size.y, size.z };

            Vector3Int pos = Vector3Int.zero;
            bool[,] mask = new bool[dims[u], dims[v]];
            bool[,] faceTowardsPositive = new bool[dims[u], dims[v]];
            Color32[,] colorMask = new Color32[dims[u], dims[v]];

            for (pos[axis] = 0; pos[axis] <= dims[axis]; pos[axis]++)
            {
                for (int iu = 0; iu < dims[u]; iu++)
                {
                    for (int iv = 0; iv < dims[v]; iv++)
                    {
                        mask[iu, iv] = false;
                    }
                }

                for (pos[u] = 0; pos[u] < dims[u]; pos[u]++)
                {
                    for (pos[v] = 0; pos[v] < dims[v]; pos[v]++)
                    {
                        Vector3Int checkPos = pos;
                        Vector3Int neighborPos = pos;
                        neighborPos[axis] = pos[axis] - 1;

                        bool current = pos[axis] < dims[axis] && grid.HasVoxel(checkPos);
                        bool neighbor = pos[axis] > 0 && grid.HasVoxel(neighborPos);

                        if (current != neighbor)
                        {
                            mask[pos[u], pos[v]] = true;
                            faceTowardsPositive[pos[u], pos[v]] = neighbor;
                            colorMask[pos[u], pos[v]] = current
                                ? grid.GetColor(checkPos)
                                : grid.GetColor(neighborPos);
                        }
                    }
                }

                for (int iu = 0; iu < dims[u]; iu++)
                {
                    for (int iv = 0; iv < dims[v]; iv++)
                    {
                        if (!mask[iu, iv])
                        {
                            continue;
                        }

                        Color32 currentColor = colorMask[iu, iv];

                        int width = 1;
                        while (iu + width < dims[u] &&
                               mask[iu + width, iv] &&
                               ColorsEqual(colorMask[iu + width, iv], currentColor) &&
                               faceTowardsPositive[iu + width, iv] == faceTowardsPositive[iu, iv])
                        {
                            width++;
                        }

                        int height = 1;
                        bool done = false;
                        while (iv + height < dims[v] && !done)
                        {
                            for (int k = 0; k < width; k++)
                            {
                                if (!mask[iu + k, iv + height] ||
                                    !ColorsEqual(colorMask[iu + k, iv + height], currentColor) ||
                                    faceTowardsPositive[iu + k, iv + height] != faceTowardsPositive[iu, iv])
                                {
                                    done = true;
                                    break;
                                }
                            }

                            if (!done)
                            {
                                height++;
                            }
                        }

                        pos[u] = iu;
                        pos[v] = iv;
                        AddQuad(meshData, pos, axis, width, height, currentColor,
                            faceTowardsPositive[iu, iv]);

                        for (int ku = 0; ku < width; ku++)
                        {
                            for (int kv = 0; kv < height; kv++)
                            {
                                mask[iu + ku, iv + kv] = false;
                            }
                        }
                    }
                }
            }
        }

        private void GreedyMeshAxis(RuntimeVoxelGrid grid, MeshData meshData, int axis)
        {
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            Vector3Int size = grid.Size;
            int[] dims = { size.x, size.y, size.z };

            Vector3Int pos = Vector3Int.zero;
            bool[,] mask = new bool[dims[u], dims[v]];
            bool[,] faceTowardsPositive = new bool[dims[u], dims[v]];
            Color32[,] colorMask = new Color32[dims[u], dims[v]];
            int[,] submeshMask = new int[dims[u], dims[v]];

            for (pos[axis] = 0; pos[axis] <= dims[axis]; pos[axis]++)
            {
                for (int iu = 0; iu < dims[u]; iu++)
                {
                    for (int iv = 0; iv < dims[v]; iv++)
                    {
                        mask[iu, iv] = false;
                    }
                }

                for (pos[u] = 0; pos[u] < dims[u]; pos[u]++)
                {
                    for (pos[v] = 0; pos[v] < dims[v]; pos[v]++)
                    {
                        Vector3Int checkPos = pos;
                        Vector3Int neighborPos = pos;
                        neighborPos[axis] = pos[axis] - 1;

                        bool current = pos[axis] < dims[axis] && grid.HasVoxel(checkPos);
                        bool neighbor = pos[axis] > 0 && grid.HasVoxel(neighborPos);

                        if (current != neighbor)
                        {
                            mask[pos[u], pos[v]] = true;
                            faceTowardsPositive[pos[u], pos[v]] = neighbor;
                            colorMask[pos[u], pos[v]] = current
                                ? grid.GetColor(checkPos)
                                : grid.GetColor(neighborPos);
                            submeshMask[pos[u], pos[v]] = current
                                ? grid.GetSubmeshIndex(checkPos)
                                : grid.GetSubmeshIndex(neighborPos);
                        }
                    }
                }

                for (int iu = 0; iu < dims[u]; iu++)
                {
                    for (int iv = 0; iv < dims[v]; iv++)
                    {
                        if (!mask[iu, iv])
                        {
                            continue;
                        }

                        Color32 currentColor = colorMask[iu, iv];
                        int currentSubmesh = submeshMask[iu, iv];

                        int width = 1;
                        while (iu + width < dims[u] &&
                               mask[iu + width, iv] &&
                               ColorsEqual(colorMask[iu + width, iv], currentColor) &&
                               submeshMask[iu + width, iv] == currentSubmesh &&
                               faceTowardsPositive[iu + width, iv] == faceTowardsPositive[iu, iv])
                        {
                            width++;
                        }

                        int height = 1;
                        bool done = false;
                        while (iv + height < dims[v] && !done)
                        {
                            for (int k = 0; k < width; k++)
                            {
                                if (!mask[iu + k, iv + height] ||
                                    !ColorsEqual(colorMask[iu + k, iv + height], currentColor) ||
                                    submeshMask[iu + k, iv + height] != currentSubmesh ||
                                    faceTowardsPositive[iu + k, iv + height] != faceTowardsPositive[iu, iv])
                                {
                                    done = true;
                                    break;
                                }
                            }

                            if (!done)
                            {
                                height++;
                            }
                        }

                        pos[u] = iu;
                        pos[v] = iv;
                        AddQuad(meshData, (Vector3)(pos + grid.Offset) - Vector3.one * 0.5f,
                            axis, width, height, currentColor,
                            faceTowardsPositive[iu, iv], currentSubmesh);

                        for (int ku = 0; ku < width; ku++)
                        {
                            for (int kv = 0; kv < height; kv++)
                            {
                                mask[iu + ku, iv + kv] = false;
                            }
                        }
                    }
                }
            }
        }

        private bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        private void AddQuad(
            MeshData meshData,
            Vector3 pos,
            int axis,
            int width,
            int height,
            Color32 color,
            bool normalTowardsPositive,
            int submeshIndex = 0)
        {
            int baseIndex = meshData.vertices.Count;

            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            Vector3 origin = pos;

            Vector3 du = Vector3.zero;
            du[u] = width;

            Vector3 dv = Vector3.zero;
            dv[v] = height;

            Vector3 v0 = origin;
            Vector3 v1 = origin + du;
            Vector3 v2 = origin + du + dv;
            Vector3 v3 = origin + dv;

            meshData.vertices.Add(v0); meshData.colors.Add(color);
            meshData.vertices.Add(v1); meshData.colors.Add(color);
            meshData.vertices.Add(v2); meshData.colors.Add(color);
            meshData.vertices.Add(v3); meshData.colors.Add(color);

            if (normalTowardsPositive)
            {
                List<int> triangles = meshData.triangles[submeshIndex];
                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);

                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }
            else
            {
                List<int> triangles = meshData.triangles[submeshIndex];
                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);

                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 3);
                triangles.Add(baseIndex + 2);
            }
        }

        private class MeshData
        {
            public List<Vector3> vertices = new List<Vector3>();
            public List<Color32> colors = new List<Color32>();
            public List<List<int>> triangles;

            public MeshData(int submeshCount)
            {
                triangles = new List<List<int>>(submeshCount);
                for (int i = 0; i < submeshCount; i++)
                {
                    triangles.Add(new List<int>());
                }
            }
        }

        private class VoxelGrid
        {
            private Dictionary<Vector3Int, Voxel> m_voxels = new Dictionary<Vector3Int, Voxel>();
            private Vector3Int m_size;
            private Vector3Int m_offset;

            public Vector3Int Size => m_size;

            public VoxelGrid(IModel model)
            {
                int minX = int.MaxValue;
                int minY = int.MaxValue;
                int minZ = int.MaxValue;
                int maxX = int.MinValue;
                int maxY = int.MinValue;
                int maxZ = int.MinValue;

                foreach (Voxel voxel in model.Voxels)
                {
                    int x = voxel.LocalPosition.X;
                    int y = voxel.LocalPosition.Y;
                    int z = voxel.LocalPosition.Z;

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    minZ = Math.Min(minZ, z);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                    maxZ = Math.Max(maxZ, z);
                }

                m_offset = new Vector3Int(minX, minY, minZ);
                m_size = new Vector3Int(
                    maxX - minX + 1,
                    maxY - minY + 1,
                    maxZ - minZ + 1
                );

                foreach (Voxel voxel in model.Voxels)
                {
                    Vector3Int pos = new Vector3Int(
                        voxel.LocalPosition.X - m_offset.x,
                        voxel.LocalPosition.Y - m_offset.y,
                        voxel.LocalPosition.Z - m_offset.z
                    );

                    m_voxels[pos] = voxel;
                }
            }

            public bool HasVoxel(Vector3Int pos)
            {
                return m_voxels.ContainsKey(pos);
            }

            public Color32 GetColor(Vector3Int pos)
            {
                if (m_voxels.TryGetValue(pos, out Voxel voxel))
                {
                    return new Color32(voxel.Color.R, voxel.Color.G, voxel.Color.B, voxel.Color.A);
                }

                return new Color32(255, 255, 255, 255);
            }
        }

        private class RuntimeVoxelGrid
        {
            private readonly Dictionary<Vector3Int, RuntimeVoxelCell> m_voxels =
                new Dictionary<Vector3Int, RuntimeVoxelCell>();
            private readonly Vector3Int m_size;

            public Vector3Int Size => m_size;
            public Vector3Int Offset { get; }

            public RuntimeVoxelGrid(
                RuntimeVoxDocument.RuntimeModel model,
                Color32[] palette,
                IReadOnlyList<int> paletteSubmeshIndices)
            {
                int minX = int.MaxValue;
                int minY = int.MaxValue;
                int minZ = int.MaxValue;
                int maxX = int.MinValue;
                int maxY = int.MinValue;
                int maxZ = int.MinValue;

                foreach (RuntimeVoxDocument.RuntimeVoxel voxel in model.EnumerateVoxels(palette))
                {
                    Vector3Int pos = voxel.Position;

                    minX = Math.Min(minX, pos.x);
                    minY = Math.Min(minY, pos.y);
                    minZ = Math.Min(minZ, pos.z);
                    maxX = Math.Max(maxX, pos.x);
                    maxY = Math.Max(maxY, pos.y);
                    maxZ = Math.Max(maxZ, pos.z);
                }

                if (minX == int.MaxValue)
                {
                    m_size = Vector3Int.zero;
                    return;
                }

                Offset = new Vector3Int(minX, minY, minZ);
                m_size = new Vector3Int(
                    maxX - minX + 1,
                    maxY - minY + 1,
                    maxZ - minZ + 1
                );

                foreach (RuntimeVoxDocument.RuntimeVoxel voxel in model.EnumerateVoxels(palette))
                {
                    Vector3Int normalized = voxel.Position - Offset;
                    m_voxels[normalized] = new RuntimeVoxelCell(
                        voxel.Color,
                        VoxMeshBuilder.GetSubmeshIndex(
                            voxel.PaletteIndex,
                            paletteSubmeshIndices));
                }
            }

            public bool HasVoxel(Vector3Int pos)
            {
                return m_voxels.ContainsKey(pos);
            }

            public Color32 GetColor(Vector3Int pos)
            {
                if (m_voxels.TryGetValue(pos, out RuntimeVoxelCell voxel))
                {
                    return voxel.Color;
                }

                return new Color32(255, 255, 255, 255);
            }

            public int GetSubmeshIndex(Vector3Int pos)
            {
                return m_voxels.TryGetValue(pos, out RuntimeVoxelCell voxel)
                    ? voxel.SubmeshIndex
                    : 0;
            }
        }

        private readonly struct RuntimeVoxelCell
        {
            public Color32 Color { get; }
            public int SubmeshIndex { get; }

            public RuntimeVoxelCell(Color32 color, int submeshIndex)
            {
                Color = color;
                SubmeshIndex = submeshIndex;
            }
        }

        private static int GetSubmeshIndex(
            byte paletteIndex,
            IReadOnlyList<int> paletteSubmeshIndices)
        {
            int offset = paletteIndex - 1;
            return paletteSubmeshIndices != null &&
                offset >= 0 && offset < paletteSubmeshIndices.Count
                ? paletteSubmeshIndices[offset]
                : 0;
        }
    }
}
