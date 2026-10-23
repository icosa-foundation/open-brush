// Copyright 2026 The Open Brush Authors
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

using NUnit.Framework;
using TiltBrush.FrameAnimation;

namespace TiltBrush.Tests
{
    internal class TestAnimationDrawingReferenceTracker
    {
        [Test]
        public void OverlappingUndoAndSaveOwnersReleaseOnlyAfterBothFinish()
        {
            var tracker = new AnimationDrawingReferenceTracker();
            var drawing = new AnimationDrawingId(11);

            tracker.Retain(new[] { drawing });
            tracker.Retain(new[] { drawing });

            Assert.AreEqual(2, tracker.GetReferenceCount(drawing));
            Assert.IsFalse(tracker.Release(drawing));
            Assert.IsTrue(tracker.IsRetained(drawing));
            Assert.IsTrue(tracker.Release(drawing));
            Assert.IsFalse(tracker.IsRetained(drawing));
        }

        [Test]
        public void EmptyDrawingNeverAcquiresCanvasLifetimeOwnership()
        {
            var tracker = new AnimationDrawingReferenceTracker();

            tracker.Retain(new[] { AnimationDrawingId.Empty });

            Assert.AreEqual(0, tracker.GetReferenceCount(AnimationDrawingId.Empty));
            Assert.IsFalse(tracker.Release(AnimationDrawingId.Empty));
        }

        [Test]
        public void RepeatedRetainReleaseCyclesReturnToZero()
        {
            var tracker = new AnimationDrawingReferenceTracker();
            var first = new AnimationDrawingId(1);
            var second = new AnimationDrawingId(2);

            for (int iteration = 0; iteration < 100; iteration++)
            {
                tracker.Retain(new[] { first, second });
                Assert.IsTrue(tracker.Release(first));
                Assert.IsTrue(tracker.Release(second));
            }

            Assert.AreEqual(0, tracker.GetReferenceCount(first));
            Assert.AreEqual(0, tracker.GetReferenceCount(second));
        }

        [Test]
        public void ReleasingUnknownDrawingDoesNotAffectExistingOwners()
        {
            var tracker = new AnimationDrawingReferenceTracker();
            var retained = new AnimationDrawingId(4);
            tracker.Retain(new[] { retained });

            Assert.IsFalse(tracker.Release(new AnimationDrawingId(99)));

            Assert.AreEqual(1, tracker.GetReferenceCount(retained));
        }
    }
}
