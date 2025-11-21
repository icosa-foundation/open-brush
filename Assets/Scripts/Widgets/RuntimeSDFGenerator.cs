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

using UnityEngine;
using IsoMesh;

namespace TiltBrush
{
    /// <summary>
    /// Runtime generator for Signed Distance Fields from meshes
    /// Based on IsoMesh's GPU-accelerated compute shader approach
    /// </summary>
    public static class RuntimeSDFGenerator
    {
        // Compute shader property IDs
        private static class PropertyIDs
        {
            public static readonly int InputVertices = Shader.PropertyToID("_InputVertices");
            public static readonly int InputNormals = Shader.PropertyToID("_InputNormals");
            public static readonly int InputTriangles = Shader.PropertyToID("_InputTriangles");
            public static readonly int BoundsBuffer = Shader.PropertyToID("_BoundsBuffer");
            public static readonly int Samples = Shader.PropertyToID("_Samples");
            public static readonly int PackedUVs = Shader.PropertyToID("_PackedUVs");
            public static readonly int TriangleCount = Shader.PropertyToID("_TriangleCount");
            public static readonly int VertexCount = Shader.PropertyToID("_VertexCount");
            public static readonly int MinBounds = Shader.PropertyToID("_MinBounds");
            public static readonly int MaxBounds = Shader.PropertyToID("_MaxBounds");
            public static readonly int Padding = Shader.PropertyToID("_Padding");
            public static readonly int Size = Shader.PropertyToID("_Size");
            public static readonly int ModelTransformMatrix = Shader.PropertyToID("_ModelTransformMatrix");
        }

