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
        ///
        /// Erased strokes are left out. They are invisible, and the edits tools make - recreating
        /// geometry, moving a stroke between canvases - would bring one back.
        public static IEnumerable<Stroke> PeersOf(Stroke stroke)
        {
            if (!Enabled || stroke == null) { yield break; }
            foreach (var peer in stroke.SymmetryPeers)
            {
                if (peer.IsGeometryEnabled) { yield return peer; }
            }
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
            for (int i = 0; i < count; ++i)
            {
                foreach (var peer in PeersOf(strokes[i]))
                {
                    if (seen.Add(peer)) { strokes.Add(peer); }
                }
            }
        }

        /// The canvas a stroke's geometry belongs to. The selection canvas is a staging area a
        /// stroke passes through while selected, so a selected stroke counts as being in the
        /// canvas it will return to.
        private static CanvasScript EffectiveCanvas(Stroke stroke)
        {
            return stroke.Canvas == App.Scene.SelectionCanvas && stroke.m_PreviousCanvas != null
                ? stroke.m_PreviousCanvas
                : stroke.Canvas;
        }

        /// C = Mpeer * Mstroke.inverse: the canvas-space transform that carries the stroke onto
        /// its peer, from the symmetry transforms the two were drawn with.
        ///
        /// False when the symmetry mode didn't record a fixed relationship between the two (a
        /// sketch saved before peers were recorded, or a mode like TwoHanded), in which case the
        /// caller should leave the peer alone rather than guess.
        public static bool TryGetPeerSymmetryTransform(
            Stroke stroke, Stroke peer, out TrTransform toPeer)
        {
            toPeer = TrTransform.identity;
            var group = stroke?.SymmetryPeerGroup;
            if (group == null || peer == null || !ReferenceEquals(group, peer.SymmetryPeerGroup))
            {
                return false;
            }

            // The transforms describe the strokes as they were drawn, in the canvas they were
            // drawn into. If they no longer share a canvas, that relationship no longer holds.
            if (EffectiveCanvas(stroke) != EffectiveCanvas(peer)) { return false; }

            var transforms = group.Settings?.PointerTransforms;
            int from = stroke.SymmetryPointerIndex;
            int to = peer.SymmetryPointerIndex;
            if (transforms == null || from < 0 || to < 0 ||
                from >= transforms.Count || to >= transforms.Count)
            {
                return false;
            }

            toPeer = transforms[to] * transforms[from].inverse;
            return toPeer.IsFinite();
        }

        /// The transform that does to 'peer' what xf_CS does to 'stroke'. Both are canvas-space
        /// left transforms, the form stroke edits take: the peer's version is C * xf * C.inverse.
        public static bool TryGetPeerTransform(
            Stroke stroke, Stroke peer, TrTransform xf_CS, out TrTransform peerXf_CS)
        {
            peerXf_CS = TrTransform.identity;
            if (!TryGetPeerSymmetryTransform(stroke, peer, out TrTransform toPeer)) { return false; }
            peerXf_CS = toPeer * xf_CS * toPeer.inverse;
            return peerXf_CS.IsFinite();
        }

        /// A stroke selected after the widget has moved follows only the later movement.
        /// Both selection preview and deselection baking use this canvas-space delta.
        internal static TrTransform SelectionMovement(TrTransform selectionXf,
            TrTransform joinXf) => selectionXf * joinXf.inverse;

        internal static TrTransform PeerSelectionMovement(TrTransform toPeer,
            TrTransform selectionXf, TrTransform joinXf)
        {
            var moved = SelectionMovement(selectionXf, joinXf);
            return toPeer * moved * toPeer.inverse;
        }

        /// The control points a peer should take when 'stroke' is reshaped to newControlPoints -
        /// what the reshape tool needs to sculpt a whole group at once.
        ///
        /// Each point moves by the peer's version of how that point moved on the stroke, so the
        /// peer keeps any differences of its own rather than being snapped onto an exact mirror
        /// image. Point i maps to point i, as the two were drawn.
        ///
        /// Call before the edit is applied to the stroke; false when the peer can't be mirrored
        /// point for point, or when the symmetry didn't record how the two are related.
        public static bool TryGetPeerControlPoints(
            Stroke stroke, Stroke peer,
            PointerManager.ControlPoint[] newControlPoints,
            out PointerManager.ControlPoint[] peerControlPoints)
        {
            peerControlPoints = null;
            if (!TryGetPeerSymmetryTransform(stroke, peer, out TrTransform toPeer)) { return false; }

            var oldControlPoints = stroke.m_ControlPoints;
            int count = oldControlPoints.Length;
            if (newControlPoints.Length != count || peer.m_ControlPoints.Length != count)
            {
                return false;
            }

            var result = (PointerManager.ControlPoint[])peer.m_ControlPoints.Clone();
            Quaternion toPeerRotation = toPeer.rotation;
            Quaternion fromPeerRotation = Quaternion.Inverse(toPeerRotation);
            for (int i = 0; i < count; ++i)
            {
                Vector3 moved = newControlPoints[i].m_Pos - oldControlPoints[i].m_Pos;
                bool turned = newControlPoints[i].m_Orient != oldControlPoints[i].m_Orient;
                if (moved == Vector3.zero && !turned) { continue; }

                result[i].m_Pos += toPeer.MultiplyVector(moved);
                if (turned)
                {
                    // The same turn, seen from the peer's side of the symmetry.
                    Quaternion turn = newControlPoints[i].m_Orient *
                        Quaternion.Inverse(oldControlPoints[i].m_Orient);
                    result[i].m_Orient =
                        (toPeerRotation * turn * fromPeerRotation) * result[i].m_Orient;
                }
            }
            peerControlPoints = result;
            return true;
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

        /// The per-control-point override colours a peer should take when 'stroke' is given
        /// newOverrideColors in newMode - what the tint tool needs to paint a group at once.
        ///
        /// Peers are drawn point for point alongside the stroke, so point i maps to point i. Only
        /// the points the edit actually changes are touched on the peer, and each keeps its own
        /// offset from the stroke's colour, in HSV, so a group drawn with a colour shift stays
        /// shifted. Alpha is left as the peer had it; brushes such as QuillFlatBrush use it for
        /// per-vertex opacity.
        ///
        /// Call before the edit is applied to the stroke, and false when the peer can't be
        /// mirrored point for point - a stroke simplified differently, or a scripted symmetry
        /// pointer that started a new stroke mid-line.
        public static bool TryGetPeerPointColors(
            Stroke stroke, Stroke peer,
            List<Color32?> newOverrideColors, ColorOverrideMode newMode,
            out List<Color32?> peerColors, out ColorOverrideMode peerMode)
        {
            peerColors = null;
            peerMode = peer.m_ColorOverrideMode;

            int count = stroke.m_ControlPoints.Length;
            if (peer.m_ControlPoints.Length != count) { return false; }

            // A null list means the stroke's overrides were dropped altogether, which mirrors as
            // a clear at every point the stroke had one.
            bool clearingAll = newOverrideColors == null;
            if (!clearingAll && newOverrideColors.Count != count) { return false; }

            var result = HasOverridesFor(peer, count)
                ? new List<Color32?>(peer.m_OverrideColors)
                : new List<Color32?>(new Color32?[count]);

            // As the tint tool does for the stroke itself: a peer moving to Replace keeps the
            // colour its existing overrides were showing under the old mode.
            if (newMode == ColorOverrideMode.Replace &&
                peer.m_ColorOverrideMode != ColorOverrideMode.Replace)
            {
                for (int i = 0; i < count; ++i)
                {
                    if (result[i].HasValue) { result[i] = peer.GetColor(i); }
                }
            }
            // Clearing the stroke's last override says nothing about how the peer's remaining
            // ones should be read, so the peer keeps its own mode in that case.
            peerMode = newMode == ColorOverrideMode.None ? peer.m_ColorOverrideMode : newMode;

            bool anyOverrides = false;
            bool strokeHasOverrides = HasOverridesFor(stroke, count);
            for (int i = 0; i < count; ++i)
            {
                Color32? before = strokeHasOverrides ? stroke.m_OverrideColors[i] : null;
                Color32? after = clearingAll ? null : newOverrideColors[i];
                if (!SameColor(before, after))
                {
                    result[i] = after.HasValue
                        ? OffsetPointColor(after.Value, stroke.GetColor(i), peer.GetColor(i))
                        : (Color32?)null;
                }
                anyOverrides |= result[i].HasValue;
            }

            if (!anyOverrides)
            {
                peerColors = null;
                peerMode = ColorOverrideMode.None;
                return true;
            }
            peerColors = result;
            return true;
        }

        private static bool HasOverridesFor(Stroke stroke, int count)
        {
            return stroke.m_OverrideColors != null && stroke.m_OverrideColors.Count == count;
        }

        private static bool SameColor(Color32? a, Color32? b)
        {
            if (a.HasValue != b.HasValue) { return false; }
            return !a.HasValue || a.Value.Equals(b.Value);
        }

        /// As OffsetColorLike, for a single control point, keeping the peer's own alpha.
        private static Color32 OffsetPointColor(Color32 newColor, Color32 sourceColor, Color32 peerColor)
        {
            Color32 result = OffsetColorLike(newColor, sourceColor, peerColor);
            result.a = peerColor.a;
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
                foreach (var peer in PeersOf(strokes[i]))
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
                foreach (var peer in PeersOf(stroke))
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
