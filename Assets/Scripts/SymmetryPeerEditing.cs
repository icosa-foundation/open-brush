// Copyright 2024 The Open Brush Authors
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
    /// Opt-in editing of symmetry peers: when a stroke drawn with symmetry is edited, the strokes
    /// the symmetry created alongside it are edited to match.
    ///
    /// This is the one place tools ask about peers, so that adding peer awareness to a tool is a
    /// call to WithPeers() (for edits that treat strokes as a set, like deletion or recolouring)
    /// or to GatherPeerTransforms() (for edits that move strokes, where each peer needs the
    /// mirrored version of the transform).
    ///
    /// Off by default. Turn it on in Open Brush.cfg with Flags.SymmetryPeerEditing, or at runtime
    /// through the symmetry.peerediting API command.
    public static class SymmetryPeerEditing
    {
        private static bool? m_Enabled;

        public static bool Enabled
        {
            get
            {
                if (m_Enabled == null && App.UserConfig != null)
                {
                    m_Enabled = App.UserConfig.Flags.SymmetryPeerEditing;
                }
                return m_Enabled ?? false;
            }
            set { m_Enabled = value; }
        }

        /// The strokes the symmetry created alongside this one; empty when peer editing is off.
        public static IEnumerable<Stroke> PeersOf(Stroke stroke)
        {
            if (!Enabled || stroke == null) { return System.Linq.Enumerable.Empty<Stroke>(); }
            return stroke.SymmetryPeers;
        }

        /// The passed strokes plus their symmetry peers, without duplicates. Returns the strokes
        /// unchanged when peer editing is off, so callers can use it unconditionally.
        public static List<Stroke> WithPeers(IEnumerable<Stroke> strokes)
        {
            var result = new List<Stroke>();
            var seen = new HashSet<Stroke>(new ReferenceComparer<Stroke>());
            foreach (var stroke in strokes)
            {
                if (seen.Add(stroke)) { result.Add(stroke); }
            }
            AppendPeers(result, seen, result.Count);
            return result;
        }

        /// The symmetry peers of the passed strokes that aren't themselves in the set, for edits
        /// that already handle the strokes they were given. Empty when peer editing is off.
        public static List<Stroke> PeersOutside(IEnumerable<Stroke> strokes)
        {
            var all = new List<Stroke>();
            var seen = new HashSet<Stroke>(new ReferenceComparer<Stroke>());
            foreach (var stroke in strokes)
            {
                if (seen.Add(stroke)) { all.Add(stroke); }
            }
            int numStrokes = all.Count;
            AppendPeers(all, seen, numStrokes);
            return all.GetRange(numStrokes, all.Count - numStrokes);
        }

        /// Appends the peers of the first 'count' entries, skipping any already seen.
        private static void AppendPeers(List<Stroke> strokes, HashSet<Stroke> seen, int count)
        {
            if (!Enabled) { return; }
            for (int i = 0; i < count; ++i)
            {
                foreach (var peer in strokes[i].SymmetryPeers)
                {
                    if (seen.Add(peer)) { strokes.Add(peer); }
                }
            }
        }

        /// The transform that does to 'peer' what xf_CS does to 'stroke'.
        ///
        /// The two strokes are related by C = Mpeer * Mstroke.inverse, where M is the symmetry
        /// transform each was drawn with, so the peer's version of the edit is C * xf * C.inverse.
        /// False when the symmetry mode didn't record a fixed relationship between the two (a
        /// sketch saved before peers were recorded, or a mode like TwoHanded), in which case the
        /// caller should leave the peer alone rather than guess.
        public static bool TryGetPeerTransform(
            Stroke stroke, Stroke peer, TrTransform xf_CS, out TrTransform peerXf_CS)
        {
            peerXf_CS = TrTransform.identity;
            var group = stroke?.SymmetryPeerGroup;
            if (group == null || peer == null || !ReferenceEquals(group, peer.SymmetryPeerGroup))
            {
                return false;
            }

            // The transforms describe the strokes as they were drawn, in the canvas they were
            // drawn into. If they no longer share a canvas, that relationship no longer holds.
            if (stroke.Canvas != peer.Canvas) { return false; }

            var transforms = group.Settings?.PointerTransforms;
            int from = stroke.SymmetryPointerIndex;
            int to = peer.SymmetryPointerIndex;
            if (transforms == null || from < 0 || to < 0 ||
                from >= transforms.Count || to >= transforms.Count)
            {
                return false;
            }

            TrTransform toPeer = transforms[to] * transforms[from].inverse;
            peerXf_CS = toPeer * xf_CS * toPeer.inverse;
            return peerXf_CS.IsFinite();
        }

        // ---- Repainting ------------------------------------------------------------------- //
        //
        // Symmetry can give each of its pointers a different colour, brush or size - a colour
        // shift across the mirrors, or per-pointer colours and brushes from a symmetry script -
        // so a peer is kept consistent with the stroke being repainted rather than made identical
        // to it. A group whose strokes all matched to begin with still ends up all matching.

        /// What a peer should become when 'stroke' is repainted with the passed values:
        /// - colour keeps the peer's offset from the stroke's colour, in HSV
        /// - size keeps the peer's ratio to the stroke's size
        /// - brush follows only if the peer was using the same brush as the stroke
        ///
        /// Call before the repaint is applied: the stroke's current values are the "before" side
        /// of each relationship.
        public static void GetPeerRepaintParams(
            Stroke stroke, Stroke peer,
            Color newColor, Guid newGuid, float newSize,
            out Color peerColor, out Guid peerGuid, out float peerSize)
        {
            peerColor = OffsetColorLike(newColor, stroke.m_Color, peer.m_Color);
            peerGuid = peer.m_BrushGuid == stroke.m_BrushGuid ? newGuid : peer.m_BrushGuid;
            peerSize = Mathf.Approximately(stroke.m_BrushSize, 0f)
                ? newSize
                : peer.m_BrushSize * (newSize / stroke.m_BrushSize);
        }

        /// newColor, moved by the offset that takes sourceColor to peerColor. Hue wraps, the rest
        /// clamps, and the new colour's alpha is kept as picked.
        private static Color OffsetColorLike(Color newColor, Color sourceColor, Color peerColor)
        {
            Color.RGBToHSV(newColor, out float hNew, out float sNew, out float vNew);
            Color.RGBToHSV(sourceColor, out float hSource, out float sSource, out float vSource);
            Color.RGBToHSV(peerColor, out float hPeer, out float sPeer, out float vPeer);

            Color result = Color.HSVToRGB(
                Mathf.Repeat(hNew + (hPeer - hSource), 1f),
                Mathf.Clamp01(sNew + (sPeer - sSource)),
                Mathf.Clamp01(vNew + (vPeer - vSource)));
            result.a = newColor.a;
            return result;
        }

        /// For every peer outside the passed set, the repaint values that keep it consistent with
        /// the stroke it belongs with. The four out lists are parallel, and all are left empty
        /// when peer editing is off.
        ///
        /// The value lists are parallel to 'strokes', as RepaintStrokeCommand takes them.
        public static void GatherPeerRepaints(
            IReadOnlyList<Stroke> strokes,
            IReadOnlyList<Color> colors, IReadOnlyList<Guid> guids, IReadOnlyList<float> sizes,
            List<Stroke> outPeers, List<Color> outColors, List<Guid> outGuids, List<float> outSizes)
        {
            outPeers.Clear();
            outColors.Clear();
            outGuids.Clear();
            outSizes.Clear();
            if (!Enabled) { return; }

            var handled = new HashSet<Stroke>(new ReferenceComparer<Stroke>());
            foreach (var stroke in strokes)
            {
                handled.Add(stroke);
            }
            for (int i = 0; i < strokes.Count; ++i)
            {
                foreach (var peer in strokes[i].SymmetryPeers)
                {
                    if (!handled.Add(peer)) { continue; }
                    GetPeerRepaintParams(
                        strokes[i], peer, colors[i], guids[i], sizes[i],
                        out Color peerColor, out Guid peerGuid, out float peerSize);
                    outPeers.Add(peer);
                    outColors.Add(peerColor);
                    outGuids.Add(peerGuid);
                    outSizes.Add(peerSize);
                }
            }
        }

        /// Collects the peers of the passed strokes along with the transform each one needs so
        /// that it mirrors xf_CS being applied to the stroke it belongs with. Strokes in the
        /// passed set are skipped: an edit already moves those directly.
        ///
        /// outPeers and outTransforms are parallel, and both are left empty when peer editing is
        /// off, so callers can gather unconditionally and apply what they get.
        public static void GatherPeerTransforms(
            IEnumerable<Stroke> strokes, TrTransform xf_CS,
            List<Stroke> outPeers, List<TrTransform> outTransforms)
        {
            outPeers.Clear();
            outTransforms.Clear();
            if (!Enabled) { return; }

            var handled = new HashSet<Stroke>(new ReferenceComparer<Stroke>());
            foreach (var stroke in strokes)
            {
                handled.Add(stroke);
            }
            foreach (var stroke in strokes)
            {
                foreach (var peer in stroke.SymmetryPeers)
                {
                    if (!handled.Add(peer)) { continue; }
                    if (TryGetPeerTransform(stroke, peer, xf_CS, out TrTransform peerXf))
                    {
                        outPeers.Add(peer);
                        outTransforms.Add(peerXf);
                    }
                }
            }
        }
    }
} // namespace TiltBrush
