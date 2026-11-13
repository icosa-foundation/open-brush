// Copyright 2022 The Open Brush Authors
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
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush
{
    public class JoinStrokeCommand : BaseCommand
    {
        private Stroke m_StrokeA;
        private Stroke m_StrokeB;
        private JoinStrokeType m_JoinType;

        private List<PointerManager.ControlPoint> m_InitialCP;
        private List<PointerManager.ControlPoint> m_NewCP;
        private readonly List<Color32?> m_InitialColors;
        private readonly List<Color32?> m_NewColors;
        private readonly ColorOverrideMode m_InitialColorMode;
        private readonly ColorOverrideMode m_NewColorMode;
        private readonly SymmetryStrokeGroup m_GroupA;
        private readonly SymmetryStrokeGroup m_GroupB;
        private readonly int m_PointerA;
        private readonly int m_PointerB;
        // Joining two groups of one mirror: stroke A keeps its place in its group, which becomes
        // the joined group, and stroke B (now part of A) leaves its own.
        private readonly bool m_KeepGroupA;

        /// What joining two strokes means for their symmetry links.
        public enum SymmetryJoin
        {
            /// Neither stroke is linked.
            Unlinked,
            /// Two copies of one linked group, across the mirror: the result is one
            /// self-symmetric stroke, which leaves the group.
            SameGroup,
            /// Strokes from two groups of the same mirror: every pair of copies joins, and the
            /// result is one linked group.
            PeerGroups,
            /// Linked with unlinked, or linked under different mirrors.
            Refused,
        }

        public static SymmetryJoin ClassifySymmetryJoin(Stroke strokeA, Stroke strokeB)
        {
            var groupA = strokeA.SymmetryPeerGroup?.Mirror != null ? strokeA.SymmetryPeerGroup : null;
            var groupB = strokeB.SymmetryPeerGroup?.Mirror != null ? strokeB.SymmetryPeerGroup : null;
            if (groupA == null && groupB == null) { return SymmetryJoin.Unlinked; }
            if (groupA == null || groupB == null ||
                !ReferenceEquals(groupA.Mirror, groupB.Mirror))
            {
                return SymmetryJoin.Refused;
            }
            if (ReferenceEquals(groupA, groupB)) { return SymmetryJoin.SameGroup; }
            return HasCompleteCounterpartPairing(strokeA, strokeB)
                ? SymmetryJoin.PeerGroups : SymmetryJoin.Refused;
        }

        public static bool CanJoin(Stroke strokeA, Stroke strokeB) =>
            ClassifySymmetryJoin(strokeA, strokeB) != SymmetryJoin.Refused;

        private static bool HasCompleteCounterpartPairing(Stroke strokeA, Stroke strokeB)
        {
            var paired = new HashSet<Stroke> { strokeB };
            foreach (var peerA in SymmetryPeerEditing.PeersOf(strokeA))
            {
                var peerB = Counterpart(strokeA, strokeB, peerA);
                if (peerB == null || !paired.Add(peerB)) { return false; }
            }
            return strokeB.SymmetryPeerGroup.Strokes
                .Where(stroke => stroke.IsGeometryEnabled).All(paired.Contains);
        }

        private static bool ValidateSymmetryJoin(Stroke strokeA, Stroke strokeB)
        {
            var join = ClassifySymmetryJoin(strokeA, strokeB);
            if (join == SymmetryJoin.Refused)
            {
                throw new ArgumentException("Linked strokes require matching mirrors and complete counterpart pairing");
            }
            return join == SymmetryJoin.PeerGroups;
        }

        private enum JoinStrokeType
        {
            FirstFirst,
            FirstLast,
            LastFirst,
            LastLast
        }

        public JoinStrokeCommand(
            Stroke strokeA, Stroke strokeB, BaseCommand parent = null)
            : this(strokeA, strokeB, parent,
                ValidateSymmetryJoin(strokeA, strokeB))
        {
            if (!m_KeepGroupA) { return; }

            // Every other copy of A joins its counterpart in B's group: the copy that is to B what
            // it is to A. Validation requires a complete one-to-one pairing of visible copies.
            // Afterwards the whole group is derived from A, so it stays an exact symmetric set.
            foreach (var peerA in SymmetryPeerEditing.PeersOf(strokeA))
            {
                var peerB = Counterpart(strokeA, strokeB, peerA);
                if (peerB != null)
                {
                    new JoinStrokeCommand(peerA, peerB, this, keepGroupA: true);
                }
            }
            new RederiveSymmetryGroupCommand(strokeA, this);
        }

        /// The visible copy in B's group that peerA's pointer would have drawn from B: with
        /// pointer transforms P, A at index a and B at index b, peerA (index j) pairs with the
        /// copy at the index k for which P[k] = P[j] · P[a]⁻¹ · P[b].
        private static Stroke Counterpart(Stroke strokeA, Stroke strokeB, Stroke peerA)
        {
            var transforms = strokeA.SymmetryPeerGroup.Mirror.Settings?.PointerTransforms;
            int a = strokeA.SymmetryPointerIndex;
            int b = strokeB.SymmetryPointerIndex;
            int j = peerA.SymmetryPointerIndex;
            if (transforms == null || !InRange(a) || !InRange(b) || !InRange(j)) { return null; }
            TrTransform wanted = transforms[j] * transforms[a].inverse * transforms[b];
            foreach (var peerB in strokeB.SymmetryPeerGroup.Strokes)
            {
                int k = peerB.SymmetryPointerIndex;
                if (!ReferenceEquals(peerB, strokeB) && peerB.IsGeometryEnabled && InRange(k) &&
                    SamePlacement(transforms[k], wanted))
                {
                    return peerB;
                }
            }
            return null;

            bool InRange(int index) => index >= 0 && index < transforms.Count;
        }

        /// Equal up to float noise; a rotation and its negated quaternion count as the same.
        private static bool SamePlacement(TrTransform x, TrTransform y)
        {
            float tolerance = 1e-3f * Mathf.Max(1f, x.translation.magnitude);
            return (x.translation - y.translation).magnitude <= tolerance &&
                Mathf.Abs(Quaternion.Dot(x.rotation, y.rotation)) >= 0.9999f &&
                Mathf.Abs(x.scale - y.scale) <= 1e-3f * Mathf.Max(1f, Mathf.Abs(x.scale));
        }

        private JoinStrokeCommand(
            Stroke strokeA, Stroke strokeB, BaseCommand parent, bool keepGroupA) : base(parent)
        {
            m_KeepGroupA = keepGroupA;
            m_StrokeA = strokeA;
            m_StrokeB = strokeB;
            m_GroupA = strokeA.SymmetryPeerGroup;
            m_GroupB = strokeB.SymmetryPeerGroup;
            m_PointerA = strokeA.SymmetryPointerIndex;
            m_PointerB = strokeB.SymmetryPointerIndex;
            m_JoinType = JoinStrokeType.FirstFirst;
            m_InitialCP = strokeA.m_ControlPoints.ToList();
            m_InitialColors = strokeA.m_OverrideColors?.ToList();
            m_InitialColorMode = strokeA.m_ColorOverrideMode;

            float prevDistance = (m_StrokeA.m_ControlPoints[0].m_Pos - strokeB.m_ControlPoints[0].m_Pos).sqrMagnitude;
            float distanceTest;

            int lastIndexA = m_StrokeA.m_ControlPoints.Length - 1;
            int lastIndexB = strokeB.m_ControlPoints.Length - 1;

            distanceTest = (m_StrokeA.m_ControlPoints[0].m_Pos - strokeB.m_ControlPoints[lastIndexB].m_Pos).sqrMagnitude;
            if (distanceTest < prevDistance)
            {
                m_JoinType = JoinStrokeType.FirstLast;
                prevDistance = distanceTest;
            }

            distanceTest = (m_StrokeA.m_ControlPoints[lastIndexA].m_Pos - strokeB.m_ControlPoints[0].m_Pos).sqrMagnitude;
            if (distanceTest < prevDistance)
            {
                m_JoinType = JoinStrokeType.LastFirst;
                prevDistance = distanceTest;
            }

            distanceTest = (m_StrokeA.m_ControlPoints[lastIndexA].m_Pos - strokeB.m_ControlPoints[lastIndexB].m_Pos).sqrMagnitude;
            if (distanceTest < prevDistance)
            {
                m_JoinType = JoinStrokeType.LastLast;
            }

            var joined = BuildJoinedStrokeData(strokeA, strokeB,
                prepend: m_JoinType == JoinStrokeType.FirstFirst || m_JoinType == JoinStrokeType.FirstLast,
                reverse: m_JoinType == JoinStrokeType.FirstFirst || m_JoinType == JoinStrokeType.LastLast);
            m_NewCP = joined.Points;
            m_NewColors = joined.Colors;
            m_NewColorMode = joined.Mode;
        }

        internal static (List<PointerManager.ControlPoint> Points, List<Color32?> Colors, ColorOverrideMode Mode)
            BuildJoinedStrokeData(Stroke strokeA, Stroke strokeB, bool prepend, bool reverse)
        {
            var colors = ApiMethods.GetJoinedStrokeColors(new[] { strokeA, strokeB }, strokeA);
            int firstCount = strokeA.m_ControlPoints.Length;
            return (JoinData(strokeA.m_ControlPoints, strokeB.m_ControlPoints, prepend, reverse),
                colors.Colors == null ? null : JoinData(colors.Colors.Take(firstCount),
                    colors.Colors.Skip(firstCount), prepend, reverse), colors.Mode);
        }

        private static List<T> JoinData<T>(IEnumerable<T> first, IEnumerable<T> second, bool prepend, bool reverse)
        {
            if (reverse) { second = second.Reverse(); }
            return (prepend ? second.Concat(first) : first.Concat(second)).ToList();
        }

        protected override void OnRedo()
        {
            if (!m_KeepGroupA) { m_StrokeA.LeaveSymmetryGroup(); }
            m_StrokeB.LeaveSymmetryGroup();
            ModifyStroke(m_StrokeA, m_NewCP, m_NewColors, m_NewColorMode);
            m_StrokeB.Uncreate();
        }

        protected override void OnUndo()
        {
            ModifyStroke(m_StrokeA, m_InitialCP, m_InitialColors, m_InitialColorMode);
            m_StrokeB.Recreate();
            if (m_GroupA != null) { m_StrokeA.SetSymmetryGroup(m_GroupA, m_PointerA); }
            if (m_GroupB != null) { m_StrokeB.SetSymmetryGroup(m_GroupB, m_PointerB); }
        }

        private void ModifyStroke(Stroke stroke, IEnumerable<PointerManager.ControlPoint> newControlPoints,
            List<Color32?> colors, ColorOverrideMode colorMode)
        {
            stroke.m_ControlPoints = newControlPoints.ToArray();
            stroke.m_OverrideColors = colors?.ToList();
            stroke.m_ColorOverrideMode = colorMode;
            stroke.Uncreate();
            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Recreate(null, stroke.Canvas);
        }

        public override bool NeedsSave { get { return true; } }

    }
}
