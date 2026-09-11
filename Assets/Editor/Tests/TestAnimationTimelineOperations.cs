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
using System.Linq;
using NUnit.Framework;
using TiltBrush.FrameAnimation;

namespace TiltBrush.Tests
{
    internal class TestAnimationTimelineOperations
    {
        private static bool IsFilled(AnimationTimelineModel.FrameValue value) =>
            !value.DrawingId.IsEmpty || value.PathToken != null;

        private static AnimationTimelineModel.FrameValue Empty(long identity) =>
            new(AnimationDrawingId.Empty, spanIdentity: identity);

        private static AnimationTimelineModel.FrameValue Drawing(long id) =>
            new(new AnimationDrawingId(id));

        private static AnimationTimelineModel CreateModel(params AnimationTimelineModel.FrameValue[][] tracks)
        {
            var model = new AnimationTimelineModel();
            var ids = new List<int>();
            var visible = new List<bool>();
            var deleted = new List<bool>();
            var values = new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>();
            for (int track = 0; track < tracks.Length; track++)
            {
                ids.Add(track + 1);
                visible.Add(true);
                deleted.Add(false);
                values.Add(tracks[track]);
            }
            model.Rebuild(ids, visible, deleted, values);
            return model;
        }

        private static AnimationDrawingId ResolveDrawing(
            AnimationTimelineModel model, int track, int frame)
        {
            Assert.IsTrue(model.TryResolve(track, frame, out AnimationTimelineModel.Span span));
            return span.Value.DrawingId;
        }

        [Test]
        public void RemoveSpanTrimsTrailingEmptyDurationAndKeepsTracksAligned()
        {
            AnimationTimelineModel model = CreateModel(
                new[] { Drawing(1), Drawing(1), Empty(1), Empty(1) },
                new[] { Empty(2), Drawing(2), Empty(2), Empty(2) });
            long nextEmpty = 10;

            model.ApplyEdit(tracks => AnimationTimelineOperations.RemoveSpan(
                tracks, 0, 0, 2, () => Empty(nextEmpty++), IsFilled));

            Assert.AreEqual(2, model.Length);
            Assert.AreEqual(2, model.Tracks[0].Length);
            Assert.AreEqual(2, model.Tracks[1].Length);
            Assert.AreEqual(new AnimationDrawingId(2), ResolveDrawing(model, 1, 1));
            Assert.IsTrue(ResolveDrawing(model, 0, 0).IsEmpty);
        }

        [Test]
        public void LegacyFrameLengthsExpandOnceWithStableKeyBoundaries()
        {
            long nextEmpty = 100;

            List<AnimationTimelineModel.FrameValue> frames =
                AnimationTimelineOperations.ExpandLegacyFrameLengths(
                    new[] { 3, 0, 2 }, () => Empty(nextEmpty++));

            Assert.AreEqual(6, frames.Count);
            Assert.AreEqual(100, frames[0].SpanIdentity);
            Assert.AreEqual(100, frames[2].SpanIdentity);
            Assert.AreEqual(101, frames[3].SpanIdentity);
            Assert.AreEqual(102, frames[4].SpanIdentity);
            Assert.AreEqual(102, frames[5].SpanIdentity);

            AnimationTimelineModel model = CreateModel(frames.ToArray());
            Assert.AreEqual(3, model.Tracks[0].Spans.Count);
            CollectionAssert.AreEqual(
                new[] { 3, 1, 2 }, model.Tracks[0].Spans.Select(span => span.Duration));
        }

        [Test]
        public void MoveHeldSpanRightThenLeftRestoresResolvedFrames()
        {
            AnimationTimelineModel model = CreateModel(
                new[] { Drawing(1), Drawing(1) });
            AnimationTimelineModel.Snapshot original = model.CreateSnapshot();
            long nextEmpty = 20;

            model.ApplyEdit(tracks =>
            {
                Assert.IsTrue(AnimationTimelineOperations.MoveSpan(
                    tracks, 0, 0, 2, true, () => Empty(nextEmpty++), IsFilled,
                    out int movedTo));
                Assert.AreEqual(1, movedTo);
            });
            Assert.IsTrue(ResolveDrawing(model, 0, 0).IsEmpty);
            Assert.AreEqual(new AnimationDrawingId(1), ResolveDrawing(model, 0, 1));
            Assert.AreEqual(new AnimationDrawingId(1), ResolveDrawing(model, 0, 2));

            model.ApplyEdit(tracks =>
            {
                Assert.IsTrue(AnimationTimelineOperations.MoveSpan(
                    tracks, 0, 1, 2, false, () => Empty(nextEmpty++), IsFilled,
                    out int movedTo));
                Assert.AreEqual(0, movedTo);
            });

            AnimationTimelineModel restored = CreateModel(
                new[] { Drawing(9) });
            restored.Restore(original);
            for (int frame = 0; frame < restored.Length; frame++)
            {
                Assert.AreEqual(
                    ResolveDrawing(restored, 0, frame), ResolveDrawing(model, 0, frame));
            }
        }

