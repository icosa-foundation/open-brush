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
    /// A transient grab target used to transform or resize one SDF component.
    internal sealed class SdfComponentHandle : GrabWidget
    {
        private const float k_TransformVisualDiameter = 0.075f;
        private const float k_DimensionVisualDiameter = 0.05f;

        private SdfEditorPopup m_Owner;
        private Renderer m_Renderer;
        private int m_ComponentIndex;
        private int m_DimensionIndex = -1;
        private Vector3 m_DimensionAxis;
        private float m_VisualDiameter;
        private TrTransform m_DragStart;
        private Vector4 m_ResizeStart;
        private bool m_Selected;

        internal bool IsBeingDragged => m_UserInteracting;
        internal int ComponentIndex => m_ComponentIndex;
        internal bool IsDimensionHandle => m_DimensionIndex >= 0;
        internal int DimensionIndex => m_DimensionIndex;
        internal Vector3 DimensionAxis => m_DimensionAxis;

        internal static SdfComponentHandle Create(
            SdfEditorPopup owner, int componentIndex, TrTransform pose_GS,
            Material sourceMaterial)
        {
            return Create(
                owner, componentIndex, -1, Vector3.zero, pose_GS,
                PrimitiveType.Sphere, k_TransformVisualDiameter, sourceMaterial);
        }

        internal static SdfComponentHandle CreateDimension(
            SdfEditorPopup owner, int componentIndex, int dimensionIndex,
            Vector3 dimensionAxis, TrTransform pose_GS, Material sourceMaterial)
        {
            return Create(
                owner, componentIndex, dimensionIndex, dimensionAxis, pose_GS,
                PrimitiveType.Cube, k_DimensionVisualDiameter, sourceMaterial);
        }

        private static SdfComponentHandle Create(
            SdfEditorPopup owner, int componentIndex, int dimensionIndex,
            Vector3 dimensionAxis, TrTransform pose_GS, PrimitiveType visualType,
            float visualDiameter, Material sourceMaterial)
        {
            GameObject handleObject = GameObject.CreatePrimitive(visualType);
            handleObject.SetActive(false);

            var handle = handleObject.AddComponent<SdfComponentHandle>();
            handle.m_Owner = owner;
            handle.m_ComponentIndex = componentIndex;
            handle.m_DimensionIndex = dimensionIndex;
            handle.m_DimensionAxis = dimensionAxis.normalized;
            handle.m_VisualDiameter = visualDiameter;
            handle.m_ShowDuration = 0.01f;
            handle.m_GrabDistance = 0.11f;
            handle.m_CollisionRadius = 0.08f;
            handle.m_RecordMovements = false;
            handle.m_Mesh = handleObject.transform;
            handle.m_Renderer = handleObject.GetComponent<Renderer>();
            handle.m_TintableMeshes = new[] { handle.m_Renderer };
            if (sourceMaterial != null)
            {
                handle.m_Renderer.sharedMaterial = sourceMaterial;
            }

            Transform canvas = App.Scene.SelectionCanvas.transform;
            handleObject.transform.SetParent(canvas, false);
            handle.SetPose(pose_GS);
            handle.UpdateName();
            HierarchyUtils.RecursivelySetLayer(
                handleObject.transform, App.Scene.SelectionCanvas.gameObject.layer);

            handleObject.SetActive(true);
            handle.Show(true, false);
            return handle;
        }

        internal void SetComponentIndex(int componentIndex)
        {
            m_ComponentIndex = componentIndex;
            UpdateName();
        }

        private void UpdateName()
        {
            gameObject.name = IsDimensionHandle
                ? $"SDF Component {m_ComponentIndex + 1} Dimension {m_DimensionIndex + 1} Handle"
                : $"SDF Component Handle {m_ComponentIndex + 1}";
        }

        internal void SetPose(TrTransform pose_GS)
        {
            if (m_UserInteracting)
            {
                return;
            }
            transform.position = pose_GS.translation;
            transform.rotation = pose_GS.rotation;
            float canvasScale = Mathf.Max(1e-5f, transform.parent.lossyScale.x);
            transform.localScale = Vector3.one * (m_VisualDiameter / canvasScale);
        }

        internal void SetSelected(bool selected)
        {
            m_Selected = selected;
            ApplyRestingColor();
        }

        private void ApplyRestingColor()
        {
            if (m_Renderer == null)
            {
                return;
            }
            Color color;
            if (m_Selected)
            {
                color = new Color(1f, 0.75f, 0.15f, 0.9f);
            }
            else if (IsDimensionHandle)
            {
                color = new Color(
                    0.35f + 0.5f * Mathf.Abs(m_DimensionAxis.x),
                    0.35f + 0.5f * Mathf.Abs(m_DimensionAxis.y),
                    0.35f + 0.5f * Mathf.Abs(m_DimensionAxis.z), 0.8f);
            }
            else
            {
                color = new Color(0.35f, 0.8f, 1f, 0.65f);
            }
            if (m_Renderer.material.HasProperty("_Color"))
            {
                m_Renderer.material.color = color;
            }
        }

        internal void DisposeHandle()
        {
            m_Owner = null;
            if (m_Registered && WidgetManager.m_Instance != null &&
                WidgetManager.m_Instance.IsInitialized)
            {
                WidgetManager.m_Instance.UnregisterGrabWidget(gameObject);
                m_Registered = false;
            }
            Destroy(gameObject);
        }

        public override bool CanGrabDuringDeselection()
        {
            return true;
        }

        public override void Activate(bool active)
        {
            base.Activate(active);
            if (!active)
            {
                ApplyRestingColor();
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (m_UserInteracting)
            {
                if (IsDimensionHandle)
                {
                    m_Owner?.PreviewHandleResize(this);
                }
                else
                {
                    m_Owner?.PreviewHandleTransform(this);
                }
            }
        }

        protected override void OnUserBeginInteracting()
        {
            base.OnUserBeginInteracting();
            if (IsDimensionHandle)
            {
                m_ResizeStart = m_Owner != null
                    ? m_Owner.BeginHandleResize(this)
                    : Vector4.zero;
            }
            else
            {
                m_DragStart = m_Owner != null
                    ? m_Owner.BeginHandleTransform(this)
                    : TrTransform.identity;
            }
        }

        protected override void OnUserEndInteracting()
        {
            base.OnUserEndInteracting();
            if (IsDimensionHandle)
            {
                m_Owner?.CommitHandleResize(this, m_ResizeStart);
            }
            else
            {
                m_Owner?.CommitHandleTransform(this, m_DragStart);
            }
        }

        protected override void OnDestroy()
        {
            if (m_Registered && WidgetManager.m_Instance != null &&
                WidgetManager.m_Instance.IsInitialized)
            {
                WidgetManager.m_Instance.UnregisterGrabWidget(gameObject);
                m_Registered = false;
            }
            base.OnDestroy();
        }
    }
}
