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
    public class SnipStrokeCommand : BaseCommand
    {
        private Stroke m_InitialStroke;
        private Stroke m_NewStroke;

        private PointerManager.ControlPoint[] m_InitialCP;
        private PointerManager.ControlPoint[] m_SnippedCP1;
        private PointerManager.ControlPoint[] m_SnippedCP2;
        private bool[] m_InitialDrops;
        private bool[] m_SnippedDrops1;
        private bool[] m_SnippedDrops2;

        private int m_SnipIndex;
        private readonly SymmetryPeerEditing.BrokenLink m_BrokenLink;

        public SnipStrokeCommand(
            Stroke stroke, int snipIndex, BaseCommand parent = null)
            : this(stroke, snipIndex, parent, null, true)
        {
        }

        private SnipStrokeCommand(Stroke stroke, int snipIndex, BaseCommand parent,
            SymmetryStrokeGroup newGroup, bool linkPeers) : base(parent)
        {
            m_InitialStroke = stroke;
            m_SnipIndex = snipIndex;
            m_InitialCP = (PointerManager.ControlPoint[])stroke.m_ControlPoints.Clone();
            m_InitialDrops = (bool[])stroke.m_ControlPointsToDrop.Clone();
            m_NewStroke = SketchMemoryScript.m_Instance.DuplicateStroke(stroke, stroke.Canvas, null);
            m_SnippedCP1 = m_InitialCP.Take(m_SnipIndex).ToArray();
            m_SnippedCP2 = m_InitialCP.Skip(m_SnipIndex).ToArray();
            m_SnippedDrops1 = m_InitialDrops.Take(m_SnipIndex).ToArray();
            m_SnippedDrops2 = m_InitialDrops.Skip(m_SnipIndex).ToArray();

            if (newGroup != null)
            {
                m_NewStroke.JoinSymmetryGroup(newGroup, stroke.SymmetryPointerIndex);
            }

            if (linkPeers && stroke.SymmetryPeerGroup != null)
            {
                var group = stroke.SymmetryPeerGroup;
                if (CanSnipPeers(stroke))
                {
                    // Copies that no longer match the stroke point for point are derived from it
                    // first, so the same snip index cuts every copy at the same place. This runs
                    // now because the peer snips below record the copies as they are.
                    if (!PointCountsMatch(stroke))
                    {
                        new RederiveSymmetryGroupCommand(stroke, this).Redo();
                    }
                    var splitGroup = new SymmetryStrokeGroup(group.Mirror);
                    m_NewStroke.JoinSymmetryGroup(splitGroup, stroke.SymmetryPointerIndex);
                    foreach (var peer in SymmetryPeerEditing.PeersOf(stroke))
                    {
                        new SnipStrokeCommand(peer, snipIndex, this, splitGroup, false);
                    }
                }
                else
                {
                    // Only a group whose members aren't all in the mirror's canvas gets here.
                    m_BrokenLink = new SymmetryPeerEditing.BrokenLink(group);
                }
            }
        }

        /// Every visible copy can be snipped with the stroke: the mirror relates them all.
        /// Erased copies are left as they are.
        private static bool CanSnipPeers(Stroke stroke)
        {
            var group = stroke.SymmetryPeerGroup;
            if (group.Count < 2 || group.Mirror == null)
            {
                return false;
            }
            foreach (var peer in SymmetryPeerEditing.PeersOf(stroke))
            {
                if (!SymmetryPeerEditing.TryGetPeerSymmetryTransform(stroke, peer, out _))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool PointCountsMatch(Stroke stroke)
        {
            foreach (var peer in SymmetryPeerEditing.PeersOf(stroke))
            {
                if (peer.m_ControlPoints.Length != stroke.m_ControlPoints.Length) { return false; }
            }
            return true;
        }

        protected override void OnRedo()
        {
            m_BrokenLink?.Break();
            ModifyStroke(m_InitialStroke, m_SnippedCP1, m_SnippedDrops1);
            ModifyStroke(m_NewStroke, m_SnippedCP2, m_SnippedDrops2);
        }

        protected override void OnUndo()
        {
            ModifyStroke(m_InitialStroke, m_InitialCP, m_InitialDrops);

            switch (m_NewStroke.m_Type)
            {
                case Stroke.Type.BrushStroke:
                    BaseBrushScript brushScript = m_NewStroke.m_Object.GetComponent<BaseBrushScript>();
                    if (brushScript)
                    {
                        brushScript.HideBrush(true);
                    }
                    break;
                case Stroke.Type.BatchedBrushStroke:
                    m_NewStroke.m_BatchSubset.m_ParentBatch.DisableSubset(m_NewStroke.m_BatchSubset);
                    break;
            }
            m_BrokenLink?.Restore();
        }

        private void ModifyStroke(Stroke stroke, PointerManager.ControlPoint[] newControlPoints,
            bool[] droppedPoints)
        {
            stroke.m_ControlPoints = (PointerManager.ControlPoint[])newControlPoints.Clone();
            stroke.m_ControlPointsToDrop = (bool[])droppedPoints.Clone();
            stroke.InvalidateCopy();
            stroke.Uncreate();
            stroke.Recreate();
        }

        public override bool NeedsSave { get { return true; } }

    }
}
