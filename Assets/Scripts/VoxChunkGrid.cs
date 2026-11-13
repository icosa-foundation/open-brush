// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // Derived spatial index of the authoritative cell dictionary. All writes go
    // through VoxelDefinition.SetCell, keeping lookup, mesh invalidation and bounds together.
    internal sealed class VoxChunkGrid
    {
        internal const int Side = 16;
        internal sealed class Chunk
        {
            internal readonly byte[] Data = new byte[Side * Side * Side];
            internal readonly int[] PaletteCounts = new int[256];
            internal readonly HashSet<byte> UsedPalette = new HashSet<byte>();
            internal int Count;
            internal long Version;
            internal ulong Hash;
            internal bool BoundsDirty = true;
            internal Vector3Int Min, Max;
            internal void UpdateBounds()
            {
                if (!BoundsDirty) return;
                Min = Vector3Int.one * int.MaxValue;
                Max = Vector3Int.one * int.MinValue;
                for (int z = 0; z < Side; ++z)
                    for (int y = 0; y < Side; ++y)
                        for (int x = 0; x < Side; ++x)
                            if (Data[x + Side * (y + Side * z)] != 0)
                            {
                                var cell = new Vector3Int(x, y, z);
                                Min = Vector3Int.Min(Min, cell);
                                Max = Vector3Int.Max(Max, cell);
                            }
                BoundsDirty = false;
            }
        }

        private readonly Dictionary<Vector3Int, Chunk> m_Chunks = new Dictionary<Vector3Int, Chunk>();
        internal IReadOnlyDictionary<Vector3Int, Chunk> Chunks => m_Chunks;
        private bool m_BoundsDirty = true;
        private long m_Version;
        private Vector3Int m_Min, m_Max;
        internal Vector3Int Min { get { UpdateBounds(); return m_Min; } }
        internal Vector3Int Max { get { UpdateBounds(); return m_Max; } }
        internal bool Empty => m_Chunks.Count == 0;

        internal static Vector3Int Coordinate(Vector3Int cell) => new Vector3Int(cell.x >> 4, cell.y >> 4, cell.z >> 4);
        internal static int Index(Vector3Int local) => local.x + Side * (local.y + Side * local.z);

        internal byte Get(Vector3Int cell)
        {
            Vector3Int key = Coordinate(cell);
            return m_Chunks.TryGetValue(key, out var chunk) ? chunk.Data[Index(cell - key * Side)] : (byte)0;
        }

        internal void Set(Vector3Int cell, byte value)
        {
            Vector3Int key = Coordinate(cell);
            if (!m_Chunks.TryGetValue(key, out var chunk))
            {
                if (value == 0) return;
                chunk = new Chunk();
                m_Chunks.Add(key, chunk);
            }
            Vector3Int local = cell - key * Side;
            int index = Index(local);
            byte before = chunk.Data[index];
            if (before == value) return;
            if (before != 0)
            {
                --chunk.Count;
                if (--chunk.PaletteCounts[before] == 0) chunk.UsedPalette.Remove(before);
            }
            if (value != 0)
            {
                ++chunk.Count;
                if (++chunk.PaletteCounts[value] == 1) chunk.UsedPalette.Add(value);
            }
            chunk.Data[index] = value;
            if (before != 0) chunk.Hash ^= CellHash(index, before);
            if (value != 0) chunk.Hash ^= CellHash(index, value);
            chunk.Version = ++m_Version;
            if (before == 0 || value == 0) chunk.BoundsDirty = m_BoundsDirty = true;
            for (int axis = 0; axis < 3; ++axis)
            {
                var neighbor = key;
                if (local[axis] == 0) { --neighbor[axis]; Touch(neighbor); }
                neighbor = key;
                if (local[axis] == Side - 1) { ++neighbor[axis]; Touch(neighbor); }
            }
            if (chunk.Count == 0) m_Chunks.Remove(key);
        }

        private void Touch(Vector3Int key)
        {
            if (m_Chunks.TryGetValue(key, out var chunk)) chunk.Version = ++m_Version;
        }

        private static ulong CellHash(int index, byte value)
        {
            ulong hash = (ulong)(index * 256 + value) + 0x9e3779b97f4a7c15UL;
            hash = (hash ^ (hash >> 30)) * 0xbf58476d1ce4e5b9UL;
            return (hash ^ (hash >> 27)) * 0x94d049bb133111ebUL;
        }

        internal ulong MeshHash(Vector3Int coordinate, Color32[] palette, IReadOnlyList<int> slots,
            IReadOnlyList<bool> occludes)
        {
            ulong hash = 17;
            for (int axis = -1; axis < 6; ++axis)
            {
                Vector3Int key = coordinate;
                if (axis >= 0) key[axis / 2] += axis % 2 == 0 ? -1 : 1;
                m_Chunks.TryGetValue(key, out var chunk);
                hash = (hash * 1099511628211UL) ^ (chunk?.Hash ?? 0);
                if (chunk == null) continue;
                foreach (byte i in chunk.UsedPalette.OrderBy(index => index))
                {
                    Color32 color = palette[i - 1];
                    ulong value = color.r | (ulong)color.g << 8 | (ulong)color.b << 16 | (ulong)color.a << 24;
                    value ^= (ulong)(slots?[i - 1] ?? 0) << 32;
                    if (occludes == null || occludes[i - 1]) value ^= 1UL << 48;
                    hash = (hash * 1099511628211UL) ^ value;
                }
            }
            return hash;
        }

        private void UpdateBounds()
        {
            if (!m_BoundsDirty) return;
            m_Min = Vector3Int.one * int.MaxValue;
            m_Max = Vector3Int.one * int.MinValue;
            foreach (var pair in m_Chunks)
            {
                pair.Value.UpdateBounds();
                m_Min = Vector3Int.Min(m_Min, pair.Key * Side + pair.Value.Min);
                m_Max = Vector3Int.Max(m_Max, pair.Key * Side + pair.Value.Max);
            }
            m_BoundsDirty = false;
        }

        internal IEnumerable<KeyValuePair<Vector3Int, byte>> Region(Vector3Int min, Vector3Int max)
        {
            if (Empty) yield break;
            min = Vector3Int.Max(min, Min);
            max = Vector3Int.Min(max, Max);
            Vector3Int first = Coordinate(min), last = Coordinate(max);
            for (int z = first.z; z <= last.z; ++z)
                for (int y = first.y; y <= last.y; ++y)
                    for (int x = first.x; x <= last.x; ++x)
                    {
                        var key = new Vector3Int(x, y, z);
                        if (!m_Chunks.TryGetValue(key, out var chunk)) continue;
                        Vector3Int origin = key * Side;
                        Vector3Int low = Vector3Int.Max(min - origin, Vector3Int.zero);
                        Vector3Int high = Vector3Int.Min(max - origin, Vector3Int.one * (Side - 1));
                        for (int iz = low.z; iz <= high.z; ++iz)
                            for (int iy = low.y; iy <= high.y; ++iy)
                                for (int ix = low.x; ix <= high.x; ++ix)
                                {
                                    var local = new Vector3Int(ix, iy, iz);
                                    byte value = chunk.Data[Index(local)];
                                    if (value != 0) yield return new KeyValuePair<Vector3Int, byte>(origin + local, value);
                                }
                    }
        }
    }
}
