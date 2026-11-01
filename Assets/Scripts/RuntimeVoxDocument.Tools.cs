// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class RuntimeVoxDocument
    {
        internal bool RemoveConnectedRegion(RuntimeModel model, Vector3Int seed)
        {
            if (!ContainsModel(model) || !model.RemoveVoxel(seed)) return false;
            var pending = new Queue<Vector3Int>();
            pending.Enqueue(seed);
            while (pending.Count > 0)
            {
                Vector3Int cell = pending.Dequeue();
                foreach (var direction in s_FaceNeighbors)
                {
                    Vector3Int neighbor = cell + direction;
                    if (model.RemoveVoxel(neighbor)) pending.Enqueue(neighbor);
                }
            }
            return true;
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
