// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using UnityEngine;

namespace TiltBrush
{
    // Transient grip target. Only the resulting document edit belongs in sketch history.
    public sealed class VoxelGridResizeHandle : GrabWidget
    {
        private VoxelTool m_Owner;
        private bool m_Available;
        private float m_Size = 1f;
        private InputManager.ControllerName m_Controller;
        internal RuntimeVoxDocument.RuntimeModel Model { get; private set; }
        internal int Face { get; private set; }
        internal bool Hovered { get; private set; }
        public override bool AllowDormancy => false;

        protected override void Awake()
        {
            m_ShowDuration = 0.05f;
            m_RecordMovements = false;
            m_AllowSnapping = false;
            m_BoxCollider = gameObject.AddComponent<BoxCollider>();
            m_BoxCollider.isTrigger = true;
            m_GrabCollider = m_BoxCollider;
            base.Awake();
        }

        internal void Configure(VoxelTool owner, RuntimeVoxDocument.RuntimeModel model, int face,
            TrTransform pose, Vector3 min, Vector3 max)
        {
            // Keep a held object's identity stable until the native grab has ended.
            if (m_UserInteracting && (Model != model || Face != face)) return;
            m_Owner = owner;
            Model = model;
            Face = face;
            Vector3 center = (min + max) * 0.5f;
            center[face / 2] = face % 2 == 0 ? min[face / 2] : max[face / 2];
            LocalTransform = TrTransform.TRS(pose * center, pose.rotation,
                Mathf.Abs(pose.scale) * VoxEditPreviewRenderer.GetExpansionHandleSize(pose));
            SetAvailable(model.Size[face / 2] < RuntimeVoxDocument.MaxModelDimension);
        }

        internal void SetAvailable(bool available)
        {
            m_Available = available;
            m_BoxCollider.enabled = available;
            if (available && !gameObject.activeSelf) Show(true, false);
            else if (available && !Showing) Show(true, false);
            else if (!available && !m_UserInteracting) gameObject.SetActive(false);
            if (!available) Hovered = false;
        }

        public override float GetActivationScore(Vector3 worldPosition, InputManager.ControllerName controller)
        {
            if (!m_Available || m_Owner == null || !m_Owner.CanGrabResizeHandle) return -1f;
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            float distance = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
            // Within the handle, prefer it to the model body or selection bounds.
            return distance <= 0.5f ? 1.1f - distance * 0.1f : -1f;
        }

        public override void Activate(bool active) => Hovered = active;
        public override float GetSignedWidgetSize() => m_Size;
        protected override void SetWidgetSizeInternal(float size)
        {
            m_Size = size;
            transform.localScale = Vector3.one * size;
        }

        protected override void OnUserBeginInteracting()
        {
            m_Controller = m_InteractingController;
            m_Owner?.BeginGridResize(this, InputManager.m_Instance.GetControllerPosition(m_Controller));
        }

        public override void RecordAndSetPosRot(TrTransform inputXf)
        {
            ClearVelocities();
            m_Owner?.UpdateGridResize(this, InputManager.m_Instance.GetControllerPosition(m_Controller));
        }

        protected override void OnUserEndInteracting()
        {
            ClearVelocities();
            m_Owner?.EndGridResize(this);
            if (!m_Available) gameObject.SetActive(false);
        }

        protected override void OnUpdate()
        {
            if (m_UserInteracting) m_Owner?.DrawHeldGridResize(this);
        }

        public override void OnPreDestroy()
        {
            if (m_Registered && WidgetManager.m_Instance != null)
            {
                WidgetManager.m_Instance.UnregisterGrabWidget(gameObject);
                m_Registered = false;
            }
            m_Owner?.CancelGridResize(this);
            m_Owner = null;
        }
    }
}
