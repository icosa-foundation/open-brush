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
using UnityEngine;

namespace TiltBrush
{

    /// Geometry helpers for <see cref="PathFill"/>. Everything here is deterministic and
    /// free of Unity scene state, so it can be exercised directly from edit-mode tests.
    public static class PathFillGeometry
    {
        // -------------------------------------------------------------------------------
        // Path preparation
        // -------------------------------------------------------------------------------

        public static float BoundsDiagonal(IList<Vector3> path)
        {
            if (path == null || path.Count == 0) { return 0f; }
            Vector3 lo = path[0], hi = path[0];
            for (int i = 1; i < path.Count; ++i)
            {
                lo = Vector3.Min(lo, path[i]);
                hi = Vector3.Max(hi, path[i]);
            }
            return (hi - lo).magnitude;
        }

        /// Removes consecutive duplicates and the repeated closing point, if any. Returns
        /// indices into `path` rather than points, so that callers can carry per-point
        /// attributes (colour, pressure) through to the boundary. The returned loop is
        /// implicitly closed: the edge from the last point back to the first is not
        /// represented by a duplicated entry.
        public static List<int> WeldAndOpen(IList<Vector3> path, float weldTolerance)
        {
            float tolSq = weldTolerance * weldTolerance;
            var result = new List<int>(path.Count);
            for (int i = 0; i < path.Count; ++i)
            {
                if (result.Count > 0 &&
                    (path[i] - path[result[result.Count - 1]]).sqrMagnitude <= tolSq)
                {
                    continue;
                }
                result.Add(i);
            }
            while (result.Count > 1 &&
                   (path[result[result.Count - 1]] - path[result[0]]).sqrMagnitude <= tolSq)
            {
                result.RemoveAt(result.Count - 1);
            }
            return result;
        }

        /// Ramer-Douglas-Peucker simplification of a closed loop, as indices into `path`.
        /// The loop is cut at two far-apart anchors so the recursion has well-defined
        /// endpoints; the tolerance is then adjusted until the result has between 3 and
        /// maxPoints vertices.
        public static List<int> SimplifyClosed(IList<Vector3> path, List<int> loop,
                                               float tolerance, int maxPoints)
        {
            if (loop.Count <= 3) { return loop; }

            int anchor = 0;
            float best = -1f;
            for (int i = 1; i < loop.Count; ++i)
            {
                float d = (path[loop[i]] - path[loop[0]]).sqrMagnitude;
                if (d > best) { best = d; anchor = i; }
            }
            if (anchor == 0) { return loop; }

            // Loosen until we are under the cap, then tighten if we over-simplified.
            float tol = tolerance;
            List<int> simplified = SimplifyClosedAt(path, loop, anchor, tol);
            for (int attempt = 0; attempt < 24 && simplified.Count > maxPoints; ++attempt)
            {
                tol *= 2f;
                simplified = SimplifyClosedAt(path, loop, anchor, tol);
            }
            for (int attempt = 0; attempt < 24 && simplified.Count < 3; ++attempt)
            {
                tol *= 0.5f;
                simplified = SimplifyClosedAt(path, loop, anchor, tol);
            }
            if (simplified.Count < 3 || simplified.Count > maxPoints)
            {
                return UniformSample(loop, Mathf.Min(maxPoints, loop.Count));
            }
            return simplified;
        }

        private static List<int> SimplifyClosedAt(IList<Vector3> path, List<int> loop,
                                                  int anchor, float tolerance)
        {
            int n = loop.Count;
            var first = new List<int>(anchor + 1);
            for (int i = 0; i <= anchor; ++i) { first.Add(loop[i]); }
            var second = new List<int>(n - anchor + 1);
            for (int i = anchor; i < n; ++i) { second.Add(loop[i]); }
            second.Add(loop[0]);

            List<int> a = Simplify(path, first, tolerance);
            List<int> b = Simplify(path, second, tolerance);

            // a ends where b starts, and b ends where a starts.
            var result = new List<int>(a.Count + b.Count - 2);
            result.AddRange(a);
            for (int i = 1; i < b.Count - 1; ++i) { result.Add(b[i]); }
            return result;
        }

