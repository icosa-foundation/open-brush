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

using System;
using UnityEngine;
using IsoMesh;

namespace TiltBrush
{
    /// <summary>
    /// Stencil that uses imported 3D models with IsoMesh SDF integration
    /// Requires IsoMesh package: https://github.com/EmmetOT/IsoMesh
    /// </summary>
    public class ModelStencil : StencilWidget
    {
        [Header("Model Stencil Configuration")]
        [SerializeField] private Model m_Model;
        [SerializeField] private SDFMeshAsset m_SDFMeshAsset;
        [SerializeField] private ComputeShader m_SDFComputeShader;

        [Header("SDF Generation Settings")]
        [Tooltip("Padding around mesh bounds for SDF (default: 0.2)")]
        [SerializeField] private float m_SDFPadding = 0.2f;

        // MeshCollider fallback thresholds (triangle count)
        // Only generate collider if mesh is below these thresholds
        private const int MESH_COLLIDER_THRESHOLD_MOBILE = 5000;   // 5K triangles for mobile
        private const int MESH_COLLIDER_THRESHOLD_DESKTOP = 20000;  // 20K triangles for desktop

        private MeshCollider m_MeshCollider;
        private MeshFilter m_MeshFilter;
        private Transform m_ModelInstance;
        private int m_TotalTriangleCount = 0;

        public override Vector3 Extents
        {
            get
            {
                return m_Size * Vector3.one;
            }
            set
            {
                if (value.x == value.y && value.x == value.z)
                {
                    SetSignedWidgetSize(value.x);
                }
                else
                {
                    throw new ArgumentException("ModelStencil does not support non-uniform extents");
                }
            }
        }

        public Model Model
        {
            get => m_Model;
            set
            {
                m_Model = value;
                LoadModel();
            }
        }

        protected override void Awake()
        {
            m_Type = StencilType.Custom;
            base.Awake();
        }

        private void Start()
        {
            if (m_Model != null)
            {
                LoadModel();
            }
        }

        /// <summary>
        /// Load the model and setup for use as a stencil
        /// </summary>
        private void LoadModel()
        {
            if (m_Model == null || m_Model.m_ModelParent == null)
                return;

            // Clean up existing model instance
            if (m_ModelInstance != null)
            {
                Destroy(m_ModelInstance.gameObject);
            }

            // Instantiate model
            m_ModelInstance = Instantiate(m_Model.m_ModelParent);
            m_ModelInstance.gameObject.SetActive(true);
            m_ModelInstance.parent = transform;
            m_ModelInstance.localPosition = Vector3.zero;
            m_ModelInstance.localRotation = Quaternion.identity;
            m_ModelInstance.localScale = Vector3.one;

            // Generate SDF at runtime if not manually assigned
            if (m_SDFMeshAsset == null)
            {
                GenerateSDFAtRuntime();
            }

            // Count total triangles
            m_TotalTriangleCount = CountTotalTriangles();

            // Determine triangle threshold based on platform
            int threshold;
#if UNITY_ANDROID || UNITY_IOS || MOBILE_INPUT
            threshold = MESH_COLLIDER_THRESHOLD_MOBILE;
            string platform = "mobile";
#else
            threshold = MESH_COLLIDER_THRESHOLD_DESKTOP;
            string platform = "desktop";
#endif

            // Only setup mesh collider if below threshold
            if (m_TotalTriangleCount <= threshold)
            {
                SetupMeshCollider();
                Debug.Log($"ModelStencil: Created MeshCollider fallback ({m_TotalTriangleCount:N0} triangles < {threshold:N0} {platform} threshold)");
            }
            else
            {
                Debug.LogWarning($"ModelStencil: Mesh too complex for collider fallback ({m_TotalTriangleCount:N0} triangles > {threshold:N0} {platform} threshold). " +
                               "IsoMesh SDFMeshAsset REQUIRED for this model. Generate via Tools > Mesh to SDF");
            }
        }

