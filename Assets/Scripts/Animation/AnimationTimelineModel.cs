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

using System;
using System.Collections.Generic;

namespace TiltBrush.FrameAnimation
{
    public readonly struct AnimationDrawingId : IEquatable<AnimationDrawingId>
    {
        public static readonly AnimationDrawingId Empty = new(0);

        public long Value { get; }
        public bool IsEmpty => Value == 0;

        public AnimationDrawingId(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public bool Equals(AnimationDrawingId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AnimationDrawingId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(AnimationDrawingId left, AnimationDrawingId right) => left.Equals(right);
        public static bool operator !=(AnimationDrawingId left, AnimationDrawingId right) => !left.Equals(right);
    }

    /// Sparse, Canvas-independent timeline. A compatibility frame list can be projected into this
    /// model while commands and persistence are migrated incrementally.
    public sealed class AnimationTimelineModel
    {
        /// A temporary, Canvas-independent edit view. It is expanded only for the duration of an
        /// atomic model edit; committed storage remains normalized spans.
        public sealed class EditableTrack
        {
            public int Id { get; set; }
            public bool Visible { get; set; }
            public bool Deleted { get; set; }
            public List<FrameValue> Frames { get; }

            internal EditableTrack(
                int id, bool visible, bool deleted, List<FrameValue> frames)
            {
                Id = id;
                Visible = visible;
                Deleted = deleted;
                Frames = frames;
            }
        }

        public sealed class Snapshot
        {
            internal IReadOnlyList<TrackSnapshot> Tracks { get; }
            public IReadOnlyList<AnimationDrawingId> DrawingIds { get; }

            internal Snapshot(
                IReadOnlyList<TrackSnapshot> tracks,
                IReadOnlyList<AnimationDrawingId> drawingIds)
            {
                Tracks = tracks;
                DrawingIds = drawingIds;
            }
        }

        internal sealed class TrackSnapshot
        {
            internal int Id { get; }
            internal bool Visible { get; }
            internal bool Deleted { get; }
            internal int Length { get; }
            internal IReadOnlyList<Span> Spans { get; }

            internal TrackSnapshot(
                int id, bool visible, bool deleted, int length, IReadOnlyList<Span> spans)
            {
                Id = id;
                Visible = visible;
                Deleted = deleted;
                Length = length;
                Spans = spans;
            }
        }

        public readonly struct FrameValue : IEquatable<FrameValue>
        {
            public AnimationDrawingId DrawingId { get; }
            public bool Deleted { get; }
            public bool FrameExists { get; }
            public object PathToken { get; }
            public long SpanIdentity { get; }

            public FrameValue(
                AnimationDrawingId drawingId, bool deleted = false, bool frameExists = true,
                object pathToken = null, long spanIdentity = 0)
            {
                DrawingId = drawingId;
                Deleted = deleted;
                FrameExists = frameExists;
                PathToken = pathToken;
                SpanIdentity = spanIdentity;
            }

            public bool Equals(FrameValue other)
            {
                return DrawingId == other.DrawingId && Deleted == other.Deleted &&
                    FrameExists == other.FrameExists && ReferenceEquals(PathToken, other.PathToken) &&
                    SpanIdentity == other.SpanIdentity;
            }

            public override bool Equals(object obj) => obj is FrameValue other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = DrawingId.GetHashCode();
                    hash = hash * 397 ^ Deleted.GetHashCode();
                    hash = hash * 397 ^ FrameExists.GetHashCode();
                    hash = hash * 397 ^ (PathToken?.GetHashCode() ?? 0);
                    hash = hash * 397 ^ SpanIdentity.GetHashCode();
                    return hash;
                }
            }
        }

        public readonly struct Span
        {
            public int StartFrame { get; }
            public int Duration { get; }
            public int EndFrameExclusive => StartFrame + Duration;
            public FrameValue Value { get; }

            public Span(int startFrame, int duration, FrameValue value)
            {
                if (startFrame < 0) throw new ArgumentOutOfRangeException(nameof(startFrame));
                if (duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
                StartFrame = startFrame;
                Duration = duration;
                Value = value;
            }

            public bool Contains(int frame) => frame >= StartFrame && frame < EndFrameExclusive;
        }

        public readonly struct SerializableDrawingLocation
        {
            public AnimationDrawingId DrawingId { get; }
            public int Frame { get; }
            public int Track { get; }

            public SerializableDrawingLocation(
                AnimationDrawingId drawingId, int frame, int track)
            {
                DrawingId = drawingId;
                Frame = frame;
                Track = track;
            }
        }

        public sealed class Track
        {
            private readonly List<Span> m_Spans;

            public int Id { get; }
            public bool Visible { get; }
            public bool Deleted { get; }
            public int Length { get; }
            public IReadOnlyList<Span> Spans => m_Spans;

            internal Track(int id, bool visible, bool deleted, int length, List<Span> spans)
            {
                Id = id;
                Visible = visible;
                Deleted = deleted;
                Length = length;
                m_Spans = spans;
            }

            public bool TryResolve(int frame, out Span span)
            {
                if (frame < 0 || frame >= Length)
                {
                    span = default;
                    return false;
                }

                int low = 0;
                int high = m_Spans.Count - 1;
                while (low <= high)
                {
                    int middle = low + ((high - low) / 2);
                    Span candidate = m_Spans[middle];
                    if (frame < candidate.StartFrame)
                    {
                        high = middle - 1;
                    }
                    else if (frame >= candidate.EndFrameExclusive)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        span = candidate;
                        return true;
                    }
                }
                span = default;
                return false;
            }
        }

        private readonly List<Track> m_Tracks = new();
        private readonly Dictionary<int, int> m_TrackIdToIndex = new();
        private readonly Dictionary<AnimationDrawingId, (int, int)> m_DrawingLocations = new();

        public IReadOnlyList<Track> Tracks => m_Tracks;
        public int Length { get; private set; }

        public void Rebuild(
            IReadOnlyList<int> trackIds, IReadOnlyList<bool> trackVisibility,
            IReadOnlyList<bool> trackDeletion, IReadOnlyList<IReadOnlyList<FrameValue>> frames)
        {
            if (trackIds == null || trackVisibility == null || trackDeletion == null || frames == null)
            {
                throw new ArgumentNullException();
            }
            if (trackIds.Count != frames.Count || trackVisibility.Count != frames.Count ||
                trackDeletion.Count != frames.Count)
            {
                throw new ArgumentException("Track metadata and frame lists must have equal lengths");
            }
            var uniqueTrackIds = new HashSet<int>();
            for (int trackIndex = 0; trackIndex < frames.Count; trackIndex++)
            {
                if (frames[trackIndex] == null)
                {
                    throw new ArgumentException($"Animation track {trackIds[trackIndex]} has no frames");
                }
                if (!uniqueTrackIds.Add(trackIds[trackIndex]))
                {
                    throw new ArgumentException(
                        $"Duplicate animation track ID {trackIds[trackIndex]}");
                }
            }

            m_Tracks.Clear();
            m_TrackIdToIndex.Clear();
            m_DrawingLocations.Clear();
            Length = 0;

            for (int trackIndex = 0; trackIndex < frames.Count; trackIndex++)
            {
                IReadOnlyList<FrameValue> trackFrames = frames[trackIndex];
                var spans = new List<Span>();
                if (trackFrames.Count > 0)
                {
                    int spanStart = 0;
                    FrameValue spanValue = trackFrames[0];
                    IndexDrawing(spanValue.DrawingId, trackIndex, 0);
                    for (int frameIndex = 1; frameIndex < trackFrames.Count; frameIndex++)
                    {
                        FrameValue value = trackFrames[frameIndex];
                        IndexDrawing(value.DrawingId, trackIndex, frameIndex);
                        if (value.Equals(spanValue)) continue;
                        spans.Add(new Span(spanStart, frameIndex - spanStart, spanValue));
                        spanStart = frameIndex;
                        spanValue = value;
                    }
                    spans.Add(new Span(spanStart, trackFrames.Count - spanStart, spanValue));
                }

                int trackId = trackIds[trackIndex];
                m_TrackIdToIndex[trackId] = trackIndex;
                m_Tracks.Add(new Track(
                    trackId, trackVisibility[trackIndex], trackDeletion[trackIndex],
                    trackFrames.Count, spans));
                Length = Math.Max(Length, trackFrames.Count);
            }
        }

        private void IndexDrawing(AnimationDrawingId drawingId, int trackIndex, int frameIndex)
        {
            if (!drawingId.IsEmpty && !m_DrawingLocations.ContainsKey(drawingId))
            {
                m_DrawingLocations.Add(drawingId, (trackIndex, frameIndex));
            }
        }

        public bool TryResolve(int trackIndex, int frameIndex, out Span span)
        {
            if (trackIndex < 0 || trackIndex >= m_Tracks.Count)
            {
                span = default;
                return false;
            }
            return m_Tracks[trackIndex].TryResolve(frameIndex, out span);
        }

        public bool TryGetDrawingLocation(AnimationDrawingId drawingId, out (int, int) location)
        {
            return m_DrawingLocations.TryGetValue(drawingId, out location);
        }

        public bool TryGetTrackIndex(int trackId, out int trackIndex)
        {
            return m_TrackIdToIndex.TryGetValue(trackId, out trackIndex);
        }

        public bool TryGetSerializableTrackIndex(int runtimeTrackIndex, out int serializedTrackIndex)
        {
            serializedTrackIndex = -1;
            if (runtimeTrackIndex < 0 || runtimeTrackIndex >= m_Tracks.Count ||
                m_Tracks[runtimeTrackIndex].Deleted)
            {
                return false;
            }

            serializedTrackIndex = 0;
            for (int trackIndex = 0; trackIndex < runtimeTrackIndex; trackIndex++)
            {
                if (!m_Tracks[trackIndex].Deleted) serializedTrackIndex++;
            }
            return true;
        }

        public IEnumerable<SerializableDrawingLocation> EnumerateSerializableDrawingLocations()
        {
            int serializedTrackIndex = 0;
            foreach (Track track in m_Tracks)
            {
                if (track.Deleted) continue;
                foreach (Span span in track.Spans)
                {
                    if (!span.Value.DrawingId.IsEmpty)
                    {
                        yield return new SerializableDrawingLocation(
                            span.Value.DrawingId, span.StartFrame, serializedTrackIndex);
                    }
                }
                serializedTrackIndex++;
            }
        }

        /// Applies an edit atomically. The callback operates on an expanded value view, and the
        /// result is normalized back into sparse spans only if the callback completes.
        public void ApplyEdit(Action<List<EditableTrack>> edit)
        {
            if (edit == null) throw new ArgumentNullException(nameof(edit));

            var editableTracks = new List<EditableTrack>(m_Tracks.Count);
            foreach (Track track in m_Tracks)
            {
                var frames = new List<FrameValue>(track.Length);
                foreach (Span span in track.Spans)
                {
                    for (int frame = span.StartFrame; frame < span.EndFrameExclusive; frame++)
                    {
                        frames.Add(span.Value);
                    }
                }
                if (frames.Count != track.Length)
                {
                    throw new InvalidOperationException(
                        $"Track {track.Id} span coverage is {frames.Count}, expected {track.Length}");
                }
                editableTracks.Add(new EditableTrack(
                    track.Id, track.Visible, track.Deleted, frames));
            }

            edit(editableTracks);

            var trackIds = new List<int>(editableTracks.Count);
            var trackVisibility = new List<bool>(editableTracks.Count);
            var trackDeletion = new List<bool>(editableTracks.Count);
            var framesByTrack = new List<IReadOnlyList<FrameValue>>(editableTracks.Count);
            var uniqueTrackIds = new HashSet<int>();
            foreach (EditableTrack track in editableTracks)
            {
                if (track == null) throw new InvalidOperationException("An edited track cannot be null");
                if (!uniqueTrackIds.Add(track.Id))
                {
                    throw new InvalidOperationException($"Duplicate animation track ID {track.Id}");
                }
                if (track.Frames == null)
                {
                    throw new InvalidOperationException($"Animation track {track.Id} has no frame list");
                }
                trackIds.Add(track.Id);
                trackVisibility.Add(track.Visible);
                trackDeletion.Add(track.Deleted);
                framesByTrack.Add(track.Frames);
            }
            Rebuild(trackIds, trackVisibility, trackDeletion, framesByTrack);
        }

        public Snapshot CreateSnapshot()
        {
            var tracks = new List<TrackSnapshot>(m_Tracks.Count);
            var drawingIds = new HashSet<AnimationDrawingId>();
            foreach (Track track in m_Tracks)
            {
                var spans = new List<Span>(track.Spans.Count);
                foreach (Span span in track.Spans)
                {
                    spans.Add(span);
                    if (!span.Value.DrawingId.IsEmpty)
                    {
                        drawingIds.Add(span.Value.DrawingId);
                    }
                }
                tracks.Add(new TrackSnapshot(
                    track.Id, track.Visible, track.Deleted, track.Length, spans.AsReadOnly()));
            }
            return new Snapshot(
                tracks.AsReadOnly(), new List<AnimationDrawingId>(drawingIds).AsReadOnly());
        }

        public void Restore(Snapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            m_Tracks.Clear();
            m_TrackIdToIndex.Clear();
            m_DrawingLocations.Clear();
            Length = 0;
            for (int trackIndex = 0; trackIndex < snapshot.Tracks.Count; trackIndex++)
            {
                TrackSnapshot source = snapshot.Tracks[trackIndex];
                var spans = new List<Span>(source.Spans.Count);
                foreach (Span span in source.Spans)
                {
                    spans.Add(span);
                    IndexDrawing(span.Value.DrawingId, trackIndex, span.StartFrame);
                }

                m_TrackIdToIndex.Add(source.Id, trackIndex);
                m_Tracks.Add(new Track(
                    source.Id, source.Visible, source.Deleted, source.Length, spans));
                Length = Math.Max(Length, source.Length);
            }
        }
    }
}
