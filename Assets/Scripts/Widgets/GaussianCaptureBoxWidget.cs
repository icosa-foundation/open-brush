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
    // Defines the volume capture area for Gaussian splat capture.
    // Cameras are distributed through a box volume and capture from multiple angles.
    // Both properties are in world space, accounting for scene scale.
    public class GaussianCaptureBoxWidget : ShapeWidget
    {
        protected override IWidgetShape Shape => BoxShape.Instance;

        // Scene-space center of the capture volume.
        public Vector3 VolumeCenter => transform.localPosition;

        // Scene-space extents of the capture volume.
        public Vector3 VolumeExtents => transform.localScale;

        public override GrabWidget Clone()
        {
            return Clone(transform.position, transform.rotation, GetSignedWidgetSize());
        }

        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            GaussianCaptureBoxWidget clone = Instantiate(WidgetManager.m_Instance.GaussianCaptureBoxWidgetPrefab);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            clone.m_SkipIntroAnim = true;
            clone.m_ShowTimer = clone.m_ShowDuration;
            clone.transform.parent = transform.parent;
            clone.Show(true, false);
            clone.SetSignedWidgetSize(size);
            clone.CloneInitialMaterials(this);
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            return clone;
        }
    }
} // namespace TiltBrush
