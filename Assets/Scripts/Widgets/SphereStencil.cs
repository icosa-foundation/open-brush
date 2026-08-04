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
    public class SphereStencil : StencilWidget
    {
        public override Vector3 Extents
        {
            get { return SphereShape.GetExtents(m_Size); }
            set
            {
                SetSignedWidgetSize(SphereShape.GetSizeFromExtents(value));
            }
        }

        protected override IWidgetShape Shape => SphereShape.Instance;

        protected override void Awake()
        {
            base.Awake();
            m_Type = StencilType.Sphere;
        }

        public override void FindClosestPointOnSurface(Vector3 pos,
                                                       out Vector3 surfacePos, out Vector3 surfaceNorm)
        {
            SphereShape.FindClosestPointOnSurface(
                transform, GetSignedWidgetSize(), pos, out surfacePos, out surfaceNorm);
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

    }
} // namespace TiltBrush
