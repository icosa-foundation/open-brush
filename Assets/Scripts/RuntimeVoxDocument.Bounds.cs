// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class RuntimeVoxDocument
    {
        // Include a drawing cell with one empty cell beyond it where the format permits.
        // Call once per drawing frame, before applying its interpolated cells.
        internal bool TryGrowDrawingBounds(RuntimeModel model, ref Vector3Int cell, out Vector3Int shift)
        {
            shift = Vector3Int.zero;
            var upper = Vector3Int.zero;
            for (int axis = 0; axis < 3; ++axis)
            {
                int size = model.Size[axis];
                long requiredLower = Math.Max(0L, -(long)cell[axis]);
                long requiredUpper = Math.Max(0L, (long)cell[axis] + 1 - size);
                if (size + requiredLower + requiredUpper > MaxModelDimension) return false;
                int spare = MaxModelDimension - size - (int)requiredLower - (int)requiredUpper;
                int lower = (int)requiredLower;
                int higher = (int)requiredUpper;
                if (cell[axis] - (long)1 < 0 && spare > 0) { ++lower; --spare; }
                if ((long)cell[axis] + 2 > size && spare > 0) ++higher;
                shift[axis] = lower;
                upper[axis] = higher;
            }
            ResizeModelBounds(model, shift, upper);
            cell += shift;
            return true;
        }

        // Tighten only after a gesture. Negative padding rebases indices and origins
        // together, so both generated and imported artwork stays visually fixed.
        internal bool FitDrawingBounds(RuntimeModel model)
        {
            Vector3Int min = model.Size;
            Vector3Int max = Vector3Int.one * -1;
            foreach (var cell in model.Voxels.Keys)
            {
                min = Vector3Int.Min(min, cell);
                max = Vector3Int.Max(max, cell);
            }
            var lower = Vector3Int.zero;
            var upper = Vector3Int.zero;
            for (int axis = 0; axis < 3; ++axis)
            {
                int start;
                int dimension;
                if (model.Voxels.Count == 0)
                {
                    dimension = 3;
                    start = model.Size[axis] / 2 - 1;
                }
                else
                {
                    int occupied = max[axis] - min[axis] + 1;
                    int padding = Mathf.Min(2, MaxModelDimension - occupied);
                    start = min[axis] - (padding + 1) / 2;
                    dimension = Mathf.Max(3, occupied + padding);
                }
                lower[axis] = -start;
                upper[axis] = start + dimension - model.Size[axis];
            }
            return ResizeModelBounds(model, lower, upper);
        }
    }
}
