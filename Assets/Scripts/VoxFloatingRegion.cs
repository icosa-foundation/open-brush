// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // Geometry is captured once per selection. Poses and stamp history are small
    // immutable states; voxel dictionaries are composed only for saving/finalizing.
    internal sealed class VoxFloatingRegion
    {
        internal sealed class Geometry
        {
            internal readonly IReadOnlyDictionary<Vector3Int, byte> Cells, Background;
            internal readonly Vector3Int CellMin, CellMax, BackgroundMin, BackgroundMax;
            internal Geometry(RuntimeVoxDocument.RuntimeModel model, Vector3Int min, Vector3Int max)
            {
                var cells = new Dictionary<Vector3Int, byte>();
                var background = new Dictionary<Vector3Int, byte>();
                foreach (var cell in model.Voxels)
                    if (RuntimeVoxDocument.InRegion(cell.Key, min, max)) cells.Add(cell.Key, cell.Value);
                    else background.Add(cell.Key, cell.Value);
                Cells = cells;
                Background = background;
                GetBounds(Cells, out CellMin, out CellMax);
                GetBounds(Background, out BackgroundMin, out BackgroundMax);
            }
        }

        internal sealed class StampNode
        {
            internal readonly StampNode Previous;
            internal readonly TrTransform Pose;
            internal readonly Vector3Int Min, Max;
            internal readonly int Count;
            internal StampNode(StampNode previous, TrTransform pose, Vector3Int min, Vector3Int max)
            {
                Previous = previous;
                Pose = pose;
                Min = previous == null ? min : Vector3Int.Min(previous.Min, min);
                Max = previous == null ? max : Vector3Int.Max(previous.Max, max);
                Count = (previous?.Count ?? 0) + 1;
            }
        }

        internal readonly Geometry SourceGeometry;
        internal readonly StampNode Stamps;
        internal readonly TrTransform Pose;
        internal readonly RuntimeVoxDocument.RuntimeModel Model;
        internal readonly Vector3Int Min, Max, IndexOrigin, CellMin, CellMax, BackgroundMin, BackgroundMax;
        internal readonly Vector3 AnchorOffset;
        internal IReadOnlyDictionary<Vector3Int, byte> Cells { get; }
        internal bool HasBackground => SourceGeometry.Background.Count != 0 || Stamps != null;

        // This materialization is deliberately only used by document composition.
        internal IReadOnlyDictionary<Vector3Int, byte> Background => ComposeBackground();

        private Dictionary<Vector3Int, byte> ComposeBackground()
        {
            var cells = SourceGeometry.Background.ToDictionary(cell => cell.Key, cell => cell.Value);
            var ordered = new Stack<StampNode>();
            for (var stamp = Stamps; stamp != null; stamp = stamp.Previous) ordered.Push(stamp);
            while (ordered.Count != 0)
            {
                var stamp = ordered.Pop();
                foreach (var cell in SourceGeometry.Cells)
                    cells[Vector3Int.RoundToInt(stamp.Pose * (Vector3)cell.Key)] = cell.Value;
            }
            return cells;
        }
        internal VoxFloatingRegion(RuntimeVoxDocument.RuntimeModel model, Vector3Int min, Vector3Int max)
        {
            Model = model;
            Min = min;
            Max = max;
            IndexOrigin = model.SourceCellOffset;
            AnchorOffset = model.TransformOffset;
            SourceGeometry = new Geometry(model, min, max);
            Pose = TrTransform.identity;
            CellMin = SourceGeometry.CellMin;
            CellMax = SourceGeometry.CellMax;
            BackgroundMin = SourceGeometry.BackgroundMin;
            BackgroundMax = SourceGeometry.BackgroundMax;
            Cells = SourceGeometry.Cells;
        }

        private VoxFloatingRegion(VoxFloatingRegion basis, TrTransform pose, StampNode stamps,
            Vector3Int min, Vector3Int max)
        {
            Model = basis.Model;
            Min = min;
            Max = max;
            IndexOrigin = basis.IndexOrigin;
            AnchorOffset = basis.AnchorOffset;
            SourceGeometry = basis.SourceGeometry;
            Pose = pose;
            Stamps = stamps;
            Vector3Int a = Vector3Int.RoundToInt(pose * (Vector3)SourceGeometry.CellMin);
            Vector3Int b = Vector3Int.RoundToInt(pose * (Vector3)SourceGeometry.CellMax);
            CellMin = Vector3Int.Min(a, b);
            CellMax = Vector3Int.Max(a, b);
            BackgroundMin = SourceGeometry.BackgroundMin;
            BackgroundMax = SourceGeometry.BackgroundMax;
            if (stamps != null)
            {
                BackgroundMin = SourceGeometry.Background.Count == 0 ? stamps.Min : Vector3Int.Min(BackgroundMin, stamps.Min);
                BackgroundMax = SourceGeometry.Background.Count == 0 ? stamps.Max : Vector3Int.Max(BackgroundMax, stamps.Max);
            }
            Cells = new TransformedCells(SourceGeometry.Cells, pose);
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

        internal bool CanTransformFrom(VoxFloatingRegion basis, Vector3Int pivot,
            Quaternion rotation, Vector3Int translation)
        {
            Vector3Int a = RuntimeVoxDocument.TransformRegionCell(basis.CellMin, pivot, rotation, translation);
            Vector3Int b = RuntimeVoxDocument.TransformRegionCell(basis.CellMax, pivot, rotation, translation);
            Vector3Int min = Vector3Int.Min(a, b);
            Vector3Int max = Vector3Int.Max(a, b);
            if (HasBackground)
            {
                min = Vector3Int.Min(min, BackgroundMin);
                max = Vector3Int.Max(max, BackgroundMax);
            }
            Vector3Int size = max - min + Vector3Int.one;
            return size.x <= RuntimeVoxDocument.MaxModelDimension &&
                size.y <= RuntimeVoxDocument.MaxModelDimension && size.z <= RuntimeVoxDocument.MaxModelDimension;
        }

        internal VoxFloatingRegion TransformFrom(VoxFloatingRegion basis, Vector3Int pivot,
            Quaternion rotation, Vector3Int translation)
        {
            var delta = TrTransform.TR((Vector3)pivot + translation - rotation * (Vector3)pivot, rotation);
            Vector3Int a = RuntimeVoxDocument.TransformRegionCell(basis.Min, pivot, rotation, translation);
            Vector3Int b = RuntimeVoxDocument.TransformRegionCell(basis.Max, pivot, rotation, translation);
            return new VoxFloatingRegion(this, delta * basis.Pose, Stamps, Vector3Int.Min(a, b), Vector3Int.Max(a, b));
        }

        private static bool SameTransform(TrTransform a, TrTransform b)
            => (a.translation - b.translation).sqrMagnitude < 0.000001f && Quaternion.Angle(a.rotation, b.rotation) < 0.01f;

        internal bool SamePose(VoxFloatingRegion other)
            => ReferenceEquals(SourceGeometry, other.SourceGeometry) && Min == other.Min && Max == other.Max &&
                SameTransform(Pose, other.Pose);

        internal VoxFloatingRegion Stamp()
        {
            if (Stamps != null && SameTransform(Stamps.Pose, Pose)) return this;
            return new VoxFloatingRegion(this, Pose, new StampNode(Stamps, Pose, CellMin, CellMax), Min, Max);
        }

        internal bool TryCompose(RuntimeVoxDocument document)
        {
            var cells = ComposeBackground();
            foreach (var cell in Cells) cells[cell.Key] = cell.Value;
            return document.TrySetRegionCells(Model, cells, AnchorOffset, IndexOrigin, out _);
        }

        private sealed class TransformedCells : IReadOnlyDictionary<Vector3Int, byte>
        {
            private readonly IReadOnlyDictionary<Vector3Int, byte> m_Source;
            private readonly TrTransform m_Pose, m_Inverse;
            internal TransformedCells(IReadOnlyDictionary<Vector3Int, byte> source, TrTransform pose)
            {
                m_Source = source;
                m_Pose = pose;
                m_Inverse = pose.inverse;
            }
            public int Count => m_Source.Count;
            public IEnumerable<Vector3Int> Keys => m_Source.Keys.Select(cell => Vector3Int.RoundToInt(m_Pose * (Vector3)cell));
            public IEnumerable<byte> Values => m_Source.Values;
            public byte this[Vector3Int key] => m_Source[Vector3Int.RoundToInt(m_Inverse * (Vector3)key)];
            public bool ContainsKey(Vector3Int key) => m_Source.ContainsKey(Vector3Int.RoundToInt(m_Inverse * (Vector3)key));
            public bool TryGetValue(Vector3Int key, out byte value)
                => m_Source.TryGetValue(Vector3Int.RoundToInt(m_Inverse * (Vector3)key), out value);
            public IEnumerator<KeyValuePair<Vector3Int, byte>> GetEnumerator()
            {
                foreach (var cell in m_Source)
                    yield return new KeyValuePair<Vector3Int, byte>(Vector3Int.RoundToInt(m_Pose * (Vector3)cell.Key), cell.Value);
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
