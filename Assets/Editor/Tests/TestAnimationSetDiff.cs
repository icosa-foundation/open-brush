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

using System.Collections.Generic;
using NUnit.Framework;
using TiltBrush.FrameAnimation;

namespace TiltBrush.Tests
{
    internal class TestAnimationSetDiff
    {
        [Test]
        public void SameResolvedFrameDoesNotNeedApplying()
        {
            Assert.IsFalse(AnimationSetDiff.ShouldApplyFrame(4, 4));
            Assert.IsTrue(AnimationSetDiff.ShouldApplyFrame(4, 5));
        }

        [Test]
        public void HeldDrawingDoesNotToggle()
        {
            var previous = new HashSet<int> { 1, 2 };
            var next = new HashSet<int> { 1, 3 };
            var hide = new List<int>();
            var show = new List<int>();

            AnimationSetDiff.GetChanges(previous, next, hide, show);

            CollectionAssert.AreEquivalent(new[] { 2 }, hide);
            CollectionAssert.AreEquivalent(new[] { 3 }, show);
        }

        [Test]
        public void IdenticalVisibleSetsCauseNoWork()
        {
            var visible = new HashSet<int> { 1, 2 };
            var hide = new List<int>();
            var show = new List<int>();

            AnimationSetDiff.GetChanges(visible, visible, hide, show);

            Assert.IsEmpty(hide);
            Assert.IsEmpty(show);
        }
    }
}
