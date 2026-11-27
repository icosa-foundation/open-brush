// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using UnityEngine;

namespace TiltBrush
{
    // Alternating nested footprints: cube, cube plus its six new face layers, cube.
    // Disjoint boxes drive both stamping and preview without allocating per-cell lists.
    internal readonly struct VoxBrushFootprint
    {
        public int Radius { get; }
        public bool Expanded { get; }
        public int Extent => Radius + (Expanded ? 1 : 0);
        public int BoxCount => Expanded ? 7 : 1;

        public VoxBrushFootprint(int step)
        {
            Radius = step / 2;
            Expanded = step % 2 != 0;
        }

        public void GetBox(int index, out Vector3Int min, out Vector3Int max)
        {
            min = Vector3Int.one * -Radius;
            max = Vector3Int.one * Radius;
            if (index == 0) return;
            int face = index - 1;
            int coordinate = face % 2 == 0 ? -Extent : Extent;
            min[face / 2] = max[face / 2] = coordinate;
        }

        public bool IntersectsVolume(Vector3Int cell, Vector3Int size)
            => cell.x + Extent >= 0 && cell.y + Extent >= 0 && cell.z + Extent >= 0 &&
                cell.x - Extent < size.x && cell.y - Extent < size.y && cell.z - Extent < size.z;
    }
}
