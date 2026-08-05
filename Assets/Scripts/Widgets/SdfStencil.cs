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
        private SDFGroup m_SdfManager;

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
            base.Awake();
            m_Type = StencilType.SDF;
            m_SdfManager = Instantiate(
                WidgetManager.m_Instance.m_SDFManager, transform, false);
            var sdfTransform = m_SdfManager.transform;
            sdfTransform.localPosition = Vector3.zero;
            sdfTransform.localRotation = Quaternion.identity;
            sdfTransform.localScale = Vector3.one;

            RegisterGeneratedMeshRenderer();
        }

        private void RegisterGeneratedMeshRenderer()
        {
            SDFGroupMeshGenerator generator =
                m_SdfManager.GetComponentInChildren<SDFGroupMeshGenerator>(true);
            if (generator == null)
            {
                Debug.LogWarning(
                    "SDFGuideSetup: SDF manager has no mesh generator", this);
                return;
            }

            MeshRenderer generatedRenderer = generator.MeshRenderer;
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
                default:
                    throw new ArgumentException(
                        $"Unknown SDF primitive type '{type}'. Expected sphere, torus, cuboid, boxframe, or cylinder.",
                        nameof(type));
            }
        }

        public static string PrimitiveTypeName(SDFPrimitiveType type)
        {
            return type.ToString().ToLowerInvariant();
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
            if (localTransform.scale <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(localTransform), localTransform.scale,
                    "An SDF primitive transform must have a positive scale.");
            }
            if (blend < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(blend), blend, "SDF blend cannot be negative.");
            }
            if (PrimitiveCount == 0 && operation != SDFCombineType.SmoothUnion)
            {
                throw new ArgumentException(
                    "The first SDF primitive must use the union operation.", nameof(operation));
            }

            var primitiveObject = new GameObject(PrimitiveTypeName(type));
            primitiveObject.SetActive(false);
            primitiveObject.transform.SetParent(m_SdfManager.transform, false);
            ApplyLocalTransform(primitiveObject.transform, localTransform);

            SDFPrimitive primitive = primitiveObject.AddComponent<SDFPrimitive>();
            primitive.Configure(type, geometry, operation, blend);
            primitiveObject.SetActive(true);
            return primitive;
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
                RefreshSdf();
            }
        }

        public void ClearPrimitives()
        {
            foreach (SDFPrimitive primitive in GetPrimitives())
            {
                primitive.gameObject.SetActive(false);
                primitive.transform.SetParent(null, false);
                Destroy(primitive.gameObject);
            }
        }

        public void RefreshSdf()
        {
            m_SdfManager.RequestUpdate(onlySendBufferOnChange: false);
        }

        private void ValidatePrimitive(SDFPrimitive primitive)
        {
            if (primitive == null || primitive.Group != m_SdfManager)
            {
                throw new ArgumentException("The primitive does not belong to this SDF guide.", nameof(primitive));
            }
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
        }

        protected override void OnHideStart()
        {
            base.OnHideStart();
            m_SdfManager.gameObject.SetActive(false);
            m_hasValidHit = false;
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
