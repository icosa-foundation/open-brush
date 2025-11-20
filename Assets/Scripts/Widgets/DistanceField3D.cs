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
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Rendering;

namespace TiltBrush
{
    /// <summary>
    /// Runtime 3D distance field generator using Jump Flood Algorithm.
    /// Generates unsigned distance fields from meshes for use with custom guides/stencils.
    /// </summary>
    public class DistanceField3D : MonoBehaviour
    {
        [Header("Volume Configuration")]
        [SerializeField] private Vector3Int m_GridSize = new Vector3Int(64, 64, 64);
        [SerializeField] private float m_VoxelizationThreshold = 0.5f;

        [Header("Compute Shader")]
        [SerializeField] private ComputeShader m_JumpFloodShader;

        // RenderTextures
        private RenderTexture m_FieldStable;  // Final published field
        private RenderTexture m_FieldA;        // Ping-pong buffer A
        private RenderTexture m_FieldB;        // Ping-pong buffer B

        // JFA state
        private bool m_JfaRunning = false;
        private int m_CurrentStep = 0;
        private RenderTexture m_JfaSrc;
        private RenderTexture m_JfaDst;

        // Rebuild state
        private bool m_NeedsRebuild = false;
        private bool m_VoxelizationDone = false;
        private bool m_Initialized = false;

        // Mesh data
        private Mesh m_TargetMesh;
        private Bounds m_MeshBounds;
        private Transform m_MeshTransform;

        // Voxelization job
        private VoxelizationJob m_VoxelJob;
        private JobHandle m_VoxelJobHandle;
        private NativeArray<Vector4> m_SeedData;
        private bool m_JobScheduled = false;

        // Shader kernel indices
        private int m_KernelJumpFlood;
        private int m_KernelClear;

        // Shader property IDs
        private static readonly int s_InputID = Shader.PropertyToID("_Input");
        private static readonly int s_OutputID = Shader.PropertyToID("_Output");
        private static readonly int s_SizeID = Shader.PropertyToID("_Size");
        private static readonly int s_StepID = Shader.PropertyToID("_Step");

        // Debug timing
        private float m_VoxelizationStartTime;
        private float m_JfaStartTime;
        private float m_TotalStartTime;
        private int m_JfaIterationCount;
        private int m_SeedCount;

        public Vector3Int GridSize => m_GridSize;
        public bool IsReady => m_Initialized && !m_NeedsRebuild && !m_JfaRunning;
        public RenderTexture DistanceField => m_FieldStable;
        public Bounds Bounds => m_MeshBounds;

        /// <summary>
        /// Initialize the distance field system
        /// </summary>
        public void Initialize(ComputeShader shader)
        {
            if (m_Initialized) return;

            m_JumpFloodShader = shader;

            if (m_JumpFloodShader == null)
            {
                Debug.LogError("DistanceField3D: JumpFlood compute shader not assigned!");
                return;
            }

            // Find kernel indices
            m_KernelJumpFlood = m_JumpFloodShader.FindKernel("JumpFlood");
            m_KernelClear = m_JumpFloodShader.FindKernel("ClearVolume");

            // Allocate RenderTextures
            AllocateTextures();

            // Clear all volumes
            ClearVolume(m_FieldA);
            ClearVolume(m_FieldB);
            m_FieldStable = m_FieldA;

            m_Initialized = true;
        }

        private void AllocateTextures()
        {
            ReleaseTextures();

            m_FieldA = CreateVolumeTexture("DF_FieldA");
            m_FieldB = CreateVolumeTexture("DF_FieldB");
        }

        private RenderTexture CreateVolumeTexture(string name)
        {
            var rt = new RenderTexture(m_GridSize.x, m_GridSize.y, 0, RenderTextureFormat.ARGBFloat)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = m_GridSize.z,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            rt.Create();
            return rt;
        }

        private void ClearVolume(RenderTexture volume)
        {
            if (volume == null || m_JumpFloodShader == null) return;

            m_JumpFloodShader.SetTexture(m_KernelClear, s_OutputID, volume);
            m_JumpFloodShader.SetInts(s_SizeID, m_GridSize.x, m_GridSize.y, m_GridSize.z);

            int gx = Mathf.CeilToInt(m_GridSize.x / 8f);
            int gy = Mathf.CeilToInt(m_GridSize.y / 8f);
            int gz = Mathf.CeilToInt(m_GridSize.z / 8f);

            m_JumpFloodShader.Dispatch(m_KernelClear, gx, gy, gz);
        }

