// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public partial class SelectionWidget
    {
        private ModelWidget m_VoxRegionGrabWidget;
        private TrTransform m_VoxRegionSelectionStart, m_VoxRegionModelStart, m_VoxRegionVirtualGrab;
        private bool m_VoxRegionRecordMovements;
        internal bool VoxRegionGrabActive => m_VoxRegionGrabWidget != null;
        internal bool HasVoxRegionSelection => SelectionManager.m_Instance.SelectedWidgets
            .OfType<ModelWidget>().Any(widget => widget.HasVoxRegionSelection);

        private void BeginVoxRegionGrab()
        {
            var widget = SelectionManager.m_Instance.SelectedWidgets.OfType<ModelWidget>()
                .FirstOrDefault(model => model.HasVoxRegionSelection);
            if (widget == null) return;
            widget.UserInteracting(true, m_InteractingController);
            if (!widget.VoxRegionGrabActive) return;
            m_VoxRegionGrabWidget = widget;
            m_VoxRegionSelectionStart = m_VoxRegionVirtualGrab = base.GetGrabbedTrTransform();
            m_VoxRegionModelStart = widget.GetGrabbedTrTransform();
            m_VoxRegionRecordMovements = m_RecordMovements;
            m_RecordMovements = false;
            ClearVelocities();
        }

        public override TrTransform GetGrabbedTrTransform()
            => VoxRegionGrabActive ? m_VoxRegionVirtualGrab : base.GetGrabbedTrTransform();

        public override void RecordAndSetPosRot(TrTransform input)
        {
            if (!VoxRegionGrabActive)
            {
                base.RecordAndSetPosRot(input);
                return;
            }
            input.scale = m_VoxRegionSelectionStart.scale;
            m_VoxRegionVirtualGrab = input;
            m_VoxRegionGrabWidget.RecordAndSetPosRot(
                input * m_VoxRegionSelectionStart.inverse * m_VoxRegionModelStart);
        }

        public override void RecordAndSetSize(float value)
        {
            if (!HasVoxRegionSelection) base.RecordAndSetSize(value);
        }

        public override void SetVelocities(Vector3 linear, Vector3 angular, Vector3 pivot)
        {
            if (HasVoxRegionSelection) ClearVelocities();
            else base.SetVelocities(linear, angular, pivot);
        }

        protected override void OnUserEndInteracting()
        {
            if (!VoxRegionGrabActive)
            {
                base.OnUserEndInteracting();
                return;
            }
            try
            {
                m_VoxRegionGrabWidget.UserInteracting(false);
                ClearVelocities();
                base.OnUserEndInteracting();
            }
            finally
            {
                m_RecordMovements = m_VoxRegionRecordMovements;
                m_VoxRegionGrabWidget = null;
            }
        }
    }
}
