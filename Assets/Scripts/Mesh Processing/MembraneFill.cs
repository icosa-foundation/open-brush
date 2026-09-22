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
    /// Resolution depends only on the brush budget, never the shape. Every rebuild starts
    /// from the boundary mean and performs the same number of Jacobi iterations, including
    /// on replay. All position updates are convex combinations; no projection, geometric
    /// classification, convergence threshold, or previous-frame state influences the mesh.
    /// This is a prototype, not a minimal-area solver or an intersection-free surface.
    /// </summary>
    public static class MembraneFill
    {
        private const int kResolution = 24;
        private const int kRelaxationSteps = 192;

        public static PathFill.Result Fill(IList<Vector3> path, PathFill.Options options)
        {
            if (path == null || path.Count < 2) { return null; }
            int budget = options.MaxVertices > 0 ? options.MaxVertices : 3000;
            budget = Math.Min(budget, ushort.MaxValue);
            int n = kResolution;
            while (n > 0 && (options.Faceted ? 6 * n * n : (n + 1) * (n + 1)) > budget)
            {
                --n;
            }
            if (n < 1) { return null; }

            // Include the implicit closing segment. Zero-length segments are skipped by
            // the sampler; no scale-dependent welding changes the stroke as it grows.
            var lengths = new double[path.Count + 1];
            for (int i = 0; i < path.Count; ++i)
            {
                if (!PathFillGeometry.IsFinite(path[i])) { return null; }
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
            var vertices = new Vector3[count];
            var work = new Vector3[count];
            var uvs = new Vector2[count];
            var boundary = new Vector3[rimCount];
            bool hasColors = options.PathColors != null && options.PathColors.Count == path.Count;
            var colors = hasColors ? new Color[count] : null;
            var colorWork = hasColors ? new Color[count] : null;
            Vector3 mean = Vector3.zero;
            Color meanColor = Color.clear;
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
                vertices[index] = work[index] = boundary[i] = point;
                mean += point / rimCount;
                if (hasColors)
                {
                    Color color = Color.Lerp(options.PathColors[segment], options.PathColors[next], t);
                    colors[index] = colorWork[index] = color;
                    meanColor += color / rimCount;
                }
            }

            for (int y = 0; y <= n; ++y)
            {
                for (int x = 0; x <= n; ++x)
                {
                    int i = y * stride + x;
                    uvs[i] = new Vector2((float)x / n, (float)y / n);
                    if (x == 0 || y == 0 || x == n || y == n) { continue; }
                    vertices[i] = work[i] = mean;
                    if (hasColors) { colors[i] = colorWork[i] = meanColor; }
                }
            }

            for (int step = 0; step < kRelaxationSteps; ++step)
            {
                for (int y = 1; y < n; ++y)
                {
                    for (int x = 1; x < n; ++x)
                    {
                        int i = y * stride + x;
                        work[i] = vertices[i - 1] * 0.25f + vertices[i + 1] * 0.25f
                            + vertices[i - stride] * 0.25f + vertices[i + stride] * 0.25f;
                        if (hasColors)
                        {
                            colorWork[i] = (colors[i - 1] + colors[i + 1]
                                + colors[i - stride] + colors[i + stride]) * 0.25f;
                        }
                    }
                }
                var swap = vertices; vertices = work; work = swap;
                var colorSwap = colors; colors = colorWork; colorWork = colorSwap;
            }

            var triangles = new int[6 * n * n];
            int corner = 0;
            for (int y = 0; y < n; ++y)
            {
                for (int x = 0; x < n; ++x)
                {
                    int a = y * stride + x;
                    triangles[corner++] = a;
                    triangles[corner++] = a + 1;
                    triangles[corner++] = a + stride + 1;
                    triangles[corner++] = a;
                    triangles[corner++] = a + stride + 1;
                    triangles[corner++] = a + stride;
                }
            }
            Color32[] packedColors = hasColors ? new Color32[count] : null;
            if (hasColors)
            {
                for (int i = 0; i < count; ++i) { packedColors[i] = colors[i]; }
            }
            Vector3[] normals;
            if (options.Faceted)
            {
                PathFillGeometry.Facet(ref vertices, ref uvs, ref packedColors,
                    ref triangles, out normals, Vector3.up);
            }
            else
            {
                normals = PathFillGeometry.ComputeNormals(vertices, triangles, Vector3.up);
            }
            // Reuse the brush's mesh transport. Plane diagnostics and BoundaryIndices do
            // not apply: the rim is resampled, rather than a subset of control points.
            return new PathFill.Result
            {
                Vertices = vertices, Triangles = triangles, Normals = normals,
                Uvs = uvs, Colors = packedColors, Boundary = boundary,
                BoundaryIndices = null
            };
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
