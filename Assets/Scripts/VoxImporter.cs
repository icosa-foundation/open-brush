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
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using VoxReader;
using VoxReader.Interfaces;
using Vector3 = UnityEngine.Vector3;

namespace TiltBrush
{
    public class VoxImporter
    {
        private readonly Material m_standardMaterial;
        private readonly string m_path;
        private readonly string m_dir;
        private readonly List<string> m_warnings = new List<string>();
        private readonly ImportMaterialCollector m_collector;

        // Mesh generation mode
        public enum MeshMode
        {
            Optimized,      // Greedy meshing with face culling
            SeparateCubes   // Individual cube per voxel
        }

        private MeshMode m_meshMode = MeshMode.Optimized;

        public VoxImporter(string path, MeshMode meshMode = MeshMode.Optimized)
        {
            m_path = path;
            m_dir = Path.GetDirectoryName(path);
            m_collector = new ImportMaterialCollector(m_dir, m_path);
            m_standardMaterial = ModelCatalog.m_Instance.m_VoxLoaderStandardMaterial;
            m_meshMode = meshMode;
        }

        public (GameObject, List<string> warnings, ImportMaterialCollector) Import()
        {
            try
            {
                // Read the .vox file using VoxReader library
                IVoxFile voxFile = VoxReader.VoxReader.Read(m_path);

                // Create parent GameObject
                GameObject parent = new GameObject(Path.GetFileNameWithoutExtension(m_path));

                // Process each model in the vox file
                IModel[] models = voxFile.Models;

                if (models.Length == 0)
                {
                    m_warnings.Add("VOX file contains no models");
                    return (parent, m_warnings.Distinct().ToList(), m_collector);
                }

                for (int i = 0; i < models.Length; i++)
                {
                    IModel model = models[i];

                    if (model.Voxels.Length == 0)
                    {
                        m_warnings.Add($"Model {i} ({model.Name}) contains no voxels");
                        continue;
                    }

                    GameObject modelObject = new GameObject($"Model_{i}_{model.Name}");
                    modelObject.transform.SetParent(parent.transform, false);

                    // Generate mesh based on mode
                    Mesh mesh = m_meshMode == MeshMode.Optimized
                        ? GenerateOptimizedMesh(model)
                        : GenerateSeparateCubesMesh(model);

                    if (mesh != null)
                    {
                        var mf = modelObject.AddComponent<MeshFilter>();
                        mf.mesh = mesh;

                        var mr = modelObject.AddComponent<MeshRenderer>();
                        mr.material = m_standardMaterial;

                        var collider = modelObject.AddComponent<BoxCollider>();
                        collider.size = mesh.bounds.size;
                        collider.center = mesh.bounds.center;
                    }
                }

                return (parent, m_warnings.Distinct().ToList(), m_collector);
            }
            catch (Exception ex)
            {
                m_warnings.Add($"Failed to import VOX file: {ex.Message}");
                Debug.LogException(ex);
                GameObject errorObject = new GameObject("VOX_Import_Error");
                return (errorObject, m_warnings.Distinct().ToList(), m_collector);
            }
        }

