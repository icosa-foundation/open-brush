// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    internal sealed class VoxSymmetryEdits
    {
        internal sealed class Copy
        {
            internal Matrix4x4 Linear, Inverse;
            internal Vector3Int Center, Min, Max, Last;
            internal Color32 Color;
            internal int PaletteIndex;
            internal IReadOnlyDictionary<string, string> Material;
            internal bool Valid, HasLast;
            internal bool PreviewInvalid;
            internal Vector3Int PreviewMin, PreviewMax;
        }

        internal struct Result
        {
            internal bool Changed;
            internal bool PaletteFull;
        }

        private readonly List<Vector3Int> m_Path = new();
        private readonly List<Vector3Int> m_Stamp = new();
        private readonly HashSet<Vector3Int> m_Claimed = new();

        internal void CollectPreview(RuntimeVoxDocument.RuntimeModel model, IList<Copy> copies,
            VoxBrushFootprint footprint, VoxelTool.EditMode mode, bool wholePath,
            Dictionary<Vector3Int, Color32> cells)
        {
            cells.Clear();
            Vector3Int virtualSize = model.Size;
            Vector3Int virtualShift = Vector3Int.zero;
            foreach (var copy in copies)
            {
                copy.PreviewInvalid = false;
                if (!copy.Valid) continue;
                Vector3Int from = wholePath && copy.HasLast ? copy.Last : copy.Center;
                copy.PreviewMin = Vector3Int.Min(from, copy.Center) + copy.Min;
                copy.PreviewMax = Vector3Int.Max(from, copy.Center) + copy.Max;
                if (mode == VoxelTool.EditMode.Add)
                {
                    if (!RuntimeVoxDocument.TryGetDrawingPadding(virtualSize, copy.PreviewMin + virtualShift,
                            0, out var lower, out var upper, copy.PreviewMax + virtualShift))
                    {
                        copy.PreviewInvalid = true;
                        continue;
                    }
                    virtualSize += lower + upper;
                    virtualShift += lower;
                }
                Vector3Int min = mode == VoxelTool.EditMode.Add ? Vector3Int.Min(from, copy.Center) : -copy.Max;
                Vector3Int max = mode == VoxelTool.EditMode.Add ? Vector3Int.Max(from, copy.Center) : model.Size - Vector3Int.one - copy.Min;
                VoxBrushRasterizer.GetPath(from, copy.Center, min, max, m_Path);
                foreach (var center in m_Path)
                {
                    VoxBrushRasterizer.GetCells(footprint, copy.Linear, copy.Inverse, center,
                        copy.Min, copy.Max, model, mode != VoxelTool.EditMode.Add, m_Stamp);
                    Color32 color = mode == VoxelTool.EditMode.Erase ? new Color32(230, 31, 20, 255) : copy.Color;
                    foreach (var cell in m_Stamp) cells.TryAdd(cell, color);
                }
            }
        }

        internal Result Apply(RuntimeVoxDocument document, RuntimeVoxDocument.RuntimeModel model,
            IList<Copy> copies, VoxBrushFootprint footprint, VoxelTool.EditMode mode, bool countBoundsChanges)
        {
            m_Claimed.Clear();
            Vector3Int frameShift = Vector3Int.zero;
            var result = new Result();
            foreach (var copy in copies)
            {
                if (!copy.Valid) continue;
                int paletteIndex = -1;
                if (mode == VoxelTool.EditMode.Add)
                {
                    Vector3Int previous = copy.HasLast ? copy.Last : copy.Center;
                    Vector3Int min = Vector3Int.Min(previous, copy.Center) + copy.Min;
                    Vector3Int max = Vector3Int.Max(previous, copy.Center) + copy.Max;
                    if (!RuntimeVoxDocument.TryGetDrawingPadding(model.Size, min, 0, out var lower, out var upper, max))
                    {
                        copy.HasLast = false;
                        continue;
                    }
                    paletteIndex = ResolvePalette(document, copy);
                    result.PaletteFull |= paletteIndex == 0;
                    if (paletteIndex != 0 && document.ResizeModelBounds(model, lower, upper))
                    {
                        foreach (var other in copies)
                        {
                            other.Center += lower;
                            if (other.HasLast) other.Last += lower;
                        }
                        frameShift += lower;
                        result.Changed |= countBoundsChanges;
                    }
                }
                ApplyCopy(document, model, copy, footprint, mode, frameShift, ref paletteIndex, ref result);
                copy.Last = copy.Center;
                copy.HasLast = paletteIndex != 0;
            }
            return result;
        }

        private static int ResolvePalette(RuntimeVoxDocument document, Copy copy) => copy.PaletteIndex > 0
            ? copy.PaletteIndex : document.GetOrAddPaletteColor(copy.Color, copy.Material);

        private void ApplyCopy(RuntimeVoxDocument document, RuntimeVoxDocument.RuntimeModel model, Copy copy,
            VoxBrushFootprint footprint, VoxelTool.EditMode mode, Vector3Int frameShift,
            ref int paletteIndex, ref Result result)
        {
            Vector3Int from = copy.HasLast ? copy.Last : copy.Center;
            Vector3Int min = mode == VoxelTool.EditMode.Add ? Vector3Int.Min(from, copy.Center) : -copy.Max;
            Vector3Int max = mode == VoxelTool.EditMode.Add ? Vector3Int.Max(from, copy.Center) : model.Size - Vector3Int.one - copy.Min;
            VoxBrushRasterizer.GetPath(from, copy.Center, min, max, m_Path);
            foreach (var center in m_Path)
            {
                VoxBrushRasterizer.GetCells(footprint, copy.Linear, copy.Inverse, center,
                    copy.Min, copy.Max, model, mode != VoxelTool.EditMode.Add, m_Stamp);
                foreach (var cell in m_Stamp)
                {
                    // Stable keys preserve primary-copy precedence when a later
                    // copy grows the grid and rebases all earlier voxel indices.
                    if (!m_Claimed.Add(cell - frameShift) || !model.IsInBounds(cell)) continue;
                    if (mode == VoxelTool.EditMode.Erase) { result.Changed |= model.RemoveVoxel(cell); continue; }
                    if (paletteIndex < 0) paletteIndex = ResolvePalette(document, copy);
                    if (paletteIndex == 0) { result.PaletteFull = true; continue; }
                    result.Changed |= model.AddOrUpdateVoxel(cell, (byte)paletteIndex);
                }
            }
        }
    }
}
