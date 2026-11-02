// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class RuntimeVoxDocument
    {
        internal static bool InRegion(Vector3Int cell, Vector3Int min, Vector3Int max)
            => cell.x >= min.x && cell.y >= min.y && cell.z >= min.z &&
                cell.x <= max.x && cell.y <= max.y && cell.z <= max.z;

        // Return an exact cube orientation, rather than independently rounding Euler
        // angles (which can pick the wrong orientation near a gimbal singularity).
        internal static Quaternion SnapRegionRotation(Quaternion rotation)
        {
            Quaternion best = Quaternion.identity;
            float distance = float.MaxValue;
            foreach (Vector3 forward in s_FaceNeighbors)
                foreach (Vector3 up in s_FaceNeighbors)
                {
                    if (Vector3.Dot(forward, up) != 0) continue;
                    Quaternion candidate = Quaternion.LookRotation(forward, up);
                    float angle = Quaternion.Angle(rotation, candidate);
                    if (angle >= distance) continue;
                    distance = angle;
                    best = candidate;
                }
            return best;
        }

        internal static Vector3Int TransformRegionCell(Vector3Int cell, Vector3Int pivot,
            Quaternion rotation, Vector3Int translation)
            => pivot + Vector3Int.RoundToInt(rotation * (Vector3)(cell - pivot)) + translation;

        internal static void GetRegionGrabDelta(TrTransform widget, TrTransform grid, TrTransform desired,
            Vector3Int pivot, out Quaternion rotation, out Vector3Int translation)
        {
            desired.scale = widget.scale;
            TrTransform desiredGrid = desired * widget.inverse * grid;
            rotation = SnapRegionRotation(Quaternion.Inverse(grid.rotation) * desiredGrid.rotation);
            translation = Vector3Int.RoundToInt(grid.inverse * (desiredGrid * (Vector3)pivot) - pivot);
        }

        // Compose a floating layer in its fixed original grid coordinates. Rebasing
        // affects the rendered/saved view, never the retained background or payload.
        internal bool TrySetRegionCells(RuntimeModel model, Dictionary<Vector3Int, byte> result,
            Vector3 anchorOffset, Vector3Int anchorOrigin, out Vector3Int rebase)
        {
            rebase = Vector3Int.zero;
            if (!ContainsModel(model) || result.Count == 0) return false;
            Vector3Int low = result.Keys.Aggregate(Vector3Int.Min);
            Vector3Int high = result.Keys.Aggregate(Vector3Int.Max);
            Vector3Int size = high - low + Vector3Int.one;
            for (int axis = 0; axis < 3; ++axis)
            {
                if (size[axis] > MaxModelDimension) return false;
                int padding = Mathf.Min(2, MaxModelDimension - size[axis]);
                low[axis] -= (padding + 1) / 2;
                size[axis] = Mathf.Max(3, size[axis] + padding);
            }
            rebase = -low;
            Vector3Int shift = rebase;
            // Replace only this instance's definition; linked source instances stay put.
            model.Definition = new VoxelDefinition(size, model.SourceModelId,
                result.ToDictionary(cell => cell.Key + shift, cell => cell.Value));
            model.TransformOffset = anchorOffset - model.GlobalRotation.MultiplyVector(rebase);
            model.SourceCellOffset = anchorOrigin + rebase;
            ViewState.VisualsDirty = true;
            return true;
        }
    }
}
