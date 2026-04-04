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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    // Defines the dome capture volume for Gaussian splat capture.
    // Cameras are distributed on the selected dome shape around DomeCenter,
    // all looking inward at DomeCenter.
    // Both properties are in world space, accounting for scene scale.
    public class GaussianCaptureSphereWidget : GaussianCaptureWidgetBase
    {
        protected override IWidgetShape Shape => SphereShape.Instance;

        [SerializeField] private int m_NumRings = 4;
        [SerializeField] private int m_ViewsPerRing = 20;
        [SerializeField] private StencilType m_CaptureShapeType = StencilType.Sphere;

        private readonly List<GameObject> m_PreviewMarkers = new List<GameObject>();
        private static Mesh s_FrustumMesh;
        private static float s_CachedFov;
        private static float s_CachedAspect;

        public int NumRings
        {
            get => Mathf.Max(1, m_NumRings);
            set => m_NumRings = Mathf.Max(1, value);
        }

        public int ViewsPerRing
        {
            get => Mathf.Max(1, m_ViewsPerRing);
            set => m_ViewsPerRing = Mathf.Max(1, value);
        }

        public StencilType CaptureShapeType
        {
            get => m_CaptureShapeType == StencilType.InteriorDome
                ? StencilType.InteriorDome
                : StencilType.Sphere;
            set => m_CaptureShapeType = value == StencilType.InteriorDome
                ? StencilType.InteriorDome
                : StencilType.Sphere;
        }

        // Scene-space center of the capture dome (the point cameras look at).
        public Vector3 DomeCenter => transform.localPosition;

        // Scene-space radius of the capture dome.
        public float DomeRadius => m_Size * 0.5f;

        protected override void Awake()
        {
            base.Awake();
            RestoreStencilWidgetLayers();
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
            return "Hold X/A while scaling to change rings + views";
        }

        protected override bool TryApplyCaptureStep(int stepCount, out string statusText)
        {
            NumRings += stepCount;
            ViewsPerRing += stepCount;
            statusText = $"Rings: {NumRings} Views/Ring: {ViewsPerRing}";
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            var runtime = CameraCaptureRuntime.m_Instance;
            if (runtime == null) return;
            float r = transform.lossyScale.x * 0.5f;
            var poses = runtime.GetDomeCameraPoses(
                transform.position, r, NumRings, ViewsPerRing, CaptureShapeType);
            float frustumDepth = r * 0.12f;
            float fov = runtime.cameraToUse != null ? runtime.cameraToUse.fieldOfView : 60f;
            float aspect = runtime.width > 0 && runtime.height > 0
                ? (float)runtime.width / runtime.height : 16f / 9f;
            float h = frustumDepth * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float w = h * aspect;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
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

            float r = transform.lossyScale.x * 0.5f;
            var poses = runtime.GetDomeCameraPoses(
                transform.position, r, NumRings, ViewsPerRing, CaptureShapeType);
            float frustumDepth = r * 0.12f;

            EnsureFrustumMesh(runtime);

            while (m_PreviewMarkers.Count < poses.Count)
                m_PreviewMarkers.Add(CreateFrustumMarker(new Color(0.2f, 0.8f, 1f)));

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

        // Builds a unit frustum mesh (apex at origin, base at z=1) using line topology.
        // The GameObject is scaled to the desired frustum depth at draw time.
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
                Vector3.zero,              // 0 apex
                new Vector3(-w,  h, 1f),   // 1 top-left
                new Vector3( w,  h, 1f),   // 2 top-right
                new Vector3(-w, -h, 1f),   // 3 bottom-left
                new Vector3( w, -h, 1f),   // 4 bottom-right
            };
            s_FrustumMesh.SetIndices(new[]
            {
                0, 1,  0, 2,  0, 3,  0, 4,  // apex to corners
                1, 2,  2, 4,  4, 3,  3, 1   // near-plane rectangle
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
            GaussianCaptureSphereWidget widget =
                Instantiate(WidgetManager.m_Instance.GaussianCaptureSphereWidgetPrefab);
            widget.m_SkipIntroAnim = true;
            widget.transform.parent = App.Instance.m_CanvasTransform;
            widget.transform.localScale = Vector3.one;
            widget.SetSignedWidgetSize(tilt.Transform.scale);
            widget.CaptureShapeType = tilt.ShapeType;
            if (tilt.NumRings.HasValue)
            {
                widget.NumRings = tilt.NumRings.Value;
            }
            if (tilt.ViewsPerRing.HasValue)
            {
                widget.ViewsPerRing = tilt.ViewsPerRing.Value;
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
            GaussianCaptureSphereWidget clone = Instantiate(WidgetManager.m_Instance.GaussianCaptureSphereWidgetPrefab);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            clone.m_SkipIntroAnim = true;
            clone.m_ShowTimer = clone.m_ShowDuration;
            clone.transform.parent = transform.parent;
            clone.Show(true, false);
            clone.SetSignedWidgetSize(size);
            clone.CaptureShapeType = CaptureShapeType;
            clone.NumRings = NumRings;
            clone.ViewsPerRing = ViewsPerRing;
            clone.CloneInitialMaterials(this);
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            return clone;
        }
    }
} // namespace TiltBrush
