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
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public class CustomStencil : StencilWidget
    {

        public Mesh m_DefaultMesh;

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
            EditableModelManager.SetCustomStencil(this, PreviewPolyhedron.m_Instance.m_PolyMesh);
        }

        public void SetCustomStencil(Mesh mesh = null)
        {
            var collider = GetComponentInChildren<MeshCollider>();
            collider.sharedMesh = mesh ?? m_DefaultMesh;
            collider.GetComponentInChildren<MeshFilter>().mesh = mesh;
        }

        public void SetColliderScale(float scale)
        {
            var collider = GetComponentInChildren<MeshCollider>();
            collider.transform.localScale = Vector3.one * scale * 0.5f; // Compensate for the prefab having a scale of 2
        }

        public override void FindClosestPointOnSurface(Vector3 pos, out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            var collider = GetComponentInChildren<MeshCollider>();
            surfacePos = collider.ClosestPoint(pos);
            surfaceNorm = Vector3.zero;
            RaycastHit hit;
            if (Physics.Raycast(pos, surfacePos - pos, out hit))
            {
                surfaceNorm = hit.normal;
            }
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
