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

namespace TiltBrush
{
    /// Shows the symmetry peers of a selection following it while it is being moved.
    ///
    /// Each peer is moved where it lies, by the peer's version of the selection's transform, the
    /// same way a mirror's strokes follow the mirror. The peers are put back exactly as they were
    /// when the preview ends, because the move is only written into the strokes for real by the
    /// deselect that bakes it - this is a preview and owns none of the result.
    public static class SymmetryPeerPreview
    {
        private static readonly List<Stroke> m_Strokes = new List<Stroke>();
        // Maps the stroke each peer is following onto that peer.
        private static readonly List<TrTransform> m_ToPeer = new List<TrTransform>();
        // Per peer, everything the preview has moved it by so far.
        private static readonly List<TrTransform> m_Applied = new List<TrTransform>();

        public static bool IsShowing => m_Strokes.Count > 0;

        /// Starts following the passed strokes, replacing anything showing already. Call when the
        /// set of strokes being moved changes.
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
                    m_Strokes.Add(peer);
                    m_ToPeer.Add(toPeer);
                    m_Applied.Add(TrTransform.identity);
                }
            }
        }

        /// Moves each peer to where the passed selection transform puts it. Cheap enough to call
        /// every frame: each stroke's geometry is transformed where it lies.
        public static void UpdateTransform(TrTransform selectionXf)
        {
            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                TrTransform target = m_ToPeer[i] * selectionXf * m_ToPeer[i].inverse;
                if (!target.IsFinite()) { continue; }
                TrTransform step = target * m_Applied[i].inverse;
                if (step == TrTransform.identity) { continue; }
                // Geometry only: the peer's data still says where it really is, so the deselect
                // works out the move from the right place and a save taken mid-drag is correct.
                if (m_Strokes[i].TransformGeometryInPlace(step, updateControlPoints: false))
                {
                    m_Applied[i] = target;
                }
            }
        }

        /// Puts every peer back exactly where it was. The deselect is what moves them for real.
        public static void Hide()
        {
            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                if (m_Applied[i] != TrTransform.identity)
                {
                    m_Strokes[i].TransformGeometryInPlace(
                        m_Applied[i].inverse, updateControlPoints: false);
                }
            }
            Forget();
        }

        /// Drops the preview without putting anything back, for when the strokes are going away
        /// anyway. Only for teardown; Hide() is what callers want.
        public static void Forget()
        {
            m_Strokes.Clear();
            m_ToPeer.Clear();
            m_Applied.Clear();
        }
    }
} // namespace TiltBrush