        /// <summary>
        /// Trigger a rebuild of the distance field for the given mesh
        /// </summary>
        public void RebuildForMesh(Mesh mesh, Transform meshTransform, Bounds bounds)
        {
            if (!m_Initialized)
            {
                Debug.LogWarning("DistanceField3D: Not initialized, cannot rebuild");
                return;
            }

            m_TargetMesh = mesh;
            m_MeshTransform = meshTransform;
            m_MeshBounds = bounds;
            m_NeedsRebuild = true;
            m_VoxelizationDone = false;

            // Start overall timing
            m_TotalStartTime = Time.realtimeSinceStartup;

            StartVoxelization();
        }

        private void StartVoxelization()
        {
            if (m_TargetMesh == null)
            {
                Debug.LogWarning("DistanceField3D: No target mesh for voxelization");
                return;
            }

            // Allocate seed data
            int totalVoxels = m_GridSize.x * m_GridSize.y * m_GridSize.z;

            // Log grid configuration
            Debug.Log($"DistanceField3D: Starting voxelization - Grid: {m_GridSize.x}x{m_GridSize.y}x{m_GridSize.z} " +
                      $"({totalVoxels:N0} voxels), Mesh vertices: {m_TargetMesh.vertexCount:N0}, " +
                      $"Triangles: {m_TargetMesh.triangles.Length / 3:N0}");
            if (!m_SeedData.IsCreated || m_SeedData.Length != totalVoxels)
            {
                if (m_SeedData.IsCreated) m_SeedData.Dispose();
                m_SeedData = new NativeArray<Vector4>(totalVoxels, Allocator.Persistent);
            }

            // Get mesh data
            var vertices = m_TargetMesh.vertices;
            var triangles = m_TargetMesh.triangles;

            var nativeVertices = new NativeArray<Vector3>(vertices, Allocator.TempJob);
            var nativeTriangles = new NativeArray<int>(triangles, Allocator.TempJob);

            // Setup voxelization job
            // Note: mesh vertices and bounds are both in widget local space
            m_VoxelJob = new VoxelizationJob
            {
                vertices = nativeVertices,
                triangles = nativeTriangles,
                gridSize = m_GridSize,
                bounds = m_MeshBounds,
                threshold = m_VoxelizationThreshold,
                seeds = m_SeedData
            };

            // Schedule job
            m_VoxelizationStartTime = Time.realtimeSinceStartup;
            m_VoxelJobHandle = m_VoxelJob.Schedule(totalVoxels, 64);
            m_JobScheduled = true;
        }

        void Update()
        {
            // Check if voxelization job is done
            if (m_JobScheduled && m_VoxelJobHandle.IsCompleted)
            {
                m_VoxelJobHandle.Complete();
                m_JobScheduled = false;

                // Dispose temporary arrays
                if (m_VoxelJob.vertices.IsCreated) m_VoxelJob.vertices.Dispose();
                if (m_VoxelJob.triangles.IsCreated) m_VoxelJob.triangles.Dispose();

                // Count seeds
                m_SeedCount = 0;
                for (int i = 0; i < m_SeedData.Length; i++)
                {
                    if (m_SeedData[i].w >= 0) // Has seed if w >= 0
                    {
                        m_SeedCount++;
                    }
                }

                float voxelizationTime = (Time.realtimeSinceStartup - m_VoxelizationStartTime) * 1000f;
                Debug.Log($"DistanceField3D: Voxelization complete - {m_SeedCount:N0} seed points " +
                          $"({(m_SeedCount * 100f / m_SeedData.Length):F2}% of volume), Time: {voxelizationTime:F2}ms");

                // Upload seed data to GPU
                UploadSeedData();

                m_VoxelizationDone = true;
            }

            // Start JFA when voxelization is done
            if (m_VoxelizationDone && !m_JfaRunning)
            {
                StartJFA();
            }

            // Run one JFA step per frame
            if (m_JfaRunning)
            {
                RunJFAStep();
            }
        }

        private void UploadSeedData()
        {
            if (!m_SeedData.IsCreated) return;

            // Convert NativeArray to Color array for texture upload
            var colors = new Color[m_SeedData.Length];
            for (int i = 0; i < m_SeedData.Length; i++)
            {
                var v = m_SeedData[i];
                colors[i] = new Color(v.x, v.y, v.z, v.w);
            }

            // Upload to FieldA
            var texture3D = new Texture3D(m_GridSize.x, m_GridSize.y, m_GridSize.z,
                                          TextureFormat.RGBAFloat, false);
            texture3D.SetPixels(colors);
            texture3D.Apply();

            Graphics.CopyTexture(texture3D, m_FieldA);
            Destroy(texture3D);
        }

