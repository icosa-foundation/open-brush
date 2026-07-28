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
using IsoMesh;
using UnityEngine;

namespace TiltBrush
{
    public class SdfStencil : StencilWidget
    {
        private SDFGroup m_SdfManager;

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
