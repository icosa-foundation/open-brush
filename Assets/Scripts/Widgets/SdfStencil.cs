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
using UnityEngine;

namespace TiltBrush
{
    public class SdfStencil : StencilWidget
    {
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
            var sdfTransform = WidgetManager.m_Instance.m_SDFManager.transform;
            sdfTransform.SetParent(transform, false);
            sdfTransform.localPosition = Vector3.zero;
            sdfTransform.localRotation = Quaternion.identity;
            sdfTransform.localScale = Vector3.one;
        }

        protected override void OnHideStart()
        {
            base.OnHideStart();
            var sdfTransform = WidgetManager.m_Instance.m_SDFManager.transform;
            sdfTransform.SetParent(WidgetManager.m_Instance.transform, false);
            sdfTransform.localPosition = Vector3.zero;
            sdfTransform.localRotation = Quaternion.identity;
            sdfTransform.localScale = Vector3.one;
        }

        /// <summary>
        /// Convenience accessor for the SDF manager's transform in world space.
        /// </summary>
        private TrTransform SdfTransform
        {
            get { return TrTransform.FromTransform(WidgetManager.m_Instance.m_SDFManager.transform); }
        }

        // Smoothing for jitter reduction
        private Vector3 m_lastHitPos = Vector3.zero;
        private Vector3 m_lastHitNormal = Vector3.forward;
        private bool m_hasValidHit = false;
        private const float SMOOTHING_FACTOR = 0.7f;

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
            
            // Create cone of rays (forward + angled directions)
            Vector3[] rayDirections = {
                forward,                                    // Center
                forward + right * 0.3f,                    // Right
                forward - right * 0.3f,                    // Left  
                forward + up * 0.3f,                       // Up
                forward - up * 0.3f,                       // Down
                forward + (right + up) * 0.2f,             // Top-right
                forward + (right - up) * 0.2f,             // Bottom-right
                forward + (-right + up) * 0.2f,            // Top-left
                forward + (-right - up) * 0.2f,            // Bottom-left
            };
            
            foreach (Vector3 rayDir in rayDirections) {
                Vector3 normalizedDir = rayDir.normalized;
                
                if (WidgetManager.m_Instance.m_SDFManager.Raycast(origin, normalizedDir, out Vector3 hitPoint, out Vector3 hitNormal)) {
                    float distance = Vector3.Distance(origin, hitPoint);
                    
                    // Increased range - consider hits up to 0.5 units away
                    if (distance > 0.05f && distance < closestDistance) {
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
                if (m_hasValidHit) {
                    closestHit = Vector3.Lerp(m_lastHitPos, closestHit, 1f - SMOOTHING_FACTOR);
                    closestNormal = Vector3.Slerp(m_lastHitNormal, closestNormal, 1f - SMOOTHING_FACTOR).normalized;
                }
                
                m_lastHitPos = closestHit;
                m_lastHitNormal = closestNormal;
                m_hasValidHit = true;
                
                surfacePos = closestHit;
                surfaceNorm = closestNormal;
                
                // Transform normal from canvas space to world space
                Vector3 worldNormal = Coords.CanvasPose.rotation * closestNormal;
                surfaceNorm = worldNormal;
                
                // Debug rotation issue
                if (Time.frameCount % 60 == 0) {
                    Debug.Log($"ROTATION_DEBUG: widget rotation={transform.rotation.eulerAngles}");
                    Debug.Log($"ROTATION_DEBUG: canvas rotation={Coords.CanvasPose.rotation.eulerAngles}");
                    Debug.Log($"ROTATION_DEBUG: original normal={closestNormal}, transformed normal={worldNormal}");
                }
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
