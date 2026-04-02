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

using System;
using UnityEngine;

namespace TiltBrush
{
    // Defines the volume capture area for Gaussian splat capture.
    // Cameras are distributed through a box volume and capture from multiple angles.
    // Both properties are in scene space, accounting for scene scale.
    public class GaussianCaptureBoxWidget : ShapeWidget
    {
        protected override IWidgetShape Shape => BoxShape.Instance;

        private Vector3 m_AspectRatio = Vector3.one;
        private Axis? m_LockedManipulationAxis;

        public override Vector3 CustomDimension
        {
            get => m_AspectRatio;
            set { m_AspectRatio = value; UpdateScale(); }
        }

        // Scene-space center of the capture volume.
        public Vector3 VolumeCenter => transform.localPosition;

        // Scene-space extents of the capture volume.
        public Vector3 VolumeExtents => m_Size * m_AspectRatio;

        protected override void UpdateScale()
        {
            float maxAspect = m_AspectRatio.Max();
            m_AspectRatio /= maxAspect;
            m_Size *= maxAspect;
            transform.localScale = m_Size * m_AspectRatio;
            UpdateMaterialScale();
        }

        protected override void SpoofScaleForShowAnim(float showRatio)
        {
            transform.localScale = m_Size * showRatio * m_AspectRatio;
        }

        protected override void OnUserBeginTwoHandGrab(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInObject)
        {
            base.OnUserBeginTwoHandGrab(primaryHand, secondaryHand, secondaryHandInObject);
            if (!secondaryHandInObject)
            {
                Vector3 handsInObjectSpace = transform.InverseTransformDirection(primaryHand - secondaryHand);
                Vector3 abs = handsInObjectSpace.Abs();
                if (abs.x > abs.y && abs.x > abs.z)
                    m_LockedManipulationAxis = Axis.X;
                else if (abs.y > abs.z)
                    m_LockedManipulationAxis = Axis.Y;
                else
                    m_LockedManipulationAxis = Axis.Z;
            }
            else
            {
                m_LockedManipulationAxis = Axis.Invalid;
            }
        }

        protected override void OnUserEndTwoHandGrab()
        {
            base.OnUserEndTwoHandGrab();
            m_LockedManipulationAxis = null;
        }

        public override Axis GetScaleAxis(
            Vector3 handA, Vector3 handB,
            out Vector3 axisVec, out float extent)
        {
            Debug.Assert(m_LockedManipulationAxis != null);
            Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;
            float parentScale = TrTransform.FromTransform(transform.parent).scale;

            switch (axis)
            {
                case Axis.X:
                case Axis.Y:
                case Axis.Z:
                    Vector3 axisVec_LS = Vector3.zero;
                    axisVec_LS[(int)axis] = 1;
                    axisVec = transform.TransformDirection(axisVec_LS);
                    extent = parentScale * VolumeExtents[(int)axis];
                    break;
                case Axis.Invalid:
                    axisVec = default;
                    extent = default;
                    break;
                default:
                    throw new NotImplementedException(axis.ToString());
            }
            return axis;
        }

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

        public static void FromTiltGaussianCapture(TiltGaussianCapture tilt)
        {
            GaussianCaptureBoxWidget widget =
                Instantiate(WidgetManager.m_Instance.GaussianCaptureBoxWidgetPrefab);
            widget.m_SkipIntroAnim = true;
            widget.transform.parent = App.Instance.m_CanvasTransform;
            widget.transform.localScale = Vector3.one;
            widget.SetSignedWidgetSize(tilt.Transform.scale);
            widget.CustomDimension = tilt.AspectRatio;
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
            GaussianCaptureBoxWidget clone = Instantiate(WidgetManager.m_Instance.GaussianCaptureBoxWidgetPrefab);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            clone.m_SkipIntroAnim = true;
            clone.m_ShowTimer = clone.m_ShowDuration;
            clone.transform.parent = transform.parent;
            clone.Show(true, false);
            clone.SetSignedWidgetSize(size);
            clone.CustomDimension = CustomDimension;
            clone.CloneInitialMaterials(this);
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            return clone;
        }
    }
} // namespace TiltBrush
