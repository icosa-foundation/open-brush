// Copyright 2025 The Open Brush Authors
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
using Unity.VectorGraphics.OpenBrush;
using UnityEngine;

namespace TiltBrush
{

    /// The 2D triangulation step of <see cref="PathFill"/>, backed by the tessellator that
    /// ships with com.unity.vectorgraphics (a vendored libtess). Using it rather than a
    /// hand-rolled ear clipper is what gives the fill true 2D fill semantics: non-zero and
    /// even-odd winding, self-intersecting outlines, and multiple contours forming holes.
    public static class PathFillTessellator
    {
        /// Tessellates a single closed outline. See <see cref="PathFillTessellateFn"/>.
        public static bool Tessellate(IList<Vector2> outline, PathFillRule rule,
                                      List<Vector2> outVertices, List<int> outTriangles)
        {
            return Tessellate(new[] { outline }, rule, outVertices, outTriangles);
        }

        /// Tessellates one or more closed contours as a single filled shape. Extra contours
        /// become holes or additional regions according to the fill rule, exactly as
        /// sub-paths of a compound path do in a 2D drawing app.
        public static bool Tessellate(IList<IList<Vector2>> contours, PathFillRule rule,
                                      List<Vector2> outVertices, List<int> outTriangles)
        {
            if (contours == null || contours.Count == 0) { return false; }

            var bezierContours = new List<BezierContour>(contours.Count);
            foreach (IList<Vector2> contour in contours)
            {
                if (contour == null || contour.Count < 3) { continue; }
                bezierContours.Add(MakePolygonContour(contour));
            }
            if (bezierContours.Count == 0) { return false; }

            var shape = new Shape
            {
                Contours = bezierContours.ToArray(),
                Fill = new SolidFill
                {
                    Color = Color.white,
                    Mode = rule == PathFillRule.OddEven ? FillMode.OddEven : FillMode.NonZero,
                },
                // Forces the libtess path rather than the centroid fan, which cannot
                // represent concave regions or holes.
                IsConvex = false,
                PathProps = new PathProperties(),
            };

            var scene = new Scene
            {
                Root = new SceneNode { Shapes = new List<Shape> { shape } }
            };

            // The contours are already polylines, so ask for no curve flattening at all. A
            // step distance longer than the whole outline means the tracer emits vertices
            // only at the segment boundaries we supplied, i.e. exactly our input corners.
            float perimeter = 0f;
            foreach (BezierContour contour in bezierContours)
            {
                BezierPathSegment[] segments = contour.Segments;
                for (int i = 0; i < segments.Length; ++i)
                {
                    perimeter += (segments[(i + 1) % segments.Length].P0 - segments[i].P0).magnitude;
                }
            }
            var tessellationOptions = new VectorUtils.TessellationOptions
            {
                StepDistance = Mathf.Max(perimeter, 1f) * 4f,
                MaxCordDeviation = float.MaxValue,
                MaxTanAngleDeviation = Mathf.PI * 0.5f,
                SamplingStepSize = 0.01f,
                OutlinesOnly = false,
                ConvexOutlinesOnly = false,
            };

            List<VectorUtils.Geometry> geometries;
            try
            {
                geometries = VectorUtils.TessellateScene(scene, tessellationOptions);
            }
            catch (Exception e)
            {
                Debug.LogWarning("PathFill: tessellation failed: " + e.Message);
                return false;
            }
            if (geometries == null || geometries.Count == 0) { return false; }

            outVertices.Clear();
            outTriangles.Clear();
            int dropped = 0;
            foreach (VectorUtils.Geometry geometry in geometries)
            {
                if (geometry.Vertices == null || geometry.Indices == null) { continue; }
                int offset = outVertices.Count;
                for (int i = 0; i < geometry.Vertices.Length; ++i)
                {
                    outVertices.Add(geometry.Vertices[i]);
                }
                int limit = geometry.Vertices.Length;
                for (int i = 0; i + 2 < geometry.Indices.Length; i += 3)
                {
                    int a = geometry.Indices[i];
                    int b = geometry.Indices[i + 1];
                    int c = geometry.Indices[i + 2];

                    // LibTess pads the slots of a polygon it did not fill with Undef (-1), and
                    // the package casts its indices to UInt16 unchecked, so that padding
                    // arrives here as 65535. Forwarding it would index a vertex that does not
                    // exist. Degenerate faces reach us too, because LibTess is run with its
                    // NoEmptyPolygons option off.
                    if (a >= limit || b >= limit || c >= limit || a == b || b == c || c == a)
                    {
                        ++dropped;
                        continue;
                    }

                    outTriangles.Add(offset + a);
                    outTriangles.Add(offset + b);
                    outTriangles.Add(offset + c);
                }
            }
            DroppedTriangles = dropped;
            return outVertices.Count >= 3 && outTriangles.Count >= 3;
        }

        /// Number of triangles the last call discarded as malformed. Surfaced for diagnosis;
        /// a non-zero value means the tessellator produced something we could not use.
        public static int DroppedTriangles { get; private set; }

        /// A closed BezierContour whose segments are straight lines. BezierPathSegment holds
        /// a start point plus two cubic control points; the segment's end point is the next
        /// segment's start, and for a closed contour the last segment wraps to the first.
        private static BezierContour MakePolygonContour(IList<Vector2> polygon)
        {
            int n = polygon.Count;
            var segments = new BezierPathSegment[n];
            for (int i = 0; i < n; ++i)
            {
                Vector2 from = polygon[i];
                Vector2 to = polygon[(i + 1) % n];
                Vector2 delta = to - from;
                segments[i] = new BezierPathSegment
                {
                    P0 = from,
                    P1 = from + delta / 3f,
                    P2 = from + delta * (2f / 3f),
                };
            }
            return new BezierContour { Segments = segments, Closed = true };
        }
    }
} // namespace TiltBrush
