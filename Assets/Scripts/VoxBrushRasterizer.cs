// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    internal static class VoxBrushRasterizer
    {
        internal static bool TryGetBounds(VoxBrushFootprint footprint, Matrix4x4 linear,
            out Vector3Int min, out Vector3Int max)
        {
            Vector3 lower = Vector3.one * float.PositiveInfinity;
            Vector3 upper = Vector3.one * float.NegativeInfinity;
            for (int box = 0; box < footprint.BoxCount; ++box)
            {
                footprint.GetBox(box, out var first, out var last);
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 point = linear.MultiplyVector(new Vector3(
                        (corner & 1) == 0 ? first.x - 0.5f : last.x + 0.5f,
                        (corner & 2) == 0 ? first.y - 0.5f : last.y + 0.5f,
                        (corner & 4) == 0 ? first.z - 0.5f : last.z + 0.5f));
                    lower = Vector3.Min(lower, point);
                    upper = Vector3.Max(upper, point);
                }
            }
            min = max = default;
            if (!VoxToolTarget.IsFinite(lower) || !VoxToolTarget.IsFinite(upper) ||
                lower.magnitude > int.MaxValue / 4f || upper.magnitude > int.MaxValue / 4f) return false;
            min = Vector3Int.CeilToInt(lower);
            max = Vector3Int.FloorToInt(upper);
            return true;
        }

        private static bool IsGridTransform(Matrix4x4 linear)
        {
            int usedAxes = 0;
            for (int i = 0; i < 3; ++i)
            {
                Vector3 column = linear.GetColumn(i);
                var axis = Vector3Int.RoundToInt(column);
                if ((column - (Vector3)axis).sqrMagnitude > 0.0000000001f || axis.sqrMagnitude != 1) return false;
                int bit = axis.x != 0 ? 1 : axis.y != 0 ? 2 : 4;
                if ((usedAxes & bit) != 0) return false;
                usedAxes |= bit;
            }
            return true;
        }

        private static bool Contains(VoxBrushFootprint footprint, Vector3 point)
        {
            var cell = Vector3Int.FloorToInt(point + Vector3.one * 0.5f);
            int x = Mathf.Abs(cell.x), y = Mathf.Abs(cell.y), z = Mathf.Abs(cell.z);
            int r = footprint.Radius;
            if (x <= r && y <= r && z <= r) return true;
            return footprint.Expanded &&
                ((x == r + 1 && y <= r && z <= r) || (y == r + 1 && x <= r && z <= r) ||
                    (z == r + 1 && x <= r && y <= r));
        }

        // The same quantized volume supplies edits and previews. Exact lattice
        // symmetries use integer cells; arbitrary rotations sample cell centers.
        internal static void GetCells(VoxBrushFootprint footprint, Matrix4x4 linear, Matrix4x4 inverse,
            Vector3Int center, Vector3Int minOffset, Vector3Int maxOffset,
            RuntimeVoxDocument.RuntimeModel model, bool occupiedOnly, List<Vector3Int> cells)
        {
            cells.Clear();
            if (IsGridTransform(linear))
            {
                for (int box = 0; box < footprint.BoxCount; ++box)
                {
                    footprint.GetBox(box, out var min, out var max);
                    for (int x = min.x; x <= max.x; ++x)
                        for (int y = min.y; y <= max.y; ++y)
                            for (int z = min.z; z <= max.z; ++z)
                            {
                                var cell = center + Vector3Int.RoundToInt(linear.MultiplyVector(new Vector3(x, y, z)));
                                if (!occupiedOnly || model.TryGetPaletteIndex(cell, out _)) cells.Add(cell);
                            }
                }
                return;
            }
            Vector3Int first = center + minOffset;
            Vector3Int last = center + maxOffset;
            if (occupiedOnly)
            {
                first = Vector3Int.Max(first, Vector3Int.zero);
                last = Vector3Int.Min(last, model.Size - Vector3Int.one);
                long volume = (long)Mathf.Max(0, last.x - first.x + 1) * Mathf.Max(0, last.y - first.y + 1) * Mathf.Max(0, last.z - first.z + 1);
                if (volume > model.Voxels.Count)
                {
                    foreach (var cell in model.Voxels.Keys)
                        if (cell.x >= first.x && cell.y >= first.y && cell.z >= first.z &&
                            cell.x <= last.x && cell.y <= last.y && cell.z <= last.z &&
                            Contains(footprint, inverse.MultiplyVector(cell - center))) cells.Add(cell);
                    return;
                }
            }
            for (int x = first.x; x <= last.x; ++x)
                for (int y = first.y; y <= last.y; ++y)
                    for (int z = first.z; z <= last.z; ++z)
                    {
                        var cell = new Vector3Int(x, y, z);
                        if (occupiedOnly && !model.TryGetPaletteIndex(cell, out _)) continue;
                        if (Contains(footprint, inverse.MultiplyVector(cell - center))) cells.Add(cell);
                    }
        }

        // Clip before walking, so fast controller movement outside a grid does not
        // turn a short edit into an unbounded sequence of off-grid stamps.
        internal static void GetPath(Vector3Int from, Vector3Int to, Vector3Int min, Vector3Int max,
            List<Vector3Int> path)
        {
            path.Clear();
            Vector3 start = from;
            Vector3 delta = (Vector3)to - start;
            float enter = 0, exit = 1;
            for (int axis = 0; axis < 3; ++axis)
            {
                if (delta[axis] == 0)
                {
                    if (start[axis] < min[axis] || start[axis] > max[axis]) return;
                    continue;
                }
                float first = (min[axis] - start[axis]) / delta[axis];
                float last = (max[axis] - start[axis]) / delta[axis];
                if (first > last) (first, last) = (last, first);
                enter = Mathf.Max(enter, first);
                exit = Mathf.Min(exit, last);
                if (enter > exit) return;
            }
            Vector3Int current = Vector3Int.RoundToInt(start + delta * enter);
            Vector3Int end = Vector3Int.RoundToInt(start + delta * exit);
            Vector3Int difference = end - current;
            var step = new Vector3Int(Math.Sign(difference.x), Math.Sign(difference.y), Math.Sign(difference.z));
            var interval = new Vector3(difference.x == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(difference.x),
                difference.y == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(difference.y),
                difference.z == 0 ? float.PositiveInfinity : 1f / Mathf.Abs(difference.z));
            Vector3 next = interval * 0.5f;
            path.Add(current);
            while (current != end)
            {
                float crossing = Mathf.Min(next.x, Mathf.Min(next.y, next.z));
                if (next.x <= crossing) { current.x += step.x; next.x += interval.x; }
                if (next.y <= crossing) { current.y += step.y; next.y += interval.y; }
                if (next.z <= crossing) { current.z += step.z; next.z += interval.z; }
                path.Add(current);
            }
        }
    }
}
