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

namespace TiltBrush
{
    /// <summary>
    /// Stencil that uses imported 3D models with distance field acceleration
    /// </summary>
    public class ModelStencil : StencilWidget
    {
        [Header("Model Stencil Configuration")]
        [SerializeField] private Model m_Model;
        [SerializeField] private ComputeShader m_JumpFloodShader;

        private DistanceField3D m_DistanceField;
        private MeshCollider m_MeshCollider;
        private MeshFilter m_MeshFilter;
        private Transform m_ModelInstance;
        private bool m_DistanceFieldReady = false;

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
            m_Type = StencilType.Custom; // Using Custom type for now
            base.Awake();

            // Initialize distance field component
            m_DistanceField = gameObject.AddComponent<DistanceField3D>();
        }

        private void Start()
        {
            // Load compute shader from resources
            if (m_JumpFloodShader == null)
            {
                // Try to load from Resources folder or assign manually in inspector
                m_JumpFloodShader = Resources.Load<ComputeShader>("JumpFlood3D");

                if (m_JumpFloodShader == null)
                {
                    Debug.LogError("ModelStencil: Could not find JumpFlood3D compute shader. " +
                        "Please assign it manually or place it in a Resources folder.");
                }
            }

            if (m_DistanceField != null && m_JumpFloodShader != null)
            {
                m_DistanceField.Initialize(m_JumpFloodShader);
            }

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

            // Setup mesh collider
            SetupMeshCollider();

            // Trigger distance field rebuild
            RebuildDistanceField();
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

                for (int i = 0; i < meshFilters.Length; i++)
                {
                    combine[i].mesh = meshFilters[i].sharedMesh;
                    combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
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
        /// Rebuild the distance field for the current model
        /// </summary>
        public void RebuildDistanceField()
        {
            if (m_DistanceField == null || m_MeshCollider == null || m_MeshCollider.sharedMesh == null)
                return;

            m_DistanceFieldReady = false;

            // Get bounds
            Bounds bounds = m_MeshCollider.bounds;

            // Convert to local bounds
            bounds.center = transform.InverseTransformPoint(bounds.center);
            bounds.size = transform.InverseTransformVector(bounds.size);

            // Trigger rebuild
            m_DistanceField.RebuildForMesh(m_MeshCollider.sharedMesh, transform, bounds);
        }

        void Update()
        {
            // Check if distance field is ready
            if (!m_DistanceFieldReady && m_DistanceField != null)
            {
                m_DistanceFieldReady = m_DistanceField.IsReady;

                if (m_DistanceFieldReady)
                {
                    Debug.Log("ModelStencil: Distance field generation complete!");
                }
            }
        }

        /// <summary>
        /// Find the closest point on the stencil surface using the distance field
        /// </summary>
        public override void FindClosestPointOnSurface(Vector3 pos,
                                                       out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            // Use distance field if ready, otherwise fall back to mesh collider
            if (m_DistanceFieldReady && m_DistanceField != null)
            {
                FindClosestPointUsingDistanceField(pos, out surfacePos, out surfaceNorm);
            }
            else
            {
                FindClosestPointUsingCollider(pos, out surfacePos, out surfaceNorm);
            }
        }

        private void FindClosestPointUsingDistanceField(Vector3 pos,
                                                         out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            // Query distance field
            Vector3 nearestPoint;
            float distance = m_DistanceField.QueryDistance(pos, out nearestPoint);

            if (distance >= 0)
            {
                surfacePos = nearestPoint;

                // Estimate normal by sampling nearby points
                // This is a simple gradient-based approach
                float epsilon = 0.01f;
                Vector3 px = nearestPoint + Vector3.right * epsilon;
                Vector3 py = nearestPoint + Vector3.up * epsilon;
                Vector3 pz = nearestPoint + Vector3.forward * epsilon;

                Vector3 dummy;
                float dx = m_DistanceField.QueryDistance(px, out dummy);
                float dy = m_DistanceField.QueryDistance(py, out dummy);
                float dz = m_DistanceField.QueryDistance(pz, out dummy);

                if (dx >= 0 && dy >= 0 && dz >= 0)
                {
                    Vector3 gradient = new Vector3(
                        dx - distance,
                        dy - distance,
                        dz - distance
                    );

                    if (gradient.sqrMagnitude > 0.0001f)
                    {
                        surfaceNorm = gradient.normalized;
                    }
                    else
                    {
                        surfaceNorm = (pos - nearestPoint).normalized;
                    }
                }
                else
                {
                    surfaceNorm = (pos - nearestPoint).normalized;
                }
            }
            else
            {
                // Fall back to collider method
                FindClosestPointUsingCollider(pos, out surfacePos, out surfaceNorm);
            }
        }

        private void FindClosestPointUsingCollider(Vector3 pos,
                                                    out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            if (m_MeshCollider != null)
            {
                surfacePos = m_MeshCollider.ClosestPoint(pos);

                // Raycast to get normal
                Vector3 direction = surfacePos - pos;
                if (direction.sqrMagnitude < 0.0001f)
                {
                    // Point is very close, use outward direction
                    surfaceNorm = (pos - transform.position).normalized;
                }
                else
                {
                    RaycastHit hit;
                    if (Physics.Raycast(pos, direction, out hit, direction.magnitude * 2f))
                    {
                        surfaceNorm = hit.normal;
                    }
                    else
                    {
                        surfaceNorm = -direction.normalized;
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
            if (m_MeshCollider == null)
                return -1f;

            // Simple box-based activation for now
            Bounds bounds = m_MeshCollider.bounds;
            Vector3 closestPoint = bounds.ClosestPoint(vControllerPos);
            float distance = Vector3.Distance(vControllerPos, closestPoint);

            if (bounds.Contains(vControllerPos))
            {
                // Inside the bounds
                return 1.0f;
            }
            else
            {
                // Outside, use distance-based score
                float maxDist = bounds.extents.magnitude;
                float score = 1.0f - Mathf.Clamp01(distance / maxDist);
                return score > 0 ? score : -1f;
            }
        }

        protected override Axis GetInferredManipulationAxis(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside)
        {
            return Axis.Invalid; // Uniform scaling only for now
        }

        protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis)
        {
            // Not implemented for model stencils yet
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
            if (m_MeshCollider != null)
            {
                TrTransform colliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
                    TrTransform.FromTransform(m_MeshCollider.transform);
                Bounds bounds = new Bounds(colliderToCanvasXf * m_MeshCollider.bounds.center, Vector3.zero);

                // Add bounds corners
                Vector3 extents = m_MeshCollider.bounds.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = m_MeshCollider.bounds.center + new Vector3(
                        (i & 1) == 0 ? extents.x : -extents.x,
                        (i & 2) == 0 ? extents.y : -extents.y,
                        (i & 4) == 0 ? extents.z : -extents.z
                    );
                    bounds.Encapsulate(colliderToCanvasXf * corner);
                }

                return bounds;
            }
            return base.GetBounds_SelectionCanvasSpace();
        }

        /// <summary>
        /// Create a model stencil from an existing model
        /// </summary>
        public static ModelStencil CreateFromModel(Model model, ComputeShader jfaShader = null)
        {
            var prefab = WidgetManager.m_Instance.ModelStencilPrefab;
            if (prefab == null)
            {
                Debug.LogError("ModelStencil: No prefab assigned to WidgetManager");
                return null;
            }

            var stencil = Instantiate(prefab);
            stencil.m_JumpFloodShader = jfaShader;
            stencil.Model = model;
            stencil.transform.parent = App.Instance.m_CanvasTransform;
            stencil.Show(true, false);

            return stencil;
        }
    }
}
