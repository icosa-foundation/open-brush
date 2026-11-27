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
using UnityEngine;

namespace TiltBrush
{
    public class CustomStencil : StencilWidget
    {

        public Mesh m_DefaultMesh;
        private Mesh m_ProjectionMesh;
        private Vector3[] m_ProjectionVertices;
        private int[] m_ProjectionTriangles;

        public Mesh SourceMesh
        {
            get
            {
                var collider = GetComponentInChildren<MeshCollider>();
                return collider != null && collider.sharedMesh != null
                    ? collider.sharedMesh
                    : m_DefaultMesh;
            }
        }

        internal Transform SourceMeshTransform
        {
            get
            {
                var collider = GetComponentInChildren<MeshCollider>();
                return collider != null ? collider.transform : transform;
            }
        }

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
                    throw new ArgumentException("PolyStencil does not support non-uniform extents");
                }
            }
        }

        protected override void Awake()
        {
            m_Type = StencilType.Custom;
            base.Awake();
        }

        public void SetCustomStencil(Mesh mesh = null)
        {
            var collider = GetComponentInChildren<MeshCollider>();
            collider.sharedMesh = mesh ?? m_DefaultMesh;
            m_ProjectionMesh = null;
            collider.GetComponentInChildren<MeshFilter>().mesh = mesh;
        }

        public void SetColliderScale(float scale)
        {
            var collider = GetComponentInChildren<MeshCollider>();
            collider.transform.localScale = Vector3.one * scale * 0.5f; // Compensate for the prefab having a scale of 2
        }

        public override void FindClosestPointOnSurface(Vector3 pos, out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            Mesh mesh = SourceMesh;
            if (mesh == null)
            {
                throw new InvalidOperationException("The custom guide has no surface mesh.");
            }
            // Snapping queries run every frame; retain immutable mesh arrays instead of
            // allocating vertex/index copies for each query. SetCustomStencil invalidates them.
            if (m_ProjectionMesh != mesh)
            {
                m_ProjectionVertices = mesh.vertices;
                m_ProjectionTriangles = mesh.triangles;
                m_ProjectionMesh = mesh;
            }
            Matrix4x4 toWorld = SourceMeshTransform.localToWorldMatrix;
            if (!TryFindClosestMeshPoint(m_ProjectionVertices, m_ProjectionTriangles,
                toWorld, toWorld.inverse.transpose, pos, out surfacePos, out surfaceNorm))
            {
                throw new InvalidOperationException("The custom guide has no nondegenerate surface triangles.");
            }
        }

        internal static bool TryFindClosestMeshPoint(Vector3[] vertices, int[] triangles,
            Matrix4x4 toWorld, Matrix4x4 normalMatrix, Vector3 query,
            out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            surfacePos = query;
            surfaceNorm = Vector3.zero;
            float closestSquared = float.PositiveInfinity;
            float closestAlignment = -1f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 localA = vertices[triangles[i]];
                Vector3 localB = vertices[triangles[i + 1]];
                Vector3 localC = vertices[triangles[i + 2]];
                Vector3 a = toWorld.MultiplyPoint3x4(localA);
                Vector3 b = toWorld.MultiplyPoint3x4(localB);
                Vector3 c = toWorld.MultiplyPoint3x4(localC);
                Vector3 geometricNormal = Vector3.Cross(b - a, c - a);
                Vector3 normal = normalMatrix.MultiplyVector(
                    Vector3.Cross(localB - localA, localC - localA)).normalized;
                if (geometricNormal.sqrMagnitude == 0f || normal.sqrMagnitude == 0f) { continue; }
                Vector3 candidate = ModelStencil.ClosestTrianglePoint(
                    query, a, b, c, geometricNormal.normalized);
                Vector3 toQuery = query - candidate;
                float squared = toQuery.sqrMagnitude;
                float alignment = Mathf.Abs(Vector3.Dot(toQuery, normal));
                bool tied = Mathf.Approximately(squared, closestSquared);
                // At a shared edge, prefer the non-tangent face normal so an exterior
                // query cannot be classified as a boundary point by a tied tangent face.
                if ((squared < closestSquared && !tied) || (tied && alignment > closestAlignment))
                {
                    closestSquared = squared;
                    closestAlignment = alignment;
                    surfacePos = candidate;
                    surfaceNorm = normal;
                }
            }
            return !float.IsInfinity(closestSquared);
        }

        override public float GetActivationScore(
            Vector3 vControllerPos, InputManager.ControllerName name)
        {
            float fRadius = Mathf.Abs(GetSignedWidgetSize()) * 0.5f * Coords.CanvasPose.scale;
            float baseScore = (1.0f - (transform.position - vControllerPos).magnitude / fRadius);
            // don't try to scale if invalid; scaling by zero will make it look valid
            if (baseScore < 0) { return baseScore; }
            return baseScore * Mathf.Pow(1 - m_Size / m_MaxSize_CS, 2);
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
                MeshCollider collider = m_Collider as MeshCollider;
                TrTransform colliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
                    TrTransform.FromTransform(m_Collider.transform);
                Bounds bounds = new Bounds(colliderToCanvasXf * collider.bounds.center, Vector3.zero);

                // Polys are invariant with rotation, so take out the rotation from the transform and just
                // add the two opposing corners.
                colliderToCanvasXf.rotation = Quaternion.identity;
                bounds.Encapsulate(colliderToCanvasXf * (collider.bounds.center + collider.bounds.extents));
                bounds.Encapsulate(colliderToCanvasXf * (collider.bounds.center - collider.bounds.extents));

                return bounds;
            }
            return base.GetBounds_SelectionCanvasSpace();
        }
    }
} // namespace TiltBrush
