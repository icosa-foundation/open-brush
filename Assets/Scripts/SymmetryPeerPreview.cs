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
    ///
    /// The preview moves the control points along with the geometry, even though it is only a
    /// preview. Anything that rebuilds a stroke - tinting or repainting it, for instance -
    /// regenerates its geometry from its control points, so a stroke previewed by moving its
    /// geometry alone would jump back the moment another tool touched it, and the preview would
    /// then owe it an inverse transform it had never been given.
    public static class SymmetryPeerPreview
    {
        private static readonly List<Stroke> m_Strokes = new List<Stroke>();
        // Maps the stroke each peer is following onto that peer.
        private static readonly List<TrTransform> m_ToPeer = new List<TrTransform>();
        private static readonly List<TrTransform> m_JoinTransforms = new List<TrTransform>();
        // Per peer, everything the preview has moved it by so far.
        private static readonly List<TrTransform> m_Applied = new List<TrTransform>();

        public static bool IsShowing => m_Strokes.Count > 0 || SymmetryWidgetPreview.IsShowing;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            // With domain reload disabled, peers from the previous run can outlive their batches.
            // Their geometry is gone, so discard the preview without trying to restore it.
            Forget();
        }

        /// Starts following the passed strokes, replacing anything showing already. Call when the
        /// set of strokes being moved changes.
        public static void Show(IEnumerable<Stroke> strokes)
        {
            Hide();
            SymmetryWidgetPreview.Show();

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
                    m_JoinTransforms.Add(
                        SelectionManager.m_Instance.SelectionTransformWhenSelected(stroke));
                    m_Applied.Add(TrTransform.identity);
                }
            }
        }

        /// Moves each peer to where the passed selection transform puts it. Cheap enough to call
        /// every frame: each stroke's geometry is transformed where it lies.
        public static void UpdateTransform(TrTransform selectionXf)
        {
            SymmetryWidgetPreview.Update(selectionXf);
            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                TrTransform target = SymmetryPeerEditing.PeerSelectionMovement(
                    m_ToPeer[i], selectionXf, m_JoinTransforms[i]);
                if (!target.IsFinite()) { continue; }
                TrTransform step = target * m_Applied[i].inverse;
                if (step == TrTransform.identity) { continue; }
                if (m_Strokes[i].TransformGeometryInPlace(step))
                {
                    m_Applied[i] = target;
                }
            }
        }

        /// Hands a previewed peer over to the command that bakes the move, if the preview has
        /// moved it by exactly 'expected': it then stays where it is, its control points already
        /// moved, and is no longer the preview's to put back.
        public static bool TryCommit(Stroke peer, TrTransform expected)
        {
            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                if (!ReferenceEquals(m_Strokes[i], peer)) { continue; }
                if (!SamePlacement(m_Applied[i], expected)) { return false; }
                m_Strokes.RemoveAt(i);
                m_ToPeer.RemoveAt(i);
                m_JoinTransforms.RemoveAt(i);
                m_Applied.RemoveAt(i);
                return true;
            }
            return false;
        }

        private static bool SamePlacement(TrTransform a, TrTransform b)
        {
            float tolerance = 1e-4f * UnityEngine.Mathf.Max(1f, a.translation.magnitude);
            return (a.translation - b.translation).magnitude <= tolerance &&
                UnityEngine.Mathf.Abs(UnityEngine.Quaternion.Dot(a.rotation, b.rotation)) >= 0.99999f &&
                UnityEngine.Mathf.Abs(a.scale - b.scale) <= 1e-4f * UnityEngine.Mathf.Max(1f, UnityEngine.Mathf.Abs(a.scale));
        }

        /// Puts every peer back exactly where it was. The deselect is what moves them for real.
        public static void Hide()
        {
            SymmetryWidgetPreview.Hide();
            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                if (m_Applied[i] != TrTransform.identity)
                {
                    TrTransform inverse = m_Applied[i].inverse;
                    if (!m_Strokes[i].TransformGeometryInPlace(inverse))
                    {
                        // A repaint can replace a batched stroke with an unbatched one while
                        // previewing. Restore from its current points, preserving that edit.
                        var stroke = m_Strokes[i];
                        var points = (PointerManager.ControlPoint[])stroke.m_ControlPoints.Clone();
                        for (int point = 0; point < points.Length; ++point)
                        {
                            var pose = inverse * TrTransform.TR(points[point].m_Pos,
                                points[point].m_Orient);
                            points[point].m_Pos = pose.translation;
                            points[point].m_Orient = pose.rotation;
                        }
                        stroke.RestoreMirrorControlPoints(points, stroke.m_BrushScale * inverse.scale);
                    }
                }
            }
            Forget();
        }

        /// Drops the preview without putting anything back, for when the strokes are going away
        /// anyway. Only for teardown; Hide() is what callers want.
        public static void Forget()
        {
            SymmetryWidgetPreview.Forget();
            m_Strokes.Clear();
            m_ToPeer.Clear();
            m_JoinTransforms.Clear();
            m_Applied.Clear();
        }
    }
} // namespace TiltBrush
