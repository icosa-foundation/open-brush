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
    internal class TestAnimationTimelineModel
    {
        [Test]
        public void RebuildMergesHeldAndEmptyFramesIntoSpans()
        {
            var model = new AnimationTimelineModel();
            var drawing = new AnimationDrawingId(1);
            var frames = new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
            {
                new List<AnimationTimelineModel.FrameValue>
                {
                    new(drawing), new(drawing), new(AnimationDrawingId.Empty),
                    new(AnimationDrawingId.Empty), new(drawing)
                }
            };

            model.Rebuild(new[] { 10 }, new[] { true }, new[] { false }, frames);

            Assert.AreEqual(5, model.Length);
            Assert.AreEqual(3, model.Tracks[0].Spans.Count);
            Assert.AreEqual(2, model.Tracks[0].Spans[0].Duration);
            Assert.IsTrue(model.Tracks[0].Spans[1].Value.DrawingId.IsEmpty);
            Assert.AreEqual(2, model.Tracks[0].Spans[1].Duration);
        }

        [Test]
        public void ResolveReturnsContainingSpanAndStableDrawingLocation()
        {
            var model = new AnimationTimelineModel();
            var drawing = new AnimationDrawingId(7);
            var frames = new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
            {
                new List<AnimationTimelineModel.FrameValue>
                {
                    new(AnimationDrawingId.Empty), new(drawing), new(drawing)
                }
            };
            model.Rebuild(new[] { 42 }, new[] { true }, new[] { false }, frames);

            Assert.IsTrue(model.TryResolve(0, 2, out AnimationTimelineModel.Span span));
            Assert.AreEqual(drawing, span.Value.DrawingId);
            Assert.AreEqual(1, span.StartFrame);
            Assert.AreEqual(2, span.Duration);
            Assert.IsTrue(model.TryGetDrawingLocation(drawing, out (int, int) location));
            Assert.AreEqual((0, 1), location);
        }

        [Test]
        public void SpanBoundariesDoNotResolveOutsideTrack()
        {
            var model = new AnimationTimelineModel();
            var frames = new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
            {
                new List<AnimationTimelineModel.FrameValue> { new(new AnimationDrawingId(1)) }
            };
            model.Rebuild(new[] { 1 }, new[] { true }, new[] { false }, frames);

            Assert.IsFalse(model.TryResolve(0, -1, out _));
            Assert.IsFalse(model.TryResolve(0, 1, out _));
            Assert.IsFalse(model.TryResolve(1, 0, out _));
        }

        [Test]
        public void EmptyKeyIdentityPreservesSeparateSpansWhileHeldFramesMerge()
        {
            var model = new AnimationTimelineModel();
            var frames = new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
            {
                new List<AnimationTimelineModel.FrameValue>
                {
                    new(AnimationDrawingId.Empty, spanIdentity: 1),
                    new(AnimationDrawingId.Empty, spanIdentity: 1),
                    new(AnimationDrawingId.Empty, spanIdentity: 2)
                }
            };
            model.Rebuild(new[] { 9 }, new[] { true }, new[] { false }, frames);

            Assert.AreEqual(2, model.Tracks[0].Spans.Count);
            Assert.AreEqual(2, model.Tracks[0].Spans[0].Duration);
            Assert.AreEqual(1, model.Tracks[0].Spans[1].Duration);
            Assert.IsTrue(model.TryGetTrackIndex(9, out int trackIndex));
            Assert.AreEqual(0, trackIndex);
        }
    }
}
