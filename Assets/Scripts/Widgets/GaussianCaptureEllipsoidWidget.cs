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
    public class GaussianCaptureEllipsoidWidget : GaussianCaptureWidgetBase
    {
        private struct AxisDirection
        {
            public Axis axis;
            public Vector3 direction;
        }

        private const float kRadiusInObjectSpace = 0.5f;
        private const float kMinAspectRatio = 0.2f;

        private static readonly AxisDirection[] sm_AxisDirections =
        {
            new AxisDirection { axis = Axis.X, direction = new Vector3(1, 0, 0) },
            new AxisDirection { axis = Axis.Y, direction = new Vector3(0, 1, 0) },
            new AxisDirection { axis = Axis.Z, direction = new Vector3(0, 0, 1) },
            new AxisDirection { axis = Axis.XY, direction = new Vector3(1, 1, 0).normalized },
            new AxisDirection { axis = Axis.XZ, direction = new Vector3(1, 0, 1).normalized },
            new AxisDirection { axis = Axis.YZ, direction = new Vector3(0, 1, 1).normalized }
        };

        protected override IWidgetShape Shape => null;

        [SerializeField] private int m_NumRings = 4;
        [SerializeField] private int m_ViewsPerRing = 20;
        [SerializeField] private Vector3 m_AspectRatio = Vector3.one;

        private readonly List<GameObject> m_PreviewMarkers = new List<GameObject>();
        private static Mesh s_FrustumMesh;
        private static float s_CachedFov;
        private static float s_CachedAspect;
        private Axis? m_LockedManipulationAxis;

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

        public StencilType CaptureShapeType => StencilType.Ellipsoid;

        public override Vector3 CustomDimension
        {
            get => m_AspectRatio;
            set
            {
                m_AspectRatio = value;
                UpdateScale();
            }
        }

        public Vector3 DomeExtents => m_Size * m_AspectRatio;
        public Vector3 DomeRadii => DomeExtents * 0.5f;

        protected override void Awake()
        {
            base.Awake();
            RestoreStencilWidgetLayers();
            UpdateScale();
        }

        public override float GetActivationScore(Vector3 controllerPos, InputManager.ControllerName name)
        {
            Vector3 pos_OS = transform.InverseTransformPoint(controllerPos);
            float baseScore = 1f - pos_OS.magnitude / kRadiusInObjectSpace;
            if (baseScore < 0)
            {
                return baseScore;
            }
            return baseScore * Mathf.Pow(1 - m_Size / m_MaxSize_CS, 2);
        }

        public override Bounds GetBounds_SelectionCanvasSpace()
        {
            if (m_Collider is SphereCollider sphere)
            {
                TrTransform colliderToCanvasXf = App.Scene.SelectionCanvas.Pose.inverse *
                    TrTransform.FromTransform(m_Collider.transform);
                Bounds bounds = new Bounds(colliderToCanvasXf * sphere.center, Vector3.zero);

                for (int i = 0; i < 8; i++)
                {
                    bounds.Encapsulate(colliderToCanvasXf * (sphere.center +
                        sphere.radius * new Vector3(
                            (i & 1) == 0 ? -1.0f : 1.0f,
                            (i & 2) == 0 ? -1.0f : 1.0f,
                            (i & 4) == 0 ? -1.0f : 1.0f)));
                }

                return bounds;
            }
            return base.GetBounds_SelectionCanvasSpace();
        }

        protected override void UpdateScale()
        {
            float maxAspect = m_AspectRatio.Max();
            m_AspectRatio /= maxAspect;
            m_Size *= maxAspect;
            m_AspectRatio = CMax(m_AspectRatio, Vector3.one * kMinAspectRatio);
            Vector3 extent_GS = m_Size * m_AspectRatio;
            float extent_OS = kRadiusInObjectSpace * 2f;
            transform.localScale = extent_GS / extent_OS;
            UpdateMaterialScale();
        }

        protected override void SpoofScaleForShowAnim(float showRatio)
        {
            Vector3 extent_GS = m_Size * m_AspectRatio;
            float extent_OS = kRadiusInObjectSpace * 2f;
            transform.localScale = (extent_GS / extent_OS) * showRatio;
        }

        protected override void OnUserBeginTwoHandGrab(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInObject)
        {
            base.OnUserBeginTwoHandGrab(primaryHand, secondaryHand, secondaryHandInObject);
            m_LockedManipulationAxis = GetInferredManipulationAxis(
                primaryHand, secondaryHand, secondaryHandInObject);
        }

        protected override void OnUserEndTwoHandGrab()
        {
            base.OnUserEndTwoHandGrab();
            m_LockedManipulationAxis = null;
        }

        public override Axis GetScaleAxis(Vector3 handA, Vector3 handB, out Vector3 axisVec, out float extent)
        {
            Debug.Assert(m_LockedManipulationAxis != null);
            Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

            float parentScale = TrTransform.FromTransform(transform.parent).scale;
            Vector3 delta = handB - handA;
            Vector3 extents = DomeExtents;

            switch (axis)
            {
                case Axis.X:
                case Axis.Y:
                case Axis.Z:
                    Vector3 axisVec_OS = Vector3.zero;
                    axisVec_OS[(int)axis] = 1;
                    axisVec = transform.TransformDirection(axisVec_OS);
                    extent = parentScale * extents[(int)axis];
                    break;
                case Axis.YZ:
                {
                    Vector3 plane = transform.rotation * new Vector3(1, 0, 0);
                    axisVec = (delta - Vector3.Dot(delta, plane) * plane).normalized;
                    extent = parentScale * Mathf.Max(extents[1], extents[2]);
                    break;
                }
                case Axis.XZ:
                {
                    Vector3 plane = transform.rotation * new Vector3(0, 1, 0);
                    axisVec = (delta - Vector3.Dot(delta, plane) * plane).normalized;
                    extent = parentScale * Mathf.Max(extents[0], extents[2]);
                    break;
                }
                case Axis.XY:
                {
                    Vector3 plane = transform.rotation * new Vector3(0, 0, 1);
                    axisVec = (delta - Vector3.Dot(delta, plane) * plane).normalized;
                    extent = parentScale * Mathf.Max(extents[0], extents[1]);
                    break;
                }
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
            Vector3 aspectRatio = m_AspectRatio;
            switch (axis)
            {
                case Axis.X:
                case Axis.Y:
                case Axis.Z:
                    aspectRatio[(int)axis] *= deltaScale;
                    break;
                case Axis.YZ:
                    aspectRatio[1] *= deltaScale;
                    aspectRatio[2] *= deltaScale;
                    break;
                case Axis.XZ:
                    aspectRatio[0] *= deltaScale;
                    aspectRatio[2] *= deltaScale;
                    break;
                case Axis.XY:
                    aspectRatio[0] *= deltaScale;
                    aspectRatio[1] *= deltaScale;
                    break;
                default:
                    throw new NotImplementedException(axis.ToString());
            }

            if (m_RecordMovements)
            {
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new MoveWidgetCommand(this, LocalTransform, aspectRatio));
            }
            else
            {
                m_AspectRatio = aspectRatio;
                UpdateScale();
            }
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
            var poses = runtime.GetDomeCameraPoses(
                transform, DomeRadii, NumRings, ViewsPerRing, CaptureShapeType);
            float frustumDepth = DomeRadii.Max() * 0.12f;
            float fov = runtime.cameraToUse != null ? runtime.cameraToUse.fieldOfView : 60f;
            float aspect = runtime.width > 0 && runtime.height > 0
                ? (float)runtime.width / runtime.height : 16f / 9f;
            float h = frustumDepth * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float w = h * aspect;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            foreach (var (pos, rot) in poses)
            {
                DrawFrustumGizmo(pos, rot, w, h, frustumDepth);
            }
        }

        private static void DrawFrustumGizmo(Vector3 pos, Quaternion rot, float w, float h, float d)
        {
            var tl = pos + rot * new Vector3(-w, h, d);
            var tr = pos + rot * new Vector3(w, h, d);
            var bl = pos + rot * new Vector3(-w, -h, d);
            var br = pos + rot * new Vector3(w, -h, d);
            Gizmos.DrawLine(pos, tl); Gizmos.DrawLine(pos, tr);
            Gizmos.DrawLine(pos, bl); Gizmos.DrawLine(pos, br);
            Gizmos.DrawLine(tl, tr); Gizmos.DrawLine(tr, br);
            Gizmos.DrawLine(br, bl); Gizmos.DrawLine(bl, tl);
        }

        private void UpdatePreviewMarkers()
        {
            var runtime = CameraCaptureRuntime.m_Instance;
            if (runtime == null)
            {
                ClearPreviewMarkers();
                return;
            }

            var poses = runtime.GetDomeCameraPoses(
                transform, DomeRadii, NumRings, ViewsPerRing, CaptureShapeType);
            float frustumDepth = DomeRadii.Max() * 0.12f;

            EnsureFrustumMesh(runtime);

            while (m_PreviewMarkers.Count < poses.Count)
            {
                m_PreviewMarkers.Add(CreateFrustumMarker(new Color(0.2f, 0.8f, 1f)));
            }

            for (int i = 0; i < m_PreviewMarkers.Count; i++)
            {
                m_PreviewMarkers[i].SetActive(i < poses.Count);
            }

            for (int i = 0; i < poses.Count; i++)
            {
                m_PreviewMarkers[i].transform.SetPositionAndRotation(poses[i].position, poses[i].rotation);
                m_PreviewMarkers[i].transform.localScale = Vector3.one * frustumDepth;
            }
        }

        private void ClearPreviewMarkers()
        {
            foreach (var go in m_PreviewMarkers)
            {
                if (go != null) Destroy(go);
            }
            m_PreviewMarkers.Clear();
        }

        private static void EnsureFrustumMesh(CameraCaptureRuntime runtime)
        {
            float fov = runtime.cameraToUse != null ? runtime.cameraToUse.fieldOfView : 60f;
            float aspect = runtime.width > 0 && runtime.height > 0
                ? (float)runtime.width / runtime.height : 16f / 9f;
            if (s_FrustumMesh != null && Mathf.Approximately(fov, s_CachedFov)
                                      && Mathf.Approximately(aspect, s_CachedAspect))
            {
                return;
            }

            float h = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float w = h * aspect;
            s_FrustumMesh = new Mesh { name = "CameraFrustum" };
            s_FrustumMesh.vertices = new[]
            {
                Vector3.zero,
                new Vector3(-w, h, 1f),
                new Vector3(w, h, 1f),
                new Vector3(-w, -h, 1f),
                new Vector3(w, -h, 1f),
            };
            s_FrustumMesh.SetIndices(new[]
            {
                0, 1, 0, 2, 0, 3, 0, 4,
                1, 2, 2, 4, 4, 3, 3, 1
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
            var widget = Instantiate(WidgetManager.m_Instance.GaussianCaptureEllipsoidWidgetPrefab);
            widget.m_SkipIntroAnim = true;
            widget.transform.parent = App.Instance.m_CanvasTransform;
            widget.transform.localScale = Vector3.one;
            widget.SetSignedWidgetSize(tilt.Transform.scale);
            widget.CustomDimension = tilt.AspectRatio;
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
            var clone = Instantiate(WidgetManager.m_Instance.GaussianCaptureEllipsoidWidgetPrefab);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.position = position;
            clone.transform.rotation = rotation;
            clone.m_SkipIntroAnim = true;
            clone.m_ShowTimer = clone.m_ShowDuration;
            clone.transform.parent = transform.parent;
            clone.Show(true, false);
            clone.SetSignedWidgetSize(size);
            clone.CustomDimension = CustomDimension;
            clone.NumRings = NumRings;
            clone.ViewsPerRing = ViewsPerRing;
            clone.CloneInitialMaterials(this);
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            return clone;
        }

        private Axis GetInferredManipulationAxis(
            Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside)
        {
            if (secondaryHandInside)
            {
                return Axis.Invalid;
            }

            Vector3 handsInObjectSpace = transform.InverseTransformDirection(primaryHand - secondaryHand);
            Vector3 abs = handsInObjectSpace.Abs();

            Axis bestAxis = Axis.Invalid;
            float bestDot = 0f;
            for (int i = 0; i < sm_AxisDirections.Length; ++i)
            {
                float dot = Vector3.Dot(abs, sm_AxisDirections[i].direction);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestAxis = sm_AxisDirections[i].axis;
                }
            }
            return bestAxis;
        }

        private static Vector3 CMax(Vector3 va, Vector3 vb)
        {
            return new Vector3(
                Mathf.Max(va.x, vb.x),
                Mathf.Max(va.y, vb.y),
                Mathf.Max(va.z, vb.z));
        }
    }
} // namespace TiltBrush
