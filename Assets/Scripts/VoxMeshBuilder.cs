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
        public Mesh GenerateOptimizedMesh(RuntimeVoxDocument.RuntimeModel model, Color32[] palette)
        {
            RuntimeVoxelGrid grid = new RuntimeVoxelGrid(model, palette);
            MeshData meshData = GreedyMesh(grid);
            return CreateMesh($"{model.Name}_Optimized", meshData);
        }

        public Mesh GenerateSeparateCubesMesh(RuntimeVoxDocument.RuntimeModel model, Color32[] palette)
        {
            MeshData meshData = new MeshData();

            foreach (RuntimeVoxDocument.RuntimeVoxel voxel in model.EnumerateVoxels(palette))
            {
                AddCube(meshData, voxel.Position, voxel.Color);
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
            MeshData meshData = new MeshData();

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

                AddCube(meshData, position, color);
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
            mesh.SetTriangles(meshData.triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            return mesh;
        }

        private void AddCube(MeshData meshData, Vector3 center, Color32 color)
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

                meshData.triangles.Add(vertexOffset + 0);
                meshData.triangles.Add(vertexOffset + 1);
                meshData.triangles.Add(vertexOffset + 2);

                meshData.triangles.Add(vertexOffset + 0);
                meshData.triangles.Add(vertexOffset + 2);
                meshData.triangles.Add(vertexOffset + 3);
            }
        }

        private MeshData GreedyMesh(VoxelGrid grid)
        {
            MeshData meshData = new MeshData();

            GreedyMeshAxis(grid, meshData, 0);
            GreedyMeshAxis(grid, meshData, 1);
            GreedyMeshAxis(grid, meshData, 2);

            return meshData;
        }

        private MeshData GreedyMesh(RuntimeVoxelGrid grid)
        {
            MeshData meshData = new MeshData();

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
                               ColorsEqual(colorMask[iu + width, iv], currentColor))
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
                                    !ColorsEqual(colorMask[iu + k, iv + height], currentColor))
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
                               ColorsEqual(colorMask[iu + width, iv], currentColor))
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
                                    !ColorsEqual(colorMask[iu + k, iv + height], currentColor))
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

        private bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        private void AddQuad(
            MeshData meshData,
            Vector3Int pos,
            int axis,
            int width,
            int height,
            Color32 color,
            bool normalTowardsPositive)
        {
            int baseIndex = meshData.vertices.Count;

            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            Vector3 origin = new Vector3(pos.x, pos.y, pos.z);

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
                meshData.triangles.Add(baseIndex + 0);
                meshData.triangles.Add(baseIndex + 1);
                meshData.triangles.Add(baseIndex + 2);

                meshData.triangles.Add(baseIndex + 0);
                meshData.triangles.Add(baseIndex + 2);
                meshData.triangles.Add(baseIndex + 3);
            }
            else
            {
                meshData.triangles.Add(baseIndex + 0);
                meshData.triangles.Add(baseIndex + 2);
                meshData.triangles.Add(baseIndex + 1);

                meshData.triangles.Add(baseIndex + 0);
                meshData.triangles.Add(baseIndex + 3);
                meshData.triangles.Add(baseIndex + 2);
            }
        }

        private class MeshData
        {
            public List<Vector3> vertices = new List<Vector3>();
            public List<Color32> colors = new List<Color32>();
            public List<int> triangles = new List<int>();
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
            private readonly Dictionary<Vector3Int, Color32> m_voxels = new Dictionary<Vector3Int, Color32>();
            private readonly Vector3Int m_size;

            public Vector3Int Size => m_size;

            public RuntimeVoxelGrid(RuntimeVoxDocument.RuntimeModel model, Color32[] palette)
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

                Vector3Int offset = new Vector3Int(minX, minY, minZ);
                m_size = new Vector3Int(
                    maxX - minX + 1,
                    maxY - minY + 1,
                    maxZ - minZ + 1
                );

                foreach (RuntimeVoxDocument.RuntimeVoxel voxel in model.EnumerateVoxels(palette))
                {
                    Vector3Int normalized = voxel.Position - offset;
                    m_voxels[normalized] = voxel.Color;
                }
            }

            public bool HasVoxel(Vector3Int pos)
            {
                return m_voxels.ContainsKey(pos);
            }

            public Color32 GetColor(Vector3Int pos)
            {
                if (m_voxels.TryGetValue(pos, out Color32 color))
                {
                    return color;
                }

                return new Color32(255, 255, 255, 255);
            }
        }
    }
}
