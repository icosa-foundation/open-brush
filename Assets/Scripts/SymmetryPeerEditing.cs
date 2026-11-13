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
    /// Questions about a stroke's symmetry peers. Edits to a linked group are made to the copy
    /// that was touched; the other copies are then derived from it (RederiveSymmetryGroupCommand),
    /// so tools don't mirror their edits themselves.
    ///
    /// There is no switch: a stroke has peers exactly when a linked mirror owns it, and strokes
    /// drawn with plain symmetry have none. See SymmetryMirrors.
    public static class SymmetryPeerEditing
    {
        /// Captures a group's membership so an independent spatial edit can break its links
        /// without making Undo lose them.
        internal sealed class BrokenLink
        {
            private readonly SymmetryStrokeGroup m_Group;
            private readonly List<(Stroke stroke, int index)> m_Members;

            internal SymmetryStrokeGroup Group => m_Group;

            internal BrokenLink(SymmetryStrokeGroup group)
            {
                m_Group = group;
                m_Members = new List<(Stroke stroke, int index)>();
                foreach (var stroke in group.Strokes)
                {
                    m_Members.Add((stroke, stroke.SymmetryPointerIndex));
                }
            }

            internal void Break() => m_Group.Disband();

            internal void Restore()
            {
                foreach (var member in m_Members)
                {
                    member.stroke.JoinSymmetryGroup(m_Group, member.index);
                }
            }
        }

        /// True when a linked mirror owns this stroke, so edits to it reach its peers.
        public static bool IsLinked(Stroke stroke) => stroke?.SymmetryPeerGroup?.Mirror != null;

        /// The other members of this stroke's group; empty for a stroke no linked mirror owns.
        /// Works whichever mirror is active: a mirror's own settings say how its strokes relate.
        ///
        /// Erased strokes are left out. They are invisible, and the edits tools make - recreating
        /// geometry, moving a stroke between canvases - would bring one back.
        public static IEnumerable<Stroke> PeersOf(Stroke stroke)
        {
            if (!IsLinked(stroke)) { yield break; }
            foreach (var peer in stroke.SymmetryPeers)
            {
                if (peer.IsGeometryEnabled) { yield return peer; }
            }
        }

        /// The symmetry peers of the passed strokes that aren't themselves in the set, for edits
        /// that already handle the strokes they were given. Empty for unlinked strokes.
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
            if (group?.Mirror == null || peer == null ||
                !ReferenceEquals(group, peer.SymmetryPeerGroup))
            {
                return false;
            }

            // The transforms describe the strokes as they were drawn, in the canvas they were
            // drawn into. If they no longer share a canvas, that relationship no longer holds.
            if (EffectiveCanvas(stroke) != EffectiveCanvas(peer)) { return false; }
            if (EffectiveCanvas(stroke) != group.Mirror.Canvas) { return false; }

            var transforms = group.Mirror.Settings?.PointerTransforms;
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
    }
} // namespace TiltBrush
