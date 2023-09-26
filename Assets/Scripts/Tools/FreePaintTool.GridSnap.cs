// Copyright 2021 The Tilt Brush Authors
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
    public partial class FreePaintTool
    {
        private bool m_GridSnapActive;

        // based off of the MultiplyPoint3x4
        // note that positional offset (matrix[0].w, matrix[1].w, matrix[2].w) has been REVERSE and is subtracting
        // don't ask me why I had to do this, but it had to be done to make it work in this scenario.
        static Vector3 WorldToCanvasPos(Matrix4x4 matrix, Vector3 point)
        {
            Vector3 res;
            res.x = matrix.m00 * point.x + matrix.m01 * point.y + matrix.m02 * point.z - matrix.m03;
            res.y = matrix.m10 * point.x + matrix.m11 * point.y + matrix.m12 * point.z - matrix.m13;
            res.z = matrix.m20 * point.x + matrix.m21 * point.y + matrix.m22 * point.z - matrix.m23;
            return res;
        }

        public static Vector3 SnapToGrid(Vector3 position)
        {

            float gridSubdivision = SelectionManager.m_Instance.SnappingGridSize;

            Vector3 localCanvasPos = App.Scene.MainCanvas.transform.worldToLocalMatrix.MultiplyPoint3x4(position);

            Vector3 roundedCanvasPos =
                new Vector3(
                    Mathf.Round(localCanvasPos.x / gridSubdivision) * gridSubdivision,
                    Mathf.Round(localCanvasPos.y / gridSubdivision) * gridSubdivision,
                    Mathf.Round(localCanvasPos.z / gridSubdivision) * gridSubdivision
                );

            roundedCanvasPos = new Vector3(
                SelectionManager.m_Instance.m_EnableSnapTranslationX ? roundedCanvasPos.x : localCanvasPos.x,
                SelectionManager.m_Instance.m_EnableSnapTranslationY ? roundedCanvasPos.y : localCanvasPos.y,
                SelectionManager.m_Instance.m_EnableSnapTranslationZ ? roundedCanvasPos.z : localCanvasPos.z
            );

            Vector3 offset = new Vector3(
                Mathf.Abs(roundedCanvasPos.x - localCanvasPos.x),
                Mathf.Abs(roundedCanvasPos.y - localCanvasPos.y),
                Mathf.Abs(roundedCanvasPos.z - localCanvasPos.z)
            );

            // If we're close to a grid point then always snap
            Vector3 snappedPos = roundedCanvasPos;

            // Otherwise allow a degree of freedom in the appropriate snap axis
            // (but only if we're not very close to a snap point)
            const float stickiness = 4f;
            if (offset.x > offset.y && offset.x > offset.z) // x is biggest
            {
                if (offset.x > gridSubdivision / stickiness)
                {
                    snappedPos = new Vector3(localCanvasPos.x, roundedCanvasPos.y, roundedCanvasPos.z);
                }
            }
            else if (offset.y > offset.x && offset.y > offset.z) // y is biggest
            {
                if (offset.y > gridSubdivision / stickiness)
                {
                    snappedPos = new Vector3(roundedCanvasPos.x, localCanvasPos.y, roundedCanvasPos.z);
                }
            }
            else // z is biggest
            {
                if (offset.z > gridSubdivision / stickiness)
                {
                    snappedPos = new Vector3(roundedCanvasPos.x, roundedCanvasPos.y, localCanvasPos.z);
                }
            }

            return App.Scene.MainCanvas.transform.localToWorldMatrix.MultiplyPoint3x4(snappedPos);
        }

    }
}