        private Mesh GenerateOptimizedMesh(IModel model)
        {
            try
            {
                // Create a 3D grid to store voxel data for efficient neighbor lookup
                VoxelGrid grid = new VoxelGrid(model);

                // Generate mesh with greedy meshing and face culling
                MeshData meshData = GreedyMesh(grid);

                // Create Unity mesh
                Mesh mesh = new Mesh();
                mesh.name = $"{model.Name}_Optimized";

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
            catch (Exception ex)
            {
                m_warnings.Add($"Failed to generate optimized mesh for model {model.Name}: {ex.Message}");
                Debug.LogException(ex);
                return null;
            }
        }

        private Mesh GenerateSeparateCubesMesh(IModel model)
        {
            try
            {
                MeshData meshData = new MeshData();

                // Generate a cube for each voxel
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

                // Create Unity mesh
                Mesh mesh = new Mesh();
                mesh.name = $"{model.Name}_Cubes";

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
            catch (Exception ex)
            {
                m_warnings.Add($"Failed to generate separate cubes mesh for model {model.Name}: {ex.Message}");
                Debug.LogException(ex);
                return null;
            }
        }

        private void AddCube(MeshData meshData, Vector3 center, Color32 color)
        {
            int baseIndex = meshData.vertices.Count;
            float size = 1.0f;
            float half = size * 0.5f;

            // Define 8 corners of the cube
            Vector3[] corners = new Vector3[8]
            {
                center + new Vector3(-half, -half, -half),  // 0: left-bottom-back
                center + new Vector3( half, -half, -half),  // 1: right-bottom-back
                center + new Vector3( half,  half, -half),  // 2: right-top-back
                center + new Vector3(-half,  half, -half),  // 3: left-top-back
                center + new Vector3(-half, -half,  half),  // 4: left-bottom-front
                center + new Vector3( half, -half,  half),  // 5: right-bottom-front
                center + new Vector3( half,  half,  half),  // 6: right-top-front
                center + new Vector3(-half,  half,  half)   // 7: left-top-front
            };

            // Add all 24 vertices (4 per face, 6 faces)
            // Front face
            meshData.vertices.Add(corners[4]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[5]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[6]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[7]); meshData.colors.Add(color);

            // Back face
            meshData.vertices.Add(corners[1]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[0]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[3]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[2]); meshData.colors.Add(color);

            // Top face
            meshData.vertices.Add(corners[7]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[6]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[2]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[3]); meshData.colors.Add(color);

            // Bottom face
            meshData.vertices.Add(corners[0]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[1]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[5]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[4]); meshData.colors.Add(color);

            // Right face
            meshData.vertices.Add(corners[5]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[1]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[2]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[6]); meshData.colors.Add(color);

            // Left face
            meshData.vertices.Add(corners[0]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[4]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[7]); meshData.colors.Add(color);
            meshData.vertices.Add(corners[3]); meshData.colors.Add(color);

            // Add triangles (2 per face, 12 total)
            for (int i = 0; i < 6; i++)
            {
                int vertexOffset = baseIndex + i * 4;

                // First triangle
                meshData.triangles.Add(vertexOffset + 0);
                meshData.triangles.Add(vertexOffset + 1);
                meshData.triangles.Add(vertexOffset + 2);

                // Second triangle
                meshData.triangles.Add(vertexOffset + 0);
                meshData.triangles.Add(vertexOffset + 2);
                meshData.triangles.Add(vertexOffset + 3);
            }
        }

        private MeshData GreedyMesh(VoxelGrid grid)
        {
            MeshData meshData = new MeshData();

            // Process each axis/direction
            // X-axis faces (left/right)
            GreedyMeshAxis(grid, meshData, 0);

            // Y-axis faces (bottom/top)
            GreedyMeshAxis(grid, meshData, 1);

            // Z-axis faces (back/front)
            GreedyMeshAxis(grid, meshData, 2);

            return meshData;
        }

        private void GreedyMeshAxis(VoxelGrid grid, MeshData meshData, int axis)
        {
            // axis: 0 = X, 1 = Y, 2 = Z
            int u = (axis + 1) % 3;  // First perpendicular axis
            int v = (axis + 2) % 3;  // Second perpendicular axis

            Vector3Int size = grid.Size;
            int[] dims = { size.x, size.y, size.z };

            Vector3Int pos = Vector3Int.zero;
            bool[,] mask = new bool[dims[u], dims[v]];
            Color32[,] colorMask = new Color32[dims[u], dims[v]];

            // Sweep through each slice along the axis
            for (pos[axis] = 0; pos[axis] <= dims[axis]; pos[axis]++)
            {
                // Reset mask
                for (int iu = 0; iu < dims[u]; iu++)
                {
                    for (int iv = 0; iv < dims[v]; iv++)
                    {
                        mask[iu, iv] = false;
                    }
                }

                // Build mask for this slice
                for (pos[u] = 0; pos[u] < dims[u]; pos[u]++)
                {
                    for (pos[v] = 0; pos[v] < dims[v]; pos[v]++)
                    {
                        Vector3Int checkPos = pos;
                        Vector3Int neighborPos = pos;
                        neighborPos[axis] = pos[axis] - 1;

                        // Check if we need a face here (boundary between solid and empty)
                        bool current = pos[axis] < dims[axis] && grid.HasVoxel(checkPos);
                        bool neighbor = pos[axis] > 0 && grid.HasVoxel(neighborPos);

                        if (current != neighbor)
                        {
                            mask[pos[u], pos[v]] = true;
                            colorMask[pos[u], pos[v]] = current
                                ? grid.GetColor(checkPos)
                                : grid.GetColor(neighborPos);
                        }
                    }
                }

                // Generate mesh from mask using greedy meshing
                for (int iu = 0; iu < dims[u]; iu++)
                {
                    for (int iv = 0; iv < dims[v]; iv++)
                    {
                        if (!mask[iu, iv]) continue;

                        Color32 currentColor = colorMask[iu, iv];

                        // Find the width of the quad
                        int width = 1;
                        while (iu + width < dims[u] &&
                               mask[iu + width, iv] &&
                               ColorsEqual(colorMask[iu + width, iv], currentColor))
                        {
                            width++;
                        }

                        // Find the height of the quad
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
                            if (!done) height++;
                        }

                        // Add quad to mesh
                        pos[u] = iu;
                        pos[v] = iv;
                        AddQuad(meshData, pos, axis, width, height, currentColor);

                        // Clear mask for merged area
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

        private void AddQuad(MeshData meshData, Vector3Int pos, int axis, int width, int height, Color32 color)
        {
            int baseIndex = meshData.vertices.Count;

            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            Vector3 origin = new Vector3(pos.x, pos.y, pos.z);

            Vector3 du = Vector3.zero;
            du[u] = width;

            Vector3 dv = Vector3.zero;
            dv[v] = height;

            // Determine if we need to flip the face based on axis direction
            bool flip = pos[axis] < 0 || (pos[axis] == 0 && axis == 0);

            // Create quad vertices
            Vector3 v0 = origin;
            Vector3 v1 = origin + du;
            Vector3 v2 = origin + du + dv;
            Vector3 v3 = origin + dv;

            if (flip)
            {
                meshData.vertices.Add(v0); meshData.colors.Add(color);
                meshData.vertices.Add(v3); meshData.colors.Add(color);
                meshData.vertices.Add(v2); meshData.colors.Add(color);
                meshData.vertices.Add(v1); meshData.colors.Add(color);

                meshData.triangles.Add(baseIndex + 0);
                meshData.triangles.Add(baseIndex + 1);
                meshData.triangles.Add(baseIndex + 2);

                meshData.triangles.Add(baseIndex + 0);
                meshData.triangles.Add(baseIndex + 2);
                meshData.triangles.Add(baseIndex + 3);
            }
            else
            {
                meshData.vertices.Add(v0); meshData.colors.Add(color);
                meshData.vertices.Add(v1); meshData.colors.Add(color);
                meshData.vertices.Add(v2); meshData.colors.Add(color);
                meshData.vertices.Add(v3); meshData.colors.Add(color);

                meshData.triangles.Add(baseIndex + 0);
                meshData.triangles.Add(baseIndex + 2);
                meshData.triangles.Add(baseIndex + 1);

                meshData.triangles.Add(baseIndex + 0);
                meshData.triangles.Add(baseIndex + 3);
                meshData.triangles.Add(baseIndex + 2);
            }
        }

        // Helper class to store mesh data
        private class MeshData
        {
            public List<Vector3> vertices = new List<Vector3>();
            public List<Color32> colors = new List<Color32>();
            public List<int> triangles = new List<int>();
        }

        // Helper class for voxel grid lookup
        private class VoxelGrid
        {
            private Dictionary<Vector3Int, Voxel> m_voxels = new Dictionary<Vector3Int, Voxel>();
            private Vector3Int m_size;

            public Vector3Int Size => m_size;

            public VoxelGrid(IModel model)
            {
                // Build voxel dictionary and calculate bounds
                int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;

                foreach (Voxel voxel in model.Voxels)
                {
                    Vector3Int pos = new Vector3Int(
                        voxel.LocalPosition.X,
                        voxel.LocalPosition.Y,
                        voxel.LocalPosition.Z
                    );

                    m_voxels[pos] = voxel;

                    minX = Math.Min(minX, pos.x);
                    minY = Math.Min(minY, pos.y);
                    minZ = Math.Min(minZ, pos.z);
                    maxX = Math.Max(maxX, pos.x);
                    maxY = Math.Max(maxY, pos.y);
                    maxZ = Math.Max(maxZ, pos.z);
                }

                m_size = new Vector3Int(
                    maxX - minX + 1,
                    maxY - minY + 1,
                    maxZ - minZ + 1
                );
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
    }
}
