// Copyright 2020 The Tilt Brush Authors
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
using System.Linq;
using IsoMesh;
using UnityEngine;

namespace TiltBrush
{
    public class SdfStencil : StencilWidget
    {
        private const double k_VisualRefreshIntervalSeconds = 0.1;
        private const double k_FinalVisualRefreshDelaySeconds = 0.2;

        private SDFGroup m_SdfManager;
        private SDFGroupMeshGenerator m_MeshGenerator;
        private int m_RequestedVisualRevision;
        private int m_StartedVisualRevision;
        private int m_CompletedVisualRevision;
        private int m_FinalizedVisualRevision;
        private bool m_StartedVisualGenerationIsFinal;
        private double m_LastVisualMutationTime;
        private double m_LastVisualGenerationStartTime = double.NegativeInfinity;

        internal struct PrimitiveDefinition
        {
            public SDFPrimitiveType Type;
            public Vector4 Geometry;
            public TrTransform Transform;
            public SDFCombineType Operation;
            public float Blend;
            public bool Flip;

            public PrimitiveDefinition(
                SDFPrimitiveType type, Vector4 geometry, TrTransform transform,
                SDFCombineType operation, float blend, bool flip)
            {
                Type = type;
                Geometry = geometry;
                Transform = transform;
                Operation = operation;
                Blend = blend;
                Flip = flip;
            }
        }

