// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using System.Runtime.CompilerServices;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("Read-only details of an occupied voxel at the time of a hit query")]
    [MoonSharpUserData]
    public class VoxHitApiWrapper
    {
        [LuaDocsDescription("The editable model containing this voxel; check model.isValid before reusing a stored hit")]
        public VoxModelApiWrapper model { get; }

        [LuaDocsDescription("Integer cell coordinates in the model's local voxel grid")]
        public Vector3 cell { get; }

        [LuaDocsDescription("The center of this cell in canvas coordinates")]
        public Vector3 position { get; }

        [LuaDocsDescription("The cell's palette index, from 1 to 255")]
        public int paletteIndex { get; }

        [LuaDocsDescription("The cell's RGB color and alpha from its palette entry")]
        public Color color { get; }

        internal VoxHitApiWrapper(VoxModelApiWrapper model, Vector3Int cell, int paletteIndex)
        {
            this.model = model;
            this.cell = cell;
            this.paletteIndex = paletteIndex;
            position = model.VoxelToCanvas(cell);
            color = model.document._Document.Palette[paletteIndex - 1];
        }
    }

    [LuaDocsDescription("An occupied voxel surface hit by a ray; inherited position remains the cell center")]
    [MoonSharpUserData]
    public sealed class VoxRayHitApiWrapper : VoxHitApiWrapper
    {
        [LuaDocsDescription("The surface intersection point in canvas coordinates")]
        public Vector3 point { get; }

        [LuaDocsDescription("The outward unit surface normal in canvas coordinates")]
        public Vector3 normal { get; }

        [LuaDocsDescription("The outward face direction in the model's local voxel grid: one of the six signed axis vectors")]
        public Vector3 face { get; }

        [LuaDocsDescription("The distance from the ray origin to the surface, in canvas units")]
        public float distance { get; }

        internal VoxRayHitApiWrapper(VoxModelApiWrapper model, VoxToolTarget.RayHit hit, TrTransform canvasPose)
            : base(model, hit.Voxel.Cell, hit.Voxel.PaletteIndex)
        {
            var inverse = canvasPose.inverse;
            point = inverse * hit.Point_GS;
            normal = inverse.MultiplyVector(hit.Normal_GS).normalized;
            face = hit.Face;
            distance = hit.Distance_GS / Mathf.Abs(canvasPose.scale);
        }
    }

    public partial class VoxApiWrapper
    {
        private sealed class QueryDocument
        {
            internal VoxDocumentApiWrapper Wrapper;
        }

        // External previews belong to a widget, never to the shared library Model.
        // Weak keys allow documents from closed sketches to be collected.
        private static readonly ConditionalWeakTable<ModelWidget, QueryDocument> s_QueryDocuments = new();
        private static readonly VoxToolTarget s_HitTarget = new(widget => GetQueryDocument(widget)?._Document);

        private static VoxDocumentApiWrapper GetQueryDocument(ModelWidget widget)
        {
            var cached = s_QueryDocuments.GetValue(widget, _ => new QueryDocument());
            if (cached.Wrapper != null && cached.Wrapper.isValid) return cached.Wrapper;
            var document = widget.EditableVoxDocument ?? widget.Model.CreateEditableVoxDocument();
            if (document == null) return null;
            cached.Wrapper = new VoxDocumentApiWrapper(document, widget,
                externalSource: widget.IsExternalVox ? widget.Model : null);
            return cached.Wrapper;
        }

        [LuaDocsDescription("Finds the nearest occupied voxel touching a sphere on the active layer. Position and optional radius are in canvas units. Returns nil when there is no hit. Querying an external VOX model does not embed it; the first successful edit does.")]
        [LuaDocsExample("local hit = Vox:FindHit(Brush.position, 0.02)")]
        public static VoxHitApiWrapper FindHit(Vector3 canvasPosition, float radius = 0f)
        {
            if (radius < 0 || float.IsNaN(radius) || float.IsInfinity(radius))
                throw new ArgumentOutOfRangeException(nameof(radius), "Voxel hit radius must be finite and nonnegative.");
            var canvas = App.Scene?.ActiveCanvas;
            if (canvas == null || WidgetManager.m_Instance == null || !WidgetManager.m_Instance.IsInitialized) return null;
            if (!s_HitTarget.TryFindCell(canvas, canvas.Pose * canvasPosition,
                    Mathf.Abs(canvas.Pose.scale) * radius, out var hit)) return null;
            var document = GetQueryDocument(hit.Widget);
            return new VoxHitApiWrapper(document.Model(hit.ModelIndex), hit.Cell, hit.PaletteIndex);
        }

        [LuaDocsDescription("Finds the first occupied voxel surface along a ray on the active layer. Origin, direction and distance use canvas space; direction need not be normalized. Returns nil on a miss. A ray starting inside occupied cells returns the first exposed exit face. A ray on a surface can hit at distance zero. Edge/corner crossings are processed X, then Y, then Z. Querying does not embed an external model.")]
        [LuaDocsExample("local hit = Vox:Raycast(Brush.position, Brush.direction, 1)")]
        public static VoxRayHitApiWrapper Raycast(Vector3 origin, Vector3 direction, float maxDistance = 1f)
        {
            if (maxDistance < 0 || float.IsNaN(maxDistance) || float.IsInfinity(maxDistance))
                throw new ArgumentOutOfRangeException(nameof(maxDistance), "Voxel ray distance must be finite and nonnegative.");
            if (!VoxToolTarget.IsFinite(origin))
                throw new ArgumentException("Voxel ray origin must be finite.", nameof(origin));
            float length = direction.magnitude;
            if (!VoxToolTarget.IsFinite(direction) || length == 0 || float.IsInfinity(length))
                throw new ArgumentException("Voxel ray direction must be finite and nonzero.", nameof(direction));
            var canvas = App.Scene?.ActiveCanvas;
            if (canvas == null || WidgetManager.m_Instance == null || !WidgetManager.m_Instance.IsInitialized) return null;
            var pose = canvas.Pose;
            var ray = new Ray(pose * origin, pose.MultiplyVector(direction / length));
            if (!s_HitTarget.TryRaycast(canvas, ray, maxDistance * Mathf.Abs(pose.scale), out var hit)) return null;
            var document = GetQueryDocument(hit.Voxel.Widget);
            return new VoxRayHitApiWrapper(document.Model(hit.Voxel.ModelIndex), hit, pose);
        }
    }
}