        /// <summary>
        /// Generate an SDF from a mesh
        /// </summary>
        /// <param name="mesh">Source mesh</param>
        /// <param name="size">Voxel resolution (32, 64, 128, 256)</param>
        /// <param name="padding">Padding around mesh bounds</param>
        /// <param name="computeShader">SDF compute shader</param>
        /// <returns>SDFMeshAsset created in memory</returns>
        public static SDFMeshAsset GenerateSDF(Mesh mesh, int size, float padding, ComputeShader computeShader)
        {
            if (mesh == null)
            {
                Debug.LogError("RuntimeSDFGenerator: Mesh is null");
                return null;
            }

            if (computeShader == null)
            {
                Debug.LogError("RuntimeSDFGenerator: Compute shader is null. Assign it in ModelStencil.");
                return null;
            }

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Get mesh data
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector3[] normals = mesh.normals;

            if (normals == null || normals.Length == 0)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            // Create compute buffers
            ComputeBuffer verticesBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            ComputeBuffer trianglesBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
            ComputeBuffer normalsBuffer = new ComputeBuffer(normals.Length, sizeof(float) * 3);
            ComputeBuffer boundsBuffer = new ComputeBuffer(6, sizeof(int));

            int voxelCount = size * size * size;
            ComputeBuffer samplesBuffer = new ComputeBuffer(voxelCount, sizeof(float));

            // Set buffer data
            verticesBuffer.SetData(vertices);
            trianglesBuffer.SetData(triangles);
            normalsBuffer.SetData(normals);

            // Find kernels
            int computeBoundsKernel = computeShader.FindKernel("CS_ComputeMeshBounds");
            int sampleKernel = computeShader.FindKernel("CS_SampleMeshDistances");

            // === Phase 1: Compute Bounds ===
            computeShader.SetBuffer(computeBoundsKernel, PropertyIDs.InputVertices, verticesBuffer);
            computeShader.SetBuffer(computeBoundsKernel, PropertyIDs.InputTriangles, trianglesBuffer);
            computeShader.SetBuffer(computeBoundsKernel, PropertyIDs.BoundsBuffer, boundsBuffer);
            computeShader.SetInt(PropertyIDs.TriangleCount, triangles.Length);
            computeShader.SetMatrix(PropertyIDs.ModelTransformMatrix, Matrix4x4.identity);

            int boundsThreadGroups = Mathf.CeilToInt(triangles.Length / 64f);
            computeShader.Dispatch(computeBoundsKernel, boundsThreadGroups, 1, 1);

            // Get bounds
            int[] boundsData = new int[6];
            boundsBuffer.GetData(boundsData);

            const float packingMultiplier = 1000f;
            Vector3 minBounds = new Vector3(
                boundsData[0] / packingMultiplier,
                boundsData[1] / packingMultiplier,
                boundsData[2] / packingMultiplier
            );
            Vector3 maxBounds = new Vector3(
                boundsData[3] / packingMultiplier,
                boundsData[4] / packingMultiplier,
                boundsData[5] / packingMultiplier
            );

            minBounds -= Vector3.one * padding;
            maxBounds += Vector3.one * padding;

            // === Phase 2: Sample Distance Field ===
            computeShader.SetBuffer(sampleKernel, PropertyIDs.InputVertices, verticesBuffer);
            computeShader.SetBuffer(sampleKernel, PropertyIDs.InputTriangles, trianglesBuffer);
            computeShader.SetBuffer(sampleKernel, PropertyIDs.InputNormals, normalsBuffer);
            computeShader.SetBuffer(sampleKernel, PropertyIDs.Samples, samplesBuffer);
            computeShader.SetInt(PropertyIDs.Size, size);
            computeShader.SetFloat(PropertyIDs.Padding, padding);
            computeShader.SetInt(PropertyIDs.TriangleCount, triangles.Length);
            computeShader.SetInt(PropertyIDs.VertexCount, vertices.Length);
            computeShader.SetVector(PropertyIDs.MinBounds, minBounds);
            computeShader.SetVector(PropertyIDs.MaxBounds, maxBounds);
            computeShader.SetMatrix(PropertyIDs.ModelTransformMatrix, Matrix4x4.identity);

            int threadGroups = Mathf.CeilToInt(size / 8f);
            computeShader.Dispatch(sampleKernel, threadGroups, threadGroups, threadGroups);

            // Get samples
            float[] samples = new float[voxelCount];
            samplesBuffer.GetData(samples);

            // Cleanup
            verticesBuffer.Dispose();
            trianglesBuffer.Dispose();
            normalsBuffer.Dispose();
            boundsBuffer.Dispose();
            samplesBuffer.Dispose();

            stopwatch.Stop();
            Debug.Log($"RuntimeSDFGenerator: Generated {size}³ SDF in {stopwatch.ElapsedMilliseconds}ms");

            // Create SDFMeshAsset in memory (not saved to disk)
            SDFMeshAsset asset = ScriptableObject.CreateInstance<SDFMeshAsset>();

            // Use reflection to set private fields (SDFMeshAsset.Create is Editor-only)
            var type = typeof(SDFMeshAsset);
            type.GetField("m_sourceMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(asset, mesh);
            type.GetField("m_samples", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(asset, samples);
            type.GetField("m_size", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(asset, size);
            type.GetField("m_padding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(asset, padding);
            type.GetField("m_minBounds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(asset, minBounds);
            type.GetField("m_maxBounds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(asset, maxBounds);
            type.GetField("m_tessellationLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(asset, 0);
            type.GetField("m_packedUVs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(asset, null);

            return asset;
        }

        /// <summary>
        /// Determine appropriate SDF size based on mesh complexity and platform
        /// </summary>
        public static int GetRecommendedSDFSize(int triangleCount)
        {
#if UNITY_ANDROID || UNITY_IOS || MOBILE_INPUT
            // Mobile: smaller SDFs for memory constraints
            if (triangleCount < 10000) return 32;
            if (triangleCount < 50000) return 64;
            return 64; // Cap at 64 for mobile
#else
            // Desktop: higher quality SDFs
            if (triangleCount < 5000) return 64;
            if (triangleCount < 20000) return 128;
            if (triangleCount < 100000) return 128;
            return 256; // Cap at 256 for very complex models
#endif
        }
    }
}
