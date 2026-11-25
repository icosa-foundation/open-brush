// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using ImmPlayer.Authoring;
using ImmPlayer.Exporter;
using UnityEngine;

namespace TiltBrush.FrameAnimation
{
    /// <summary>Canvas-local Quill paint and exposure handles for one Open Brush track.</summary>
    internal sealed class ImmAnimationTrackSnapshot
    {
        private static readonly Dictionary<Guid, BrushSectionType> s_Sections =
            new Dictionary<Guid, BrushSectionType>
            {
                { new Guid(Quill.BRUSH_QUILL_CYLINDER), BrushSectionType.Circle },
                { new Guid(Quill.BRUSH_QUILL_ELLIPSE), BrushSectionType.Ellipse },
                { new Guid(Quill.BRUSH_QUILL_CUBE), BrushSectionType.Square },
                { new Guid(Quill.BRUSH_QUILL_RIBBON), BrushSectionType.Segment }
            };

        internal readonly struct Recolor
        {
            public readonly Stroke Stroke;
            public readonly AnimationDrawingId DrawingId;
            public readonly long Before, After;

            private Recolor(Stroke stroke, AnimationDrawingId drawingId, long before, long after)
            {
                Stroke = stroke;
                DrawingId = drawingId;
                Before = before;
                After = after;
            }

            internal Recolor Advance(Stroke stroke, AnimationDrawingId id, long before, long after) =>
                new Recolor(stroke, id, Stroke == stroke && DrawingId == id && After == before ? Before : before, after);
        }

        private readonly Dictionary<long, Stroke[]> m_StrokeOrder;
        private readonly int m_TrackId;
        private readonly Dictionary<long, ImmLivePaintTrack.Drawing> m_ById;

        public long[] Frames { get; }
        public ImmLivePaintTrack.Drawing[] Drawings { get; }

        private ImmAnimationTrackSnapshot(int trackId, long[] frames,
            Dictionary<long, ImmLivePaintTrack.Drawing> byId, Dictionary<long, Stroke[]> strokeOrder)
        {
            m_TrackId = trackId;
            m_ById = byId;
            m_StrokeOrder = strokeOrder;
            Frames = frames;
            Drawings = new ImmLivePaintTrack.Drawing[byId.Count];
            byId.Values.CopyTo(Drawings, 0);
        }

        public static bool TryCreate(AnimationTimelineModel.Track track,
            Func<AnimationDrawingId, FrameDrawing> resolve, Func<CanvasScript, IEnumerable<Stroke>> strokes,
            Func<CanvasScript, bool> hasWidgets, out ImmAnimationTrackSnapshot snapshot,
            ImmAnimationTrackSnapshot previous = null, Action<FrameDrawing> verifyCachedGeometry = null,
            Recolor recolor = default)
        {
            snapshot = null;
            if (track == null || !track.Visible || track.Deleted || track.Length == 0 ||
                track.Length > 1000000) return false; // IMM's maximum frame table size.

            var frames = new long[track.Length];
            var byId = new Dictionary<long, ImmLivePaintTrack.Drawing>();
            var blankIds = new HashSet<long>();
            var strokeOrder = new Dictionary<long, Stroke[]>();
            foreach (var span in track.Spans)
            {
                var value = span.Value;
                if (value.Deleted || value.DrawingId.IsEmpty)
                {
                    blankIds.Add(0);
                    continue;
                }
                if (value.PathToken != null) return false;

                long id = value.DrawingId.Value;
                if (blankIds.Contains(id)) continue;
                if (!byId.ContainsKey(id))
                {
                    var drawing = resolve(value.DrawingId);
                    if (drawing == null || drawing.Canvas == null || hasWidgets(drawing.Canvas)) return false;
                    ImmLivePaintTrack.Drawing paint = default;
                    bool cached = previous != null && previous.m_TrackId == track.Id &&
                        previous.m_ById.TryGetValue(id, out paint) && paint.Revision == drawing.ContentRevision;
                    var order = paint == null ? null : previous.m_StrokeOrder[id];
                    if (!cached && verifyCachedGeometry == null && paint != null &&
                        recolor.DrawingId == drawing.Id && recolor.Before == paint.Revision &&
                        recolor.After == drawing.ContentRevision &&
                        TryRecolor(drawing, recolor.Stroke, order, paint, out var updated))
                    {
                        paint = updated;
                    }
                    else if (!cached || verifyCachedGeometry != null)
                    {
                        if (!TryProjectDrawing(drawing, strokes(drawing.Canvas), out var current, out order))
                        {
                            if (cached) verifyCachedGeometry(drawing);
                            return false;
                        }
                        if (!cached)
                        {
                            paint = current;
                        }
                        else if (!SamePaint(paint.Strokes, current.Strokes))
                        {
                            // Undo of an opaque/compound command can change a canvas without a
                            // stroke notification. Only invalidate revisions for actual changes.
                            verifyCachedGeometry(drawing);
                            paint = new ImmLivePaintTrack.Drawing(id, drawing.ContentRevision,
                                current.Strokes, current.BiggestStroke);
                        }
                    }
                    if (paint.Strokes.Length == 0)
                    {
                        // Editable empty canvases and sparse blanks share a native drawing.
                        blankIds.Add(id);
                        continue;
                    }
                    byId.Add(id, paint);
                    strokeOrder.Add(id, order);
                }
                for (int i = span.StartFrame; i < span.EndFrameExclusive; ++i) frames[i] = id;
            }

            if (byId.Count == 0) return false;
            if (blankIds.Count > 0)
            {
                byId.Add(0, new ImmLivePaintTrack.Drawing(0, 0, Array.Empty<ImmLiveDrawingEdits.Stroke>(), 0));
            }
            snapshot = new ImmAnimationTrackSnapshot(track.Id, frames, byId, strokeOrder);
            return true;
        }

