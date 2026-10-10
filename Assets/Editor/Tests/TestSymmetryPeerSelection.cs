// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under
// the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
// ANY KIND, either express or implied. See the License for permissions and limitations.

using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    internal class TestSymmetryPeerSelection : MathTestUtils
    {
        [Test]
        public void PeerAddedAfterSelectionMovedFollowsOnlyLaterMovement()
        {
            var toPeer = new Plane(Vector3.right, Vector3.zero).ToTrTransform();
            var joined = TrTransform.T(new Vector3(5, 0, 0));
            var current = TrTransform.T(new Vector3(8, 0, 0));

            var peerMove = SymmetryPeerEditing.PeerSelectionMovement(toPeer, current, joined);
            AssertAlmostEqual(new Vector3(-4, 0, 0),
                peerMove.MultiplyPoint(new Vector3(-1, 0, 0)));
        }

        [Test]
        public void IdentityFinalSelectionCanStillMoveLateJoiningStroke()
        {
            var toPeer = new Plane(Vector3.right, Vector3.zero).ToTrTransform();
            var joined = TrTransform.T(new Vector3(5, 0, 0));
            var current = TrTransform.identity;

            var peerMove = SymmetryPeerEditing.PeerSelectionMovement(toPeer, current, joined);
            AssertAlmostEqual(new Vector3(4, 0, 0),
                peerMove.MultiplyPoint(new Vector3(-1, 0, 0)));
        }
    }
}
