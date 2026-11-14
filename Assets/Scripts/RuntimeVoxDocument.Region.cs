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

        private static readonly Quaternion[] s_RegionRotations = CreateRegionRotations();

        private static Quaternion[] CreateRegionRotations()
        {
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            var rotations = new List<Quaternion>(24);
            foreach (var forward in axes)
                foreach (var up in axes)
                    if (Vector3.Dot(forward, up) == 0) rotations.Add(Quaternion.LookRotation(forward, up));
            return rotations.ToArray();
        }

        // Return an exact cube orientation, rather than independently rounding Euler
        // angles (which can pick the wrong orientation near a gimbal singularity).
        internal static Quaternion SnapRegionRotation(Quaternion rotation)
        {
            Quaternion best = Quaternion.identity;
            float score = -1f;
            foreach (Quaternion candidate in s_RegionRotations)
            {
                float dot = Mathf.Abs(Quaternion.Dot(rotation, candidate));
                if (dot <= score) continue;
                score = dot;
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

    }
}