        // A color-only edit preserves the ordered stroke membership, widths and geometry limits.
        // Recreate the target from its effective per-point colors and share all other immutable arrays.
        private static bool TryRecolor(FrameDrawing drawing, Stroke stroke, Stroke[] order,
            ImmLivePaintTrack.Drawing previous, out ImmLivePaintTrack.Drawing updated)
        {
            updated = null;
            int index = Array.IndexOf(order, stroke);
            if (index < 0 || stroke.Canvas != drawing.Canvas || !IncludeSourceStroke(stroke) ||
                !TryProjectStroke(stroke, out var replacement, out _)) return false;
            var old = previous.Strokes[index];
            if (old.Brush != replacement.Brush || old.Points.Length != replacement.Points.Length) return false;
            for (int i = 0; i < old.Points.Length; i++)
                if (old.Points[i].Width != replacement.Points[i].Width) return false;

            var strokes = (ImmLiveDrawingEdits.Stroke[])previous.Strokes.Clone();
            strokes[index] = replacement;
            updated = new ImmLivePaintTrack.Drawing(drawing.Id.Value, drawing.ContentRevision,
                strokes, previous.BiggestStroke);
            return true;
        }

        private static bool SamePaint(ImmLiveDrawingEdits.Stroke[] before, ImmLiveDrawingEdits.Stroke[] after)
        {
            if (before.Length != after.Length) return false;
            for (int i = 0; i < before.Length; ++i)
            {
                var a = before[i];
                var b = after[i];
                if (a.Brush != b.Brush || a.Visibility != b.Visibility || a.Points.Length != b.Points.Length)
                    return false;
                for (int j = 0; j < a.Points.Length; ++j)
                {
                    PaintPoint p = a.Points[j], q = b.Points[j];
                    if (!p.Position.Equals(q.Position) || !p.Normal.Equals(q.Normal) ||
                        !p.Direction.Equals(q.Direction) || !p.Color.Equals(q.Color) ||
                        p.Alpha != q.Alpha || p.Width != q.Width || p.Length != q.Length || p.Time != q.Time)
                        return false;
                }
            }
            return true;
        }

        private static bool TryProjectDrawing(FrameDrawing drawing, IEnumerable<Stroke> source,
            out ImmLivePaintTrack.Drawing result, out Stroke[] order)
        {
            result = default;
            order = null;
            var sources = new List<Stroke>();
            var strokes = new List<ImmLiveDrawingEdits.Stroke>();
            float biggest = 0;
            int pointCount = 0;
            foreach (Stroke stroke in source)
            {
                if (!TryProjectStroke(stroke, out var paint, out float width)) return false;
                pointCount += paint.Points.Length;
                if (strokes.Count >= 65536 || pointCount > 1048576) return false;
                strokes.Add(paint);
                sources.Add(stroke);
                biggest = Mathf.Max(biggest, width);
            }
            order = sources.ToArray();
            result = new ImmLivePaintTrack.Drawing(drawing.Id.Value, drawing.ContentRevision, strokes.ToArray(), biggest);
            return true;
        }

