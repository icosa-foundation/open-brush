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

using UnityEngine;

namespace TiltBrush
{

    /// Quad strip brush that fades opacity in across the stroke's first solid and out across its
    /// last one.
    ///
    /// Derives from QuadStripBrushStretchUV because that is the geometry the Dark brush used
    /// before this subclass existed, so swapping the script does not change the brush's UV
    /// behaviour. Nothing here depends on how UVs are laid out: only vertex alpha is touched.
    ///
    /// One solid is one step of the ribbon, so the fades span a fixed fraction of the stroke's
    /// width rather than a fraction of its length: a short stroke and a long one fade over the
    /// same distance. The fragment shader does the actual gradient by interpolating alpha between
    /// the solid's two edges, which is the same mechanism the material already uses for its
    /// width fade.
    ///
    /// The head ramp is written once, on the solid that is created first. The tail ramp moves:
    /// the solid that is currently last is ramped, and the one that was last before it is restored
    /// to full opacity. That bookkeeping is unavoidable because vertex data does not heal itself,
    /// and the base class will not do it either -- QuadStripBrushStretchUV never touches alpha.
    ///
    /// Width-wise fading is deliberately not handled here; the material does that
    /// (see Brush/Darken, _EdgeFadeoff).
    class LineWithFadeBrush : QuadStripBrushStretchUV
    {
        [Header("End Fades")]
        [Tooltip("Fade opacity in across the stroke's first solid")]
        [SerializeField] private bool m_FadeStart = true;
        [Tooltip("Fade opacity out across the stroke's final solid")]
        [SerializeField] private bool m_FadeEnd = true;

        // The solid that was the end of the stroke on the previous pass, and so is the one still
        // carrying the tail ramp. Everything from here up to the current end solid gets swept back
        // to full opacity. Reset when the segment changes, because a break makes it moot.
        private int m_RestoreFrom = -1;
        private int m_RestoreInitialQuad = -1;

        override protected void UpdateUVsForSegment(int iQuad0, int iQuad1, float size)
        {
            base.UpdateUVsForSegment(iQuad0, iQuad1, size);

            int quadsPerSolid = m_EnableBackfaces ? 2 : 1;
            int iSolid0 = Mathf.Max(0, m_InitialQuadIndex / quadsPerSolid);
            int iSolid1 = Mathf.Min(m_LeadingQuadIndex / quadsPerSolid, m_NumQuads / quadsPerSolid);
            if (iSolid1 <= iSolid0) { return; }

            if (m_RestoreInitialQuad != m_InitialQuadIndex)
            {
                m_RestoreInitialQuad = m_InitialQuadIndex;
                m_RestoreFrom = iSolid0;
            }

            var colors = m_Geometry.m_Colors;

            // Head: dim the outer edge of the first solid. It is never shared with another solid,
            // so this only has to be written once and never changes afterwards.
            if (m_FadeStart)
            {
                SetTrailingAlpha(colors, iSolid0, 0);
            }

            // Tail: the stroke's end is whichever solid was laid down most recently. Solids that
            // used to be the end and no longer are have to go back to full opacity, or they stay
            // dimmed for the rest of the stroke. The sweep runs from the previous end solid up to
            // the solid before the current end, so the ramp written last pass is undone here and
            // a fresh one is laid down below.
            int iLastSolid = iSolid1 - 1;
            if (m_FadeEnd)
            {
                for (int iSolid = m_RestoreFrom; iSolid < iLastSolid; ++iSolid)
                {
                    SetTrailingAlpha(colors, iSolid, 255);
                    SetLeadingAlpha(colors, iSolid, 255);
                }
                SetLeadingAlpha(colors, iLastSolid, 0);
            }
            m_RestoreFrom = iLastSolid;
        }

        private void SetTrailingAlpha(Color32[] colors, int iSolid, byte alpha)
        {
            int iVert = iSolid * Stride;
            colors[iVert + 0].a = alpha;
            colors[iVert + 2].a = alpha;
            colors[iVert + 3].a = alpha;
            if (m_EnableBackfaces)
            {
                MirrorQuadFace(colors, iVert);
            }
        }

        private void SetLeadingAlpha(Color32[] colors, int iSolid, byte alpha)
        {
            int iVert = iSolid * Stride;
            colors[iVert + 1].a = alpha;
            colors[iVert + 4].a = alpha;
            colors[iVert + 5].a = alpha;
            if (m_EnableBackfaces)
            {
                MirrorQuadFace(colors, iVert);
            }
        }
    }
} // namespace TiltBrush
