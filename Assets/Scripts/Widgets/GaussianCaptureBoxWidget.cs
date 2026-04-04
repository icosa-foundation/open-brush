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
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    // Defines the volume capture area for Gaussian splat capture.
    // Cameras are distributed through a box volume and capture from multiple angles.
    // Both properties are in scene space, accounting for scene scale.
    public class GaussianCaptureBoxWidget : GaussianCaptureWidgetBase
    {
        private enum SubdivisionAxis
        {
            X,
            Y,
            Z
        }

        protected override IWidgetShape Shape => BoxShape.Instance;

        [SerializeField] private int m_SubdivX = 2;
        [SerializeField] private int m_SubdivY = 2;
        [SerializeField] private int m_SubdivZ = 2;

        private readonly List<GameObject> m_PreviewMarkers = new List<GameObject>();
        private static Mesh s_FrustumMesh;
        private static float s_CachedFov;
        private static float s_CachedAspect;

        private Vector3 m_AspectRatio = Vector3.one;
        private Axis? m_LockedManipulationAxis;
        private SubdivisionAxis m_SelectedSubdivisionAxis = SubdivisionAxis.X;

        public override Vector3 CustomDimension
        {
            get => m_AspectRatio;
            set { m_AspectRatio = value; UpdateScale(); }
        }

        // Scene-space center of the capture volume.
        public Vector3 VolumeCenter => transform.localPosition;

        // Scene-space extents of the capture volume.
        public Vector3 VolumeExtents => m_Size * m_AspectRatio;

        public int SubdivX
        {
            get => Mathf.Max(1, m_SubdivX);
            set => m_SubdivX = Mathf.Max(1, value);
        }

        public int SubdivY
        {
            get => Mathf.Max(1, m_SubdivY);
            set => m_SubdivY = Mathf.Max(1, value);
        }

        public int SubdivZ
        {
            get => Mathf.Max(1, m_SubdivZ);
            set => m_SubdivZ = Mathf.Max(1, value);
        }

        protected override void Awake()
        {
            base.Awake();
            RestoreStencilWidgetLayers();
        }

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
                m_LockedManipulationAxis = GetDominantAxisFromHands(primaryHand, secondaryHand);
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

        protected override void OnUpdate()
        {
            base.OnUpdate();
            UpdatePreviewMarkers();
        }

        private void OnDestroy()
        {
            ClearPreviewMarkers();
        }

        public override void RestoreGameObjectLayer(int layer)
        {
            HierarchyUtils.RecursivelySetLayer(transform, layer);
            RestoreStencilWidgetLayers();
        }

        protected override void InitPin()
        {
            base.InitPin();
            RestoreStencilWidgetLayers();
        }

        protected override string GetAdjustmentHintText()
        {
            return $"Hold X/A while scaling to change subdiv ({m_SelectedSubdivisionAxis})";
        }

        protected override bool TryApplyCaptureStep(int stepCount, out string statusText)
        {
            switch (m_SelectedSubdivisionAxis)
            {
                case SubdivisionAxis.X:
                    SubdivX += stepCount;
                    statusText = $"Subdiv X: {SubdivX}";
                    return true;
                case SubdivisionAxis.Y:
                    SubdivY += stepCount;
                    statusText = $"Subdiv Y: {SubdivY}";
                    return true;
                default:
                    SubdivZ += stepCount;
                    statusText = $"Subdiv Z: {SubdivZ}";
                    return true;
            }
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

        public override void PrepareCaptureAdjustmentForAxis(Axis axis)
        {
            SetActiveSubdivisionAxis(axis);
        }

        public override void PrepareCaptureAdjustmentFromHands(
            Vector3 primaryHand, Vector3 secondaryHand)
        {
            SetActiveSubdivisionAxisFromHands(primaryHand, secondaryHand);
        }

        private void SetActiveSubdivisionAxis(Axis axis)
        {
            m_SelectedSubdivisionAxis = axis switch
            {
                Axis.X => SubdivisionAxis.X,
                Axis.Y => SubdivisionAxis.Y,
                Axis.Z => SubdivisionAxis.Z,
                _ => m_SelectedSubdivisionAxis
            };
        }

        private void SetActiveSubdivisionAxisFromHands(Vector3 primaryHand, Vector3 secondaryHand)
        {
            SetActiveSubdivisionAxis(GetDominantAxisFromHands(primaryHand, secondaryHand));
        }

        private Axis GetDominantAxisFromHands(Vector3 primaryHand, Vector3 secondaryHand)
        {
            Vector3 handsInObjectSpace = transform.InverseTransformDirection(primaryHand - secondaryHand);
            Vector3 abs = handsInObjectSpace.Abs();
            if (abs.x > abs.y && abs.x > abs.z)
            {
                return Axis.X;
            }
            if (abs.y > abs.z)
            {
                return Axis.Y;
            }
            return Axis.Z;
        }

        private void OnDrawGizmosSelected()
        {
            var runtime = CameraCaptureRuntime.m_Instance;
            if (runtime == null) return;
            var poses = runtime.GetVolumeCameraPoses(transform, SubdivX, SubdivY, SubdivZ);
            float fov = runtime.cameraToUse != null ? runtime.cameraToUse.fieldOfView : 60f;
            float aspect = runtime.width > 0 && runtime.height > 0
                ? (float)runtime.width / runtime.height : 16f / 9f;
            float frustumDepth = transform.lossyScale.magnitude * 0.05f;
            float h = frustumDepth * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float w = h * aspect;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            foreach (var (pos, rot) in poses)
                DrawFrustumGizmo(pos, rot, w, h, frustumDepth);
        }

        private static void DrawFrustumGizmo(Vector3 pos, Quaternion rot, float w, float h, float d)
        {
            var tl = pos + rot * new Vector3(-w,  h, d);
            var tr = pos + rot * new Vector3( w,  h, d);
            var bl = pos + rot * new Vector3(-w, -h, d);
            var br = pos + rot * new Vector3( w, -h, d);
            Gizmos.DrawLine(pos, tl); Gizmos.DrawLine(pos, tr);
            Gizmos.DrawLine(pos, bl); Gizmos.DrawLine(pos, br);
            Gizmos.DrawLine(tl, tr); Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl); Gizmos.DrawLine(bl, tl);
        }

        private void UpdatePreviewMarkers()
        {
            var runtime = CameraCaptureRuntime.m_Instance;
            if (runtime == null) { ClearPreviewMarkers(); return; }

            var poses = runtime.GetVolumeCameraPoses(transform, SubdivX, SubdivY, SubdivZ);
            float frustumDepth = transform.lossyScale.magnitude * 0.05f;

            EnsureFrustumMesh(runtime);

            while (m_PreviewMarkers.Count < poses.Count)
                m_PreviewMarkers.Add(CreateFrustumMarker(new Color(1f, 0.6f, 0.2f)));

            for (int i = 0; i < m_PreviewMarkers.Count; i++)
                m_PreviewMarkers[i].SetActive(i < poses.Count);

            for (int i = 0; i < poses.Count; i++)
            {
                m_PreviewMarkers[i].transform.SetPositionAndRotation(poses[i].position, poses[i].rotation);
                m_PreviewMarkers[i].transform.localScale = Vector3.one * frustumDepth;
            }
        }

        private void ClearPreviewMarkers()
        {
            foreach (var go in m_PreviewMarkers)
                if (go != null) Destroy(go);
            m_PreviewMarkers.Clear();
        }

        private static void EnsureFrustumMesh(CameraCaptureRuntime runtime)
        {
            float fov = runtime.cameraToUse != null ? runtime.cameraToUse.fieldOfView : 60f;
            float aspect = runtime.width > 0 && runtime.height > 0
                ? (float)runtime.width / runtime.height : 16f / 9f;
            if (s_FrustumMesh != null && Mathf.Approximately(fov, s_CachedFov)
                                      && Mathf.Approximately(aspect, s_CachedAspect))
                return;

            float h = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float w = h * aspect;
            s_FrustumMesh = new Mesh { name = "CameraFrustum" };
            s_FrustumMesh.vertices = new[]
            {
                Vector3.zero,
                new Vector3(-w,  h, 1f),
                new Vector3( w,  h, 1f),
                new Vector3(-w, -h, 1f),
                new Vector3( w, -h, 1f),
            };
            s_FrustumMesh.SetIndices(new[]
            {
                0, 1,  0, 2,  0, 3,  0, 4,
                1, 2,  2, 4,  4, 3,  3, 1
            }, MeshTopology.Lines, 0);
            s_CachedFov = fov;
            s_CachedAspect = aspect;
        }

        private static GameObject CreateFrustumMarker(Color color)
        {
            var go = new GameObject("CameraFrustumMarker");
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = s_FrustumMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Unlit/Color")) { color = color };
            go.transform.parent = null;
            return go;
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
            if (tilt.SubdivX.HasValue)
            {
                widget.SubdivX = tilt.SubdivX.Value;
            }
            if (tilt.SubdivY.HasValue)
            {
                widget.SubdivY = tilt.SubdivY.Value;
            }
            if (tilt.SubdivZ.HasValue)
            {
                widget.SubdivZ = tilt.SubdivZ.Value;
            }
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
            clone.SubdivX = SubdivX;
            clone.SubdivY = SubdivY;
            clone.SubdivZ = SubdivZ;
            clone.CloneInitialMaterials(this);
            clone.m_SelectedSubdivisionAxis = m_SelectedSubdivisionAxis;
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            return clone;
        }
    }
} // namespace TiltBrush
