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
    // Defines the dome capture volume for Gaussian splat capture.
    // Cameras are placed on a hemisphere of DomeRadius around DomeCenter,
    // all looking inward at DomeCenter.
    // Both properties are in world space, accounting for scene scale.
    public class GaussianCaptureSphereWidget : ShapeWidget
    {
        protected override IWidgetShape Shape => SphereShape.Instance;

        // Scene-space center of the capture dome (the point cameras look at).
        public Vector3 DomeCenter => transform.localPosition;

        // Scene-space radius of the capture dome.
        public float DomeRadius => m_Size * 0.5f;

        public static void FromTiltGaussianCapture(TiltGaussianCapture tilt)
        {
            GaussianCaptureSphereWidget widget =
                Instantiate(WidgetManager.m_Instance.GaussianCaptureSphereWidgetPrefab);
            widget.m_SkipIntroAnim = true;
            widget.transform.parent = App.Instance.m_CanvasTransform;
            widget.transform.localScale = Vector3.one;
            widget.SetSignedWidgetSize(tilt.Transform.scale);
            widget.Show(bShow: true, bPlayAudio: false);
            widget.transform.localPosition = tilt.Transform.translation;
            widget.transform.localRotation = tilt.Transform.rotation;
            if (tilt.Pinned) { widget.PinFromSave(); }
            widget.Group = App.GroupManager.GetGroupFromId(tilt.GroupId);
            widget.SetCanvas(App.Scene.GetOrCreateLayer(tilt.LayerId));
        }

        public override GrabWidget Clone()
        {
            return Clone(transform.position, transform.rotation, GetSignedWidgetSize());
        }

        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            GaussianCaptureSphereWidget clone = Instantiate(WidgetManager.m_Instance.GaussianCaptureSphereWidgetPrefab);
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