        /// <summary>
        /// Generate SDF at runtime from the model's combined mesh
        /// </summary>
        private void GenerateSDFAtRuntime()
        {
            if (m_Model == null || m_ModelInstance == null)
                return;

            if (m_SDFComputeShader == null)
            {
                Debug.LogWarning("ModelStencil: No SDF compute shader assigned. Cannot generate runtime SDF. " +
                               "Assign Compute_SDFMesh shader in Inspector or fallback to MeshCollider.");
                return;
            }

            // Get all meshes from the model
            var meshFilters = m_ModelInstance.GetComponentsInChildren<MeshFilter>();
            if (meshFilters.Length == 0)
            {
                Debug.LogWarning("ModelStencil: No meshes found in model instance");
                return;
            }

            // Combine all meshes into one for SDF generation
            Mesh combinedMesh = CombineMeshesForSDF(meshFilters);

            if (combinedMesh == null || combinedMesh.triangles.Length == 0)
            {
                Debug.LogWarning("ModelStencil: Failed to combine meshes for SDF generation");
                return;
            }

            // Determine appropriate SDF size based on triangle count
            int triangleCount = combinedMesh.triangles.Length / 3;
            int sdfSize = RuntimeSDFGenerator.GetRecommendedSDFSize(triangleCount);

            Debug.Log($"ModelStencil: Generating {sdfSize}³ SDF for model with {triangleCount:N0} triangles...");

            // Generate SDF
            m_SDFMeshAsset = RuntimeSDFGenerator.GenerateSDF(
                combinedMesh,
                sdfSize,
                m_SDFPadding,
                m_SDFComputeShader
            );

            if (m_SDFMeshAsset != null)
            {
                Debug.Log($"ModelStencil: Successfully generated SDF for '{m_Model.HumanName}'");
            }
        }

        /// <summary>
        /// Combine all meshes into a single mesh for SDF generation
        /// </summary>
        private Mesh CombineMeshesForSDF(MeshFilter[] meshFilters)
        {
            CombineInstance[] combine = new CombineInstance[meshFilters.Length];

            for (int i = 0; i < meshFilters.Length; i++)
            {
                combine[i].mesh = meshFilters[i].sharedMesh;
                combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            }

            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Support large meshes
            combinedMesh.CombineMeshes(combine, true, true);
            combinedMesh.RecalculateNormals();
            combinedMesh.RecalculateBounds();

            return combinedMesh;
        }

