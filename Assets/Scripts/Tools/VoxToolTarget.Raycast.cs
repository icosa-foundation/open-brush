// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using UnityEngine;

namespace TiltBrush
{
    internal sealed partial class VoxToolTarget
    {
        internal struct CellRayHit
        {
            internal Vector3Int Cell;
            internal Vector3Int Face;
            internal Vector3 Point_GS;
            internal Vector3 Normal_GS;
            internal float Distance;
        }

        internal struct RayHit
        {
            internal Hit Voxel;
            internal Vector3Int Face;
            internal Vector3 Point_GS;
            internal Vector3 Normal_GS;
            internal float Distance_GS;
        }

        internal bool TryRaycast(CanvasScript canvas, Ray ray_GS, float maxDistance_GS, out RayHit hit)
        {
            hit = default;
            float bestDistance = maxDistance_GS;
            bool found = false;
            foreach (ModelWidget widget in WidgetManager.m_Instance.ModelWidgets)
            {
                if (widget == null || !widget.Showing || !widget.gameObject.activeInHierarchy ||
                    widget.Canvas != canvas || !string.IsNullOrEmpty(widget.Subtree)) continue;
                Collider collider = widget.GrabCollider;
                if (collider == null || !collider.bounds.IntersectRay(ray_GS, out float distance) || distance > bestDistance) continue;
                var document = GetDocument(widget);
                if (document == null) continue;
                for (int i = 0; i < document.Models.Count; ++i)
                {
                    var model = document.Models[i];
                    if (!model.IsVisible) continue;
                    TrTransform grid_GS = TrTransform.FromTransform(widget.transform) * VoxMeshBuilder.GetModelTransform(model);
                    if (!TryRaycast(model, grid_GS, ray_GS, bestDistance, out var cellHit, widget) ||
                        (found && cellHit.Distance >= bestDistance)) continue;
                    found = true;
                    bestDistance = cellHit.Distance;
                    widget.TryGetVoxRegionCell(model, cellHit.Cell, out byte paletteIndex);
                    hit = new RayHit
                    {
                        Voxel = new Hit
                        {
                            Widget = widget, Document = document, ModelIndex = i,
                            Cell = cellHit.Cell, PaletteIndex = paletteIndex
                        },
                        Face = cellHit.Face,
                        Point_GS = cellHit.Point_GS,
                        Normal_GS = cellHit.Normal_GS,
                        Distance_GS = bestDistance,
                    };
                }
            }
            return found;
        }

