// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in coStempliance with the License.
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
using Random = UnityEngine.Random;

namespace TiltBrush
{
    public class SdfStencil : StencilWidget
    {
        private Vector3[] m_SampleDirections;

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
                    throw new ArgumentException("SphereStencil does not support non-uniform extents");
                }
            }
        }

        protected override void Awake()
        {
            const int SAMPLES = 32;
            const float FOCUS_AMOUNT = 1f;

            base.Awake();
            m_Type = StencilType.SDF;
            WidgetManager.m_Instance.m_SDFManager.transform.SetParent(transform);
            m_SampleDirections = new Vector3[SAMPLES];
            var sample = Random.insideUnitCircle;
            for (var i = 0; i < SAMPLES; i++)
            {
                m_SampleDirections[i] = Vector3.forward + new Vector3(
                    (sample.x - 0.5f) / FOCUS_AMOUNT,
                    (sample.y - 0.5f) / FOCUS_AMOUNT,
                    0f
                );
            }

        }

        protected override void OnHideStart()
        {
            base.OnHideStart();
            WidgetManager.m_Instance.m_SDFManager.transform.SetParent(WidgetManager.m_Instance.transform);
        }

        private void CastSingleDirection(Vector3 origin, Vector3 dir, out Vector3 pos, out Vector3 normal)
        {
            WidgetManager.m_Instance.m_SDFManager.Mapper.Raymarch(origin, dir, out pos, out normal);
        }

        public override void RaycastToNearest(Vector3 origin, Quaternion rot, out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            Vector3 pos, normal = Vector3.zero;
            surfacePos = origin;
            surfaceNorm = transform.forward;
            float nearestDistance = Mathf.Infinity;
            foreach (var dir in m_SampleDirections)
            {
                CastSingleDirection(origin, -((rot * App.Scene.Pose.rotation) * dir), out pos, out normal);
                float d = (pos - origin).sqrMagnitude;
                if (d < nearestDistance && d < .2 && d > 0)
                {
                    surfacePos = pos;
                    surfaceNorm = normal;
                    nearestDistance = d;
                }
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
