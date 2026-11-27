// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public sealed partial class RuntimeVoxDocument
    {
        internal const float JoinSizeTolerance = 0.01f;
        internal const float JoinAngleTolerance = 1f;

        internal static bool TryGetJoinOffset(TrTransform targetGrid, TrTransform sourceGrid,
            out Vector3Int offset)
        {
            offset = default;
            if (Mathf.Abs(targetGrid.scale) < 0.000001f || targetGrid.scale * sourceGrid.scale <= 0 ||
                Mathf.Abs(sourceGrid.scale / targetGrid.scale - 1f) > JoinSizeTolerance ||
                Quaternion.Angle(targetGrid.rotation, sourceGrid.rotation) > JoinAngleTolerance) return false;
            // Snap one grid origin, rather than rounding/resampling individual voxels.
            offset = Vector3Int.RoundToInt(targetGrid.inverse * sourceGrid.translation);
            return true;
        }

        // Grid transforms are in a common space, normally world space. Hidden grids
        // remain in their original documents and do not participate in this operation.
        internal bool TryJoin(RuntimeVoxDocument source, IReadOnlyList<TrTransform> targetGrids,
            IReadOnlyList<TrTransform> sourceGrids, int anchorIndex)
        {
            if (source == this || anchorIndex < 0 || anchorIndex >= m_models.Count ||
                targetGrids.Count != m_models.Count || sourceGrids.Count != source.Models.Count ||
                source.Models.Any(model => !model.IsVisible && model.Voxels.Count > 0)) return false;

            RuntimeModel anchor = m_models[anchorIndex];
            if (!anchor.IsVisible) return false;
            TrTransform anchorGrid = targetGrids[anchorIndex];
            var cells = new Dictionary<Vector3Int, byte>();
            var additions = new Dictionary<Vector3Int, byte>();
            for (int i = 0; i < m_models.Count; ++i)
            {
                var model = m_models[i];
                if (!model.IsVisible || model.Voxels.Count == 0) continue;
                if (!TryGetJoinOffset(anchorGrid, targetGrids[i], out var offset)) return false;
                foreach (var cell in model.Voxels)
                    cells.TryAdd(cell.Key + offset, cell.Value);
            }
            for (int i = 0; i < source.Models.Count; ++i)
            {
                var model = source.Models[i];
                if (!model.IsVisible || model.Voxels.Count == 0) continue;
                if (!TryGetJoinOffset(anchorGrid, sourceGrids[i], out var offset)) return false;
                foreach (var cell in model.Voxels)
                {
                    Vector3Int position = cell.Key + offset;
                    if (!cells.ContainsKey(position)) additions.TryAdd(position, cell.Value);
                }
            }
            if (cells.Count == 0 || !source.Models.Any(model => model.IsVisible && model.Voxels.Count > 0)) return false;

            Vector3Int min = cells.Keys.First();
            Vector3Int max = min;
            foreach (var cell in cells.Keys.Concat(additions.Keys))
            {
                min = Vector3Int.Min(min, cell);
                max = Vector3Int.Max(max, cell);
            }
            Vector3Int size = max - min + Vector3Int.one;
            if (size.x > MaxModelDimension || size.y > MaxModelDimension || size.z > MaxModelDimension) return false;

            var reserved = new HashSet<byte>(m_models.SelectMany(model => model.Voxels.Values));
            var paletteMap = new Dictionary<byte, byte>();
            var paletteBefore = (Color32[])Palette.Clone();
            var materialsBefore = new Dictionary<int, RuntimeMaterial>(m_materials);
            foreach (byte index in additions.Values.Distinct())
            {
                byte imported = ImportPaletteEntry(source, index, reserved);
                if (imported == 0)
                {
                    Array.Copy(paletteBefore, Palette, Palette.Length);
                    m_materials.Clear();
                    foreach (var material in materialsBefore) m_materials.Add(material.Key, material.Value);
                    return false;
                }
                paletteMap.Add(index, imported);
                reserved.Add(imported);
            }
            foreach (var cell in additions) cells.Add(cell.Key, paletteMap[cell.Value]);

            var merged = new RuntimeModel(anchor.Name, size, sourceModelId: -1, isCopy: false,
                globalRotation: anchor.GlobalRotation, localRotation: anchor.GlobalRotation)
            {
                TransformOffset = anchor.TransformOffset + anchor.GlobalRotation.MultiplyVector(min),
            };
            foreach (var cell in cells) merged.AddOrUpdateVoxel(cell.Key - min, cell.Value);
            m_models.RemoveAll(model => model.IsVisible);
            m_models.Add(merged);
            ViewState.VisualsDirty = true;
            return true;
        }

        private byte ImportPaletteEntry(RuntimeVoxDocument source, byte sourceIndex, HashSet<byte> reserved)
        {
            Color32 color = source.Palette[sourceIndex - 1];
            var properties = source.m_materials.TryGetValue(sourceIndex, out var material) ? material.Properties : null;
            for (int index = 1; index <= byte.MaxValue; ++index)
                if (Palette[index - 1].Equals(color) && PaletteMaterialMatches(index, properties)) return (byte)index;
            for (int index = 1; index <= byte.MaxValue; ++index)
            {
                if (reserved.Contains((byte)index)) continue;
                Palette[index - 1] = color;
                if (material == null) m_materials.Remove(index);
                else m_materials[index] = new RuntimeMaterial(index, material.Properties);
                return (byte)index;
            }
            return 0;
        }
    }
}
