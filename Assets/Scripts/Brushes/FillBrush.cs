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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{

    /// Fills the closed loop traced by the stroke, the way a 2D drawing app fills a closed
    /// path. Structurally the sibling of <see cref="HullBrush"/>: the stroke's control
    /// points are input to a whole-shape solver rather than to a ribbon, so all geometry is
    /// rebuilt from scratch whenever the control points change and lives on a single knot.
    /// Where Hull Brush wraps the points in their convex hull, Fill Brush spans them with a
    /// surface that follows the path's concavities. See <see cref="PathFill"/> for the
    /// algorithm and its limits.
    ///
    /// The stroke is treated as implicitly closed: the fill always spans the loop from the
    /// last control point back to the first, so the shape resolves as you draw, exactly as
    /// the hull does.
    ///
    /// TODO:
    /// - Rebuilding is O(output vertices * boundary points) and runs on every control point
    ///   change. The caps below keep it bounded; incremental update would be better.
    /// - Interior control points contribute nothing to the fill but are still stored.
    /// - Strongly non-planar loops are served badly; see the future work documented on
    ///   PathFill.
    public class FillBrush : GeometryBrush
    {
        /// Which regions count as inside when the loop crosses itself.
        [SerializeField] private PathFillRule m_FillRule;

        /// Boundary simplification tolerance, as a fraction of the loop's bounding box
        /// diagonal. Larger is simpler and cheaper.
        [SerializeField] private float m_SimplifyTolerance;

        /// Cap on boundary points after simplification. Drives the cost of every stage.
        [SerializeField] private int m_MaxBoundaryPoints;

        /// Cap on generated vertices. Keep this comfortably below the soft vertex limit that
        /// GeometryBrush uses to end a stroke (9000), remembering that double-sided
        /// descriptors double the count.
        [SerializeField] private int m_MaxVertices;

        /// How finely the fill is subdivided to carry the lift and shading normals.
        [SerializeField] private int m_MaxRefinementPasses;

        /// Stroke length limit, in control points.
        [SerializeField] private int m_MaxKnots;

        /// If set, a stroke that winds around an axis more than once is surfaced between
        /// its turns rather than flattened onto a plane and filled. A spiral has no interior
        /// to fill, so this is what makes one come out as a cone or a shell instead of a
        /// disc on an arbitrary plane.
        [SerializeField] private bool m_SpiralLoft;

        /// If set, each triangle carries its own face normal for flat faceted shading,
        /// mirroring HullBrush's m_Faceted. Costs one vertex per index, so keep MaxVertices
        /// in mind when turning it on.
        [SerializeField] private bool m_Faceted;

        /// If set, per-control-point colours are interpolated across the fill instead of
        /// every vertex taking the current brush colour.
        [SerializeField] private bool m_ColorFromControlPoints;

        /// If set, warn when a fill comes back malformed or too large to store. Logs only
        /// when the reported numbers change, so it stays quiet unless something is wrong.
        [SerializeField] private bool m_LogAnomalies;

        private int m_LastLoggedDropped;
        private int m_LastLoggedRepaired;
        private int m_LastLoggedClamped;
        private int m_LastLoggedOversize;

        /// Reused across rebuilds to keep per-frame allocation down.
        private List<Vector3> m_PathPositions;
        private List<Color32> m_PathColors;

        public FillBrush()
            : base(bCanBatch: true,
                upperBoundVertsPerKnot: 1,
                bDoubleSided: false)
        {
            m_PathPositions = new List<Vector3>();
            m_PathColors = new List<Color32>();
        }

        //
        // GeometryBrush API
        //

        protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf)
        {
            base.InitBrush(desc, localPointerXf);
            SetDoubleSided(desc);
            m_geometry.Layout = GetVertexLayout(desc);
        }

        public override float GetSpawnInterval(float pressure01)
        {
            return m_Desc.m_SolidMinLengthMeters_PS * POINTER_TO_LOCAL * App.METERS_TO_UNITS;
        }

        public override bool ShouldCurrentLineEnd()
        {
            // Reminder: it's ok for this method to be nondeterministic.
            return m_knots.Count > MaxKnots || base.ShouldCurrentLineEnd();
        }

        protected override void ControlPointsChanged(int iKnot0)
        {
            OnChanged_FrameKnots(iKnot0);
            OnChanged_MakeGeometry();
        }

        public override void ResetBrushForPreview(TrTransform localPointerXf)
        {
            base.ResetBrushForPreview(localPointerXf);
            OnChanged_MakeGeometry();
        }

        public override GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc)
        {
            return new GeometryPool.VertexLayout
            {
                bUseColors = true,
                bUseNormals = true,
                bUseTangents = false,
                uv0Size = 3,
                uv0Semantic = GeometryPool.Semantic.XyIsUvZIsDistance,
            };
        }

        //
        // Geometry generation
        //

        private int MaxKnots { get { return m_MaxKnots > 0 ? m_MaxKnots : 2048; } }

        private PathFill.Options MakeFillOptions()
        {
            PathFill.Options options = PathFill.Options.Default;
            options.Rule = m_FillRule;
            options.Faceted = m_Faceted;
            options.SpiralLoft = m_SpiralLoft;
            options.Diagnostics = m_LogAnomalies;
            if (m_SimplifyTolerance > 0f) { options.SimplifyTolerance = m_SimplifyTolerance; }
            if (m_MaxBoundaryPoints > 0) { options.MaxBoundaryPoints = m_MaxBoundaryPoints; }
            if (m_MaxVertices > 0) { options.MaxVertices = m_MaxVertices; }

            // Overrunning GeometryBrush's soft vertex limit ends the stroke and starts a new
            // one. For a ribbon that is invisible; for a fill it means half a shape and then
            // a second fill on top. Keep the budget inside the limit whatever the prefab
            // says, remembering that a double-sided descriptor doubles every vertex and that
            // faceting turns each index into a vertex.
            int ceiling = Mathf.Max(64, (m_SoftVertexLimit * 4) / (5 * Mathf.Max(1, NS)));
            options.MaxVertices = Mathf.Min(options.MaxVertices, ceiling);
            if (m_MaxRefinementPasses > 0) { options.MaxRefinementPasses = m_MaxRefinementPasses; }
            return options;
        }

        private void OnChanged_FrameKnots(int iKnot0)
        {
            Knot prev = m_knots[iKnot0 - 1];
            for (int iKnot = iKnot0; iKnot < m_knots.Count; ++iKnot)
            {
                Knot cur = m_knots[iKnot];
                Vector3 vMove = cur.point.m_Pos - prev.point.m_Pos;
                ComputeSurfaceFrameNew(prev.nRight, vMove.normalized, cur.point.m_Orient,
                    out cur.nRight, out cur.nSurface);

                m_knots[iKnot] = cur;
                prev = cur;
            }
        }

        /// Rebuilds the whole fill. Like HullBrush, all geometry hangs off knot 1: the shape
        /// is a property of the stroke as a whole, not of any span between control points.
        private void OnChanged_MakeGeometry()
        {
            if (m_geometry == null) { return; }
            Knot knot = m_knots[1];

            // Clear geometry because we recreate from scratch
            knot.iVert = 0;
            knot.nVert = 0;
            knot.iTri = 0;
            knot.nTri = 0;
            m_geometry.m_Vertices.SetCount(0);
            m_geometry.m_Normals.SetCount(0);
            m_geometry.m_Colors.SetCount(0);
            m_geometry.m_Texcoord0.v3.SetCount(0);
            m_geometry.m_Tris.SetCount(0);

            m_PathPositions.Clear();
            m_PathColors.Clear();
            for (int i = 0; i < m_knots.Count; ++i)
            {
                m_PathPositions.Add(m_knots[i].point.m_Pos);
                m_PathColors.Add(m_knots[i].color);
            }

            PathFill.Options options = MakeFillOptions();
            if (m_ColorFromControlPoints) { options.PathColors = m_PathColors; }

            UnityEngine.Profiling.Profiler.BeginSample("Fill Path");
            PathFill.Result fill = PathFill.Fill(m_PathPositions, options);
            UnityEngine.Profiling.Profiler.EndSample();

            // A fill needs at least three non-collinear points, so early in a stroke -- and
            // for a perfectly straight one -- there is simply nothing to draw yet.
            if (fill != null)
            {
                UnityEngine.Profiling.Profiler.BeginSample("Create Geometry");
                CreateGeometry(ref knot, fill);
                UnityEngine.Profiling.Profiler.EndSample();
            }

            m_knots[1] = knot;
        }

        private void CreateGeometry(ref Knot knot, PathFill.Result fill)
        {
            // Knot.nVert and Knot.nTri are 16-bit. Overflowing them does not throw; it wraps,
            // leaving the knot describing a range that has nothing to do with the geometry
            // actually in the pool, which draws as stray triangles stitched between unrelated
            // vertices. PathFill caps its own output, but the brush must not rely on that
            // when the cap is configurable from the prefab.
            int vertsNeeded = fill.Vertices.Length * NS;
            int trisNeeded = (fill.Triangles.Length / 3) * NS;
            if (vertsNeeded > ushort.MaxValue || trisNeeded > ushort.MaxValue)
            {
                if (m_LogAnomalies && m_LastLoggedOversize != vertsNeeded)
                {
                    m_LastLoggedOversize = vertsNeeded;
                    Debug.LogWarning(
                        $"FillBrush: fill needs {vertsNeeded} verts / {trisNeeded} tris, which " +
                        $"does not fit a knot's 16-bit counts. Lower MaxVertices on the brush " +
                        $"prefab. Skipping this rebuild.");
                }
                return;
            }

            if (m_LogAnomalies && fill.HasAnomalies &&
                (fill.DroppedTriangles != m_LastLoggedDropped ||
                 fill.RepairedVertices != m_LastLoggedRepaired ||
                 fill.ClampedVertices != m_LastLoggedClamped))
            {
                m_LastLoggedDropped = fill.DroppedTriangles;
                m_LastLoggedRepaired = fill.RepairedVertices;
                m_LastLoggedClamped = fill.ClampedVertices;
                Debug.LogWarning(
                    $"FillBrush: {fill.DroppedTriangles} triangles dropped, " +
                    $"{fill.RepairedVertices} vertices repaired, " +
                    $"{fill.ClampedVertices}/{fill.Vertices.Length} vertices clamped to the " +
                    $"boundary's range, {fill.ProjectedSelfIntersections} projected " +
                    $"self-intersections, flatness {fill.Flatness:F3}, " +
                    $"boundary {fill.Boundary.Length}, mode {fill.Mode}, " +
                    $"turns {fill.Turns:F2}. A self-overlapping or strongly non-planar path " +
                    $"has no well-defined fill; the result is bounded but arbitrary.");
            }

            Color32 fallbackColor = m_knots[m_knots.Count - 1].color;
            for (int i = 0; i < fill.Vertices.Length; ++i)
            {
                Color32 color = fill.Colors != null ? fill.Colors[i] : fallbackColor;
                AppendVert(ref knot, fill.Vertices[i], fill.Normals[i], fill.Uvs[i], color);
            }
            for (int i = 0; i < fill.Triangles.Length; i += 3)
            {
                AppendTri(ref knot, fill.Triangles[i], fill.Triangles[i + 1], fill.Triangles[i + 2]);
            }
        }

        private void AppendVert(ref Knot k, Vector3 v, Vector3 n, Vector2 uv, Color32 color)
        {
            Debug.Assert(k.iVert + k.nVert == m_geometry.m_Vertices.Count);
            // Matches HullBrush's use of the third channel; xy is the fill's own planar
            // parameterization rather than a distance along a ribbon.
            Vector3 uvw = new Vector3(uv.x, uv.y, m_BaseSize_PS);
            m_geometry.m_Vertices.Add(v);
            m_geometry.m_Normals.Add(n);
            m_geometry.m_Colors.Add(color);
            m_geometry.m_Texcoord0.v3.Add(uvw);
            k.nVert += 1;
            if (m_bDoubleSided)
            {
                m_geometry.m_Vertices.Add(v);
                m_geometry.m_Normals.Add(-n);
                m_geometry.m_Colors.Add(color);
                m_geometry.m_Texcoord0.v3.Add(uvw);
                k.nVert += 1;
            }
        }

        /// vp{0,1,2} are indices of vertex pairs
        private void AppendTri(ref Knot k, int vp0, int vp1, int vp2)
        {
            Debug.Assert((k.iTri + k.nTri) * 3 == m_geometry.m_Tris.Count);
            m_geometry.m_Tris.Add(vp0 * NS);
            m_geometry.m_Tris.Add(vp1 * NS);
            m_geometry.m_Tris.Add(vp2 * NS);
            k.nTri += 1;
            if (m_bDoubleSided)
            {
                m_geometry.m_Tris.Add(vp0 * NS + 1);
                m_geometry.m_Tris.Add(vp2 * NS + 1);
                m_geometry.m_Tris.Add(vp1 * NS + 1);
                k.nTri += 1;
            }
        }
    }

} // namespace TiltBrush