        /// <summary>
        /// Count total triangles in all mesh filters
        /// </summary>
        private int CountTotalTriangles()
        {
            int total = 0;
            var meshFilters = m_ModelInstance.GetComponentsInChildren<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    total += mf.sharedMesh.triangles.Length / 3;
                }
            }
            return total;
        }

        private void SetupMeshCollider()
        {
            // Find or create mesh collider
            m_MeshCollider = GetComponentInChildren<MeshCollider>();
            if (m_MeshCollider == null)
            {
                var colliderObj = new GameObject("Collider");
                colliderObj.transform.SetParent(transform);
                colliderObj.transform.localPosition = Vector3.zero;
                colliderObj.transform.localRotation = Quaternion.identity;
                colliderObj.transform.localScale = Vector3.one;
                m_MeshCollider = colliderObj.AddComponent<MeshCollider>();
            }

            // Get combined mesh from model
            m_MeshFilter = m_MeshCollider.GetComponent<MeshFilter>();
            if (m_MeshFilter == null)
            {
                m_MeshFilter = m_MeshCollider.gameObject.AddComponent<MeshFilter>();
            }

            // Combine all meshes from the model
            var meshFilters = m_ModelInstance.GetComponentsInChildren<MeshFilter>();
            if (meshFilters.Length > 0)
            {
                CombineInstance[] combine = new CombineInstance[meshFilters.Length];

                // Get widget's world-to-local matrix to transform everything into widget local space
                Matrix4x4 widgetToLocal = transform.worldToLocalMatrix;

                for (int i = 0; i < meshFilters.Length; i++)
                {
                    combine[i].mesh = meshFilters[i].sharedMesh;
                    // Transform from mesh's world space to widget's local space
                    // This ensures the combined mesh is in the widget's local coordinate system
                    combine[i].transform = widgetToLocal * meshFilters[i].transform.localToWorldMatrix;
                }

                Mesh combinedMesh = new Mesh();
                combinedMesh.CombineMeshes(combine, true, true);

                m_MeshFilter.mesh = combinedMesh;
                m_MeshCollider.sharedMesh = combinedMesh;

                // Update collider for this widget
                m_Collider = m_MeshCollider;
            }
        }

        /// <summary>
        /// Find the closest point on the stencil surface
        /// Uses IsoMesh SDF if available, otherwise MeshCollider (low-poly only)
        /// </summary>
        public override void FindClosestPointOnSurface(Vector3 pos,
                                                       out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            // Use IsoMesh SDF if available
            if (m_SDFMeshAsset != null)
            {
                FindClosestPointUsingSDF(pos, out surfacePos, out surfaceNorm);
                return;
            }

            // Fallback to mesh collider (if available)
            if (m_MeshCollider != null)
            {
                FindClosestPointUsingCollider(pos, out surfacePos, out surfaceNorm);
            }
            else
            {
                // No collision method available - require IsoMesh
                Debug.LogWarning($"ModelStencil: No collision method available for {m_TotalTriangleCount:N0} triangle mesh. " +
                               "Generate IsoMesh SDFMeshAsset via Tools > Mesh to SDF");

                // Fallback to simple bounds-based position
                if (m_ModelInstance != null)
                {
                    surfacePos = m_ModelInstance.position;
                    surfaceNorm = (pos - m_ModelInstance.position).normalized;
                }
                else
                {
                    surfacePos = transform.position;
                    surfaceNorm = (pos - transform.position).normalized;
                }
            }
        }

        /// <summary>
        /// Find closest point using IsoMesh SDF
        /// Uses IsoMesh's Sample() method for trilinear interpolation
        /// </summary>
        private void FindClosestPointUsingSDF(Vector3 worldPos,
                                               out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            // Transform query point to widget local space
            Vector3 localPos = transform.InverseTransformPoint(worldPos);

            // Sample the distance field using IsoMesh's built-in method
            // Note: Sample() handles clamping, normalization, and trilinear interpolation
            float distance = m_SDFMeshAsset.Sample(localPos);

            // Compute gradient for surface normal using finite differences
            // This gives us the direction of steepest ascent in the distance field
            float epsilon = (m_SDFMeshAsset.MaxBounds - m_SDFMeshAsset.MinBounds).magnitude / m_SDFMeshAsset.Size;
            Vector3 gradient = new Vector3(
                m_SDFMeshAsset.Sample(localPos + new Vector3(epsilon, 0, 0)) - distance,
                m_SDFMeshAsset.Sample(localPos + new Vector3(0, epsilon, 0)) - distance,
                m_SDFMeshAsset.Sample(localPos + new Vector3(0, 0, epsilon)) - distance
            );

            // Normalize gradient to get surface normal
            Vector3 localNormal = gradient.sqrMagnitude > 0.0001f ? gradient.normalized : Vector3.up;

            // Move point to surface along gradient
            // Negative distance means inside, positive means outside
            Vector3 localSurfacePos = localPos - localNormal * distance;

            // Transform back to world space
            surfacePos = transform.TransformPoint(localSurfacePos);
            surfaceNorm = transform.TransformDirection(localNormal).normalized;
        }

        private void FindClosestPointUsingCollider(Vector3 pos,
                                                    out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            if (m_MeshCollider != null)
            {
                surfacePos = m_MeshCollider.ClosestPoint(pos);

                // Get normal by sampling nearby points (gradient-based approach)
                // More robust than raycasting, especially for complex geometry
                Vector3 toSurface = surfacePos - pos;
                float dist = toSurface.magnitude;

                if (dist < 0.0001f)
                {
                    // Point is on/very close to surface, use outward direction from center
                    surfaceNorm = (pos - transform.position).normalized;
                }
                else
                {
                    // Sample 3 nearby points to estimate the surface tangent plane
                    Vector3 offset1 = Vector3.Cross(toSurface, Vector3.up).normalized * 0.01f;
                    if (offset1.sqrMagnitude < 0.0001f)
                    {
                        offset1 = Vector3.Cross(toSurface, Vector3.right).normalized * 0.01f;
                    }
                    Vector3 offset2 = Vector3.Cross(toSurface, offset1).normalized * 0.01f;

                    Vector3 p1 = m_MeshCollider.ClosestPoint(surfacePos + offset1);
                    Vector3 p2 = m_MeshCollider.ClosestPoint(surfacePos + offset2);

                    // Compute tangent vectors
                    Vector3 tangent1 = p1 - surfacePos;
                    Vector3 tangent2 = p2 - surfacePos;

                    // Normal is perpendicular to both tangents
                    Vector3 normal = Vector3.Cross(tangent1, tangent2);

                    if (normal.sqrMagnitude > 0.0001f)
                    {
                        surfaceNorm = normal.normalized;
                        // Ensure normal points away from query point
                        if (Vector3.Dot(surfaceNorm, toSurface) < 0)
                        {
                            surfaceNorm = -surfaceNorm;
                        }
                    }
                    else
                    {
                        // Fallback: use direction from surface to query point
                        surfaceNorm = toSurface.normalized;
                    }
                }
            }
            else
            {
                // Fallback to transform position
                surfacePos = transform.position;
                surfaceNorm = (pos - transform.position).normalized;
            }
        }

        public override float GetActivationScore(
            Vector3 vControllerPos, InputManager.ControllerName name)
        {
            Bounds bounds;

            // Get bounds from collider if available, otherwise from model instance
            if (m_MeshCollider != null)
            {
                bounds = m_MeshCollider.bounds;
            }
            else if (m_ModelInstance != null)
            {
                // Calculate bounds from all renderers
                var renderers = m_ModelInstance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    return -1f;

                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }
            else
            {
                return -1f;
            }

            // Simple box-based activation
            Vector3 closestPoint = bounds.ClosestPoint(vControllerPos);
            float distance = Vector3.Distance(vControllerPos, closestPoint);

            if (bounds.Contains(vControllerPos))
            {
                return 1.0f;
            }
            else
            {
                float maxDist = bounds.extents.magnitude;
                float score = 1.0f - Mathf.Clamp01(distance / maxDist);
                return score > 0 ? score : -1f;
            }
        }

        protected override Axis GetInferredManipulationAxis(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside)
        {
            return Axis.Invalid;
        }

        protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis)
        {
            base.RegisterHighlight();
        }

        public override Axis GetScaleAxis(
            Vector3 handA, Vector3 handB,
            out Vector3 axisVec, out float extent)
        {
            Debug.Assert(m_LockedManipulationAxis != null);
            Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

            switch (axis)
            {
                case Axis.Invalid:
                    axisVec = default(Vector3);
                    extent = default(float);
                    break;
                default:
                    throw new NotImplementedException(axis.ToString());
            }

            return axis;
        }

        public override Bounds GetBounds_SelectionCanvasSpace()
        {
            Bounds worldBounds;
            Transform boundsTransform;

            if (m_MeshCollider != null)
            {
                worldBounds = m_MeshCollider.bounds;
                boundsTransform = m_MeshCollider.transform;
            }
            else if (m_ModelInstance != null)
            {
                // Calculate bounds from all renderers
                var renderers = m_ModelInstance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    return base.GetBounds_SelectionCanvasSpace();

                worldBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    worldBounds.Encapsulate(renderers[i].bounds);
                }
                boundsTransform = m_ModelInstance;
            }
            else
            {
                return base.GetBounds_SelectionCanvasSpace();
            }

            // Transform to canvas space
            TrTransform toCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
                TrTransform.FromTransform(boundsTransform);
            Bounds canvasBounds = new Bounds(toCanvasXf * worldBounds.center, Vector3.zero);

            // Add bounds corners
            Vector3 extents = worldBounds.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = worldBounds.center + new Vector3(
                    (i & 1) == 0 ? extents.x : -extents.x,
                    (i & 2) == 0 ? extents.y : -extents.y,
                    (i & 4) == 0 ? extents.z : -extents.z
                );
                canvasBounds.Encapsulate(toCanvasXf * corner);
            }

            return canvasBounds;
        }

        /// <summary>
        /// Create a model stencil from an existing model
        /// </summary>
        public static ModelStencil CreateFromModel(Model model)
        {
            var prefab = WidgetManager.m_Instance.ModelStencilPrefab;
            if (prefab == null)
            {
                Debug.LogError("ModelStencil: No prefab assigned to WidgetManager");
                return null;
            }

            var stencil = Instantiate(prefab);
            stencil.Model = model;
            stencil.transform.parent = App.Instance.m_CanvasTransform;
            stencil.Show(true, false);

            return stencil;
        }
    }
}