        [Test]
        public void MoveIntoFilledDrawingFailsWithoutChangingTimeline()
        {
            AnimationTimelineModel model = CreateModel(
                new[] { Drawing(1), Drawing(2) });
            AnimationTimelineModel.Snapshot before = model.CreateSnapshot();

            model.ApplyEdit(tracks => Assert.IsFalse(AnimationTimelineOperations.MoveSpan(
                tracks, 0, 0, 1, true, () => Empty(4), IsFilled, out _)));

            Assert.AreEqual(2, model.Length);
            Assert.AreEqual(new AnimationDrawingId(1), ResolveDrawing(model, 0, 0));
            Assert.AreEqual(new AnimationDrawingId(2), ResolveDrawing(model, 0, 1));
            CollectionAssert.AreEquivalent(before.DrawingIds, model.CreateSnapshot().DrawingIds);
        }

        [Test]
        public void ExtendAgainstFilledFrameInsertsAlignedCellsOnEveryTrack()
        {
            AnimationTimelineModel model = CreateModel(
                new[] { Drawing(1), Drawing(2) },
                new[] { Drawing(3), Drawing(4) });
            long nextEmpty = 30;

            model.ApplyEdit(tracks => AnimationTimelineOperations.ExtendSpan(
                tracks, 0, 0, 1, () => Empty(nextEmpty++), IsFilled));

            Assert.AreEqual(3, model.Length);
            Assert.AreEqual(3, model.Tracks[0].Length);
            Assert.AreEqual(3, model.Tracks[1].Length);
            Assert.AreEqual(new AnimationDrawingId(1), ResolveDrawing(model, 0, 1));
            Assert.IsTrue(ResolveDrawing(model, 1, 1).IsEmpty);
            Assert.AreEqual(new AnimationDrawingId(4), ResolveDrawing(model, 1, 2));
        }

        [Test]
        public void DuplicateRangeUsesEmptyCellsThenInsertsAtCollision()
        {
            AnimationTimelineModel model = CreateModel(
                new[] { Drawing(1), Drawing(1), Empty(1), Drawing(2) },
                new[] { Drawing(3), Empty(2), Empty(2), Empty(2) });
            long nextEmpty = 40;

            model.ApplyEdit(tracks => AnimationTimelineOperations.DuplicateRange(
                tracks, 0, 2, 2, Drawing(9), () => Empty(nextEmpty++), IsFilled));

            Assert.AreEqual(5, model.Length);
            Assert.AreEqual(new AnimationDrawingId(9), ResolveDrawing(model, 0, 2));
            Assert.AreEqual(new AnimationDrawingId(9), ResolveDrawing(model, 0, 3));
            Assert.AreEqual(new AnimationDrawingId(2), ResolveDrawing(model, 0, 4));
            Assert.AreEqual(5, model.Tracks[1].Length);
        }

        [Test]
        public void DemotingDrawingPreservesBoundariesAndFrameMetadata()
        {
            var drawing = new AnimationDrawingId(7);
            object path = new object();
            AnimationTimelineModel model = CreateModel(new[]
            {
                new AnimationTimelineModel.FrameValue(drawing, deleted: true),
                new AnimationTimelineModel.FrameValue(drawing, deleted: true),
                Drawing(2),
                new AnimationTimelineModel.FrameValue(
                    drawing, frameExists: false, pathToken: path)
            });
            long nextEmpty = 70;

            model.ApplyEdit(tracks => Assert.AreEqual(
                3, AnimationTimelineOperations.ReplaceDrawingWithEmptySpans(
                    tracks, drawing, () => Empty(nextEmpty++))));

            Assert.AreEqual(4, model.Length);
            Assert.AreEqual(3, model.Tracks[0].Spans.Count);
            Assert.AreEqual(2, model.Tracks[0].Spans[0].Duration);
            Assert.IsTrue(model.Tracks[0].Spans[0].Value.DrawingId.IsEmpty);
            Assert.IsTrue(model.Tracks[0].Spans[0].Value.Deleted);
            Assert.AreEqual(70, model.Tracks[0].Spans[0].Value.SpanIdentity);
            Assert.IsTrue(model.Tracks[0].Spans[2].Value.DrawingId.IsEmpty);
            Assert.IsFalse(model.Tracks[0].Spans[2].Value.FrameExists);
            Assert.AreSame(path, model.Tracks[0].Spans[2].Value.PathToken);
            Assert.AreEqual(71, model.Tracks[0].Spans[2].Value.SpanIdentity);
        }

        [Test]
        public void RepeatedSnapshotRestorePreservesStableDrawingIdsAndTrackState()
        {
            AnimationTimelineModel model = CreateModel(
                new[] { Drawing(1), Drawing(1), Empty(1) },
                new[] { Empty(2), Drawing(2), Drawing(2) });
            AnimationTimelineModel.Snapshot before = model.CreateSnapshot();
            long nextEmpty = 50;
            model.ApplyEdit(tracks =>
            {
                tracks[1].Visible = false;
                AnimationTimelineOperations.ReplaceRange(tracks, 0, 1, 2, Drawing(3));
                AnimationTimelineOperations.InsertEmptyKey(
                    tracks, 1, 3, false, () => Empty(nextEmpty++), alignTracks: true);
            });
            AnimationTimelineModel.Snapshot after = model.CreateSnapshot();

            for (int iteration = 0; iteration < 25; iteration++)
            {
                model.Restore(before);
                Assert.IsTrue(model.Tracks[1].Visible);
                Assert.AreEqual(new AnimationDrawingId(1), ResolveDrawing(model, 0, 1));
                model.Restore(after);
                Assert.IsFalse(model.Tracks[1].Visible);
                Assert.AreEqual(new AnimationDrawingId(3), ResolveDrawing(model, 0, 1));
            }
        }
    }
}