        private void StartJFA()
        {
            m_CurrentStep = Mathf.Max(m_GridSize.x, m_GridSize.y, m_GridSize.z) / 2;
            m_JfaSrc = m_FieldA;
            m_JfaDst = m_FieldB;
            m_JfaRunning = true;
            m_JfaIterationCount = 0;
            m_JfaStartTime = Time.realtimeSinceStartup;

            int maxDim = Mathf.Max(m_GridSize.x, m_GridSize.y, m_GridSize.z);
            int expectedIterations = Mathf.CeilToInt(Mathf.Log(maxDim, 2));
            Debug.Log($"DistanceField3D: Starting JFA - Initial step: {m_CurrentStep}, " +
                      $"Expected iterations: {expectedIterations}");
        }

        private void RunJFAStep()
        {
            if (m_CurrentStep <= 0)
            {
                // JFA complete
                m_FieldStable = m_JfaSrc;
                m_JfaRunning = false;
                m_NeedsRebuild = false;
                m_VoxelizationDone = false;

                // Log completion
                float jfaTime = (Time.realtimeSinceStartup - m_JfaStartTime) * 1000f;
                float totalTime = (Time.realtimeSinceStartup - m_TotalStartTime) * 1000f;
                Debug.Log($"DistanceField3D: JFA complete - {m_JfaIterationCount} iterations, " +
                          $"JFA time: {jfaTime:F2}ms, Total generation time: {totalTime:F2}ms " +
                          $"(~{totalTime / Time.deltaTime / 1000f:F1} frames @ {1f / Time.deltaTime:F0} FPS)");

                return;
            }

            // Execute one JFA iteration
            m_JfaIterationCount++;
            m_JumpFloodShader.SetInt(s_StepID, m_CurrentStep);
            m_JumpFloodShader.SetInts(s_SizeID, m_GridSize.x, m_GridSize.y, m_GridSize.z);
            m_JumpFloodShader.SetTexture(m_KernelJumpFlood, s_InputID, m_JfaSrc);
            m_JumpFloodShader.SetTexture(m_KernelJumpFlood, s_OutputID, m_JfaDst);

            int gx = Mathf.CeilToInt(m_GridSize.x / 8f);
            int gy = Mathf.CeilToInt(m_GridSize.y / 8f);
            int gz = Mathf.CeilToInt(m_GridSize.z / 8f);

            m_JumpFloodShader.Dispatch(m_KernelJumpFlood, gx, gy, gz);

            // Swap buffers
            var tmp = m_JfaSrc;
            m_JfaSrc = m_JfaDst;
            m_JfaDst = tmp;

            m_CurrentStep /= 2;
        }

        /// <summary>
        /// Query the distance field at a world position
        /// Returns the distance to the nearest surface, or -1 if outside bounds
        /// </summary>
        public float QueryDistance(Vector3 worldPos, out Vector3 nearestSurfacePoint)
        {
            nearestSurfacePoint = worldPos;

            if (!IsReady || m_FieldStable == null)
                return -1f;

            // Convert world position to voxel coordinates
            Vector3 localPos = worldPos - m_MeshBounds.min;
            Vector3 normalizedPos = new Vector3(
                localPos.x / m_MeshBounds.size.x,
                localPos.y / m_MeshBounds.size.y,
                localPos.z / m_MeshBounds.size.z
            );

            // Clamp to [0,1]
            if (normalizedPos.x < 0 || normalizedPos.x > 1 ||
                normalizedPos.y < 0 || normalizedPos.y > 1 ||
                normalizedPos.z < 0 || normalizedPos.z > 1)
            {
                return -1f;
            }

            // Convert to voxel indices
            int ix = Mathf.Clamp(Mathf.FloorToInt(normalizedPos.x * m_GridSize.x), 0, m_GridSize.x - 1);
            int iy = Mathf.Clamp(Mathf.FloorToInt(normalizedPos.y * m_GridSize.y), 0, m_GridSize.y - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt(normalizedPos.z * m_GridSize.z), 0, m_GridSize.z - 1);

            // Note: Actual GPU texture readback would be expensive
            // This is a simplified version - in practice, you'd want to
            // read back the texture asynchronously or use compute shader sampling
            // For now, we'll use a compute shader approach in the stencil class

            return -1f; // Placeholder - actual implementation in ModelStencil
        }