        /// Ramer-Douglas-Peucker simplification of an open polyline, as indices into `path`.
        /// Endpoints are kept.
        public static List<int> Simplify(IList<Vector3> path, List<int> chain, float tolerance)
        {
            int n = chain.Count;
            if (n <= 2) { return new List<int>(chain); }

            var keep = new bool[n];
            keep[0] = true;
            keep[n - 1] = true;

            var stack = new Stack<int>();
            stack.Push(0);
            stack.Push(n - 1);
            while (stack.Count > 0)
            {
                int last = stack.Pop();
                int firstIndex = stack.Pop();
                if (last - firstIndex < 2) { continue; }

                float worst = -1f;
                int worstIndex = -1;
                for (int i = firstIndex + 1; i < last; ++i)
                {
                    float d = DistanceToSegment(
                        path[chain[i]], path[chain[firstIndex]], path[chain[last]]);
                    if (d > worst) { worst = d; worstIndex = i; }
                }
                if (worst > tolerance && worstIndex > 0)
                {
                    keep[worstIndex] = true;
                    stack.Push(firstIndex);
                    stack.Push(worstIndex);
                    stack.Push(worstIndex);
                    stack.Push(last);
                }
            }

            var result = new List<int>(n);
            for (int i = 0; i < n; ++i)
            {
                if (keep[i]) { result.Add(chain[i]); }
            }
            return result;
        }

        private static List<int> UniformSample(List<int> loop, int count)
        {
            count = Mathf.Max(3, Mathf.Min(count, loop.Count));
            var result = new List<int>(count);
            for (int i = 0; i < count; ++i)
            {
                result.Add(loop[(int)((long)i * loop.Count / count)]);
            }
            return result;
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq <= 0f) { return (p - a).magnitude; }
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
            return (p - (a + ab * t)).magnitude;
        }

        // -------------------------------------------------------------------------------
        // Plane fitting
        // -------------------------------------------------------------------------------

        /// Total-least-squares plane through the points: the centroid plus the eigenvector
        /// of the covariance matrix with the smallest eigenvalue. The normal is flipped to
        /// agree with the Newell normal so that it matches the loop's winding.
        /// Returns false when the points are collinear, which has no fill.
        public static bool FitPlane(IList<Vector3> points, out Vector3 origin, out Vector3 normal)
        {
            origin = Vector3.zero;
            normal = Vector3.up;
            int n = points.Count;
            if (n < 3) { return false; }

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < n; ++i) { centroid += points[i]; }
            centroid /= n;

            double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            for (int i = 0; i < n; ++i)
            {
                Vector3 d = points[i] - centroid;
                xx += (double)d.x * d.x; xy += (double)d.x * d.y; xz += (double)d.x * d.z;
                yy += (double)d.y * d.y; yz += (double)d.y * d.z; zz += (double)d.z * d.z;
            }

            var cov = new double[3, 3]
            {
                { xx, xy, xz },
                { xy, yy, yz },
                { xz, yz, zz },
            };
            double[] values;
            double[,] vectors;
            JacobiEigen(cov, out values, out vectors);

            int smallest = 0, largest = 0, middle;
            for (int i = 1; i < 3; ++i)
            {
                if (values[i] < values[smallest]) { smallest = i; }
                if (values[i] > values[largest]) { largest = i; }
            }
            middle = 3 - smallest - largest;
            if (smallest == largest) { return false; }

            // Two vanishing eigenvalues means every point lies on a line.
            if (values[middle] <= values[largest] * 1e-10) { return false; }

            var fit = new Vector3(
                (float)vectors[0, smallest], (float)vectors[1, smallest], (float)vectors[2, smallest]);
            if (fit.sqrMagnitude <= 0f) { return false; }
            fit.Normalize();

            Vector3 newell = NewellNormal(points);
            if (Vector3.Dot(fit, newell) < 0f) { fit = -fit; }

