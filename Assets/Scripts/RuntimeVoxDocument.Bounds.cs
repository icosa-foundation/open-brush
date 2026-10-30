// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class RuntimeVoxDocument
    {
        // Plan without mutating so preview and painting use the same format-limit check.
        internal static bool TryGetDrawingPadding(Vector3Int size, Vector3Int cell, int extent,
            out Vector3Int lower, out Vector3Int upper, Vector3Int? previousCell = null)
        {
            lower = upper = Vector3Int.zero;
            for (int axis = 0; axis < 3; ++axis)
            {
                long min = (long)Math.Min(cell[axis], previousCell?[axis] ?? cell[axis]) - extent;
                long max = (long)Math.Max(cell[axis], previousCell?[axis] ?? cell[axis]) + extent;
                long requiredLower = Math.Max(0L, -min);
                long requiredUpper = Math.Max(0L, max + 1 - size[axis]);
                if (size[axis] + requiredLower + requiredUpper > MaxModelDimension) return false;
                int spare = MaxModelDimension - size[axis] - (int)requiredLower - (int)requiredUpper;
                int low = (int)requiredLower;
                int high = (int)requiredUpper;
                if (min - 1 < 0 && spare > 0) { ++low; --spare; }
                if (max + 2 > size[axis] && spare > 0) ++high;
                lower[axis] = low;
                upper[axis] = high;
            }
            return true;
        }

        // Grow once for the entire frame's interpolated footprint, not once per cell.
        internal bool TryGrowDrawingBounds(RuntimeModel model, ref Vector3Int cell, out Vector3Int shift,
            int extent = 0, Vector3Int? previousCell = null)
        {
            if (!TryGetDrawingPadding(model.Size, cell, extent, out shift, out var upper, previousCell)) return false;
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
