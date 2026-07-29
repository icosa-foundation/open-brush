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

        [Header("Preview Mesh Settings")]
        [Tooltip("Resolution for preview mesh generation (higher = better quality)")]
        [SerializeField] private int m_PreviewResolution = 32;

        // MeshCollider fallback thresholds (triangle count)
        // Only generate collider if mesh is below these thresholds
        private const int MESH_COLLIDER_THRESHOLD_MOBILE = 5000;   // 5K triangles for mobile
        private const int MESH_COLLIDER_THRESHOLD_DESKTOP = 20000;  // 20K triangles for desktop

        private MeshCollider m_MeshCollider;
        private MeshFilter m_MeshFilter;
        private Transform m_ModelInstance;
        private int m_TotalTriangleCount = 0;
        private bool m_ModelLoaded = false;

        // IsoMesh components for preview mesh generation
        private SDFGroup m_SDFGroup;
        private SDFMesh m_SDFMeshComponent;
        private SDFGroupMeshGenerator m_MeshGenerator;

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
            // Only load if not already loaded (Model property setter may have already called LoadModel)
            if (m_Model != null && !m_ModelLoaded)
            {
                LoadModel();
            }
        }

        private void OnDestroy()
        {
            // Clean up IsoMesh components
            if (m_MeshGenerator != null && m_MeshGenerator.gameObject != null)
            {
                Destroy(m_MeshGenerator.gameObject); // Destroy the GameObject, not just the component
            }
            if (m_SDFGroup != null)
            {
                Destroy(m_SDFGroup); // SDFGroup is on main GameObject, destroy component only
            }
            if (m_SDFMeshComponent != null && m_SDFMeshComponent.gameObject != null)
            {
                Destroy(m_SDFMeshComponent.gameObject); // Destroy the GameObject
            }
        }

        /// <summary>
        /// Load the model and setup for use as a stencil
        /// </summary>
        private void LoadModel()
        {
            if (m_Model == null || m_Model.m_ModelParent == null)
                return;

            // Prevent double-loading (can be called from both property setter and Start())
            if (m_ModelLoaded)
            {
                Debug.Log("ModelStencil: LoadModel() called but model already loaded, skipping");
                return;
            }

            // Clean up existing model instance
            if (m_ModelInstance != null)
            {
                Destroy(m_ModelInstance.gameObject);
            }

            // Instantiate model
            m_ModelInstance = Instantiate(m_Model.m_ModelParent);
            if (m_ModelInstance == null)
            {
                Debug.LogError("ModelStencil: Failed to instantiate model - m_ModelParent returned null");
                return;
            }

            m_ModelInstance.gameObject.SetActive(true);
            m_ModelInstance.parent = transform;
            m_ModelInstance.localPosition = Vector3.zero;
            m_ModelInstance.localRotation = Quaternion.identity;
            m_ModelInstance.localScale = Vector3.one;

            // Count total triangles BEFORE generating SDF (which destroys the model instance)
            m_TotalTriangleCount = CountTotalTriangles();
            Debug.Log($"ModelStencil: Model has {m_TotalTriangleCount:N0} triangles");

            // Generate SDF at runtime if not manually assigned
            if (m_SDFMeshAsset == null)
            {
                GenerateSDFAtRuntime();
            }

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
            // if (m_TotalTriangleCount <= threshold)
            // {
            //     SetupMeshCollider();
            // }

            // Mark model as loaded to prevent double-loading
            m_ModelLoaded = true;
            Debug.Log("ModelStencil: Model loading complete");
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

                // Generate and assign preview mesh from SDF
                GenerateSDFPreviewMesh();
            }
        }

        /// <summary>
        /// Generate a preview mesh from the SDF using IsoMesh's mesh generator
        /// </summary>
        private void GenerateSDFPreviewMesh()
        {
            if (m_SDFMeshAsset == null)
            {
                Debug.LogError("ModelStencil: Cannot generate preview - SDFMeshAsset is null");
                return;
            }

            Debug.Log($"ModelStencil: Setting up IsoMesh preview mesh generation ({m_PreviewResolution}³ resolution)...");

            // Destroy the original model instance completely
            // Use DestroyImmediate since we're in runtime setup, not a Unity lifecycle method
            if (m_ModelInstance != null)
            {
                Debug.Log($"ModelStencil: Destroying original model instance: {m_ModelInstance.name} (GameObject: {m_ModelInstance.gameObject.name})");
                GameObject toDestroy = m_ModelInstance.gameObject;
                Debug.Log($"ModelStencil: About to destroy GameObject at path: {GetGameObjectPath(toDestroy)}");
                Debug.Log($"ModelStencil: GameObject has {toDestroy.transform.childCount} children");

                // Destroy all children first to ensure clean destruction
                while (toDestroy.transform.childCount > 0)
                {
                    Transform child = toDestroy.transform.GetChild(0);
                    Debug.Log($"ModelStencil: Destroying child: {child.name}");
                    DestroyImmediate(child.gameObject);
                }

                Debug.Log($"ModelStencil: All children destroyed, now destroying parent");
                DestroyImmediate(toDestroy);
                m_ModelInstance = null;
                Debug.Log("ModelStencil: Model instance destroyed immediately");

                // Verify destruction
                Transform[] allChildren = transform.GetComponentsInChildren<Transform>(true);
                Debug.Log($"ModelStencil: After destruction, StencilModel has {allChildren.Length - 1} children/descendants");
                foreach (Transform child in allChildren)
                {
                    if (child != transform)
                    {
                        Debug.Log($"ModelStencil: Remaining child: {child.name} at path: {GetGameObjectPath(child.gameObject)}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("ModelStencil: m_ModelInstance is null, cannot destroy original model");
            }

            // Create IsoMesh component hierarchy
            // This follows IsoMesh's architecture: SDFGroup -> SDFMesh -> SDFGroupMeshGenerator

            // 1. Create SDFGroup component (manages the SDF hierarchy)
            Debug.Log("ModelStencil: Creating SDFGroup component");
            m_SDFGroup = gameObject.AddComponent<SDFGroup>();

            if (m_SDFGroup == null)
            {
                Debug.LogError("ModelStencil: Failed to create SDFGroup component");
                return;
            }

            // 2. Create a child GameObject for the SDFMesh component
            Debug.Log("ModelStencil: Creating SDFMesh child object");
            GameObject sdfMeshObject = new GameObject("SDF Mesh");
            sdfMeshObject.transform.SetParent(transform);
            sdfMeshObject.transform.localPosition = Vector3.zero;
            sdfMeshObject.transform.localRotation = Quaternion.identity;
            sdfMeshObject.transform.localScale = Vector3.one;

            m_SDFMeshComponent = sdfMeshObject.AddComponent<SDFMesh>();
            if (m_SDFMeshComponent == null)
            {
                Debug.LogError("ModelStencil: Failed to create SDFMesh component");
                return;
            }

            // Assign the SDFMeshAsset using reflection (Asset property is read-only)
            Debug.Log($"ModelStencil: Assigning SDFMeshAsset (Size: {m_SDFMeshAsset.Size}, Bounds: {m_SDFMeshAsset.MinBounds} to {m_SDFMeshAsset.MaxBounds})");
            var assetField = typeof(SDFMesh).GetField("m_asset",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (assetField == null)
            {
                Debug.LogError("ModelStencil: Failed to find 'm_asset' field in SDFMesh via reflection");
                return;
            }

            assetField.SetValue(m_SDFMeshComponent, m_SDFMeshAsset);
            Debug.Log($"ModelStencil: SDFMeshAsset assigned, Asset property = {m_SDFMeshComponent.Asset != null}");

            // 3. Create a child GameObject for SDFGroupMeshGenerator component
            // IMPORTANT: SDFGroupMeshGenerator must be on its own GameObject (not the parent)
            // It will auto-create a child GameObject with the generated mesh
            Debug.Log("ModelStencil: Creating SDFGroupMeshGenerator child object");
            GameObject meshGeneratorObject = new GameObject("SDF Mesh Generator");
            meshGeneratorObject.transform.SetParent(transform);
            meshGeneratorObject.transform.localPosition = Vector3.zero;
            meshGeneratorObject.transform.localRotation = Quaternion.identity;
            meshGeneratorObject.transform.localScale = Vector3.one;

            m_MeshGenerator = meshGeneratorObject.AddComponent<SDFGroupMeshGenerator>();

            if (m_MeshGenerator == null)
            {
                Debug.LogError("ModelStencil: Failed to create SDFGroupMeshGenerator component");
                return;
            }

            // Set the SDFGroup reference on the mesh generator
            var groupField = typeof(SDFGroupMeshGenerator).GetField("m_group",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (groupField != null)
            {
                groupField.SetValue(m_MeshGenerator, m_SDFGroup);
                Debug.Log("ModelStencil: Set SDFGroup reference on mesh generator");
            }
            else
            {
                Debug.LogWarning("ModelStencil: Could not find 'm_group' field in SDFGroupMeshGenerator");
            }

            // Configure mesh generator settings using reflection (properties are read-only)
            Debug.Log("ModelStencil: Configuring IsoMesh settings via reflection");

            // VoxelSettings: Set cell count (SamplesPerSide = CellCount + 1)
            var voxelSettings = m_MeshGenerator.VoxelSettings;
            var cellCountField = typeof(VoxelSettings).GetField("m_cellCount",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cellCountField != null)
            {
                cellCountField.SetValue(voxelSettings, m_PreviewResolution - 1);
                Debug.Log($"ModelStencil: Set cell count to {m_PreviewResolution - 1} (SamplesPerSide will be {m_PreviewResolution})");
            }
            else
            {
                Debug.LogError("ModelStencil: Failed to find 'm_cellCount' field in VoxelSettings");
            }

            // MainSettings: Set output mode and auto-update
            var mainSettings = m_MeshGenerator.MainSettings;
            var outputModeField = typeof(MainSettings).GetField("m_outputMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (outputModeField != null)
            {
                outputModeField.SetValue(mainSettings, OutputMode.MeshFilter);
                Debug.Log("ModelStencil: Set output mode to MeshFilter");
            }
            else
            {
                Debug.LogError("ModelStencil: Failed to find 'm_outputMode' field in MainSettings");
            }
            mainSettings.AutoUpdate = true;
            Debug.Log("ModelStencil: Set AutoUpdate to true");

            // AlgorithmSettings: Set isosurface extraction type
            var algorithmSettings = m_MeshGenerator.AlgorithmSettings;
            var extractionTypeField = typeof(AlgorithmSettings).GetField("m_isosurfaceExtractionType",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (extractionTypeField != null)
            {
                extractionTypeField.SetValue(algorithmSettings, IsosurfaceExtractionType.SurfaceNets);
                Debug.Log("ModelStencil: Set extraction type to SurfaceNets");
            }
            else
            {
                Debug.LogError("ModelStencil: Failed to find 'm_isosurfaceExtractionType' field in AlgorithmSettings");
            }

            // Notify the mesh generator about setting changes
            Debug.Log("ModelStencil: Notifying mesh generator of setting changes");
            m_MeshGenerator.OnCellCountChanged();
            m_MeshGenerator.OnOutputModeChanged();
            m_MeshGenerator.OnIsosurfaceExtractionTypeChanged();

            // Manually initialize the group and mesh (since we're doing this at runtime)
            Debug.Log($"ModelStencil: Registering SDFMesh with SDFGroup (Group.IsReady = {m_SDFGroup.IsReady})");
            m_SDFGroup.Register(m_SDFMeshComponent);
            Debug.Log($"ModelStencil: After registration - Group.IsReady = {m_SDFGroup.IsReady}, IsRegistered = {m_SDFGroup.IsRegistered(m_SDFMeshComponent)}");

            // Start coroutine to trigger mesh generation after components are initialized
            // IsoMesh components need to go through Unity's lifecycle (OnEnable, Start) before UpdateMesh() works
            StartCoroutine(TriggerMeshGenerationCoroutine());

            Debug.Log($"ModelStencil: IsoMesh preview mesh generation setup complete");
        }

        /// <summary>
        /// Helper to get full path of a GameObject in the hierarchy
        /// </summary>
        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform current = obj.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        /// <summary>
        /// Coroutine to trigger mesh generation after IsoMesh components are initialized
        /// </summary>
        private System.Collections.IEnumerator TriggerMeshGenerationCoroutine()
        {
            // Wait a few frames for IsoMesh components to complete their initialization
            // (OnEnable, Start, etc. need to run first)
            Debug.Log("ModelStencil: Waiting for IsoMesh components to initialize...");
            yield return null; // Wait 1 frame
            yield return null; // Wait another frame to be safe

            Debug.Log("ModelStencil: Components initialized, calling UpdateMesh()...");

            if (m_MeshGenerator != null && m_SDFGroup != null && m_SDFGroup.IsReady)
            {
                // Check if the generator is initialized (private field check via reflection)
                var initializedField = typeof(SDFGroupMeshGenerator).GetField("m_initialized",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                bool isInitialized = initializedField != null && (bool)initializedField.GetValue(m_MeshGenerator);

                Debug.Log($"ModelStencil: Generator state - Initialized: {isInitialized}, Group.IsReady: {m_SDFGroup.IsReady}, Group.IsEmpty: {m_SDFGroup.IsEmpty}");

                // Now trigger mesh generation
                m_MeshGenerator.UpdateMesh();
                Debug.Log("ModelStencil: UpdateMesh() called");

                // Start checking coroutine to see if mesh was generated
                StartCoroutine(CheckMeshGenerationCoroutine());
            }
            else
            {
                Debug.LogError($"ModelStencil: Cannot call UpdateMesh() - MeshGenerator: {m_MeshGenerator != null}, SDFGroup: {m_SDFGroup != null}, Group.IsReady: {m_SDFGroup?.IsReady ?? false}");
            }
        }

        /// <summary>
        /// Coroutine to check if mesh was generated after frame delays
        /// (IsoMesh might generate meshes asynchronously over multiple frames)
        /// </summary>
        private System.Collections.IEnumerator CheckMeshGenerationCoroutine()
        {
            // Check after 1, 5, and 30 frames to see if IsoMesh generates the mesh
            int[] frameChecks = { 1, 5, 30 };

            foreach (int frameCount in frameChecks)
            {
                // Wait the specified number of frames
                for (int i = 0; i < frameCount; i++)
                {
                    yield return null;
                }

                Debug.Log($"ModelStencil: [Frame+{frameCount}] Checking if mesh was generated...");

                // Check if MeshFilter was created on the main object
                MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
                MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
                Debug.Log($"ModelStencil: [Frame+{frameCount}] MeshFilter on main: {meshFilter != null}, MeshRenderer on main: {meshRenderer != null}");
                if (meshFilter != null)
                {
                    Debug.Log($"ModelStencil: [Frame+{frameCount}] MeshFilter.mesh: {meshFilter.mesh != null}, vertices: {meshFilter.mesh?.vertexCount ?? 0}");
                    if (meshFilter.mesh != null && meshRenderer != null)
                    {
                        Debug.Log($"ModelStencil: [Frame+{frameCount}] Material: {meshRenderer.sharedMaterial?.name ?? "null"}, Enabled: {meshRenderer.enabled}");
                        yield break; // Success! Stop checking
                    }
                }

                // Check for generated mesh in children
                MeshFilter[] childFilters = GetComponentsInChildren<MeshFilter>();
                Debug.Log($"ModelStencil: [Frame+{frameCount}] Found {childFilters.Length} MeshFilters in hierarchy");

                bool foundIsoMeshGenerated = false;
                foreach (var filter in childFilters)
                {
                    if (filter.gameObject != gameObject) // Skip main object
                    {
                        string path = GetGameObjectPath(filter.gameObject);
                        // Check if this is the IsoMesh-generated mesh (not the base widget meshes)
                        if (path.Contains("SDF Mesh") || filter.gameObject.name.Contains("Generated Mesh"))
                        {
                            Debug.Log($"ModelStencil: [Frame+{frameCount}] IsoMesh child MeshFilter on '{filter.gameObject.name}' at path: {path}");
                            Debug.Log($"ModelStencil: [Frame+{frameCount}]   - Mesh: {filter.mesh != null}, Vertices: {filter.mesh?.vertexCount ?? 0}, Active: {filter.gameObject.activeSelf}");
                            MeshRenderer childRenderer = filter.GetComponent<MeshRenderer>();
                            if (childRenderer != null)
                            {
                                Debug.Log($"ModelStencil: [Frame+{frameCount}]   - Renderer enabled: {childRenderer.enabled}, Material: {childRenderer.sharedMaterial?.name ?? "null"}");
                            }
                            if (filter.mesh != null && filter.mesh.vertexCount > 0)
                            {
                                foundIsoMeshGenerated = true;
                            }
                        }
                    }
                }

                // If we found a generated mesh, stop checking
                if (foundIsoMeshGenerated)
                {
                    Debug.Log($"ModelStencil: [Frame+{frameCount}] IsoMesh successfully generated mesh!");
                    yield break;
                }

                // If this was the last check and no mesh was generated, log an error
                if (frameCount == frameChecks[frameChecks.Length - 1])
                {
                    Debug.LogError($"ModelStencil: [Frame+{frameCount}] IsoMesh did not generate any mesh after {frameCount} frames! This likely means UpdateMesh() doesn't work at runtime or requires different API.");
                }
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
            if (m_ModelInstance == null)
            {
                Debug.LogError("ModelStencil: Cannot count triangles - model instance is null");
                return 0;
            }

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
        /// </summary>
        public override void FindClosestPointOnSurface(Vector3 pos,
                                                       out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            if (m_MeshCollider != null)
            {
                FindClosestPointUsingCollider(pos, out surfacePos, out surfaceNorm);
            }
            else if (m_SDFMeshAsset != null)
            {
                FindClosestPointUsingSDF(pos, out surfacePos, out surfaceNorm);
            }
            else
            {
                // Should never happen
                Debug.LogWarning("No SDF or MeshCollider found");
                surfacePos = transform.position;
                surfaceNorm = (pos - transform.position).normalized;
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