            origin = centroid;
            normal = fit;
            return true;
        }

        /// Newell's method: the area-weighted normal of a (possibly non-planar) closed loop.
        /// Not normalized; its length is twice the projected area.
        public static Vector3 NewellNormal(IList<Vector3> loop)
        {
            Vector3 normal = Vector3.zero;
            int n = loop.Count;
            for (int i = 0; i < n; ++i)
            {
                Vector3 a = loop[i];
                Vector3 b = loop[(i + 1) % n];
                normal.x += (a.y - b.y) * (a.z + b.z);
                normal.y += (a.z - b.z) * (a.x + b.x);
                normal.z += (a.x - b.x) * (a.y + b.y);
            }
            return normal * 0.5f;
        }

        /// Right-handed basis with axisU x axisV == normal, so that a counter-clockwise
        /// triangle in the (u, v) frame has its 3D normal pointing along `normal`.
        public static void BasisFromNormal(Vector3 normal, out Vector3 axisU, out Vector3 axisV)
        {
            Vector3 helper = Mathf.Abs(normal.x) < 0.9f ? Vector3.right : Vector3.up;
            axisU = Vector3.Cross(helper, normal).normalized;
            axisV = Vector3.Cross(normal, axisU);
        }

        /// Cyclic Jacobi eigenvalue decomposition of a symmetric 3x3 matrix. Fixed sweep
        /// count, so the result is bit-for-bit reproducible. Columns of `vectors` are the
        /// eigenvectors corresponding to `values`.
        public static void JacobiEigen(double[,] matrix, out double[] values, out double[,] vectors)
        {
            var a = (double[,])matrix.Clone();
            vectors = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };

            for (int sweep = 0; sweep < 24; ++sweep)
            {
                double off = Math.Abs(a[0, 1]) + Math.Abs(a[0, 2]) + Math.Abs(a[1, 2]);
                if (off <= 1e-30) { break; }

                for (int p = 0; p < 2; ++p)
                {
                    for (int q = p + 1; q < 3; ++q)
                    {
                        if (Math.Abs(a[p, q]) <= 1e-30) { continue; }
                        double theta = (a[q, q] - a[p, p]) / (2.0 * a[p, q]);
                        double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                        if (theta == 0.0) { t = 1.0; }
                        double c = 1.0 / Math.Sqrt(t * t + 1.0);
                        double s = t * c;

                        double app = a[p, p], aqq = a[q, q], apq = a[p, q];
                        a[p, p] = app - t * apq;
                        a[q, q] = aqq + t * apq;
                        a[p, q] = 0.0;
                        a[q, p] = 0.0;
                        for (int r = 0; r < 3; ++r)
                        {
                            if (r == p || r == q) { continue; }
                            double arp = a[r, p], arq = a[r, q];
                            a[r, p] = c * arp - s * arq;
                            a[p, r] = a[r, p];
                            a[r, q] = s * arp + c * arq;
                            a[q, r] = a[r, q];
                        }
                        for (int r = 0; r < 3; ++r)
                        {
                            double vrp = vectors[r, p], vrq = vectors[r, q];
                            vectors[r, p] = c * vrp - s * vrq;
                            vectors[r, q] = s * vrp + c * vrq;
                        }
                    }
                }
            }

            values = new double[3] { a[0, 0], a[1, 1], a[2, 2] };
        }

        // -------------------------------------------------------------------------------
        // 2D helpers
        // -------------------------------------------------------------------------------

        public static Vector2 Min(IList<Vector2> points)
        {
            Vector2 lo = points[0];
            for (int i = 1; i < points.Count; ++i) { lo = Vector2.Min(lo, points[i]); }
            return lo;
        }

        public static float MaxExtent(IList<Vector2> points)
        {
            if (points.Count == 0) { return 0f; }
            Vector2 lo = points[0], hi = points[0];
            for (int i = 1; i < points.Count; ++i)
            {
                lo = Vector2.Min(lo, points[i]);
                hi = Vector2.Max(hi, points[i]);
            }
            return Mathf.Max(hi.x - lo.x, hi.y - lo.y);
        }

        public static float MeanEdgeLength(IList<Vector2> loop)
        {
            int n = loop.Count;
            if (n < 2) { return 0f; }
            float total = 0f;
            for (int i = 0; i < n; ++i) { total += (loop[(i + 1) % n] - loop[i]).magnitude; }
            return total / n;
        }

        /// Counts crossing pairs among the closed outline's edges, ignoring pairs that share
        /// an endpoint. O(n^2), which is fine for a simplified boundary.
        public static int CountSelfIntersections(IList<Vector2> loop)
        {
            int n = loop.Count;
            int count = 0;
            for (int i = 0; i < n; ++i)
            {
                int iNext = (i + 1) % n;
                for (int j = i + 1; j < n; ++j)
                {
                    int jNext = (j + 1) % n;
                    if (i == j || iNext == j || jNext == i) { continue; }
                    if (SegmentsCross(loop[i], loop[iNext], loop[j], loop[jNext])) { ++count; }
                }
            }
            return count;
        }

        private static bool SegmentsCross(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            float d0 = Cross(a1 - a0, b0 - a0);
            float d1 = Cross(a1 - a0, b1 - a0);
            float d2 = Cross(b1 - b0, a0 - b0);
            float d3 = Cross(b1 - b0, a1 - b0);
            return ((d0 > 0f) != (d1 > 0f)) && ((d2 > 0f) != (d3 > 0f));
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        // -------------------------------------------------------------------------------
        // Refinement
        // -------------------------------------------------------------------------------

        /// Uniform 1->4 subdivision, repeated until no edge is longer than targetEdge or a
        /// limit is hit. Uniform rather than adaptive so that no T-junctions can appear and
        /// the result does not depend on triangle visit order.
        public static void Subdivide(List<Vector2> vertices, List<int> triangles,
                                     float targetEdge, int maxVertices, int maxPasses)
        {
            if (targetEdge <= 0f) { return; }
            for (int pass = 0; pass < maxPasses; ++pass)
            {
                if (LongestEdge(vertices, triangles) <= targetEdge) { return; }
                // After a 1->4 split the new vertex count is V + E, and E is about half the
                // index count for a triangulated patch.
                if (vertices.Count + triangles.Count / 2 > maxVertices) { return; }
                SubdivideOnce(vertices, triangles);
            }
        }

        public static float LongestEdge(List<Vector2> vertices, List<int> triangles)
        {
            float longest = 0f;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = vertices[triangles[i]];
                Vector2 b = vertices[triangles[i + 1]];
                Vector2 c = vertices[triangles[i + 2]];
                longest = Mathf.Max(longest, (b - a).magnitude);
                longest = Mathf.Max(longest, (c - b).magnitude);
                longest = Mathf.Max(longest, (a - c).magnitude);
            }
            return longest;
        }

        private static void SubdivideOnce(List<Vector2> vertices, List<int> triangles)
        {
            long stride = vertices.Count;
            var midpoints = new Dictionary<long, int>(triangles.Count);
            var result = new List<int>(triangles.Count * 4);

            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                int ab = Midpoint(vertices, midpoints, stride, a, b);
                int bc = Midpoint(vertices, midpoints, stride, b, c);
                int ca = Midpoint(vertices, midpoints, stride, c, a);

                result.Add(a); result.Add(ab); result.Add(ca);
                result.Add(ab); result.Add(b); result.Add(bc);
                result.Add(ca); result.Add(bc); result.Add(c);
                result.Add(ab); result.Add(bc); result.Add(ca);
            }

            triangles.Clear();
            triangles.AddRange(result);
        }

        private static int Midpoint(List<Vector2> vertices, Dictionary<long, int> midpoints,
                                    long stride, int a, int b)
        {
            long key = a < b ? a * stride + b : b * stride + a;
            int index;
            if (midpoints.TryGetValue(key, out index)) { return index; }
            index = vertices.Count;
            vertices.Add((vertices[a] + vertices[b]) * 0.5f);
            midpoints[key] = index;
            return index;
        }

        /// Makes every triangle wind counter-clockwise in the 2D frame, so that the lifted
        /// surface's normals agree with the fitted plane normal.
        public static void EnsureCounterClockwise(List<Vector2> vertices, List<int> triangles)
        {
            double signedArea = 0.0;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = vertices[triangles[i]];
                Vector2 b = vertices[triangles[i + 1]];
                Vector2 c = vertices[triangles[i + 2]];
                signedArea += Cross(b - a, c - a);
            }
            if (signedArea >= 0.0) { return; }
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }
        }

        // -------------------------------------------------------------------------------
        // Lifting
        // -------------------------------------------------------------------------------

        /// Interpolates per-boundary-vertex values over the polygon's interior using
        /// Floater's mean value coordinates. Smooth, closed form (no linear solve, so no
        /// iteration-count determinism worries), and interpolating: evaluated at a boundary
        /// vertex it returns that vertex's value exactly, and on a boundary edge it returns
        /// the linear blend of the edge's endpoints. That exactness is what keeps the fill
        /// welded to the stroke it was built from.
        public static float MeanValueInterpolate(Vector2 p, IList<Vector2> polygon, IList<float> values)
        {
            var weights = new float[polygon.Count];
            MeanValueWeights(p, polygon, weights);
            float total = 0f;
            for (int i = 0; i < weights.Length; ++i) { total += weights[i] * values[i]; }
            return total;
        }

        /// Fills `weights` with normalized mean value coordinates of `p` with respect to
        /// `polygon`; they sum to 1. Weights may be negative where the polygon is concave,
        /// which is inherent to the scheme: interpolated values can overshoot the range of
        /// the boundary values there. Callers that interpolate a bounded quantity (colour,
        /// say) should clamp.
        public static void MeanValueWeights(Vector2 p, IList<Vector2> polygon, float[] weights)
        {
            var scratch = new Scratch(polygon.Count);
            MeanValueWeights(p, polygon, weights, scratch);
        }

        /// Scratch buffers for <see cref="MeanValueWeights"/>. The brush path evaluates mean
        /// value coordinates once per output vertex, every time the stroke changes, so the
        /// inner loop must not allocate.
        public class Scratch
        {
            public Vector2[] Offsets;
            public float[] Distances;
            public float[] TanHalf;

            public Scratch(int boundaryCount)
            {
                Resize(boundaryCount);
            }

            public void Resize(int boundaryCount)
            {
                if (Offsets != null && Offsets.Length >= boundaryCount) { return; }
                Offsets = new Vector2[boundaryCount];
                Distances = new float[boundaryCount];
                TanHalf = new float[boundaryCount];
            }
        }

        public static void MeanValueWeights(Vector2 p, IList<Vector2> polygon, float[] weights,
                                            Scratch scratch)
        {
            int n = polygon.Count;
            const float kEpsilon = 1e-5f;

            scratch.Resize(n);
            Vector2[] offsets = scratch.Offsets;
            float[] distances = scratch.Distances;
            float[] tanHalf = scratch.TanHalf;

            for (int i = 0; i < n; ++i) { weights[i] = 0f; }

            for (int i = 0; i < n; ++i)
            {
                offsets[i] = polygon[i] - p;
                distances[i] = offsets[i].magnitude;
                if (distances[i] < kEpsilon)
                {
                    weights[i] = 1f;
                    return;
                }
            }

            for (int i = 0; i < n; ++i)
            {
                int j = (i + 1) % n;
                float area = Cross(offsets[i], offsets[j]);
                float dot = Vector2.Dot(offsets[i], offsets[j]);
                if (Mathf.Abs(area) <= kEpsilon * distances[i] * distances[j] && dot < 0f)
                {
                    // p lies on the edge (i, j).
                    float t = distances[i] / (distances[i] + distances[j]);
                    weights[i] = 1f - t;
                    weights[j] = t;
                    return;
                }
                tanHalf[i] = Mathf.Abs(area) <= 0f
                    ? 0f
                    : (distances[i] * distances[j] - dot) / (2f * area);
            }

            double totalWeight = 0.0;
            for (int i = 0; i < n; ++i)
            {
                int prev = (i + n - 1) % n;
                double weight = (tanHalf[prev] + tanHalf[i]) / distances[i];
                weights[i] = (float)weight;
                totalWeight += weight;
            }

            if (Math.Abs(totalWeight) < 1e-12)
            {
                // Degenerate configuration; fall back to inverse distance weighting.
                totalWeight = 0.0;
                for (int i = 0; i < n; ++i)
                {
                    double weight = 1.0 / distances[i];
                    weights[i] = (float)weight;
                    totalWeight += weight;
                }
            }

            float inverse = (float)(1.0 / totalWeight);
            for (int i = 0; i < n; ++i) { weights[i] *= inverse; }
        }

        // -------------------------------------------------------------------------------
        // Normals
        // -------------------------------------------------------------------------------

        /// Area-weighted vertex normals. Vertices touched by no triangle, or by triangles
        /// that cancel out, fall back to the surface's plane normal.
        public static Vector3[] ComputeNormals(Vector3[] vertices, int[] triangles, Vector3 fallback)
        {
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Vector3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                Vector3 face = Vector3.Cross(b - a, c - a);
                normals[ia] += face;
                normals[ib] += face;
                normals[ic] += face;
            }
            for (int i = 0; i < normals.Length; ++i)
            {
                normals[i] = normals[i].sqrMagnitude > 0f ? normals[i].normalized : fallback;
            }
            return normals;
        }
    }
} // namespace TiltBrush
