// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0
// Distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Experimental spanning membrane. A square parameter grid is a topological disk:
    /// its perimeter follows the closed stroke by arc length, and its interior relaxes
    /// in 3D. Crossings do not merge vertices or change connectivity. Folds are allowed.
    ///
    /// Resolution depends only on the brush budget, never the shape. Precomputed influence
    /// weights encode 192 Jacobi iterations starting from the boundary mean, including on
    /// replay. All position updates are convex combinations; no projection, geometric
    /// classification, convergence threshold, or previous-frame state influences the mesh.
    /// This is a prototype, not a minimal-area solver or an intersection-free surface.
    /// </summary>
    public static class MembraneFill
    {
        public struct Options
        {
            public bool Faceted;
            public int MaxVertices;
            public float SimplifyToleranceAbsolute;
            public IList<Color32> PathColors;

            public static Options Default => new Options { MaxVertices = 20000 };
        }

        public sealed class Result
        {
            public Vector3[] Vertices;
            public int[] Triangles;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public Color32[] Colors;
            public Vector3[] Boundary;
        }

        private const int kResolution = 24;
        private const int kRelaxationSteps = 192;
        // Immutable coefficients shared across brushes; never cache stroke geometry here.
        private static readonly Dictionary<int, float[]> s_Influences = new Dictionary<int, float[]>();

        /// <summary>
        /// Scratch and output storage owned by one brush. A result using this workspace
        /// is borrowed until the next Fill call; the brush copies it into its geometry pool.
        /// No previous positions or colours influence the next solve.
        /// </summary>
        public sealed class Workspace
        {
            internal int Resolution;
            internal bool Faceted;
            internal double[] Lengths = new double[0];
            internal Vector3[] Vertices;
            internal Color[] Colors, RimColors;
            internal float[] Influences;
            internal Color32[] PackedColors;
            internal int[] Triangles;
            internal readonly Result Result = new Result();

            internal void WarmUp(Options options)
            {
                int n = GetResolution(options);
                if (n > 0) { Prepare(n, options.Faceted, 0); }
            }

            internal void Prepare(int n, bool faceted, int pathCount)
            {
                if (Lengths.Length < pathCount + 1)
                {
                    Array.Resize(ref Lengths, Math.Max(pathCount + 1, Math.Max(16, Lengths.Length * 2)));
                }
                if (Resolution == n && Faceted == faceted) { return; }
                Resolution = n;
                Faceted = faceted;
                int stride = n + 1;
                int count = stride * stride;
                Vertices = new Vector3[count];
                Colors = new Color[count];
                RimColors = new Color[4 * n];
                Influences = GetInfluences(n);
                Triangles = new int[6 * n * n];
                int corner = 0;
                for (int y = 0; y < n; ++y)
                {
                    for (int x = 0; x < n; ++x)
                    {
                        int a = y * stride + x;
                        Triangles[corner++] = a;
                        Triangles[corner++] = a + 1;
                        Triangles[corner++] = a + stride + 1;
                        Triangles[corner++] = a;
                        Triangles[corner++] = a + stride + 1;
                        Triangles[corner++] = a + stride;
                    }
                }
                int outputCount = faceted ? Triangles.Length : count;
                Result.Vertices = faceted ? new Vector3[outputCount] : Vertices;
                Result.Normals = new Vector3[outputCount];
                Result.Uvs = new Vector2[outputCount];
                Result.Triangles = faceted ? new int[outputCount] : Triangles;
                Result.Boundary = new Vector3[4 * n];
                PackedColors = new Color32[outputCount];
                for (int i = 0; i < outputCount; ++i)
                {
                    int gridIndex = faceted ? Triangles[i] : i;
                    Result.Uvs[i] = new Vector2((float)(gridIndex % stride) / n,
                        (float)(gridIndex / stride) / n);
                    if (faceted) { Result.Triangles[i] = i; }
                }
            }
        }

        private static float[] GetInfluences(int n)
        {
            lock (s_Influences)
            {
                if (s_Influences.TryGetValue(n, out var cached)) { return cached; }
                int stride = n + 1;
                int rimCount = 4 * n;
                var weights = new float[stride * stride * rimCount];
                var work = new float[weights.Length];
                for (int i = 0; i < rimCount; ++i)
                {
                    int index = RimIndex(i, n) * rimCount + i;
                    weights[index] = work[index] = 1;
                }
                for (int y = 1; y < n; ++y)
                {
                    for (int x = 1; x < n; ++x)
                    {
                        int start = (y * stride + x) * rimCount;
                        for (int j = 0; j < rimCount; ++j)
                        {
                            weights[start + j] = work[start + j] = 1f / rimCount;
                        }
                    }
                }
                for (int step = 0; step < kRelaxationSteps; ++step)
                {
                    for (int y = 1; y < n; ++y)
                    {
                        for (int x = 1; x < n; ++x)
                        {
                            int start = (y * stride + x) * rimCount;
                            for (int j = 0; j < rimCount; ++j)
                            {
                                int i = start + j;
                                work[i] = weights[i - rimCount] * 0.25f + weights[i + rimCount] * 0.25f
                                    + weights[i - stride * rimCount] * 0.25f
                                    + weights[i + stride * rimCount] * 0.25f;
                            }
                        }
                    }
                    var swap = weights; weights = work; work = swap;
                }
                s_Influences.Add(n, weights);
                return weights;
            }
        }

        private static int GetResolution(Options options)
        {
            int budget = options.MaxVertices > 0 ? options.MaxVertices : 3000;
            budget = Math.Min(budget, ushort.MaxValue);
            int n = kResolution;
            while (n > 0 && (options.Faceted ? 6 * n * n : (n + 1) * (n + 1)) > budget)
            {
                --n;
            }
            return n;
        }

        public static Result Fill(IList<Vector3> path, Options options,
            Workspace workspace = null)
        {
            if (path == null || path.Count < 2) { return null; }
            int n = GetResolution(options);
            if (n < 1) { return null; }

            // Include the implicit closing segment. Zero-length segments are skipped by
            // the sampler; no scale-dependent welding changes the stroke as it grows.
            workspace = workspace ?? new Workspace();
            workspace.Prepare(n, options.Faceted, path.Count);
            var lengths = workspace.Lengths;
            lengths[0] = 0;
            bool hasColors = options.PathColors != null && options.PathColors.Count == path.Count;
            Color32 uniformColor = hasColors ? options.PathColors[0] : default(Color32);
            bool uniformColors = hasColors;
            for (int i = 0; i < path.Count; ++i)
            {
                if (!IsFinite(path[i])) { return null; }
                if (uniformColors)
                {
                    Color32 c = options.PathColors[i];
                    uniformColors = c.r == uniformColor.r && c.g == uniformColor.g
                        && c.b == uniformColor.b && c.a == uniformColor.a;
                }
                Vector3 next = path[(i + 1) % path.Count];
                double dx = (double)next.x - path[i].x;
                double dy = (double)next.y - path[i].y;
                double dz = (double)next.z - path[i].z;
                lengths[i + 1] = lengths[i] + Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            double perimeter = lengths[path.Count];
            if (perimeter <= 0 || double.IsNaN(perimeter) || double.IsInfinity(perimeter))
            {
                return null;
            }

            // A just-started stroke often consists of one segment traversed in both
            // directions. That produces a full grid of zero-area triangles; near-line
            // strokes produce extremely thin triangles with unstable shading. Wait until
            // the path has enough width to be a visible surface. The absolute floor is
            // tied to the brush size by MembraneBrush's width tolerance, while the
            // relative floor also works for direct callers without a brush.
            Vector3 origin = path[0];
            Vector3 axis = Vector3.zero;
            double axisLengthSquared = 0;
            for (int i = 1; i < path.Count; ++i)
            {
                Vector3 offset = path[i] - origin;
                double squared = (double)offset.x * offset.x + (double)offset.y * offset.y
                    + (double)offset.z * offset.z;
                if (squared > axisLengthSquared)
                {
                    axis = offset;
                    axisLengthSquared = squared;
                }
            }
            if (axisLengthSquared <= 0) { return null; }
            double minimumWidth = Math.Max(Math.Sqrt(axisLengthSquared) * 0.025,
                Math.Max(0, options.SimplifyToleranceAbsolute) * 0.2);
            double minimumAreaSquared = minimumWidth * minimumWidth * axisLengthSquared;
            bool hasWidth = false;
            for (int i = 1; i < path.Count; ++i)
            {
                Vector3 offset = path[i] - origin;
                double cx = (double)axis.y * offset.z - (double)axis.z * offset.y;
                double cy = (double)axis.z * offset.x - (double)axis.x * offset.z;
                double cz = (double)axis.x * offset.y - (double)axis.y * offset.x;
                if (cx * cx + cy * cy + cz * cz >= minimumAreaSquared)
                {
                    hasWidth = true;
                    break;
                }
            }
            if (!hasWidth) { return null; }

            int stride = n + 1;
            int count = stride * stride;
            int rimCount = 4 * n;
            var vertices = workspace.Vertices;
            var boundary = workspace.Result.Boundary;
            bool interpolateColors = hasColors && !uniformColors;
            var colors = workspace.Colors;
            var rimColors = workspace.RimColors;
            int segment = 0;
            for (int i = 0; i < rimCount; ++i)
            {
                double distance = perimeter * i / rimCount;
                while (segment < path.Count - 1 && lengths[segment + 1] <= distance)
                {
                    ++segment;
                }
                double length = lengths[segment + 1] - lengths[segment];
                float t = length > 0 ? (float)((distance - lengths[segment]) / length) : 0;
                int next = (segment + 1) % path.Count;
                int index = RimIndex(i, n);
                Vector3 point = Vector3.Lerp(path[segment], path[next], t);
                vertices[index] = boundary[i] = point;
                if (interpolateColors)
                {
                    Color color = Color.Lerp(options.PathColors[segment], options.PathColors[next], t);
                    colors[index] = rimColors[i] = color;
                }
            }

            var influences = workspace.Influences;
            for (int y = 1; y < n; ++y)
            {
                for (int x = 1; x < n; ++x)
                {
                    int i = y * stride + x;
                    int start = i * rimCount;
                    Vector3 position = Vector3.zero;
                    for (int j = 0; j < rimCount; ++j)
                    {
                        float weight = influences[start + j];
                        position.x += boundary[j].x * weight;
                        position.y += boundary[j].y * weight;
                        position.z += boundary[j].z * weight;
                    }
                    vertices[i] = position;
                    if (interpolateColors)
                    {
                        Color color = Color.clear;
                        for (int j = 0; j < rimCount; ++j)
                        {
                            float weight = influences[start + j];
                            color.r += rimColors[j].r * weight;
                            color.g += rimColors[j].g * weight;
                            color.b += rimColors[j].b * weight;
                            color.a += rimColors[j].a * weight;
                        }
                        colors[i] = color;
                    }
                }
            }

            var result = workspace.Result;
            var triangles = workspace.Triangles;
            result.Colors = hasColors ? workspace.PackedColors : null;
            var normals = result.Normals;
            if (options.Faceted)
            {
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]],
                        c = vertices[triangles[i + 2]];
                    Vector3 face = Vector3.Cross(b - a, c - a);
                    face = face.sqrMagnitude > 0 ? face.normalized : Vector3.up;
                    for (int j = 0; j < 3; ++j)
                    {
                        int source = triangles[i + j];
                        result.Vertices[i + j] = vertices[source];
                        normals[i + j] = face;
                        if (hasColors)
                        {
                            result.Colors[i + j] = uniformColors ? uniformColor : (Color32)colors[source];
                        }
                    }
                }
            }
            else
            {
                result.Vertices = vertices;
                Array.Clear(normals, 0, normals.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    normals[a] += face; normals[b] += face; normals[c] += face;
                }
                for (int i = 0; i < count; ++i)
                {
                    normals[i] = normals[i].sqrMagnitude > 0 ? normals[i].normalized : Vector3.up;
                    if (hasColors) { result.Colors[i] = uniformColors ? uniformColor : (Color32)colors[i]; }
                }
            }
            // Reuse the brush's mesh transport. Plane diagnostics and BoundaryIndices do
            // not apply: the rim is resampled, rather than a subset of control points.
            return result;
        }

        /// <summary>
        /// Refine only the rim when a stroke is finished. Existing grid vertices keep their
        /// preview positions; new rim vertices sample the original path, not the coarse
        /// outline. A fan inside each affected triangle joins them to the unchanged mesh.
        /// This is independent of stroke history and also runs during saved-stroke replay.
        /// </summary>
        public static Result FillFinal(IList<Vector3> path, Options options,
            Workspace workspace = null, int boundarySubdivisions = 4)
        {
            workspace = workspace ?? new Workspace();
            var coarse = Fill(path, options, workspace);
            if (coarse == null) { return null; }
            int n = workspace.Resolution;
            int rimCount = 4 * n;
            int gridCount = (n + 1) * (n + 1);
            int boundaryTriangles = rimCount - 2; // Two corner triangles each own two rim edges.
            int budget = options.MaxVertices > 0 ? options.MaxVertices : 3000;
            budget = Math.Min(budget, ushort.MaxValue);
            int subdivisions = Math.Max(1, Math.Min(boundarySubdivisions, 8));
            while (subdivisions > 1)
            {
                int extraRimVertices = rimCount * (subdivisions - 1);
                int outputVertices = options.Faceted
                    ? 3 * (2 * n * n + 2 * boundaryTriangles + extraRimVertices)
                    : gridCount + boundaryTriangles + extraRimVertices;
                if (outputVertices <= budget) { break; }
                --subdivisions;
            }
            if (subdivisions == 1) { return coarse; }

            var vertices = new List<Vector3>(workspace.Vertices);
            var uvs = new List<Vector2>(gridCount);
            var colors = coarse.Colors != null ? new List<Color32>(new Color32[gridCount]) : null;
            for (int i = 0; i < gridCount; ++i)
            {
                uvs.Add(new Vector2((float)(i % (n + 1)) / n, (float)(i / (n + 1)) / n));
                if (colors != null && !options.Faceted) { colors[i] = coarse.Colors[i]; }
            }
            if (colors != null && options.Faceted)
            {
                for (int i = 0; i < workspace.Triangles.Length; ++i)
                {
                    colors[workspace.Triangles[i]] = coarse.Colors[i];
                }
            }

            var rimLookup = new int[gridCount];
            for (int i = 0; i < gridCount; ++i) { rimLookup[i] = -1; }
            for (int i = 0; i < rimCount; ++i) { rimLookup[RimIndex(i, n)] = i; }
            int denseCount = rimCount * subdivisions;
            var denseIndices = new int[denseCount];
            var boundary = new Vector3[denseCount];
            var lengths = workspace.Lengths;
            double perimeter = lengths[path.Count];
            int segment = 0;
            for (int i = 0; i < denseCount; ++i)
            {
                int rim = i / subdivisions;
                int sub = i % subdivisions;
                int a = RimIndex(rim, n);
                if (sub == 0)
                {
                    denseIndices[i] = a;
                    boundary[i] = vertices[a];
                    continue;
                }
                double distance = perimeter * i / denseCount;
                while (segment < path.Count - 1 && lengths[segment + 1] <= distance) { ++segment; }
                double length = lengths[segment + 1] - lengths[segment];
                float t = length > 0 ? (float)((distance - lengths[segment]) / length) : 0;
                int next = (segment + 1) % path.Count;
                Vector3 position = Vector3.Lerp(path[segment], path[next], t);
                denseIndices[i] = vertices.Count;
                boundary[i] = position;
                vertices.Add(position);
                int b = RimIndex((rim + 1) % rimCount, n);
                uvs.Add(Vector2.Lerp(uvs[a], uvs[b], (float)sub / subdivisions));
                if (colors != null)
                {
                    colors.Add(Color.Lerp(options.PathColors[segment], options.PathColors[next], t));
                }
            }

            var triangles = new List<int>();
            var polygon = new List<int>(3 * subdivisions);
            for (int i = 0; i < workspace.Triangles.Length; i += 3)
            {
                int a = workspace.Triangles[i], b = workspace.Triangles[i + 1],
                    c = workspace.Triangles[i + 2];
                polygon.Clear();
                AppendRefinedEdge(polygon, a, b, rimLookup, denseIndices, subdivisions);
                AppendRefinedEdge(polygon, b, c, rimLookup, denseIndices, subdivisions);
                AppendRefinedEdge(polygon, c, a, rimLookup, denseIndices, subdivisions);
                if (polygon.Count == 3)
                {
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    continue;
                }
                int center = vertices.Count;
                vertices.Add((vertices[a] + vertices[b] + vertices[c]) / 3);
                uvs.Add((uvs[a] + uvs[b] + uvs[c]) / 3);
                if (colors != null)
                {
                    colors.Add(((Color)colors[a] + (Color)colors[b] + (Color)colors[c]) / 3);
                }
                for (int j = 0; j < polygon.Count; ++j)
                {
                    triangles.Add(polygon[j]);
                    triangles.Add(polygon[(j + 1) % polygon.Count]);
                    triangles.Add(center);
                }
            }
            var finalVertices = vertices.ToArray();
            var finalUvs = uvs.ToArray();
            var finalColors = colors != null ? colors.ToArray() : null;
            var finalTriangles = triangles.ToArray();
            Vector3[] normals;
            if (options.Faceted)
            {
                Facet(ref finalVertices, ref finalUvs, ref finalColors,
                    ref finalTriangles, out normals, Vector3.up);
            }
            else
            {
                normals = ComputeNormals(finalVertices, finalTriangles, Vector3.up);
            }
            return new Result
            {
                Vertices = finalVertices, Triangles = finalTriangles, Normals = normals,
                Uvs = finalUvs, Colors = finalColors, Boundary = boundary
            };
        }

        private static void AppendRefinedEdge(List<int> polygon, int a, int b, int[] rimLookup,
            int[] denseIndices, int subdivisions)
        {
            polygon.Add(a);
            int rimA = rimLookup[a], rimB = rimLookup[b];
            int rimCount = denseIndices.Length / subdivisions;
            if (rimA < 0 || rimB < 0) { return; }
            int direction = (rimA + 1) % rimCount == rimB ? 1
                : (rimB + 1) % rimCount == rimA ? -1 : 0;
            if (direction == 0) { return; }
            for (int j = 1; j < subdivisions; ++j)
            {
                int index = (rimA * subdivisions + direction * j + denseIndices.Length) % denseIndices.Length;
                polygon.Add(denseIndices[index]);
            }
        }

        private static int RimIndex(int i, int n)
        {
            int side = i / n;
            int offset = i % n;
            int stride = n + 1;
            switch (side)
            {
                case 0: return offset;
                case 1: return offset * stride + n;
                case 2: return n * stride + n - offset;
                default: return (n - offset) * stride;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
                && !float.IsInfinity(value.x) && !float.IsInfinity(value.y)
                && !float.IsInfinity(value.z);
        }

        private static void Facet(ref Vector3[] vertices, ref Vector2[] uvs,
            ref Color32[] colors, ref int[] triangles, out Vector3[] normals, Vector3 fallback)
        {
            int cornerCount = triangles.Length;
            var newVertices = new Vector3[cornerCount];
            var newUvs = new Vector2[cornerCount];
            var newColors = colors != null ? new Color32[cornerCount] : null;
            var newTriangles = new int[cornerCount];
            normals = new Vector3[cornerCount];
            for (int i = 0; i + 2 < cornerCount; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Vector3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                Vector3 face = Vector3.Cross(b - a, c - a);
                face = face.sqrMagnitude > 0f ? face.normalized : fallback;
                newVertices[i] = a; newVertices[i + 1] = b; newVertices[i + 2] = c;
                newUvs[i] = uvs[ia]; newUvs[i + 1] = uvs[ib]; newUvs[i + 2] = uvs[ic];
                normals[i] = face; normals[i + 1] = face; normals[i + 2] = face;
                if (newColors != null)
                {
                    newColors[i] = colors[ia];
                    newColors[i + 1] = colors[ib];
                    newColors[i + 2] = colors[ic];
                }
                newTriangles[i] = i; newTriangles[i + 1] = i + 1; newTriangles[i + 2] = i + 2;
            }
            vertices = newVertices;
            uvs = newUvs;
            colors = newColors;
            triangles = newTriangles;
        }

        private static Vector3[] ComputeNormals(Vector3[] vertices, int[] triangles,
            Vector3 fallback)
        {
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                normals[a] += face;
                normals[b] += face;
                normals[c] += face;
            }
            for (int i = 0; i < normals.Length; ++i)
            {
                normals[i] = normals[i].sqrMagnitude > 0f ? normals[i].normalized : fallback;
            }
            return normals;
        }
    }
}
