// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
        private void ChooseActivationTarget()
        {
            Transform head = ViewpointScript.Head;
            var ray = new Ray(head.position, head.forward);
            Plane[] view = GeometryUtility.CalculateFrustumPlanes(App.VrSdk.GetVrCamera());
            ModelWidget best = null;
            float bestAlignment = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;
            foreach (var widget in WidgetManager.m_Instance.ModelWidgets)
            {
                if (!IsAvailableTarget(widget) || widget.EditableVoxDocument == null ||
                    !widget.EditableVoxDocument.Models.Any(model => model.IsVisible)) continue;
                Bounds bounds = widget.GetBounds();
                if (!GeometryUtility.TestPlanesAABB(view, bounds)) continue;
                Vector3 offset = bounds.center - head.position;
                float distance = offset.magnitude;
                float alignment = 1f - Vector3.Dot(head.forward, offset.normalized);
                // Prefer the nearest volume intersected by the gaze ray, then the
                // most centered visible volume. Do not keep retargeting during drawing.
                if (bounds.IntersectRay(ray, out float hitDistance))
                {
                    alignment = -1f;
                    distance = hitDistance;
                }
                if (alignment > bestAlignment || (alignment == bestAlignment && distance >= bestDistance)) continue;
                best = widget;
                bestAlignment = alignment;
                bestDistance = distance;
            }
            if (best != null) BindTargetWidget(best);
            else CreateModelInFrontOfUser();
        }

        private void CreateModelInFrontOfUser()
        {
            Transform head = ViewpointScript.Head;
            Vector3 worldPosition = head.position + head.forward * (0.4f * App.METERS_TO_UNITS);
            Vector3 position = App.Scene.ActiveCanvas.Pose.inverse * worldPosition;
            float size = SelectionManager.m_Instance.ConfiguredSnappingGridSize;
            Vector3 center = GetNewModelCellCenter(position, size);
            Mode = EditMode.Add;
            if (CreateGrid(center, size))
            {
                m_CreateNewGrid = false;
                // Activation creates its own undo step; later painting is a separate gesture.
                m_GestureCreationCommand = null;
            }
        }
    }
}