        internal static bool TryRaycast(RuntimeVoxDocument.RuntimeModel model, TrTransform grid_GS,
            Ray ray_GS, float maxDistance_GS, out CellRayHit hit, ModelWidget widget = null)
        {
            hit = default;
            var floating = widget?.EditableVoxDocument?.FloatingSelection;
            bool hasFloating = floating.HasValue && ReferenceEquals(floating.Value.Region.Model, model);
            var grid = model.Definition.Grid;
            if ((grid.Empty && !hasFloating) || Mathf.Abs(grid_GS.scale) < 0.000001f ||
                !IsFinite(maxDistance_GS) || maxDistance_GS < 0 || !IsFinite(ray_GS.origin) ||
                !IsFinite(ray_GS.direction) || ray_GS.direction.sqrMagnitude == 0) return false;

            Vector3Int min = grid.Min, max = grid.Max;
            TrTransform floatingInverse = TrTransform.identity;
            if (hasFloating)
            {
                var layer = floating.Value;
                floatingInverse = layer.Pose.inverse;
                Vector3Int a = Vector3Int.RoundToInt(layer.Pose * (Vector3)layer.Region.CellMin) + model.SourceCellOffset;
                Vector3Int b = Vector3Int.RoundToInt(layer.Pose * (Vector3)layer.Region.CellMax) + model.SourceCellOffset;
                min = grid.Empty ? Vector3Int.Min(a, b) : Vector3Int.Min(min, Vector3Int.Min(a, b));
                max = grid.Empty ? Vector3Int.Max(a, b) : Vector3Int.Max(max, Vector3Int.Max(a, b));
            }
            bool Occupied(Vector3Int cell) => model.Voxels.ContainsKey(cell) || (hasFloating &&
                floating.Value.Region.Payload.ContainsKey(Vector3Int.RoundToInt(floatingInverse * (Vector3)(cell - model.SourceCellOffset))));

            var inverse = grid_GS.inverse;
            Vector3 origin = inverse * ray_GS.origin;
            // Keep this unnormalized: its ray parameter remains a global-space distance.
            Vector3 direction = inverse.MultiplyVector(ray_GS.direction);
            if (!IsFinite(origin) || !IsFinite(direction)) return false;
            double entry = double.NegativeInfinity;
            double exit = double.PositiveInfinity;
            for (int axis = 0; axis < 3; ++axis)
                if (!ClipAxis(origin[axis], direction[axis], min[axis] - 0.5, max[axis] + 0.5,
                        ref entry, ref exit)) return false;
            double start = Math.Max(0, entry);
            double end = Math.Min(maxDistance_GS, exit);
            if (start > end) return false;

            var step = new Vector3Int(Math.Sign(direction.x), Math.Sign(direction.y), Math.Sign(direction.z));
            var cell = new Vector3Int(
                InitialCell(origin.x + direction.x * start, step.x, min.x, max.x),
                InitialCell(origin.y + direction.y * start, step.y, min.y, max.y),
                InitialCell(origin.z + direction.z * start, step.z, min.z, max.z));
            bool occupied = Occupied(cell);
            double nextX = NextBoundary(origin.x, direction.x, cell.x, step.x, start);
            double nextY = NextBoundary(origin.y, direction.y, cell.y, step.y, start);
            double nextZ = NextBoundary(origin.z, direction.z, cell.z, step.z, start);

            // Traverse crossed cells, not every occupied voxel. Coincident crossings
            // are processed X, then Y, then Z, giving deterministic edge/corner hits.
            while (true)
            {
                int axis = nextX <= nextY && nextX <= nextZ ? 0 : nextY <= nextZ ? 1 : 2;
                double distance = axis == 0 ? nextX : axis == 1 ? nextY : nextZ;
                if (distance > end) return false;
                Vector3Int previous = cell;
                cell[axis] += step[axis];
                bool nextOccupied = Occupied(cell);
                if (occupied != nextOccupied)
                {
                    var face = Vector3Int.zero;
                    face[axis] = occupied ? step[axis] : -step[axis];
                    hit = new CellRayHit
                    {
                        Cell = occupied ? previous : cell,
                        Face = face,
                        Point_GS = ray_GS.origin + ray_GS.direction * (float)distance,
                        Normal_GS = ((grid_GS.rotation * (Vector3)face) * Mathf.Sign(grid_GS.scale)).normalized,
                        Distance = (float)distance,
                    };
                    return true;
                }
                occupied = nextOccupied;
                if (axis == 0) nextX = NextBoundary(origin.x, direction.x, cell.x, step.x, distance);
                else if (axis == 1) nextY = NextBoundary(origin.y, direction.y, cell.y, step.y, distance);
                else nextZ = NextBoundary(origin.z, direction.z, cell.z, step.z, distance);
            }
        }

        private static bool ClipAxis(double origin, double direction, double min, double max,
            ref double entry, ref double exit)
        {
            if (direction == 0) return origin >= min && origin <= max;
            double first = (min - origin) / direction;
            double last = (max - origin) / direction;
            if (first > last) (first, last) = (last, first);
            entry = Math.Max(entry, first);
            exit = Math.Min(exit, last);
            return entry <= exit;
        }

        private static int InitialCell(double position, int step, int min, int max)
        {
            double coordinate = position + 0.5;
            double boundary = Math.Round(coordinate);
            if (Math.Abs(coordinate - boundary) <= 0.0000001)
            {
                // Start immediately before a boundary, so a ray on a surface can
                // return that face at distance zero in either direction.
                if (step != 0) return (int)boundary - (step > 0 ? 1 : 0);
                return Math.Max(min, Math.Min(max, (int)boundary));
            }
            return (int)Math.Floor(coordinate);
        }

        private static double NextBoundary(double origin, double direction, int cell, int step, double start)
            => step == 0 ? double.PositiveInfinity : Math.Max(start, (cell + step * 0.5 - origin) / direction);

        internal static bool IsFinite(Vector3 vector)
            => IsFinite(vector.x) && IsFinite(vector.y) && IsFinite(vector.z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
