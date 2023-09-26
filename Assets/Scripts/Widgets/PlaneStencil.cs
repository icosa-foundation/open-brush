// Copyright 2021 The Open Brush Authors
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
    /*
     * Implements a basic plane guide (known as a stencil in code).  This provides a cleaner way
     * for mimicking a whiteboard for usages beyond painting on a flat surface.
     */

    public class PlaneStencil : StencilWidget
    {
        private Vector3 m_AspectRatio;

        // This must be negative for brush strokes to layer on top (rather than below)
        private float m_LayeringOffset = -0.1f;

        public override Vector3 Extents
        {
            get
            {
                return m_Size * m_AspectRatio;
            }
            set
            {
                m_Size = 1f;
                m_AspectRatio = value;
                UpdateScale();
            }
        }

        public override Vector3 CustomDimension
        {
            get { return m_AspectRatio; }
            set
            {
                m_AspectRatio = value;
                UpdateScale();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            m_Type = StencilType.Plane;
            m_AspectRatio = Vector3.one;

            // The mesh's z coordinate cannot be at 0 or layering will not work in FindClosestPoint
            Vector3 meshPosition = m_Mesh.transform.localPosition;
            meshPosition.z = m_LayeringOffset;
            m_Mesh.transform.localPosition = meshPosition;
        }

        // Determine where the pointer will be snapped or "magnetized" to on the surface
        public override void FindClosestPointOnSurface(Vector3 pos,
                                                       out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            Vector3 localPos = transform.InverseTransformPoint(pos);
            Vector3 halfDimensions = ((BoxCollider)m_Collider).size * 0.5f;
            surfacePos.x = Mathf.Clamp(localPos.x, -halfDimensions.x, halfDimensions.x);
            surfacePos.y = Mathf.Clamp(localPos.y, -halfDimensions.y, halfDimensions.y);
            surfacePos.z = m_LayeringOffset; // this must not be too close to 0 or layering will not work
            surfaceNorm = -Vector3.forward;

            surfaceNorm = transform.TransformDirection(surfaceNorm);
            surfacePos = transform.TransformPoint(surfacePos);
        }

        // Determines when you can grab.  1 = best, 0 = worst, -1 = invalid
        public override float GetActivationScore(
            Vector3 vControllerPos, InputManager.ControllerName name)
        {
            float baseScore = base.GetActivationScore(vControllerPos, name);
            return baseScore;
        }

        // Determine if whether we are trying to scale along an axis or uniformly (Invalid)
        protected override Axis GetInferredManipulationAxis(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside)
        {
            if (secondaryHandInside)
            {
                return Axis.Invalid;
            }
            Vector3 vHandsInObjectSpace = transform.InverseTransformDirection(primaryHand - secondaryHand);
            Vector3 vAbs = vHandsInObjectSpace.Abs();
            if (vAbs.x > vAbs.y && vAbs.x > vAbs.z)
            {
                return Axis.X;
            }
            else if (vAbs.y > vAbs.z)
            {
                return Axis.Y;
            }
            else
            {
                return Axis.Invalid;
            }
        }

        // Apply scale and make it undo-able
        public override void RecordAndApplyScaleToAxis(float deltaScale, Axis axis)
        {
            if (m_RecordMovements)
            {
                Vector3 newDimensions = CustomDimension;
                newDimensions[(int)axis] *= deltaScale;
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(this, LocalTransform, newDimensions));
            }
            else
            {
                m_AspectRatio[(int)axis] *= deltaScale;
                UpdateScale();
            }
        }

        protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis)
        {
            if (m_HighlightMeshFilters != null)
            {
                for (int i = 0; i < m_HighlightMeshFilters.Length; i++)
                {
                    App.Instance.SelectionEffect.RegisterMesh(m_HighlightMeshFilters[i]);
                }
            }
        }

        // Using the locked axis, get the scaled direction of the axis
        public override Axis GetScaleAxis(Vector3 handA, Vector3 handB,
            out Vector3 axisVec, out float extent)
        {
            // Unexpected -- normally we're only called during a 2-handed manipulation
            Debug.Assert(m_LockedManipulationAxis != null);
            Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

            float parentScale = TrTransform.FromTransform(transform.parent).scale;

            // Fill in axisVec, extent
            switch (axis)
            {
                case Axis.X:
                case Axis.Y:
                    Vector3 axisVec_LS = Vector3.zero;
                    axisVec_LS[(int)axis] = 1;
                    axisVec = transform.TransformDirection(axisVec_LS);
                    extent = parentScale * Extents[(int)axis];
                    break;
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
            if (m_BoxCollider != null)
            {
                TrTransform boxColliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
                    TrTransform.FromTransform(m_BoxCollider.transform);
                Bounds bounds = new Bounds(boxColliderToCanvasXf * m_BoxCollider.center, Vector3.zero);

                // Transform the corners of the widget bounds into canvas space and extend the total bounds
                // to encapsulate them.
                for (int i = 0; i < 8; i++)
                {
                    bounds.Encapsulate(boxColliderToCanvasXf * (m_BoxCollider.center + Vector3.Scale(
                        m_BoxCollider.size,
                        new Vector3((i & 1) == 0 ? -0.5f : 0.5f,
                            (i & 2) == 0 ? -0.5f : 0.5f,
                            (i & 4) == 0 ? -0.5f : 0.5f))));
                }

                return bounds;
            }
            return new Bounds();
        }


        // Actually perform the scale change on the Unity transform along with tiling the material
        protected override void UpdateScale()
        {
            float maxAspect = m_AspectRatio.Max();
            m_AspectRatio /= maxAspect;
            m_Size *= maxAspect;
            transform.localScale = m_Size * m_AspectRatio;
            UpdateMaterialScale();
        }
    }
} // namespace TiltBrush
