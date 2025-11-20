// Copyright 2025 The Open Brush Authors
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
using UnityEngine;
using IsoMesh;

namespace TiltBrush
{
    /// <summary>
    /// Simple mesh generator for visualizing SDFMeshAssets
    /// Uses basic marching cubes for preview generation
    /// </summary>
    public static class SDFMeshVisualizer
    {
        /// <summary>
        /// Generate a preview mesh from an SDFMeshAsset using basic marching cubes
        /// </summary>
        /// <param name="sdfAsset">The SDF asset to visualize</param>
        /// <param name="subdivisions">Number of subdivisions (higher = more detailed, but slower)</param>
        /// <returns>Generated preview mesh</returns>
        public static Mesh GeneratePreviewMesh(SDFMeshAsset sdfAsset, int subdivisions = 32)
        {
            if (sdfAsset == null)
            {
                Debug.LogError("SDFMeshVisualizer: SDFMeshAsset is null");
                return null;
            }

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector3> normals = new List<Vector3>();

            Vector3 minBounds = sdfAsset.MinBounds;
            Vector3 maxBounds = sdfAsset.MaxBounds;
            Vector3 size = maxBounds - minBounds;
            Vector3 cellSize = size / subdivisions;

            // Simple marching cubes implementation
            for (int x = 0; x < subdivisions; x++)
            {
                for (int y = 0; y < subdivisions; y++)
                {
                    for (int z = 0; z < subdivisions; z++)
                    {
                        // Get cell corners in world space
                        Vector3 p000 = minBounds + new Vector3(x * cellSize.x, y * cellSize.y, z * cellSize.z);
                        Vector3 p100 = p000 + new Vector3(cellSize.x, 0, 0);
                        Vector3 p010 = p000 + new Vector3(0, cellSize.y, 0);
                        Vector3 p110 = p000 + new Vector3(cellSize.x, cellSize.y, 0);
                        Vector3 p001 = p000 + new Vector3(0, 0, cellSize.z);
                        Vector3 p101 = p000 + new Vector3(cellSize.x, 0, cellSize.z);
                        Vector3 p011 = p000 + new Vector3(0, cellSize.y, cellSize.z);
                        Vector3 p111 = p000 + cellSize;

                        // Sample SDF at corners
                        float s000 = sdfAsset.Sample(p000);
                        float s100 = sdfAsset.Sample(p100);
                        float s010 = sdfAsset.Sample(p010);
                        float s110 = sdfAsset.Sample(p110);
                        float s001 = sdfAsset.Sample(p001);
                        float s101 = sdfAsset.Sample(p101);
                        float s011 = sdfAsset.Sample(p011);
                        float s111 = sdfAsset.Sample(p111);

                        // Create cube configuration index
                        int cubeIndex = 0;
                        if (s000 < 0) cubeIndex |= 1;
                        if (s100 < 0) cubeIndex |= 2;
                        if (s110 < 0) cubeIndex |= 4;
                        if (s010 < 0) cubeIndex |= 8;
                        if (s001 < 0) cubeIndex |= 16;
                        if (s101 < 0) cubeIndex |= 32;
                        if (s111 < 0) cubeIndex |= 64;
                        if (s011 < 0) cubeIndex |= 128;

                        // Skip if cube is entirely inside or outside
                        if (cubeIndex == 0 || cubeIndex == 255)
                            continue;

                        // Simplified: just add a single quad for any crossing cell
                        // This is faster but lower quality than full marching cubes
                        Vector3 center = (p000 + p111) * 0.5f;
                        Vector3 normal = ComputeGradient(sdfAsset, center).normalized;

                        int baseIndex = vertices.Count;
                        vertices.Add(center + normal * cellSize.magnitude * 0.25f);
                        vertices.Add(center + Vector3.Cross(normal, Vector3.up) * cellSize.magnitude * 0.25f);
                        vertices.Add(center - normal * cellSize.magnitude * 0.25f);
                        vertices.Add(center - Vector3.Cross(normal, Vector3.up) * cellSize.magnitude * 0.25f);

                        normals.Add(normal);
                        normals.Add(normal);
                        normals.Add(normal);
                        normals.Add(normal);

                        // Add two triangles for quad
                        triangles.Add(baseIndex);
                        triangles.Add(baseIndex + 1);
                        triangles.Add(baseIndex + 2);

                        triangles.Add(baseIndex + 1);
                        triangles.Add(baseIndex + 3);
                        triangles.Add(baseIndex + 2);
                    }
                }
            }

            if (vertices.Count == 0)
            {
                Debug.LogWarning("SDFMeshVisualizer: Generated mesh has no vertices");
                return null;
            }

            Mesh mesh = new Mesh
            {
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetNormals(normals);
            mesh.RecalculateBounds();

            stopwatch.Stop();
            Debug.Log($"SDFMeshVisualizer: Generated preview mesh ({vertices.Count} vertices, {triangles.Count / 3} triangles) in {stopwatch.ElapsedMilliseconds}ms");

            return mesh;
        }

        /// <summary>
        /// Compute gradient (surface normal) at a point using finite differences
        /// </summary>
        private static Vector3 ComputeGradient(SDFMeshAsset sdfAsset, Vector3 p)
        {
            Vector3 size = sdfAsset.MaxBounds - sdfAsset.MinBounds;
            float epsilon = size.magnitude / sdfAsset.Size;

            float dx = sdfAsset.Sample(p + new Vector3(epsilon, 0, 0)) - sdfAsset.Sample(p - new Vector3(epsilon, 0, 0));
            float dy = sdfAsset.Sample(p + new Vector3(0, epsilon, 0)) - sdfAsset.Sample(p - new Vector3(0, epsilon, 0));
            float dz = sdfAsset.Sample(p + new Vector3(0, 0, epsilon)) - sdfAsset.Sample(p - new Vector3(0, 0, epsilon));

            Vector3 gradient = new Vector3(dx, dy, dz);
            return gradient.sqrMagnitude > 0.0001f ? gradient : Vector3.up;
        }
    }
}
