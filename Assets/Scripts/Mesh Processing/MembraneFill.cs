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
            internal readonly PathFill.Result Result = new PathFill.Result();

            internal void WarmUp(PathFill.Options options)
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

        private static int GetResolution(PathFill.Options options)
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

        public static PathFill.Result Fill(IList<Vector3> path, PathFill.Options options,
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
                if (!PathFillGeometry.IsFinite(path[i])) { return null; }
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
    }
}
