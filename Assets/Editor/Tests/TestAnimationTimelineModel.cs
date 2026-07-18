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
        public void LongEmptyDurationUsesOneSpanPerTrack()
        {
            const int duration = 10000;
            var emptyFrames = Enumerable.Repeat(
                new AnimationTimelineModel.FrameValue(
                    AnimationDrawingId.Empty, spanIdentity: 17), duration).ToArray();
            var model = new AnimationTimelineModel();

            model.Rebuild(
                new[] { 1, 2, 3 }, new[] { true, true, true },
                new[] { false, false, false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    emptyFrames, emptyFrames, emptyFrames
                });

            Assert.AreEqual(duration, model.Length);
            Assert.AreEqual(3, model.Tracks.Count);
            Assert.IsTrue(model.Tracks.All(track => track.Spans.Count == 1));
            Assert.IsTrue(model.Tracks.All(track => track.Spans[0].Duration == duration));
            Assert.IsTrue(model.Tracks.All(track => track.Spans[0].Value.DrawingId.IsEmpty));
        }

        [Test]
        public void RebuildRejectsDuplicateStableTrackIdsWithoutChangingExistingModel()
        {
            var drawing = new AnimationDrawingId(8);
            var model = new AnimationTimelineModel();
            model.Rebuild(
                new[] { 9 }, new[] { true }, new[] { false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new List<AnimationTimelineModel.FrameValue> { new(drawing) }
                });

            Assert.Throws<System.ArgumentException>(() => model.Rebuild(
                new[] { 4, 4 }, new[] { true, true }, new[] { false, false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new List<AnimationTimelineModel.FrameValue>(),
                    new List<AnimationTimelineModel.FrameValue>()
                }));

            Assert.AreEqual(1, model.Tracks.Count);
            Assert.AreEqual(9, model.Tracks[0].Id);
            Assert.IsTrue(model.TryResolve(0, 0, out AnimationTimelineModel.Span span));
            Assert.AreEqual(drawing, span.Value.DrawingId);
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

        [Test]
        public void SnapshotRestorePreservesSpansTracksAndDrawingLocations()
        {
            var model = new AnimationTimelineModel();
            var drawing = new AnimationDrawingId(12);
            var frames = new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
            {
                new List<AnimationTimelineModel.FrameValue>
                {
                    new(drawing), new(drawing),
                    new(AnimationDrawingId.Empty, spanIdentity: 4)
                }
            };
            model.Rebuild(new[] { 31 }, new[] { false }, new[] { true }, frames);
            AnimationTimelineModel.Snapshot snapshot = model.CreateSnapshot();

            model.Rebuild(
                new[] { 99 }, new[] { true }, new[] { false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new List<AnimationTimelineModel.FrameValue>
                    {
                        new(new AnimationDrawingId(50))
                    }
                });
            model.Restore(snapshot);

            Assert.AreEqual(3, model.Length);
            Assert.AreEqual(31, model.Tracks[0].Id);
            Assert.IsFalse(model.Tracks[0].Visible);
            Assert.IsTrue(model.Tracks[0].Deleted);
            Assert.AreEqual(2, model.Tracks[0].Spans.Count);
            Assert.IsTrue(model.TryGetDrawingLocation(drawing, out (int, int) location));
            Assert.AreEqual((0, 0), location);
            CollectionAssert.AreEquivalent(new[] { drawing }, snapshot.DrawingIds);
        }

        [Test]
        public void ApplyEditNormalizesInsertedAndReplacedFrames()
        {
            var model = new AnimationTimelineModel();
            var firstDrawing = new AnimationDrawingId(1);
            var secondDrawing = new AnimationDrawingId(2);
            model.Rebuild(
                new[] { 3 }, new[] { true }, new[] { false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new List<AnimationTimelineModel.FrameValue>
                    {
                        new(firstDrawing), new(firstDrawing), new(AnimationDrawingId.Empty)
                    }
                });

            model.ApplyEdit(tracks =>
            {
                tracks[0].Frames[1] = new AnimationTimelineModel.FrameValue(secondDrawing);
                tracks[0].Frames.Insert(2, new AnimationTimelineModel.FrameValue(secondDrawing));
                tracks[0].Visible = false;
            });

            Assert.AreEqual(4, model.Tracks[0].Length);
            Assert.AreEqual(3, model.Tracks[0].Spans.Count);
            Assert.AreEqual(firstDrawing, model.Tracks[0].Spans[0].Value.DrawingId);
            Assert.AreEqual(secondDrawing, model.Tracks[0].Spans[1].Value.DrawingId);
            Assert.AreEqual(2, model.Tracks[0].Spans[1].Duration);
            Assert.IsFalse(model.Tracks[0].Visible);
        }

        [Test]
        public void ApplyEditFailureLeavesSparseModelUnchanged()
        {
            var model = new AnimationTimelineModel();
            var drawing = new AnimationDrawingId(4);
            model.Rebuild(
                new[] { 7 }, new[] { true }, new[] { false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new List<AnimationTimelineModel.FrameValue> { new(drawing) }
                });

            Assert.Throws<System.InvalidOperationException>(() => model.ApplyEdit(tracks =>
            {
                tracks[0].Frames.Clear();
                throw new System.InvalidOperationException("cancel edit");
            }));

            Assert.AreEqual(1, model.Tracks[0].Length);
            Assert.IsTrue(model.TryResolve(0, 0, out AnimationTimelineModel.Span span));
            Assert.AreEqual(drawing, span.Value.DrawingId);
        }

        [Test]
        public void ApplyEditRejectsDuplicateStableTrackIds()
        {
            var model = new AnimationTimelineModel();
            model.Rebuild(
                new[] { 1, 2 }, new[] { true, true }, new[] { false, false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new List<AnimationTimelineModel.FrameValue>(),
                    new List<AnimationTimelineModel.FrameValue>()
                });

            Assert.Throws<System.InvalidOperationException>(() => model.ApplyEdit(tracks =>
            {
                tracks[1].Id = tracks[0].Id;
            }));
            Assert.AreEqual(2, model.Tracks.Count);
            Assert.AreEqual(2, model.Tracks[1].Id);
        }

        [Test]
        public void SerializableTrackIndexesExcludeDeletedTracksWithoutChangingStableIds()
        {
            var model = new AnimationTimelineModel();
            model.Rebuild(
                new[] { 10, 20, 30 }, new[] { true, true, true },
                new[] { false, true, false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new List<AnimationTimelineModel.FrameValue>(),
                    new List<AnimationTimelineModel.FrameValue>(),
                    new List<AnimationTimelineModel.FrameValue>()
                });

            Assert.IsTrue(model.TryGetSerializableTrackIndex(0, out int first));
            Assert.AreEqual(0, first);
            Assert.IsFalse(model.TryGetSerializableTrackIndex(1, out _));
            Assert.IsTrue(model.TryGetSerializableTrackIndex(2, out int third));
            Assert.AreEqual(1, third);
            Assert.AreEqual(30, model.Tracks[2].Id);
        }

        [Test]
        public void PersistenceProjectionUsesSpanStartsAndExcludesEmptyAndDeletedTracks()
        {
            var firstDrawing = new AnimationDrawingId(4);
            var deletedDrawing = new AnimationDrawingId(5);
            var lastDrawing = new AnimationDrawingId(6);
            var model = new AnimationTimelineModel();
            model.Rebuild(
                new[] { 1, 2, 3 }, new[] { true, true, false },
                new[] { false, true, false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new List<AnimationTimelineModel.FrameValue>
                    {
                        new(firstDrawing), new(firstDrawing),
                        new(AnimationDrawingId.Empty, spanIdentity: 1)
                    },
                    new List<AnimationTimelineModel.FrameValue> { new(deletedDrawing) },
                    new List<AnimationTimelineModel.FrameValue>
                    {
                        new(AnimationDrawingId.Empty, spanIdentity: 2), new(lastDrawing)
                    }
                });

            List<AnimationTimelineModel.SerializableDrawingLocation> locations =
                model.EnumerateSerializableDrawingLocations().ToList();

            Assert.AreEqual(2, locations.Count);
            Assert.AreEqual(firstDrawing, locations[0].DrawingId);
            Assert.AreEqual(0, locations[0].Frame);
            Assert.AreEqual(0, locations[0].Track);
            Assert.AreEqual(lastDrawing, locations[1].DrawingId);
            Assert.AreEqual(1, locations[1].Frame);
            Assert.AreEqual(1, locations[1].Track);
        }

        [Test]
        public void LegacyPersistenceProjectionRoundTripsHeldEmptyPathAndHiddenTracks()
        {
            object path = new object();
            var firstDrawing = new AnimationDrawingId(10);
            var secondDrawing = new AnimationDrawingId(11);
            var source = new AnimationTimelineModel();
            source.Rebuild(
                new[] { 100, 200, 300 }, new[] { true, false, true },
                new[] { false, false, true },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new List<AnimationTimelineModel.FrameValue>
                    {
                        new(firstDrawing), new(firstDrawing),
                        new(AnimationDrawingId.Empty, spanIdentity: 1),
                        new(AnimationDrawingId.Empty, spanIdentity: 1),
                        new(secondDrawing)
                    },
                    new List<AnimationTimelineModel.FrameValue>
                    {
                        new(AnimationDrawingId.Empty, pathToken: path, spanIdentity: 2),
                        new(AnimationDrawingId.Empty, pathToken: path, spanIdentity: 2),
                        new(AnimationDrawingId.Empty, spanIdentity: 3),
                        new(AnimationDrawingId.Empty, spanIdentity: 3),
                        new(AnimationDrawingId.Empty, spanIdentity: 3)
                    },
                    new List<AnimationTimelineModel.FrameValue>
                    {
                        new(new AnimationDrawingId(99))
                    }
                });

            List<AnimationTimelineModel.Track> serializedTracks = source.Tracks
                .Where(track => !track.Deleted)
                .ToList();
            long nextEmptyIdentity = 1000;
            var restoredFrames = new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>();
            foreach (AnimationTimelineModel.Track serializedTrack in serializedTracks)
            {
                List<AnimationTimelineModel.FrameValue> frames =
                    AnimationTimelineOperations.ExpandLegacyFrameLengths(
                        serializedTrack.Spans.Select(span => span.Duration).ToList(),
                        () => new AnimationTimelineModel.FrameValue(
                            AnimationDrawingId.Empty,
                            spanIdentity: nextEmptyIdentity++));
                foreach (AnimationTimelineModel.Span span in serializedTrack.Spans)
                {
                    if (span.Value.DrawingId.IsEmpty && span.Value.PathToken == null) continue;
                    for (int frame = span.StartFrame; frame < span.EndFrameExclusive; frame++)
                    {
                        AnimationTimelineModel.FrameValue empty = frames[frame];
                        frames[frame] = new AnimationTimelineModel.FrameValue(
                            span.Value.DrawingId, span.Value.Deleted, span.Value.FrameExists,
                            span.Value.PathToken,
                            span.Value.DrawingId.IsEmpty ? empty.SpanIdentity : 0);
                    }
                }
                restoredFrames.Add(frames);
            }

            var restored = new AnimationTimelineModel();
            restored.Rebuild(
                new[] { 1, 2 }, serializedTracks.Select(track => track.Visible).ToList(),
                new[] { false, false }, restoredFrames);

            Assert.AreEqual(2, restored.Tracks.Count);
            Assert.IsTrue(restored.Tracks[0].Visible);
            Assert.IsFalse(restored.Tracks[1].Visible);
            Assert.AreEqual(5, restored.Length);
            Assert.AreEqual(firstDrawing, restored.Tracks[0].Spans[0].Value.DrawingId);
            Assert.AreEqual(2, restored.Tracks[0].Spans[0].Duration);
            Assert.IsTrue(restored.Tracks[0].Spans[1].Value.DrawingId.IsEmpty);
            Assert.AreEqual(2, restored.Tracks[0].Spans[1].Duration);
            Assert.AreEqual(secondDrawing, restored.Tracks[0].Spans[2].Value.DrawingId);
            Assert.AreSame(path, restored.Tracks[1].Spans[0].Value.PathToken);
            Assert.AreEqual(2, restored.Tracks[1].Spans[0].Duration);
            Assert.AreEqual(3, restored.Tracks[1].Spans[1].Duration);
        }
    }
}
