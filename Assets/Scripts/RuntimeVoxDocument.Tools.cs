// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class RuntimeVoxDocument
    {
        internal RuntimeVoxDocument DetachConnectedRegion(RuntimeModel model, Vector3Int seed)
        {
            if (!ContainsModel(model) || !model.IsVisible || !model.TryGetPaletteIndex(seed, out _)) return null;
            var cells = new Dictionary<Vector3Int, byte>();
            var pending = new Queue<Vector3Int>();
            pending.Enqueue(seed);
            while (pending.Count > 0)
            {
                Vector3Int cell = pending.Dequeue();
                if (cells.ContainsKey(cell) || !model.TryGetPaletteIndex(cell, out byte index)) continue;
                cells.Add(cell, index);
                foreach (var direction in s_FaceNeighbors) pending.Enqueue(cell + direction);
            }
            // There is nothing to separate when this is already the object's only island.
            if (cells.Count == m_models.Where(item => item.IsVisible).Sum(item => item.Voxels.Count)) return null;

            var region = new RuntimeVoxDocument();
            System.Array.Copy(Palette, region.Palette, Palette.Length);
            foreach (byte index in cells.Values.Distinct())
                if (m_materials.TryGetValue(index, out var material)) region.m_materials.Add(index, material);
            string name = string.IsNullOrWhiteSpace(model.Name) ? "Voxel Region" : $"{model.Name} Region";
            var detached = new RuntimeModel(name, model.Size, sourceModelId: -1, isCopy: false,
                voxels: cells, globalRotation: model.GlobalRotation, localRotation: model.GlobalRotation,
                localTransformOffset: GetSceneTranslation(model)) { TransformOffset = model.TransformOffset };
            region.m_models.Add(detached);
            region.FitDrawingBounds(detached);

            // Other placed instances of a shared definition must keep their cells.
            MakeModelUnique(model);
            foreach (var cell in cells.Keys) model.RemoveVoxel(cell);
            if (model.Voxels.Count == 0) RemoveModel(model);
            else FitDrawingBounds(model);
            return region;
        }

        internal bool RecolorAllVoxels(Color32 color)
        {
            if (!m_models.Any(model => model.Voxels.Count > 0)) return false;
            // Match the dedicated Paint tool: allocate a diffuse color rather than
            // changing shared palette/material entries in place.
            byte index = GetOrAddPaletteColor(color);
            if (index == 0) return false;
            bool changed = false;
            foreach (var definition in m_models.Select(model => model.Definition).Distinct())
                foreach (var cell in definition.Cells.Keys.ToArray())
                {
                    if (definition.Cells[cell] == index) continue;
                    definition.Cells[cell] = index;
                    changed = true;
                }
            return changed;
        }
    }
}
