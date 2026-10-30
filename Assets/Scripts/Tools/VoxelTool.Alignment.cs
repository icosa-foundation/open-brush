// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class VoxelTool
    {
        public bool AlignCurrentModelToGrid()
        {
            EndGesture();
            EnsureCurrentTarget();
            if (!HasCurrentTarget) return false;
            var widget = CurrentTargetWidget;
            var grid = m_CurrentDocument._Document.Models.First(model => model.IsVisible);
            var gridToWidget = VoxMeshBuilder.GetModelTransform(grid);
            var canvas = App.Scene.ActiveCanvas.Pose;
            var before = TrTransform.FromTransform(widget.transform);
            var selection = SelectionManager.m_Instance;
            float spacing = selection.ConfiguredSnappingGridSize;

            // Align the voxel axes, accounting for VOX's axis convention and any
            // authored grid rotation, rather than just resetting the widget rotation.
            Quaternion gridRotation = Quaternion.Inverse(canvas.rotation) * before.rotation * gridToWidget.rotation;
            Quaternion alignedRotation = selection.QuantizeAngle(gridRotation, 90f, useEnabledAxes: false);
            var after = TrTransform.TRS(Vector3.zero,
                canvas.rotation * alignedRotation * Quaternion.Inverse(gridToWidget.rotation),
                Mathf.Sign(before.scale) * Mathf.Abs(canvas.scale) * spacing);

            Vector3 center = gridToWidget * (((Vector3)grid.Size - Vector3.one) * 0.5f);
            after.translation = before * center - after.MultiplyVector(center);
            // Use the same low-corner anchor and axis settings as voxel grab snapping.
            Vector3 corner = gridToWidget * (Vector3.one * -0.5f);
            Vector3 cornerWorld = after * corner;
            after.translation += selection.SnapToGrid_GS(cornerWorld, spacing) - cornerWorld;
            TrTransform local = widget.Canvas.Pose.inverse * after;
            if (local == widget.LocalTransform) return true;
            widget.ClearVelocities();
            return SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new AlignVoxModelToGridCommand(widget, local));
        }
    }
}
