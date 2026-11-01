// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    // Tool-local, read-only previews avoid reparsing external VOX files every frame.
    // Editing callers clone an external preview before adopting it into a widget.
    internal sealed class VoxToolTarget
    {
        internal struct Hit
        {
            public ModelWidget Widget;
            public RuntimeVoxDocument Document;
            public int ModelIndex;
            public Vector3Int Cell;
            public byte PaletteIndex;
        }

        private readonly Dictionary<Model, RuntimeVoxDocument> m_Previews = new();

        internal void Clear() => m_Previews.Clear();

        internal RuntimeVoxDocument GetDocument(ModelWidget widget)
        {
            if (widget.EditableVoxDocument != null) return widget.EditableVoxDocument;
            if (!widget.IsExternalVox || !string.IsNullOrEmpty(widget.Subtree)) return null;
            if (!m_Previews.TryGetValue(widget.Model, out var document))
            {
                document = widget.Model.CreateEditableVoxDocument();
                m_Previews.Add(widget.Model, document);
            }
            return document;
        }

        // Convert library content only after an edit succeeds, and undo the conversion
        // together with the edit. Read-only targeting never dirties a sketch.
        internal static bool Edit(ModelWidget widget, RuntimeVoxDocument preview,
            Func<RuntimeVoxDocument, bool> edit, Action<BaseCommand> addChildren = null)
        {
            bool external = widget.IsExternalVox;
            var document = external ? preview.CloneDocument() : preview;
            var before = document.CaptureSnapshot();
            if (!edit(document))
            {
                document.RestoreSnapshot(before);
                return false;
            }
            EmbedVoxModelCommand embedding = null;
            if (external)
            {
                embedding = new EmbedVoxModelCommand(widget, document);
                embedding.Redo();
            }
            var command = new ModifyVoxDocumentCommand(widget, before, document.CaptureSnapshot(), embedding);
            BaseCommand root = embedding ?? (BaseCommand)command;
            addChildren?.Invoke(root);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(root);
            return true;
        }

        internal bool TryFindCell(CanvasScript canvas, Vector3 center_GS, float radius_GS, out Hit hit)
        {
            hit = default;
            float bestDistance = float.PositiveInfinity;
            foreach (ModelWidget widget in WidgetManager.m_Instance.ModelWidgets)
            {
                if (widget == null || !widget.Showing || !widget.gameObject.activeInHierarchy ||
                    widget.Canvas != canvas || !string.IsNullOrEmpty(widget.Subtree)) continue;
                Collider collider = widget.GrabCollider;
                if (collider == null || collider.bounds.SqrDistance(center_GS) > radius_GS * radius_GS) continue;
                var document = GetDocument(widget);
                if (document == null) continue;
                for (int i = 0; i < document.Models.Count; ++i)
                {
                    var model = document.Models[i];
                    if (!model.IsVisible) continue;
                    TrTransform grid_GS = TrTransform.FromTransform(widget.transform) * VoxMeshBuilder.GetModelTransform(model);
                    if (!TryFindCell(model, grid_GS, center_GS, radius_GS, out var cell, out float distance) ||
                        distance >= bestDistance) continue;
                    bestDistance = distance;
                    model.TryGetPaletteIndex(cell, out byte paletteIndex);
                    hit = new Hit { Widget = widget, Document = document, ModelIndex = i,
                        Cell = cell, PaletteIndex = paletteIndex };
                }
            }
            return hit.Widget != null;
        }

        internal static bool TryFindCell(RuntimeVoxDocument.RuntimeModel model, TrTransform grid_GS,
            Vector3 center_GS, float radius_GS, out Vector3Int cell, out float distanceSquared)
        {
            cell = default;
            distanceSquared = float.PositiveInfinity;
            if (Mathf.Abs(grid_GS.scale) < 0.000001f || radius_GS < 0) return false;
            Vector3 center = grid_GS.inverse * center_GS;
            float radius = radius_GS / Mathf.Abs(grid_GS.scale);
            var min = Vector3Int.Max(Vector3Int.zero, Vector3Int.CeilToInt(center - Vector3.one * (radius + 0.5f)));
            var max = Vector3Int.Min(model.Size - Vector3Int.one, Vector3Int.FloorToInt(center + Vector3.one * (radius + 0.5f)));
            long volume = (long)Mathf.Max(0, max.x - min.x + 1) * Mathf.Max(0, max.y - min.y + 1) * Mathf.Max(0, max.z - min.z + 1);
            bool found = false;
            // Small tools query nearby dictionary keys. Large tools scan occupied cells,
            // avoiding work proportional to the entire declared grid volume.
            if (volume < model.Voxels.Count)
            {
                for (int x = min.x; x <= max.x; ++x)
                    for (int y = min.y; y <= max.y; ++y)
                        for (int z = min.z; z <= max.z; ++z)
                        {
                            var candidate = new Vector3Int(x, y, z);
                            if (model.TryGetPaletteIndex(candidate, out _) &&
                                ConsiderCell(candidate, center, radius, ref cell, ref distanceSquared)) found = true;
                        }
            }
            else
            {
                foreach (var candidate in model.Voxels.Keys)
                    if (ConsiderCell(candidate, center, radius, ref cell, ref distanceSquared)) found = true;
            }
            distanceSquared *= grid_GS.scale * grid_GS.scale;
            return found;
        }

        private static bool ConsiderCell(Vector3Int candidate, Vector3 center, float radius,
            ref Vector3Int cell, ref float bestDistance)
        {
            Vector3 delta = center - (Vector3)candidate;
            Vector3 outside = Vector3.Max(Vector3.zero, new Vector3(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z)) - Vector3.one * 0.5f);
            if (outside.sqrMagnitude > radius * radius || delta.sqrMagnitude >= bestDistance) return false;
            cell = candidate;
            bestDistance = delta.sqrMagnitude;
            return true;
        }
    }
}
