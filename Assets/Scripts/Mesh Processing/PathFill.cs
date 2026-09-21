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

    /// How to resolve regions when the projected outline overlaps itself.
    /// Matches the SVG / Illustrator fill rules of the same name.
    [Serializable]
    public enum PathFillRule
    {
        NonZero,
        OddEven,
    }

    /// Triangulates a closed 2D outline into a vertex/index buffer.
    /// Returns false if the outline could not be triangulated.
    /// The implementation may add vertices (at self-intersections, for example),
    /// so the output vertices are not required to correspond to the input ones.
    public delegate bool PathFillTessellateFn(
        IList<Vector2> outline,
        PathFillRule rule,
        List<Vector2> outVertices,
        List<int> outTriangles);

    /// Builds a surface that spans a closed 3D path -- the 3D analogue of filling a
    /// closed path in a 2D drawing app. <see cref="FillBrush"/> is the brush built on it.
    ///
    /// <remarks>
    /// The exact analogue of a 2D fill is the minimal surface spanning the loop (the
    /// Plateau problem, i.e. the soap film). Everything practical approximates it. This
    /// class implements the "near-planar" approximation:
    ///
    ///   1. Prepare:     weld duplicates, drop the closing point, simplify (Ramer-Douglas-Peucker).
    ///   2. Fit a plane: PCA of the boundary points, oriented to agree with the Newell normal.
    ///   3. Project:     boundary -> 2D in the plane's basis; keep each point's signed
    ///                   out-of-plane residual.
    ///   4. Tessellate:  triangulate the 2D outline under a fill rule. Because this happens
    ///                   in 2D we inherit exactly the 2D fill semantics -- non-zero vs
    ///                   even-odd winding, self-intersecting outlines, figure-eights.
    ///   5. Refine:      uniform 1->4 subdivision until interior triangles are roughly the
    ///                   size of the boundary sampling, so the lift in step 6 has somewhere
    ///                   to go and shading normals are usable.
    ///   6. Lift:        displace every vertex off the plane by the residual field,
    ///                   interpolated from the boundary with mean value coordinates.
    ///
    /// The projection is a parameterization device, not a flattening. Boundary vertices
    /// return to their exact input positions (mean value coordinates interpolate the
    /// boundary data), and interior vertices follow a smooth harmonic-like interpolation of
    /// the boundary's out-of-plane offsets. A planar loop has zero residuals everywhere, so
    /// the result is exactly the 2D fill lying in its own plane.
    ///
    /// Known limits of this approach:
    ///   - The surface is a height field over the best-fit plane, so it cannot fold back on
    ///     itself or overhang.
    ///   - If the outline self-overlaps when projected, the fill rule invents topology that
    ///     has nothing to do with the 3D curve. <see cref="Result.ProjectedSelfIntersections"/>
    ///     reports this so a caller can warn or take another route.
    ///   - Strongly non-planar loops (a curve wrapped around a cylinder, a trefoil) are
    ///     served badly by both of the above. See the "Future work" remarks below for the
    ///     minimal-surface pipeline that handles them.
    ///
    /// Determinism: strokes persist as control points and their geometry is rebuilt on load
    /// (see Stroke.Recreate), so a fill derived from stroke knots must be reproducible from
    /// those knots alone. Every stage here is deterministic: fixed iteration counts, no
    /// dependence on hash ordering, no time- or frame-dependent input. Keep it that way.
    ///
    ///
    /// FUTURE WORK: filling strongly non-planar paths ("3b")
    /// ----------------------------------------------------
    /// When <see cref="Result.Flatness"/> is large, a height field over a best-fit plane is
    /// the wrong model. The standard alternative is the mesh hole-filling pipeline
    /// (Barequet and Sharir 1995; Liepa, "Filling Holes in Meshes", 2003), which
    /// approximates the minimal surface directly and never needs a projection plane:
    ///
    ///   1. Minimum-weight triangulation of the closed polygon, by the classic O(n^3)
    ///      dynamic program over the cycle. Weight each candidate triangle lexicographically
    ///      by (maximum dihedral angle with its neighbours, area). Area alone is degenerate:
    ///      every triangulation of a simple planar polygon has identical total area -- which
    ///      is also why this pipeline reduces to the correct answer for planar input, with
    ///      the dihedral term breaking the tie. n here is the simplified boundary, so
    ///      MaxBoundaryPoints already bounds the cubic term.
    ///   2. Liepa refinement: repeatedly split triangles whose circumradius-to-edge ratio is
    ///      poor, inserting interior vertices until edge lengths match the boundary
    ///      sampling density. Without this the result is all slivers and shades badly.
    ///   3. Fairing: solve the cotangent Laplacian with the boundary pinned. Membrane
    ///      energy (Laplacian) gives a discrete minimal surface; thin-plate energy
    ///      (bi-Laplacian) gives a smoother, less pinched surface at more cost.
    ///
    /// What it buys: folds and overhangs are representable, there is no planarity threshold
    /// to tune, and planar input still reduces to the 2D fill.
    ///
    /// What it costs: the dynamic program assumes a *simple* cycle, so self-intersecting
    /// outlines have to be split into simple sub-loops first (or rejected); holes and
    /// compound paths, which the 2D fill rules give away for free, are not supported at all;
    /// and the triangulation is not guaranteed to be embedded -- a sufficiently wild curve
    /// can produce a surface that passes through itself.
    ///
    /// Where it plugs in: replace steps 3-6 above. Everything before (Prepare, simplify) and
    /// the <see cref="Result"/> contract are unchanged, so the branch belongs right after
    /// the plane fit in <see cref="Fill"/>, selected on <see cref="Result.Flatness"/> or an
    /// explicit option. The plane fit stays useful either way: it supplies the surface
    /// orientation and the planar UV parameterization.
    /// </remarks>
    public static class PathFill
    {
        /// The 2D outline is scaled to roughly this extent before being handed to the
        /// tessellator, and scaled back afterwards. Open Brush canvas units vary over many
        /// orders of magnitude; normalizing keeps the tessellator's absolute epsilons (and
        /// our own, in the mean value coordinate code) in a range they were tuned for.
        private const float kTessellationExtent = 100f;

        /// Points closer together than this fraction of the path's bounding box diagonal are
        /// welded before anything else runs.
        private const float kWeldFraction = 1e-5f;

        /// How many times the boundary is coarsened when the raw tessellation overruns the
        /// vertex budget.
        private const int kMaxCoarseningAttempts = 3;

        public struct Options
        {
            /// Winding rule used to decide which regions of a self-overlapping outline are
            /// inside. Ignored for outlines that do not overlap.
            public PathFillRule Rule;

            /// Boundary simplification tolerance, as a fraction of the path's bounding box
            /// diagonal. Zero or less selects the default.
            public float SimplifyTolerance;

            /// Hard cap on boundary points after simplification. The tolerance is doubled
            /// until the boundary fits.
            public int MaxBoundaryPoints;

            /// Approximate cap on output vertex count. Refinement stops early to respect it.
            public int MaxVertices;

            /// How many times the tessellated patch may be subdivided 1->4 while chasing
            /// an interior edge length comparable to the boundary sampling. More passes
            /// means a denser, smoother fill, bounded by MaxVertices.
            public int MaxRefinementPasses;

            /// When set, each triangle gets its own vertices carrying the face normal, for
            /// flat faceted shading rather than a smoothly interpolated surface. This makes
            /// the output vertex count equal the index count, which MaxVertices accounts for.
            public bool Faceted;

            /// When false, the fill is left flat on the best-fit plane (step 6 is skipped).
            /// Only useful for debugging and for callers that want a planar patch.
            public bool SkipLift;

            /// Optional per-path-point colours, parallel to the path passed to
            /// <see cref="Fill"/>. When supplied, <see cref="Result.Colors"/> is produced by
            /// interpolating them across the surface with the same mean value weights that
            /// drive the lift, so a multi-coloured outline bleeds into its fill.
            public IList<Color32> PathColors;

            /// Triangulator for step 4. Null selects the Unity Vector Graphics tessellator
            /// (<see cref="PathFillTessellator"/>), which implements both fill rules.
            public PathFillTessellateFn Tessellator;

            public static Options Default
            {
                get
                {
                    return new Options
                    {
                        Rule = PathFillRule.NonZero,
                        SimplifyTolerance = 0.002f,
                        MaxBoundaryPoints = 250,
                        MaxVertices = 20000,
                        MaxRefinementPasses = 4,
                        SkipLift = false,
                        Faceted = false,
                        PathColors = null,
                        Tessellator = null,
                    };
                }
            }
        }

        public class Result
        {
            public Vector3[] Vertices;
            public int[] Triangles;
            public Vector3[] Normals;
            public Vector2[] Uvs;

            /// Per-vertex colours, or null if Options.PathColors was not supplied.
            public Color32[] Colors;

            /// The simplified boundary, in input order, without a repeated closing point.
            /// These points lie exactly on the fill's edge.
            public Vector3[] Boundary;

            /// Indices into the path passed to <see cref="Fill"/>, one per boundary point,
            /// for callers that need to carry their own per-point data across the
            /// weld-and-simplify stage.
            public int[] BoundaryIndices;

            /// Best-fit plane. The normal is the surface orientation; triangles are wound
            /// counter-clockwise when viewed from the side the normal points to.
            public Vector3 PlaneOrigin;
            public Vector3 PlaneNormal;

            /// RMS out-of-plane deviation of the boundary, as a fraction of its bounding box
            /// diagonal. 0 is exactly planar. Above roughly 0.1 the height-field assumption
            /// is getting thin and the "3b" pipeline described on <see cref="PathFill"/>
            /// would serve the path better.
            public float Flatness;

            /// Number of crossing pairs in the projected outline. Non-zero means the fill
            /// rule decided something the 3D curve did not.
            public int ProjectedSelfIntersections;

            /// Triangles discarded because the tessellator produced indices or corners that
            /// could not be used. Non-zero means something upstream misbehaved.
            public int DroppedTriangles;

            /// Vertices that came out non-finite and were dropped back onto the plane.
            public int RepairedVertices;

            /// Vertices whose lift was held back to the range the boundary itself spans.
            /// A large count means the outline overlaps itself badly enough that the
            /// interpolation is no longer meaningful, even though the result is now bounded.
            public int ClampedVertices;

            /// True if this fill hit anything that should not happen. Clamping counts: on a
            /// well-conditioned outline the interpolation never leaves the boundary's range,
            /// so any clamping at all says the outline is overlapping itself badly enough
            /// that the result, though bounded, is no longer a meaningful spanning surface.
            public bool HasAnomalies
            {
                get { return DroppedTriangles > 0 || RepairedVertices > 0 || ClampedVertices > 0; }
            }
        }

        /// Builds a fill surface for a closed path. The path may be given closed (last point
        /// equal to first) or open, in which case it is treated as implicitly closed.
        /// Returns null if the path cannot be filled: fewer than three distinct points, all
        /// points collinear, or the tessellator failing.
        public static Result Fill(IList<Vector3> path, Options options)
        {
            if (path == null || path.Count < 3) { return null; }

            float diagonal = PathFillGeometry.BoundsDiagonal(path);
            if (diagonal <= 0f) { return null; }

            List<int> loopIndices = PathFillGeometry.WeldAndOpen(path, diagonal * kWeldFraction);
            if (loopIndices.Count < 3) { return null; }

            float tolerance = (options.SimplifyTolerance > 0f ? options.SimplifyTolerance : 0.002f) * diagonal;
            int maxBoundary = Mathf.Max(3, options.MaxBoundaryPoints > 0 ? options.MaxBoundaryPoints : 250);
            loopIndices = PathFillGeometry.SimplifyClosed(path, loopIndices, tolerance, maxBoundary);
            if (loopIndices.Count < 3) { return null; }

            int boundaryCount = loopIndices.Count;
            var loop = new List<Vector3>(boundaryCount);
            for (int i = 0; i < boundaryCount; ++i) { loop.Add(path[loopIndices[i]]); }

            Vector3 origin, normal;
            if (!PathFillGeometry.FitPlane(loop, out origin, out normal)) { return null; }

            Vector3 axisU, axisV;
            PathFillGeometry.BasisFromNormal(normal, out axisU, out axisV);

            // Project, keeping each boundary point's signed distance from the plane.
            var outline = new List<Vector2>(boundaryCount);
            var residuals = new float[boundaryCount];
            float sumSqResidual;
            Project(path, loopIndices, origin, normal, axisU, axisV,
                    loop, outline, ref residuals, out sumSqResidual);

            Color32[] boundaryColors = null;
            if (options.PathColors != null && options.PathColors.Count == path.Count)
            {
                boundaryColors = new Color32[boundaryCount];
                for (int i = 0; i < boundaryCount; ++i)
                {
                    boundaryColors[i] = options.PathColors[loopIndices[i]];
                }
            }

            // Normalize the 2D outline into a predictable numeric range for the tessellator.
            float extent = PathFillGeometry.MaxExtent(outline);
            if (extent <= 0f) { return null; }
            float scale = kTessellationExtent / extent;
            var scaledOutline = new List<Vector2>(boundaryCount);
            for (int i = 0; i < boundaryCount; ++i) { scaledOutline.Add(outline[i] * scale); }

            int maxVertices = options.MaxVertices > 0 ? options.MaxVertices : 20000;

            PathFillTessellateFn tessellate = options.Tessellator ?? PathFillTessellator.Tessellate;
            var verts2d = new List<Vector2>();
            var triangles = new List<int>();
            if (!tessellate(scaledOutline, options.Rule, verts2d, triangles)) { return null; }
            int droppedTriangles = PathFillGeometry.DropInvalidTriangles(verts2d, triangles);
            if (verts2d.Count < 3 || triangles.Count < 3) { return null; }

            // MaxVertices is a hard cap, not just a refinement budget. A self-crossing
            // outline makes the tessellator emit a vertex per intersection, so the raw
            // tessellation can blow far past the budget before refinement is considered --
            // and callers store geometry counts in 16-bit fields. Back off the boundary
            // detail and try again rather than handing back something they cannot hold.
            int coarseAttempts = 0;
            while (FinalVertexCount(verts2d.Count, triangles.Count, options.Faceted) > maxVertices &&
                   coarseAttempts < kMaxCoarseningAttempts)
            {
                ++coarseAttempts;
                int coarserCap = Mathf.Max(3, boundaryCount / 2);
                if (coarserCap >= boundaryCount) { break; }

                loopIndices = PathFillGeometry.SimplifyClosed(path, loopIndices, tolerance, coarserCap);
                if (loopIndices.Count < 3) { return null; }
                if (loopIndices.Count >= boundaryCount) { break; }

                boundaryCount = loopIndices.Count;
                Project(path, loopIndices, origin, normal, axisU, axisV,
                        loop, outline, ref residuals, out sumSqResidual);
                extent = PathFillGeometry.MaxExtent(outline);
                if (extent <= 0f) { return null; }
                scale = kTessellationExtent / extent;
                scaledOutline.Clear();
                for (int i = 0; i < boundaryCount; ++i) { scaledOutline.Add(outline[i] * scale); }

                if (boundaryColors != null)
                {
                    boundaryColors = new Color32[boundaryCount];
                    for (int i = 0; i < boundaryCount; ++i)
                    {
                        boundaryColors[i] = options.PathColors[loopIndices[i]];
                    }
                }

                verts2d.Clear();
                triangles.Clear();
                if (!tessellate(scaledOutline, options.Rule, verts2d, triangles)) { return null; }
                droppedTriangles += PathFillGeometry.DropInvalidTriangles(verts2d, triangles);
                if (verts2d.Count < 3 || triangles.Count < 3) { return null; }
            }

            // Still over budget after backing off: better no fill than corrupt geometry.
            if (FinalVertexCount(verts2d.Count, triangles.Count, options.Faceted) > maxVertices)
            {
                return null;
            }

            // Refine so the lift has interior vertices to act on.
            float meanEdge = PathFillGeometry.MeanEdgeLength(scaledOutline);
            int maxPasses = Mathf.Max(0, options.MaxRefinementPasses);
            PathFillGeometry.Subdivide(verts2d, triangles, meanEdge, maxVertices,
                                       options.Faceted ? maxVertices : int.MaxValue, maxPasses);

            PathFillGeometry.EnsureCounterClockwise(verts2d, triangles);

            // Lift back into 3D. Mean value coordinates are computed in the scaled frame
            // (they are scale invariant) and applied to the unscaled residuals. The same
            // weights carry the boundary colours inwards.
            int vertexCount = verts2d.Count;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            Color32[] colors = boundaryColors != null ? new Color32[vertexCount] : null;
            var weights = new float[boundaryCount];
            var scratch = new PathFillGeometry.Scratch(boundaryCount);
            int repairedVertices = 0;
            int clampedVertices = 0;

            // A surface spanning a loop cannot reach further off the plane than the loop
            // itself does: a harmonic interpolant attains its extremes on the boundary, and
            // that is the surface being approximated here. Enforcing it costs nothing when
            // the interpolation is well conditioned and is the difference between a bounded
            // surface and a shard when it is not.
            float minResidual = float.MaxValue;
            float maxResidual = -float.MaxValue;
            for (int i = 0; i < boundaryCount; ++i)
            {
                minResidual = Mathf.Min(minResidual, residuals[i]);
                maxResidual = Mathf.Max(maxResidual, residuals[i]);
            }
            Vector2 uvMin = PathFillGeometry.Min(verts2d);
            float uvExtent = PathFillGeometry.MaxExtent(verts2d);
            float uvScale = 1f / (uvExtent > 0f ? uvExtent : 1f);
            float invScale = 1f / scale;
            bool needWeights = !options.SkipLift || colors != null;
            for (int i = 0; i < vertexCount; ++i)
            {
                Vector2 p = verts2d[i];
                if (needWeights)
                {
                    PathFillGeometry.MeanValueWeights(p, scaledOutline, weights, scratch);
                }
                float height = 0f;
                if (!options.SkipLift)
                {
                    for (int j = 0; j < boundaryCount; ++j) { height += weights[j] * residuals[j]; }
                    if (height < minResidual || height > maxResidual || float.IsNaN(height))
                    {
                        height = float.IsNaN(height)
                            ? 0f
                            : Mathf.Clamp(height, minResidual, maxResidual);
                        ++clampedVertices;
                    }
                }
                if (colors != null)
                {
                    colors[i] = BlendColors(weights, boundaryColors);
                }
                Vector2 local = p * invScale;
                vertices[i] = origin + axisU * local.x + axisV * local.y + normal * height;
                uvs[i] = (p - uvMin) * uvScale;

                if (!PathFillGeometry.IsFinite(vertices[i]))
                {
                    // Should not happen, but a single non-finite vertex draws as a shard
                    // stretching off to infinity. Drop it back onto the plane instead.
                    vertices[i] = origin + axisU * local.x + axisV * local.y;
                    if (!PathFillGeometry.IsFinite(vertices[i])) { vertices[i] = origin; }
                    uvs[i] = Vector2.zero;
                    ++repairedVertices;
                }
            }

            Vector3[] normals = null;
            int[] triangleArray = triangles.ToArray();
            if (options.Faceted)
            {
                PathFillGeometry.Facet(ref vertices, ref uvs, ref colors, ref triangleArray,
                                       out normals, normal);
            }

            var result = new Result
            {
                Vertices = vertices,
                Triangles = triangleArray,
                Uvs = uvs,
                Colors = colors,
                Boundary = loop.ToArray(),
                BoundaryIndices = loopIndices.ToArray(),
                PlaneOrigin = origin,
                PlaneNormal = normal,
                Flatness = Mathf.Sqrt(sumSqResidual / boundaryCount) / diagonal,
                ProjectedSelfIntersections = PathFillGeometry.CountSelfIntersections(scaledOutline),
                DroppedTriangles = droppedTriangles,
                RepairedVertices = repairedVertices,
                ClampedVertices = clampedVertices,
            };
            result.Normals = normals ??
                PathFillGeometry.ComputeNormals(result.Vertices, result.Triangles, normal);
            return result;
        }

        public static Result Fill(IList<Vector3> path)
        {
            return Fill(path, Options.Default);
        }

        /// How many vertices the caller will end up holding. Faceting gives every triangle
        /// its own corners, so the count becomes the index count.
        private static int FinalVertexCount(int vertexCount, int indexCount, bool faceted)
        {
            return faceted ? indexCount : vertexCount;
        }

        /// Rebuilds the projected boundary for the given indices, against a fixed plane.
        /// Called again when the boundary is coarsened, which is why it reuses its buffers
        /// and why the plane is an input rather than being refitted -- refitting would move
        /// the surface under a caller that has already been told where it is.
        private static void Project(IList<Vector3> path, List<int> loopIndices,
                                    Vector3 origin, Vector3 normal, Vector3 axisU, Vector3 axisV,
                                    List<Vector3> loop, List<Vector2> outline,
                                    ref float[] residuals, out float sumSqResidual)
        {
            int count = loopIndices.Count;
            loop.Clear();
            outline.Clear();
            if (residuals.Length < count) { residuals = new float[count]; }

            sumSqResidual = 0f;
            for (int i = 0; i < count; ++i)
            {
                Vector3 p = path[loopIndices[i]];
                loop.Add(p);
                Vector3 d = p - origin;
                outline.Add(new Vector2(Vector3.Dot(d, axisU), Vector3.Dot(d, axisV)));
                float h = Vector3.Dot(d, normal);
                residuals[i] = h;
                sumSqResidual += h * h;
            }
        }

        /// Mean value weights can go negative where the outline is concave, so the blend is
        /// clamped rather than assumed to stay inside the boundary colours' range.
        private static Color32 BlendColors(float[] weights, Color32[] boundaryColors)
        {
            float r = 0f, g = 0f, b = 0f, a = 0f;
            for (int i = 0; i < weights.Length; ++i)
            {
                float w = weights[i];
                Color32 c = boundaryColors[i];
                r += w * c.r; g += w * c.g; b += w * c.b; a += w * c.a;
            }
            return new Color32(
                (byte)Mathf.Clamp(r, 0f, 255f),
                (byte)Mathf.Clamp(g, 0f, 255f),
                (byte)Mathf.Clamp(b, 0f, 255f),
                (byte)Mathf.Clamp(a, 0f, 255f));
        }

        /// Convenience wrapper that builds a Unity Mesh. Returns null if the path cannot be
        /// filled. The mesh is single-sided; render it with a double-sided material, since a
        /// fill has no meaningful back face to a user.
        public static Mesh BuildMesh(IList<Vector3> path, Options options)
        {
            Result result = Fill(path, options);
            if (result == null) { return null; }
            var mesh = new Mesh();
            if (result.Vertices.Length > 65000) { mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; }
            mesh.vertices = result.Vertices;
            mesh.triangles = result.Triangles;
            mesh.normals = result.Normals;
            mesh.uv = result.Uvs;
            if (result.Colors != null) { mesh.colors32 = result.Colors; }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
} // namespace TiltBrush