        void OnDestroy()
        {
            ReleaseTextures();

            if (m_JobScheduled)
            {
                m_VoxelJobHandle.Complete();
            }

            if (m_SeedData.IsCreated)
            {
                m_SeedData.Dispose();
            }

            if (m_VoxelJob.vertices.IsCreated) m_VoxelJob.vertices.Dispose();
            if (m_VoxelJob.triangles.IsCreated) m_VoxelJob.triangles.Dispose();
        }

        private void ReleaseTextures()
        {
            if (m_FieldA != null)
            {
                m_FieldA.Release();
                Destroy(m_FieldA);
            }
            if (m_FieldB != null)
            {
                m_FieldB.Release();
                Destroy(m_FieldB);
            }
        }

        /// <summary>
        /// Job for voxelizing a mesh into seed points
        /// Assumes vertices and bounds are in the same coordinate space (widget local space)
        /// </summary>
        private struct VoxelizationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3> vertices;
            [ReadOnly] public NativeArray<int> triangles;
            [ReadOnly] public Vector3Int gridSize;
            [ReadOnly] public Bounds bounds;
            [ReadOnly] public float threshold;

            [WriteOnly] public NativeArray<Vector4> seeds;

            public void Execute(int index)
            {
                // Convert linear index to 3D coordinates
                int z = index / (gridSize.x * gridSize.y);
                int rem = index % (gridSize.x * gridSize.y);
                int y = rem / gridSize.x;
                int x = rem % gridSize.x;

                // Calculate voxel center in local space (widget's local coordinate system)
                Vector3 normalizedPos = new Vector3(
                    (x + 0.5f) / gridSize.x,
                    (y + 0.5f) / gridSize.y,
                    (z + 0.5f) / gridSize.z
                );

                Vector3 voxelCenter = bounds.min + Vector3.Scale(normalizedPos, bounds.size);

                // Check distance to all triangles (simplified)
                bool isSeed = false;
                float minDist = float.MaxValue;

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 v0 = vertices[triangles[t]];
                    Vector3 v1 = vertices[triangles[t + 1]];
                    Vector3 v2 = vertices[triangles[t + 2]];

                    float dist = PointToTriangleDistance(voxelCenter, v0, v1, v2);
                    if (dist < minDist)
                    {
                        minDist = dist;
                    }

                    if (dist < threshold)
                    {
                        isSeed = true;
                    }
                }

                // Write result
                if (isSeed)
                {
                    seeds[index] = new Vector4(x, y, z, 0); // Seed with dist2 = 0
                }
                else
                {
                    seeds[index] = new Vector4(0, 0, 0, -1); // No seed
                }
            }

            private float PointToTriangleDistance(Vector3 point, Vector3 v0, Vector3 v1, Vector3 v2)
            {
                // Compute closest point on triangle to point
                Vector3 edge0 = v1 - v0;
                Vector3 edge1 = v2 - v0;
                Vector3 v0ToPoint = point - v0;

                float a = Vector3.Dot(edge0, edge0);
                float b = Vector3.Dot(edge0, edge1);
                float c = Vector3.Dot(edge1, edge1);
                float d = Vector3.Dot(edge0, v0ToPoint);
                float e = Vector3.Dot(edge1, v0ToPoint);

                float det = a * c - b * b;
                float s = b * e - c * d;
                float t = b * d - a * e;

                if (s + t <= det)
                {
                    if (s < 0)
                    {
                        if (t < 0)
                        {
                            // Region 4
                            s = Mathf.Clamp01(-d / a);
                            t = 0;
                        }
                        else
                        {
                            // Region 3
                            s = 0;
                            t = Mathf.Clamp01(-e / c);
                        }
                    }
                    else if (t < 0)
                    {
                        // Region 5
                        s = Mathf.Clamp01(-d / a);
                        t = 0;
                    }
                    else
                    {
                        // Region 0
                        float invDet = 1f / det;
                        s *= invDet;
                        t *= invDet;
                    }
                }
                else
                {
                    if (s < 0)
                    {
                        // Region 2
                        s = 0;
                        t = 1;
                    }
                    else if (t < 0)
                    {
                        // Region 6
                        s = 1;
                        t = 0;
                    }
                    else
                    {
                        // Region 1
                        s = Mathf.Clamp01((b + d) / (a + b));
                        t = 1 - s;
                    }
                }

                Vector3 closest = v0 + s * edge0 + t * edge1;
                return Vector3.Distance(point, closest);
            }
        }
    }
}
