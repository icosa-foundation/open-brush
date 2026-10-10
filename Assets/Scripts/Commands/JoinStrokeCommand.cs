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
            return ReferenceEquals(groupA, groupB) ? SymmetryJoin.SameGroup : SymmetryJoin.PeerGroups;
        }

        public static bool CanJoin(Stroke strokeA, Stroke strokeB) =>
            ClassifySymmetryJoin(strokeA, strokeB) != SymmetryJoin.Refused;

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
                ClassifySymmetryJoin(strokeA, strokeB) == SymmetryJoin.PeerGroups)
        {
            if (!m_KeepGroupA) { return; }

            // Every other copy of A joins its counterpart in B's group: the copy that is to B what
            // it is to A. Afterwards the whole group is derived from A, so it stays an exact
            // symmetric set even where a copy had no partner (an erased one, say).
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
            m_NewCP = strokeA.m_ControlPoints.ToList();

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

            switch (m_JoinType)
            {
                case JoinStrokeType.FirstFirst:
                    m_NewCP.InsertRange(0, strokeB.m_ControlPoints.Reverse());
                    break;
                case JoinStrokeType.FirstLast:
                    m_NewCP.InsertRange(0, strokeB.m_ControlPoints);
                    break;
                case JoinStrokeType.LastFirst:
                    m_NewCP.AddRange(strokeB.m_ControlPoints);
                    break;
                case JoinStrokeType.LastLast:
                    m_NewCP.AddRange(strokeB.m_ControlPoints.Reverse());
                    break;
            }

        }

        protected override void OnRedo()
        {
            if (!m_KeepGroupA) { m_StrokeA.LeaveSymmetryGroup(); }
            m_StrokeB.LeaveSymmetryGroup();
            ModifyStroke(m_StrokeA, m_NewCP);
            m_StrokeB.Uncreate();
        }

        protected override void OnUndo()
        {
            ModifyStroke(m_StrokeA, m_InitialCP);
            m_StrokeB.Recreate();
            if (m_GroupA != null) { m_StrokeA.SetSymmetryGroup(m_GroupA, m_PointerA); }
            if (m_GroupB != null) { m_StrokeB.SetSymmetryGroup(m_GroupB, m_PointerB); }
        }

        private void ModifyStroke(Stroke stroke, IEnumerable<PointerManager.ControlPoint> newControlPoints)
        {
            stroke.m_ControlPoints = newControlPoints.ToArray();
            stroke.Uncreate();
            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Recreate(null, stroke.Canvas);
        }

        public override bool NeedsSave { get { return true; } }

    }
}
