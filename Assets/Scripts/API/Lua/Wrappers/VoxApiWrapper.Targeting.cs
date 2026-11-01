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
    public sealed class VoxHitApiWrapper
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
    }
}
