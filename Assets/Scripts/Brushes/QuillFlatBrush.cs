// Copyright 2026 The Open Brush Authors
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

using UnityEngine;

namespace TiltBrush
{
    class QuillFlatBrush : FlatGeometryBrush
    {
        protected override bool SmoothPositions
        {
            get { return false; }
        }

        protected override void ComputeSurfaceFrame(
            Vector3 preferredRight,
            Vector3 nTangent,
            Quaternion brushOrientation,
            out Vector3 nRight,
            out Vector3 nSurface)
        {
            const float epsilon = 1e-7f;
            Vector3 yaxis = nTangent;
            if (yaxis.sqrMagnitude < epsilon * epsilon)
            {
                yaxis = brushOrientation * Vector3.forward;
            }
            yaxis.Normalize();

            Vector3 zaxis = brushOrientation * Vector3.up;
            Vector3 xaxis = Vector3.Cross(zaxis, yaxis);

            if (xaxis.sqrMagnitude >= epsilon * epsilon)
            {
                xaxis.Normalize();
            }
            else if (Mathf.Abs(yaxis.x) < 0.9f)
            {
                xaxis = new Vector3(0, yaxis.z, yaxis.y).normalized;
            }
            else if (Mathf.Abs(yaxis.y) < 0.9f)
            {
                xaxis = new Vector3(-yaxis.z, 0, yaxis.x).normalized;
            }
            else
            {
                xaxis = new Vector3(yaxis.y, -yaxis.x, 0).normalized;
            }

            zaxis = Vector3.Cross(yaxis, xaxis).normalized;
            nRight = xaxis;
            nSurface = zaxis;
        }

        protected override float GetVertexAlpha(Knot knot)
        {
            return knot.color.a / 255.0f;
        }
    }
}
