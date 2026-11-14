// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // A floating region owns only selected cells and a pose. Committed cells,
    // including stamps, remain in the model's authoritative grid.
    internal sealed class VoxFloatingRegion
    {
        internal readonly object Selection;
        internal readonly RuntimeVoxDocument.RuntimeModel Model;
        internal readonly Vector3Int OriginalMin, OriginalMax, CellMin, CellMax;
        internal readonly Vector3 AnchorOffset;
        internal readonly TrTransform Pose;
        internal readonly IReadOnlyDictionary<Vector3Int, byte> Payload;
        internal bool Lifted => Payload != null;
        internal Vector3Int Min => TransformBounds(OriginalMin, OriginalMax, true);
        internal Vector3Int Max => TransformBounds(OriginalMin, OriginalMax, false);

        internal VoxFloatingRegion(RuntimeVoxDocument.RuntimeModel model, Vector3Int min, Vector3Int max)
        {
            Selection = new object();
            Model = model;
            OriginalMin = CellMin = min - model.SourceCellOffset;
            OriginalMax = CellMax = max - model.SourceCellOffset;
            AnchorOffset = model.TransformOffset + model.GlobalRotation.MultiplyVector(model.SourceCellOffset);
            Pose = TrTransform.identity;
        }

        private VoxFloatingRegion(VoxFloatingRegion source, IReadOnlyDictionary<Vector3Int, byte> payload,
            TrTransform pose, Vector3Int cellMin, Vector3Int cellMax)
        {
            Selection = source.Selection;
            Model = source.Model;
            OriginalMin = source.OriginalMin;
            OriginalMax = source.OriginalMax;
            AnchorOffset = source.AnchorOffset;
            Payload = payload;
            Pose = pose;
            CellMin = cellMin;
            CellMax = cellMax;
        }

        internal VoxFloatingRegion Lift()
        {
            if (Lifted) return this;
            var cells = Model.Definition.Grid.Region(OriginalMin + Model.SourceCellOffset,
                    OriginalMax + Model.SourceCellOffset)
                .ToDictionary(cell => cell.Key - Model.SourceCellOffset, cell => cell.Value);
            if (cells.Count == 0) return null;
            Vector3Int min = cells.Keys.Aggregate(Vector3Int.Min), max = cells.Keys.Aggregate(Vector3Int.Max);
            return new VoxFloatingRegion(this, cells, Pose, min, max);
        }

        internal VoxFloatingRegion TransformFrom(VoxFloatingRegion basis, Vector3Int pivot,
            Quaternion rotation, Vector3Int translation)
        {
            var delta = TrTransform.TR((Vector3)pivot + translation - rotation * (Vector3)pivot, rotation);
            return new VoxFloatingRegion(this, Payload, delta * basis.Pose, CellMin, CellMax);
        }

        internal bool CanTransformFrom(VoxFloatingRegion basis, Vector3Int pivot,
            Quaternion rotation, Vector3Int translation)
        {
            var delta = TrTransform.TR((Vector3)pivot + translation - rotation * (Vector3)pivot, rotation);
            var pose = delta * basis.Pose;
            Vector3Int a = Vector3Int.RoundToInt(pose * (Vector3)CellMin);
            Vector3Int b = Vector3Int.RoundToInt(pose * (Vector3)CellMax);
            Vector3Int min = Vector3Int.Min(a, b), max = Vector3Int.Max(a, b);
            var grid = Model.Definition.Grid;
            if (!grid.Empty)
            {
                min = Vector3Int.Min(min, grid.Min - Model.SourceCellOffset);
                max = Vector3Int.Max(max, grid.Max - Model.SourceCellOffset);
            }
            Vector3Int size = max - min + Vector3Int.one;
            return size.x <= RuntimeVoxDocument.MaxModelDimension && size.y <= RuntimeVoxDocument.MaxModelDimension &&
                size.z <= RuntimeVoxDocument.MaxModelDimension;
        }

        private Vector3Int TransformBounds(Vector3Int min, Vector3Int max, bool lower)
        {
            Vector3Int a = Vector3Int.RoundToInt(Pose * (Vector3)min), b = Vector3Int.RoundToInt(Pose * (Vector3)max);
            return lower ? Vector3Int.Min(a, b) : Vector3Int.Max(a, b);
        }

        internal IEnumerable<KeyValuePair<Vector3Int, byte>> CellsAt(TrTransform pose)
        {
            if (Payload == null) yield break;
            foreach (var cell in Payload)
                yield return new KeyValuePair<Vector3Int, byte>(Vector3Int.RoundToInt(pose * (Vector3)cell.Key), cell.Value);
        }
    }

    internal readonly struct VoxCellChange
    {
        internal readonly Vector3Int Anchor;
        internal readonly byte Before, After;
        internal VoxCellChange(Vector3Int anchor, byte before, byte after)
        {
            Anchor = anchor;
            Before = before;
            After = after;
        }
        internal static List<VoxCellChange> Write(VoxFloatingRegion region)
        {
            var changes = new List<VoxCellChange>();
            foreach (var cell in region.CellsAt(region.Pose))
            {
                region.Model.TryGetPaletteIndex(cell.Key + region.Model.SourceCellOffset, out byte before);
                if (before != cell.Value) changes.Add(new VoxCellChange(cell.Key, before, cell.Value));
            }
            return changes;
        }
        internal static void Apply(RuntimeVoxDocument.RuntimeModel model, IEnumerable<VoxCellChange> changes, bool after)
        {
            foreach (var change in changes)
                model.Definition.SetCell(change.Anchor + model.SourceCellOffset, after ? change.After : change.Before);
        }
    }

    // A value snapshot read by serialization; saving never calls back into a widget.
    internal readonly struct VoxFloatingSaveState
    {
        internal readonly VoxFloatingRegion Region;
        internal readonly TrTransform Pose;
        internal VoxFloatingSaveState(VoxFloatingRegion region, TrTransform pose) { Region = region; Pose = pose; }
    }
}
