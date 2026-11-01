// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class RuntimeVoxDocument
    {
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
