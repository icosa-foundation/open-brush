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

    /// Spans a closed stroke with a relaxed membrane. Geometry is rebuilt from the whole
    /// path whenever its control points change.
    public class MembraneBrush : GeometryBrush
    {
        [SerializeField, Range(1, 8)] private int m_MembraneFinalBoundarySubdivisions = 4;

        /// Minimum visible membrane width, as a multiple of the brush's size.
        [SerializeField] private float m_SimplifyTolerance;

        /// Cap on generated vertices. Keep this comfortably below the soft vertex limit that
        /// GeometryBrush uses to end a stroke (9000), remembering that double-sided
        /// descriptors double the count.
        [SerializeField] private int m_MaxVertices;

        /// If set, each triangle carries its own face normal for flat faceted shading,
        /// mirroring HullBrush's m_Faceted. Costs one vertex per index, so keep MaxVertices
        /// in mind when turning it on.
        [SerializeField] private bool m_Faceted;

        /// If set, per-control-point colours are interpolated across the fill instead of
        /// every vertex taking the current brush colour.
        [SerializeField] private bool m_ColorFromControlPoints;

        [SerializeField, Range(0f, 0.5f)] private float m_OutlineWidth;

        /// Reused across rebuilds to keep per-frame allocation down.
        private List<Vector3> m_PathPositions;
        private List<Color32> m_PathColors;
        private readonly List<Quaternion> m_OutlineFrames = new List<Quaternion>();

        private readonly MembraneFill.Workspace m_MembraneWorkspace = new MembraneFill.Workspace();

        public MembraneBrush()
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
            m_MembraneWorkspace.WarmUp(MakeFillOptions());
            m_geometry.Layout = GetVertexLayout(desc);
        }

        public override float GetSpawnInterval(float pressure01)
        {
            return m_Desc.m_SolidMinLengthMeters_PS * POINTER_TO_LOCAL * App.METERS_TO_UNITS;
        }

        protected override void ControlPointsChanged(int iKnot0)
        {
            OnChanged_FrameKnots(iKnot0);
            OnChanged_MakeGeometry();
        }

        public override BatchSubset FinalizeBatchedBrush()
        {
            FinalizeMembraneGeometry();
            return base.FinalizeBatchedBrush();
        }

        public override void FinalizeSolitaryBrush()
        {
            FinalizeMembraneGeometry();
            base.FinalizeSolitaryBrush();
        }

        private void FinalizeMembraneGeometry()
        {
            if (m_geometry == null) { return; }
            // Both live drawing and reconstruction from saved control points finish here.
            // Read the knots directly, including any final pending position/colour update.
            OnChanged_MakeGeometry(finalQuality: true);
            m_FirstChangedControlPoint = null;
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

        private MembraneFill.Options MakeFillOptions()
        {
            MembraneFill.Options options = MembraneFill.Options.Default;
            options.Faceted = m_Faceted;
            if (m_SimplifyTolerance > 0f)
            {
                options.SimplifyToleranceAbsolute =
                    m_SimplifyTolerance * m_BaseSize_PS * POINTER_TO_LOCAL;
            }
            if (m_MaxVertices > 0) { options.MaxVertices = m_MaxVertices; }

            // Overrunning GeometryBrush's soft vertex limit ends the stroke and starts a new
            // one. For a ribbon that is invisible; for a fill it means half a shape and then
            // a second fill on top. Keep the budget inside the limit whatever the prefab
            // says, remembering that a double-sided descriptor doubles every vertex and that
            // faceting turns each index into a vertex.
            int ceiling = Mathf.Max(64, (m_SoftVertexLimit * 4) / (5 * Mathf.Max(1, NS)));
            if (m_OutlineWidth > 0f && m_Faceted)
            {
                ceiling = Mathf.Max(64, ceiling - 8 * (4 * 24 * 4 + 1));
            }
            options.MaxVertices = Mathf.Min(options.MaxVertices, ceiling);
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
        private void OnChanged_MakeGeometry(bool finalQuality = false)
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
            // The idle pointer preview is a short, open, constantly changing trail.
            // Closing it into a membrane creates tiny folded surfaces with unstable
            // lighting. A real stroke (and a complete Tool Script preview) is not in
            // preview mode and still generates its full membrane.
            if (m_PreviewMode)
            {
                m_knots[1] = knot;
                return;
            }

            for (int i = 0; i < m_knots.Count; ++i)
            {
                m_PathPositions.Add(m_knots[i].point.m_Pos);
                m_PathColors.Add(m_knots[i].color);
            }

            // The first non-collinear live samples can form a millimetre-scale
            // membrane whose lighting produces a single bright startup flash.
            // Wait for a visible footprint, but still allow tiny finished strokes.
            if (!finalQuality && !HasLivePreviewFootprint())
            {
                m_knots[1] = knot;
                return;
            }

            MembraneFill.Options options = MakeFillOptions();
            if (m_ColorFromControlPoints)
            {
                options.PathColors = m_PathColors;
            }

            UnityEngine.Profiling.Profiler.BeginSample("Fill Path");
            MembraneFill.Result fill = finalQuality
                ? MembraneFill.FillFinal(m_PathPositions, options, m_MembraneWorkspace,
                    m_OutlineWidth > 0f
                        ? Mathf.Min(m_MembraneFinalBoundarySubdivisions, 4)
                        : m_MembraneFinalBoundarySubdivisions)
                : MembraneFill.Fill(m_PathPositions, options, m_MembraneWorkspace);
            UnityEngine.Profiling.Profiler.EndSample();

            // A fill needs at least three non-collinear points, so early in a stroke -- and
            // for a perfectly straight one -- there is simply nothing to draw yet.
            if (fill != null)
            {
                UnityEngine.Profiling.Profiler.BeginSample("Create Geometry");
                CreateGeometry(ref knot, fill);
                if (m_OutlineWidth > 0f) { CreateOutline(ref knot, fill); }
                UnityEngine.Profiling.Profiler.EndSample();
            }

            m_knots[1] = knot;
        }

        private bool HasLivePreviewFootprint()
        {
            // The final knot may be a duplicate of the current pointer position.
            if (m_PathPositions.Count < 5) { return false; }

            Vector3 min = m_PathPositions[0];
            Vector3 max = min;
            for (int i = 1; i < m_PathPositions.Count; ++i)
            {
                min = Vector3.Min(min, m_PathPositions[i]);
                max = Vector3.Max(max, m_PathPositions[i]);
            }

            float minimumSpan = Mathf.Max(5f * GetSpawnInterval(1f),
                0.1f * m_BaseSize_PS * POINTER_TO_LOCAL);
            return (max - min).sqrMagnitude >= minimumSpan * minimumSpan;
        }

        private void CreateGeometry(ref Knot knot, MembraneFill.Result fill)
        {
            // Knot.nVert and Knot.nTri are 16-bit. Overflowing them does not throw; it wraps,
            // leaving the knot describing a range that has nothing to do with the geometry
            // actually in the pool, which draws as stray triangles stitched between unrelated
            // vertices. MembraneFill caps its own output, but the brush must not rely on that
            // when the cap is configurable from the prefab.
            int vertsNeeded = fill.Vertices.Length * NS;
            int trisNeeded = (fill.Triangles.Length / 3) * NS;
            if (vertsNeeded > ushort.MaxValue || trisNeeded > ushort.MaxValue)
            {
                return;
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

        private void CreateOutline(ref Knot knot, MembraneFill.Result fill)
        {
            Vector3[] boundary = fill.Boundary;
            int count = boundary.Length;
            if (count < 3 || knot.nVert + (count + 1) * 8 * NS > ushort.MaxValue
                || knot.nTri + count * 8 * NS > ushort.MaxValue)
            {
                return;
            }

            m_OutlineFrames.Clear();
            Quaternion? previousFrame = null;
            Quaternion orientation = m_knots[1].point.m_Orient;
            for (int i = 0; i < count; ++i)
            {
                Vector3 tangent = boundary[(i + 1) % count] - boundary[(i + count - 1) % count];
                if (tangent.sqrMagnitude < 1e-12f)
                {
                    tangent = boundary[(i + 1) % count] - boundary[i];
                    if (tangent.sqrMagnitude < 1e-12f)
                    {
                        tangent = boundary[i] - boundary[(i + count - 1) % count];
                    }
                    if (tangent.sqrMagnitude < 1e-12f && previousFrame.HasValue)
                    {
                        tangent = previousFrame.Value * Vector3.forward;
                    }
                    if (tangent.sqrMagnitude < 1e-12f) { return; }
                }
                previousFrame = MathUtils.ComputeMinimalRotationFrame(
                    tangent.normalized, previousFrame, orientation);
                m_OutlineFrames.Add(previousFrame.Value);
            }

            Vector3 firstTangent = m_OutlineFrames[0] * Vector3.forward;
            Quaternion closedFrame = MathUtils.ComputeMinimalRotationFrame(
                firstTangent, previousFrame, orientation);
            float closureAngle = Vector3.SignedAngle(
                closedFrame * Vector3.up, m_OutlineFrames[0] * Vector3.up, firstTangent);
            int firstVertex = knot.nVert / NS;
            float radius = m_OutlineWidth * m_BaseSize_PS * POINTER_TO_LOCAL;
            double[] lengths = m_MembraneWorkspace.Lengths;
            double perimeter = lengths[m_PathPositions.Count];
            int segment = 0;
            for (int i = 0; i <= count; ++i)
            {
                int sample = i % count;
                Quaternion sampleFrame = i == count ? closedFrame : m_OutlineFrames[sample];
                Quaternion frame = Quaternion.AngleAxis(closureAngle * i / count,
                    sampleFrame * Vector3.forward) * sampleFrame;
                Vector3 up = frame * Vector3.up;
                Vector3 right = frame * Vector3.right;
                Color32 color = m_knots[m_knots.Count - 1].color;
                if (m_ColorFromControlPoints)
                {
                    double distance = perimeter * i / count;
                    while (segment < m_PathPositions.Count - 1 && lengths[segment + 1] <= distance)
                    {
                        ++segment;
                    }
                    double segmentLength = lengths[segment + 1] - lengths[segment];
                    float blend = segmentLength > 0
                        ? (float)((distance - lengths[segment]) / segmentLength) : 0f;
                    color = Color.Lerp(m_PathColors[segment],
                        m_PathColors[(segment + 1) % m_PathColors.Count], blend);
                }
                for (int corner = 0; corner < 4; ++corner)
                {
                    float angle = corner * Mathf.PI * 0.5f;
                    Vector3 direction = -Mathf.Cos(angle) * up - Mathf.Sin(angle) * right;
                    Vector3 previousNormal = -Mathf.Cos(angle - Mathf.PI * 0.25f) * up
                        - Mathf.Sin(angle - Mathf.PI * 0.25f) * right;
                    Vector3 nextNormal = -Mathf.Cos(angle + Mathf.PI * 0.25f) * up
                        - Mathf.Sin(angle + Mathf.PI * 0.25f) * right;
                    Vector3 vertex = boundary[sample] + radius * direction;
                    AppendVert(ref knot, vertex, previousNormal,
                        new Vector2((float)i / count, corner == 0 ? 1f : (float)corner / 4), color);
                    AppendVert(ref knot, vertex, nextNormal,
                        new Vector2((float)i / count, (float)corner / 4), color);
                }
            }
            for (int i = 0; i < count; ++i)
            {
                int ring = firstVertex + i * 8;
                int nextRing = firstVertex + (i + 1) * 8;
                for (int side = 0; side < 4; ++side)
                {
                    int start = side * 2 + 1;
                    int end = ((side + 1) % 4) * 2;
                    AppendTri(ref knot, ring + start, nextRing + start, nextRing + end);
                    AppendTri(ref knot, ring + start, nextRing + end, ring + end);
                }
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
