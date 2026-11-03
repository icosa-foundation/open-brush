// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // Immutable runtime layer. The document contains its composed view, which is
    // already the finalized save image; covered background survives only here.
    internal sealed class VoxFloatingRegion
    {
        internal readonly RuntimeVoxDocument.RuntimeModel Model;
        internal readonly Vector3Int Min, Max, IndexOrigin;
        internal readonly Vector3 AnchorOffset;
        internal readonly Vector3Int CellMin, CellMax, BackgroundMin, BackgroundMax;
        internal IReadOnlyDictionary<Vector3Int, byte> Background => m_Background;
        internal IReadOnlyDictionary<Vector3Int, byte> Cells => m_Cells;
        private readonly IReadOnlyDictionary<Vector3Int, byte> m_Background;
        private readonly IReadOnlyDictionary<Vector3Int, byte> m_Cells;

        internal VoxFloatingRegion(RuntimeVoxDocument.RuntimeModel model, Vector3Int min, Vector3Int max)
        {
            Model = model;
            Min = min;
            Max = max;
            IndexOrigin = model.SourceCellOffset;
            AnchorOffset = model.TransformOffset;
            m_Cells = model.Voxels.Where(cell => RuntimeVoxDocument.InRegion(cell.Key, min, max))
                .ToDictionary(cell => cell.Key, cell => cell.Value);
            m_Background = model.Voxels.Where(cell => !m_Cells.ContainsKey(cell.Key))
                .ToDictionary(cell => cell.Key, cell => cell.Value);
            GetBounds(m_Cells, out CellMin, out CellMax);
            GetBounds(m_Background, out BackgroundMin, out BackgroundMax);
        }

        private VoxFloatingRegion(VoxFloatingRegion basis, IReadOnlyDictionary<Vector3Int, byte> background,
            IReadOnlyDictionary<Vector3Int, byte> cells, Vector3Int min, Vector3Int max)
        {
            Model = basis.Model;
            Min = min;
            Max = max;
            IndexOrigin = basis.IndexOrigin;
            AnchorOffset = basis.AnchorOffset;
            m_Background = background;
            m_Cells = cells;
            GetBounds(cells, out CellMin, out CellMax);
            GetBounds(background, out BackgroundMin, out BackgroundMax);
        }

        private static void GetBounds(IReadOnlyDictionary<Vector3Int, byte> cells,
            out Vector3Int min, out Vector3Int max)
        {
            min = Vector3Int.one * int.MaxValue;
            max = Vector3Int.one * int.MinValue;
            foreach (var cell in cells.Keys)
            {
                min = Vector3Int.Min(min, cell);
                max = Vector3Int.Max(max, cell);
            }
        }

        // Only cached bounds are inspected during a drag, never the voxel sets.
        internal bool CanTransformFrom(VoxFloatingRegion basis, Vector3Int pivot,
            Quaternion rotation, Vector3Int translation)
        {
            Vector3Int a = RuntimeVoxDocument.TransformRegionCell(basis.CellMin, pivot, rotation, translation);
            Vector3Int b = RuntimeVoxDocument.TransformRegionCell(basis.CellMax, pivot, rotation, translation);
            Vector3Int min = Vector3Int.Min(a, b);
            Vector3Int max = Vector3Int.Max(a, b);
            if (m_Background.Count != 0)
            {
                min = Vector3Int.Min(min, BackgroundMin);
                max = Vector3Int.Max(max, BackgroundMax);
            }
            Vector3Int size = max - min + Vector3Int.one;
            return size.x <= RuntimeVoxDocument.MaxModelDimension &&
                size.y <= RuntimeVoxDocument.MaxModelDimension && size.z <= RuntimeVoxDocument.MaxModelDimension;
        }

        // Use the grab's initial payload, but retain stamps made since that grab
        // began. Repeated motion must neither pick up background nor erase stamps.
        internal VoxFloatingRegion TransformFrom(VoxFloatingRegion basis, Vector3Int pivot,
            Quaternion rotation, Vector3Int translation)
        {
            var cells = basis.m_Cells.ToDictionary(
                cell => RuntimeVoxDocument.TransformRegionCell(cell.Key, pivot, rotation, translation),
                cell => cell.Value);
            Vector3Int a = RuntimeVoxDocument.TransformRegionCell(basis.Min, pivot, rotation, translation);
            Vector3Int b = RuntimeVoxDocument.TransformRegionCell(basis.Max, pivot, rotation, translation);
            return new VoxFloatingRegion(this, m_Background, cells, Vector3Int.Min(a, b), Vector3Int.Max(a, b));
        }

        internal bool SamePose(VoxFloatingRegion other)
            => Min == other.Min && Max == other.Max && m_Cells.Count == other.m_Cells.Count &&
                m_Cells.All(cell => other.m_Cells.TryGetValue(cell.Key, out byte color) && color == cell.Value);

        internal VoxFloatingRegion Stamp()
        {
            if (m_Cells.All(cell => m_Background.TryGetValue(cell.Key, out byte color) && color == cell.Value))
                return this;
            var background = m_Background.ToDictionary(cell => cell.Key, cell => cell.Value);
            foreach (var cell in m_Cells) background[cell.Key] = cell.Value;
            return new VoxFloatingRegion(this, background, m_Cells, Min, Max);
        }

        internal bool TryCompose(RuntimeVoxDocument document)
        {
            var cells = m_Background.ToDictionary(cell => cell.Key, cell => cell.Value);
            foreach (var cell in m_Cells) cells[cell.Key] = cell.Value;
            return document.TrySetRegionCells(Model, cells, AnchorOffset, IndexOrigin, out _);
        }
    }
}