        // An active batched stroke can contain no triangles (e.g. a too-short mark).
        // Keep unsupported active strokes in the stream so they still trigger canvas fallback.
        internal static bool IncludeSourceStroke(Stroke stroke)
        {
            if (!stroke.IsGeometryEnabled) return false;
            if (!s_Sections.ContainsKey(stroke.m_BrushGuid)) return true;
            if (stroke.m_Type == Stroke.Type.BatchedBrushStroke)
            {
                var subset = stroke.m_BatchSubset;
                return subset.m_VertLength > 0 && subset.m_nTriIndex >= 3;
            }
            return stroke.m_Object.TryGetComponent<GeometryBrush>(out var geometry) &&
                geometry.NumVerts > 0 && geometry.NumTris >= 3;
        }

        internal static bool TryProjectStroke(Stroke stroke, out ImmLiveDrawingEdits.Stroke result, out float biggest)
        {
            result = default;
            biggest = 0;
            if (stroke == null || !s_Sections.TryGetValue(stroke.m_BrushGuid, out var section)) return false;
            var controlPoints = stroke.m_ControlPoints;
            var dropped = stroke.m_ControlPointsToDrop;
            float maxRadius = stroke.SizeInLocalSpace * 0.5f;
            if (controlPoints == null || controlPoints.Length < 2 || !(maxRadius > 0) ||
                float.IsInfinity(maxRadius) || (dropped != null && dropped.Length != controlPoints.Length))
                return false;

            var points = new List<PaintPoint>(Math.Min(controlPoints.Length, 8192));
            float length = 0;
            uint startTime = 0;
            for (int i = 0; i < controlPoints.Length; ++i)
            {
                if (dropped != null && dropped[i]) continue;
                if (points.Count == 8192) return false;
                var cp = controlPoints[i];
                Vector3 position = ToImm(cp.m_Pos);
                // Quill brushes use the control point's orientation directly and linear pressure for radius.
                Vector3 normal = ToImm(cp.m_Orient * Vector3.up);
                Vector3 direction = ToImm(cp.m_Orient * Vector3.forward);
                if (!Finite(position) || !Finite(normal) || !Finite(direction) ||
                    !(cp.m_Pressure >= 0 && cp.m_Pressure <= 1)) return false;

                if (points.Count == 0) startTime = cp.m_TimestampMs;
                else length += Vector3.Distance(points[points.Count - 1].Position, position);
                if (float.IsInfinity(length) || cp.m_TimestampMs < startTime) return false;
                float width = maxRadius * cp.m_Pressure;
                biggest = Mathf.Max(biggest, width);
                Color srgb = stroke.GetColor(i);
                points.Add(new PaintPoint
                {
                    Position = position, Normal = normal, Direction = direction,
                    Color = srgb.linear, Alpha = srgb.a, Width = width, Length = length,
                    Time = (cp.m_TimestampMs - startTime) * 0.001f
                });
            }
            if (points.Count < 2 || length <= 0 || biggest <= 0 || !HasNativeSectionPlanes(points)) return false;
            result = new ImmLiveDrawingEdits.Stroke(section, VisibilityType.Always, points.ToArray());
            return true;
        }

        // Open Brush permits a tilted section at each control point. IMM always places the
        // section perpendicular to its computed tangent. The editable Quill importer builds
        // the same tangent from the two adjacent directions; other orientations stay on Canvas.
        private static bool HasNativeSectionPlanes(List<PaintPoint> points)
        {
            const float directionEpsilon = 1e-7f; // PaintGeometry::ComputeTangent
            Vector3 previous = Vector3.zero;
            for (int first = 0; first < points.Count;)
            {
                int next = first + 1;
                while (next < points.Count && points[next].Position.Equals(points[first].Position)) ++next;
                Vector3 outgoing = Vector3.zero;
                if (next < points.Count)
                {
                    Vector3 delta = points[next].Position - points[first].Position;
                    float length = delta.magnitude;
                    if (length < directionEpsilon) return false;
                    outgoing = delta / length;
                }
                Vector3 bisector = previous + outgoing;
                float magnitude = bisector.magnitude;
                if (!(magnitude > directionEpsilon)) return false;
                Vector3 tangent = bisector / magnitude;
                for (int i = first; i < next; ++i)
                {
                    // Tolerate unit-quaternion rounding in .tilt saves.
                    if (Vector3.Dot(points[i].Direction, tangent) < 0.9999f) return false;
                }
                previous = outgoing;
                first = next;
            }
            return true;
        }

        // The IMM player reflects document Z on the way into Unity. Widths and positions
        // already share Open Brush canvas units; the host supplies this canvas's world transform.
        private static Vector3 ToImm(Vector3 v) => new Vector3(v.x, v.y, -v.z);
        private static bool Finite(Vector3 v) => !float.IsNaN(v.sqrMagnitude) && !float.IsInfinity(v.sqrMagnitude);
    }
}
