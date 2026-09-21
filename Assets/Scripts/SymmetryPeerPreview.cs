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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// Shows the symmetry peers of a selection following it while it is being moved.
    ///
    /// A selection doesn't touch its strokes while it is being dragged - they ride the selection
    /// canvas, and the move is written into them when they are deselected. The peers follow the
    /// same way: each one is parked in a preview canvas that is moved by the peer's version of the
    /// selection's transform. Nothing about a peer changes while it is parked, only where it is
    /// drawn, so the only cost per frame is a handful of canvas transforms, and the deselect is
    /// still what writes the move into the strokes.
    ///
    /// The peers of a selection usually fall into a few groups - one per distinct relationship to
    /// the stroke they are following - so there is one preview canvas per group, not per stroke.
    public static class SymmetryPeerPreview
    {
        private class PreviewGroup
        {
            /// Maps the stroke being followed onto the peers in this group.
            public TrTransform ToPeer;
            /// The canvas the peers came from, and are drawn relative to.
            public CanvasScript Home;
            public CanvasScript Canvas;
            public readonly List<Stroke> Strokes = new List<Stroke>();
        }

        private static readonly List<PreviewGroup> m_Groups = new List<PreviewGroup>();

        public static bool IsShowing => m_Groups.Count > 0;

        /// Parks the peers of the passed strokes, replacing anything showing already. Call when
        /// the set of strokes being moved changes.
        public static void Show(IEnumerable<Stroke> strokes)
        {
            Hide();
            if (!SymmetryPeerEditing.Enabled) { return; }

            var handled = new HashSet<Stroke>(new ReferenceComparer<Stroke>());
            foreach (var stroke in strokes)
            {
                handled.Add(stroke);
            }
            foreach (var stroke in strokes)
            {
                foreach (var peer in SymmetryPeerEditing.PeersOf(stroke))
                {
                    if (!handled.Add(peer)) { continue; }
                    // A peer that is in the selection is already being moved by it.
                    if (SelectionManager.m_Instance.IsStrokeSelected(peer)) { continue; }
                    if (!SymmetryPeerEditing.TryGetPeerSymmetryTransform(
                            stroke, peer, out TrTransform toPeer))
                    {
                        continue;
                    }
                    Park(peer, toPeer);
                }
            }
        }

        private static void Park(Stroke peer, TrTransform toPeer)
        {
            CanvasScript home = peer.Canvas;
            PreviewGroup group = null;
            for (int i = 0; i < m_Groups.Count; ++i)
            {
                if (m_Groups[i].Home == home && TrTransform.Approximately(m_Groups[i].ToPeer, toPeer))
                {
                    group = m_Groups[i];
                    break;
                }
            }
            if (group == null)
            {
                group = new PreviewGroup
                {
                    ToPeer = toPeer,
                    Home = home,
                    Canvas = App.Scene.AddPreviewCanvas(),
                };
                group.Canvas.Pose = home.Pose;
                m_Groups.Add(group);
            }

            // Remember where the stroke belongs, the same way a selected stroke does, so that a
            // save while it is parked still writes it to its own layer.
            peer.m_PreviousCanvas = home;
            // SetParent, not SetParentKeepWorldPosition: the stroke's control points must not
            // change. Only the preview canvas moves.
            peer.SetParent(group.Canvas);
            group.Strokes.Add(peer);
        }

        /// Moves the preview canvases so each parked peer shows the passed selection transform
        /// mirrored onto it. Cheap enough to call every frame.
        public static void UpdateTransform(TrTransform selectionXf)
        {
            for (int i = 0; i < m_Groups.Count; ++i)
            {
                PreviewGroup group = m_Groups[i];
                TrTransform mirrored = group.ToPeer * selectionXf * group.ToPeer.inverse;
                if (!mirrored.IsFinite()) { continue; }
                // Recomputed from the home canvas rather than accumulated, so the preview stays
                // correct when the user moves or scales the world mid-drag.
                group.Canvas.Pose = group.Home.Pose * mirrored;
            }
        }

        /// Returns every parked peer to the canvas it came from, unchanged. The move itself is
        /// written into the strokes by the deselect, not by this.
        public static void Hide()
        {
            for (int i = 0; i < m_Groups.Count; ++i)
            {
                PreviewGroup group = m_Groups[i];
                foreach (var stroke in group.Strokes)
                {
                    stroke.SetParent(group.Home);
                }
                App.Scene.DestroyPreviewCanvas(group.Canvas);
            }
            m_Groups.Clear();
        }

        /// Drops the preview without putting anything back, for when the strokes are going away
        /// anyway. Only for teardown; Hide() is what callers want.
        public static void Forget()
        {
            m_Groups.Clear();
        }
    }
} // namespace TiltBrush