        public int PrimitiveCount => GetPrimitives().Count;

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
                    throw new ArgumentException("SDF Stencil does not support non-uniform extents");
                }
            }
        }

        protected override void Awake()
        {
            m_Type = StencilType.Custom;
            base.Awake();
            m_SdfManager = Instantiate(
                WidgetManager.m_Instance.m_SDFManager, transform, false);
            var sdfTransform = m_SdfManager.transform;
            sdfTransform.localPosition = Vector3.zero;
            sdfTransform.localRotation = Quaternion.identity;
            sdfTransform.localScale = Vector3.one;

            RegisterGeneratedMeshRenderer();
            RequestVisualRefresh();
        }

        private void RegisterGeneratedMeshRenderer()
        {
            m_MeshGenerator =
                m_SdfManager.GetComponentInChildren<SDFGroupMeshGenerator>(true);
            if (m_MeshGenerator == null)
            {
                Debug.LogWarning(
                    "SDFGuideSetup: SDF manager has no mesh generator", this);
                return;
            }

            m_MeshGenerator.MainSettings.AutoUpdate = false;
            m_MeshGenerator.MeshGenerationFinished += OnMeshGenerationFinished;

            MeshRenderer generatedRenderer = m_MeshGenerator.MeshRenderer;
            MeshFilter generatedFilter = generatedRenderer.GetComponent<MeshFilter>();
            if (generatedFilter == null)
            {
                Debug.LogWarning(
                    "SDFGuideSetup: Generated renderer has no mesh filter", this);
                return;
            }

            m_TintableMeshes = new Renderer[] { generatedRenderer };
            m_HighlightMeshFilters = new MeshFilter[] { generatedFilter };

            // The widget starts invisible; OnShow refreshes this using the current stencil state.
            generatedRenderer.enabled = false;
            HierarchyUtils.RecursivelySetMaterialBatchID(m_SdfManager.transform, m_BatchId);
            RestoreGameObjectLayer(App.Scene.MainCanvas.gameObject.layer);
            UpdateMaterialScale();
        }

        public static SDFPrimitiveType ParsePrimitiveType(string type)
        {
            switch (type?.Trim().ToLowerInvariant())
            {
                case "sphere": return SDFPrimitiveType.Sphere;
                case "torus": return SDFPrimitiveType.Torus;
                case "cuboid":
                case "box": return SDFPrimitiveType.Cuboid;
                case "boxframe":
                case "frame": return SDFPrimitiveType.BoxFrame;
                case "cylinder": return SDFPrimitiveType.Cylinder;
                case "capsule": return SDFPrimitiveType.Capsule;
                case "ellipsoid": return SDFPrimitiveType.Ellipsoid;
                case "cone": return SDFPrimitiveType.Cone;
                case "pyramid": return SDFPrimitiveType.Pyramid;
                default:
                    throw new ArgumentException(
                        $"Unknown SDF primitive type '{type}'. Expected sphere, torus, cuboid, boxframe, cylinder, capsule, ellipsoid, cone, or pyramid.",
                        nameof(type));
            }
        }

        public static string PrimitiveTypeName(SDFPrimitiveType type)
        {
            switch (type)
            {
                case SDFPrimitiveType.Sphere: return "sphere";
                case SDFPrimitiveType.Torus: return "torus";
                case SDFPrimitiveType.Cuboid: return "cuboid";
                case SDFPrimitiveType.BoxFrame: return "boxframe";
                case SDFPrimitiveType.Cylinder: return "cylinder";
                case SDFPrimitiveType.Capsule: return "capsule";
                case SDFPrimitiveType.Ellipsoid: return "ellipsoid";
                case SDFPrimitiveType.Cone: return "cone";
                case SDFPrimitiveType.Pyramid: return "pyramid";
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static SDFCombineType ParseOperation(string operation)
        {
            switch (operation?.Trim().ToLowerInvariant())
            {
                case "union":
                case "add": return SDFCombineType.SmoothUnion;
                case "subtract": return SDFCombineType.SmoothSubtract;
                case "intersect": return SDFCombineType.SmoothIntersect;
                default:
                    throw new ArgumentException(
                        $"Unknown SDF operation '{operation}'. Expected union, subtract, or intersect.",
                        nameof(operation));
            }
        }

        public static string OperationName(SDFCombineType operation)
        {
            switch (operation)
            {
                case SDFCombineType.SmoothUnion: return "union";
                case SDFCombineType.SmoothSubtract: return "subtract";
                case SDFCombineType.SmoothIntersect: return "intersect";
                default: throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        public IReadOnlyList<SDFPrimitive> GetPrimitives()
        {
            return m_SdfManager.GetComponentsInChildren<SDFPrimitive>(true)
                .OrderBy(primitive => primitive.transform.GetSiblingIndex())
                .ToList();
        }

        public SDFPrimitive GetPrimitive(int index)
        {
            IReadOnlyList<SDFPrimitive> primitives = GetPrimitives();
            if (index < 0)
            {
                index += primitives.Count;
            }
            if (index < 0 || index >= primitives.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index), index, $"SDF primitive index must be between 0 and {primitives.Count - 1}.");
            }
            return primitives[index];
        }

        public SDFPrimitive AddPrimitive(
            SDFPrimitiveType type, Vector4 geometry, TrTransform localTransform,
            SDFCombineType operation, float blend)
        {
            var definition = new PrimitiveDefinition(
                type, geometry, localTransform, operation, blend, false);
            ValidatePrimitiveDefinition(definition, PrimitiveCount);
            SDFPrimitive primitive = CreatePrimitive(definition);
            RefreshSdf();
            return primitive;
        }

        internal void ReplacePrimitives(IReadOnlyList<PrimitiveDefinition> definitions)
        {
            ValidatePrimitiveDefinitions(definitions);

            foreach (SDFPrimitive primitive in GetPrimitives())
            {
                primitive.gameObject.SetActive(false);
                primitive.transform.SetParent(null, false);
                Destroy(primitive.gameObject);
            }

            for (int i = 0; i < definitions.Count; ++i)
            {
                CreatePrimitive(definitions[i]);
            }
            RefreshSdf();
        }

        internal static void ValidatePrimitiveDefinitions(
            IReadOnlyList<PrimitiveDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }
            for (int i = 0; i < definitions.Count; ++i)
            {
                ValidatePrimitiveDefinition(definitions[i], i);
            }
        }

        public void SetPrimitiveGeometry(
            SDFPrimitive primitive, SDFPrimitiveType type, Vector4 geometry)
        {
            ValidatePrimitive(primitive);
            primitive.SetType(type);
            primitive.SetData(geometry);
            primitive.gameObject.name = PrimitiveTypeName(type);
            RefreshSdf();
        }

        public void SetPrimitiveTransform(SDFPrimitive primitive, TrTransform localTransform)
        {
            ValidatePrimitive(primitive);
            if (localTransform.scale <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localTransform), localTransform.scale,
                    "An SDF primitive transform must have a positive scale.");
            }
            ApplyLocalTransform(primitive.transform, localTransform);
            RefreshSdf();
        }

        public void SetPrimitiveOperation(SDFPrimitive primitive, SDFCombineType operation)
        {
            ValidatePrimitive(primitive);
            if (primitive == GetPrimitive(0) && operation != SDFCombineType.SmoothUnion)
            {
                throw new ArgumentException(
                    "The first SDF primitive must use the union operation.", nameof(operation));
            }
            primitive.SetOperation(operation);
            RefreshSdf();
        }

        public void SetPrimitiveBlend(SDFPrimitive primitive, float blend)
        {
            ValidatePrimitive(primitive);
            if (blend < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(blend), blend, "SDF blend cannot be negative.");
            }
            primitive.SetSmoothing(blend);
            RefreshSdf();
        }

        public void UpdatePrimitive(
            SDFPrimitive primitive, SDFPrimitiveType? type = null, Vector4? geometry = null,
            TrTransform? localTransform = null, SDFCombineType? operation = null,
            float? blend = null)
        {
            ValidatePrimitive(primitive);
            SDFCombineType updatedOperation = operation ?? primitive.Operation;
            float updatedBlend = blend ?? primitive.Smoothing;
            if (primitive == GetPrimitive(0) && updatedOperation != SDFCombineType.SmoothUnion)
            {
                throw new ArgumentException("The first SDF primitive must use the union operation.");
            }
            if (updatedBlend < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(blend), updatedBlend, "SDF blend cannot be negative.");
            }
            if (localTransform.HasValue && localTransform.Value.scale <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localTransform), localTransform.Value.scale,
                    "An SDF primitive transform must have a positive scale.");
            }

            SDFPrimitiveType updatedType = type ?? primitive.Type;
            primitive.Configure(
                updatedType, geometry ?? primitive.Data, updatedOperation, updatedBlend,
                primitive.Flip);
            primitive.gameObject.name = PrimitiveTypeName(updatedType);
            if (localTransform.HasValue)
            {
                ApplyLocalTransform(primitive.transform, localTransform.Value);
            }
            RefreshSdf();
        }

        public void RemovePrimitive(SDFPrimitive primitive)
        {
            ValidatePrimitive(primitive);
            primitive.gameObject.SetActive(false);
            primitive.transform.SetParent(null, false);
            Destroy(primitive.gameObject);

            IReadOnlyList<SDFPrimitive> remaining = GetPrimitives();
            if (remaining.Count > 0 && remaining[0].Operation != SDFCombineType.SmoothUnion)
            {
                remaining[0].SetOperation(SDFCombineType.SmoothUnion);
            }
            RefreshSdf();
        }

        public void ClearPrimitives()
        {
            ReplacePrimitives(Array.Empty<PrimitiveDefinition>());
        }

        protected override void PopulateSaveState(Guides.State state)
        {
            IReadOnlyList<SDFPrimitive> primitives = GetPrimitives();
            var primitiveStates = new Guides.SdfPrimitiveState[primitives.Count];
            for (int i = 0; i < primitives.Count; ++i)
            {
                SDFPrimitive primitive = primitives[i];
                primitiveStates[i] = new Guides.SdfPrimitiveState
                {
                    Type = PrimitiveTypeName(primitive.Type),
                    Geometry = primitive.Data,
                    Transform = TrTransform.FromLocalTransform(primitive.transform),
                    Operation = OperationName(primitive.Operation),
                    Blend = primitive.Smoothing,
                    Flip = primitive.Flip
                };
            }
            state.Sdf = new Guides.SdfGuideState { Primitives = primitiveStates };
        }

        protected override void ApplySaveState(Guides.State state)
        {
            if (state.Sdf == null)
            {
                throw new ArgumentException("An SDF guide state requires an Sdf definition.", nameof(state));
            }
            if (state.Sdf.Primitives == null)
            {
                throw new ArgumentException(
                    "An SDF guide definition requires a Primitives array.", nameof(state));
            }

            var definitions = new List<PrimitiveDefinition>(state.Sdf.Primitives.Length);
            for (int i = 0; i < state.Sdf.Primitives.Length; ++i)
            {
                Guides.SdfPrimitiveState primitive = state.Sdf.Primitives[i];
                if (primitive == null)
                {
                    throw new ArgumentException($"SDF primitive {i} cannot be null.", nameof(state));
                }
                if (!primitive.HasGeometry)
                {
                    throw new ArgumentException($"SDF primitive {i} requires Geometry.", nameof(state));
                }
                if (!primitive.HasTransform)
                {
                    throw new ArgumentException($"SDF primitive {i} requires Transform.", nameof(state));
                }
                if (!primitive.HasBlend)
                {
                    throw new ArgumentException($"SDF primitive {i} requires Blend.", nameof(state));
                }
                if (!primitive.HasFlip)
                {
                    throw new ArgumentException($"SDF primitive {i} requires Flip.", nameof(state));
                }

                definitions.Add(new PrimitiveDefinition(
                    ParsePrimitiveType(primitive.Type), primitive.Geometry,
                    primitive.Transform, ParseOperation(primitive.Operation),
                    primitive.Blend, primitive.Flip));
            }
            ReplacePrimitives(definitions);
        }

        public void RefreshSdf()
        {
            m_SdfManager.RequestUpdate(onlySendBufferOnChange: false);
            RequestVisualRefresh();
        }

        private void RequestVisualRefresh()
        {
            ++m_RequestedVisualRevision;
            m_LastVisualMutationTime = Time.realtimeSinceStartupAsDouble;
        }

        private void OnMeshGenerationFinished(bool _)
        {
            // A revision is considered handled even if generation failed. This avoids an
            // unsupported or failing readback producing a retry-and-log loop every frame; a later
            // edit or show operation requests another attempt.
            m_CompletedVisualRevision = Math.Max(
                m_CompletedVisualRevision, m_StartedVisualRevision);
            if (m_StartedVisualGenerationIsFinal)
            {
                m_FinalizedVisualRevision = Math.Max(
                    m_FinalizedVisualRevision, m_StartedVisualRevision);
            }

            RefreshVisibility(WidgetManager.m_Instance.StencilsDisabled);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            if (m_MeshGenerator == null || m_SdfManager == null ||
                !m_SdfManager.gameObject.activeInHierarchy)
            {
                return;
            }

            if (m_SdfManager.IsEmpty)
            {
                m_CompletedVisualRevision = m_RequestedVisualRevision;
                m_FinalizedVisualRevision = m_RequestedVisualRevision;
                return;
            }

            if (m_MeshGenerator.IsMeshGenerationPending)
            {
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            bool refreshIntervalElapsed =
                now - m_LastVisualGenerationStartTime >= k_VisualRefreshIntervalSeconds;
            bool editingIsIdle =
                now - m_LastVisualMutationTime >= k_FinalVisualRefreshDelaySeconds;
            bool needsUpdatedMesh =
                m_CompletedVisualRevision < m_RequestedVisualRevision;
            bool needsFinalMesh =
                editingIsIdle && m_FinalizedVisualRevision < m_RequestedVisualRevision;
            if (!needsUpdatedMesh && !needsFinalMesh)
            {
                return;
            }
            if (!refreshIntervalElapsed && !editingIsIdle)
            {
                return;
            }

            int targetRevision = m_RequestedVisualRevision;
            int previousStartedRevision = m_StartedVisualRevision;
            bool previousStartedWasFinal = m_StartedVisualGenerationIsFinal;
            int previousRequestCount = m_MeshGenerator.MeshGenerationRequestCount;
            m_StartedVisualRevision = targetRevision;
            m_StartedVisualGenerationIsFinal = editingIsIdle;
            m_MeshGenerator.UpdateMesh();

            if (m_MeshGenerator.MeshGenerationRequestCount == previousRequestCount)
            {
                m_StartedVisualRevision = previousStartedRevision;
                m_StartedVisualGenerationIsFinal = previousStartedWasFinal;
                return;
            }

            m_LastVisualGenerationStartTime = now;
        }

        private void ValidatePrimitive(SDFPrimitive primitive)
        {
            if (primitive == null || primitive.Group != m_SdfManager)
            {
                throw new ArgumentException("The primitive does not belong to this SDF guide.", nameof(primitive));
            }
        }

        private SDFPrimitive CreatePrimitive(PrimitiveDefinition definition)
        {
            var primitiveObject = new GameObject(PrimitiveTypeName(definition.Type));
            primitiveObject.SetActive(false);
            primitiveObject.transform.SetParent(m_SdfManager.transform, false);
            ApplyLocalTransform(primitiveObject.transform, definition.Transform);

            SDFPrimitive primitive = primitiveObject.AddComponent<SDFPrimitive>();
            primitive.Configure(
                definition.Type, definition.Geometry, definition.Operation,
                definition.Blend, definition.Flip);
            primitiveObject.SetActive(true);
            return primitive;
        }

        private static void ValidatePrimitiveDefinition(PrimitiveDefinition definition, int index)
        {
            if (!Enum.IsDefined(typeof(SDFPrimitiveType), definition.Type))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition), definition.Type, $"SDF primitive {index} has an unknown type.");
            }
            if (!Enum.IsDefined(typeof(SDFCombineType), definition.Operation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition), definition.Operation,
                    $"SDF primitive {index} has an unknown operation.");
            }
            if (index == 0 && definition.Operation != SDFCombineType.SmoothUnion)
            {
                throw new ArgumentException("The first SDF primitive must use the union operation.");
            }
            if (!IsFinite(definition.Geometry))
            {
                throw new ArgumentException($"SDF primitive {index} geometry must be finite.");
            }
            if (!definition.Transform.IsFinite())
            {
                throw new ArgumentException($"SDF primitive {index} transform must be finite.");
            }
            if (definition.Transform.scale <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition), definition.Transform.scale,
                    $"SDF primitive {index} transform must have a positive scale.");
            }
            if (!IsFinite(definition.Blend) || definition.Blend < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition), definition.Blend,
                    $"SDF primitive {index} blend must be finite and non-negative.");
            }
        }

        private static bool IsFinite(Vector4 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void ApplyLocalTransform(Transform target, TrTransform localTransform)
        {
            target.localPosition = localTransform.translation;
            target.localRotation = localTransform.rotation;
            target.localScale = Vector3.one * localTransform.scale;
        }

        protected override void OnShow()
        {
            base.OnShow();
            m_SdfManager.gameObject.SetActive(true);
            RequestVisualRefresh();
        }

        protected override void OnHideStart()
        {
            base.OnHideStart();
            m_SdfManager.gameObject.SetActive(false);
            m_hasValidHit = false;
        }

        protected override void OnDestroy()
        {
            if (m_MeshGenerator != null)
            {
                m_MeshGenerator.MeshGenerationFinished -= OnMeshGenerationFinished;
            }
            base.OnDestroy();
        }

        // Smoothing for jitter reduction
        private Vector3 m_lastHitPos = Vector3.zero;
        private Vector3 m_lastHitNormal = Vector3.forward;
        private bool m_hasValidHit = false;
        private const float SMOOTHING_FACTOR = 0.7f;
        private const float MAX_RAYCAST_DISTANCE = 0.5f;
        private static readonly Vector2[] sm_RayOffsets =
        {
            Vector2.zero,
            new Vector2(0.3f, 0),
            new Vector2(-0.3f, 0),
            new Vector2(0, 0.3f),
            new Vector2(0, -0.3f),
            new Vector2(0.2f, 0.2f),
            new Vector2(0.2f, -0.2f),
            new Vector2(-0.2f, 0.2f),
            new Vector2(-0.2f, -0.2f),
        };

        public override void FindClosestPointOnSurface(
            Vector3 pos, out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            surfacePos = m_SdfManager.GetNearestPointOnSurface(pos);
            surfaceNorm = m_SdfManager.GetSurfaceNormal(surfacePos);
        }

        public override void RaycastToNearest(Vector3 origin, Quaternion rot, out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            // Cast multiple rays in a cone pattern in front of the controller
            // Try -forward since controller might be pointing backwards
            Vector3 forward = rot * (-Vector3.forward);
            Vector3 right = rot * Vector3.right;
            Vector3 up = rot * Vector3.up;

            Vector3 closestHit = origin;
            Vector3 closestNormal = forward;
            float closestDistance = float.MaxValue;
            bool foundHit = false;

            foreach (Vector2 offset in sm_RayOffsets)
            {
                Vector3 rayDir = forward + right * offset.x + up * offset.y;
                Vector3 normalizedDir = rayDir.normalized;

                if (m_SdfManager.Raycast(
                    origin, normalizedDir, out Vector3 hitPoint, out Vector3 hitNormal,
                    MAX_RAYCAST_DISTANCE))
                {
                    float distance = Vector3.Distance(origin, hitPoint);

                    if (distance > 0.05f && distance <= MAX_RAYCAST_DISTANCE &&
                        distance < closestDistance)
                    {
                        closestHit = hitPoint;
                        closestNormal = hitNormal;
                        closestDistance = distance;
                        foundHit = true;
                    }
                }
            }

            if (foundHit)
            {
                // Apply smoothing to reduce jitter
                if (m_hasValidHit)
                {
                    closestHit = Vector3.Lerp(m_lastHitPos, closestHit, 1f - SMOOTHING_FACTOR);
                    closestNormal = Vector3.Slerp(m_lastHitNormal, closestNormal, 1f - SMOOTHING_FACTOR).normalized;
                }

                m_lastHitPos = closestHit;
                m_lastHitNormal = closestNormal;
                m_hasValidHit = true;

                surfacePos = closestHit;
                surfaceNorm = closestNormal;
            }
            else
            {
                surfacePos = origin;
                surfaceNorm = forward;
            }
        }

        override public float GetActivationScore(
            Vector3 vControllerPos, InputManager.ControllerName name)
        {
            // Keep the stand-in collider as a cheap broad-phase test, but use the signed
            // distance field itself to decide whether the controller is inside the guide.
            if (m_Collider != null && !m_Collider.bounds.Contains(vControllerPos))
            {
                return -1.0f;
            }

            float signedDistance = m_SdfManager.GetDistanceToSurface(vControllerPos);
            if (signedDistance > 0.0f)
            {
                return -1.0f;
            }

            float characteristicRadius = m_Collider != null
                ? m_Collider.bounds.extents.Max()
                : Mathf.Abs(GetSignedWidgetSize()) * 0.5f * Coords.CanvasPose.scale;
            if (characteristicRadius <= Mathf.Epsilon)
            {
                return -1.0f;
            }

            float penetrationScore = Mathf.Clamp01(-signedDistance / characteristicRadius);
            return penetrationScore * Mathf.Pow(1 - m_Size / m_MaxSize_CS, 2);
        }

        protected override Axis GetInferredManipulationAxis(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside)
        {
            return Axis.Invalid;
        }

        protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis)
        {
            throw new NotImplementedException();
        }

        public override Axis GetScaleAxis(
            Vector3 handA, Vector3 handB,
            out Vector3 axisVec, out float extent)
        {
            // Unexpected -- normally we're only called during a 2-handed manipulation
            Debug.Assert(m_LockedManipulationAxis != null);
            Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

            // Fill in axisVec, extent
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
            if (m_Collider != null)
            {
                SphereCollider sphere = m_Collider as SphereCollider;
                TrTransform colliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
                    TrTransform.FromTransform(m_Collider.transform);
                Bounds bounds = new Bounds(colliderToCanvasXf * sphere.center, Vector3.zero);

                // Spheres are invariant with rotation, so take out the rotation from the transform and just
                // add the two opposing corners.
                colliderToCanvasXf.rotation = Quaternion.identity;
                bounds.Encapsulate(colliderToCanvasXf * (sphere.center + sphere.radius * Vector3.one));
                bounds.Encapsulate(colliderToCanvasXf * (sphere.center - sphere.radius * Vector3.one));

                return bounds;
            }
            return base.GetBounds_SelectionCanvasSpace();
        }
    }
} // namespace TiltBrush
