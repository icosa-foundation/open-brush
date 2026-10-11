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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
            public SparseFrameList Frames { get; }

            internal EditableTrack(
                int id, bool visible, bool deleted, SparseFrameList frames)
            {
                Id = id;
                Visible = visible;
                Deleted = deleted;
                Frames = frames;
            }

            internal EditableTrack(
                int id, bool visible, bool deleted, IEnumerable<FrameValue> frames)
                : this(id, visible, deleted, SparseFrameList.FromValues(frames))
            {
            }
        }

        /// Mutable frame-compatible adapter backed by normalized spans. This preserves the
        /// command API while ensuring an edit does not allocate one FrameValue per timeline cell.
        public sealed class SparseFrameList : IList<FrameValue>
        {
            private readonly List<Span> m_Spans;
            private int m_Count;

            internal IReadOnlyList<Span> Spans => m_Spans;
            public int Count => m_Count;
            public bool IsReadOnly => false;

            internal SparseFrameList(int count, IEnumerable<Span> spans)
            {
                m_Count = count;
                m_Spans = spans.ToList();
                ValidateCoverage();
            }

            public FrameValue this[int index]
            {
                get
                {
                    int spanIndex = FindSpanIndex(index);
                    return m_Spans[spanIndex].Value;
                }
                set => ReplaceRange(index, 1, value);
            }

            public void Add(FrameValue item) => InsertRepeat(m_Count, 1, item);

            public void AddRange(IEnumerable<FrameValue> items)
            {
                if (items == null) throw new ArgumentNullException(nameof(items));
                using IEnumerator<FrameValue> enumerator = items.GetEnumerator();
                if (!enumerator.MoveNext()) return;
                FrameValue runValue = enumerator.Current;
                int runLength = 1;
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Equals(runValue))
                    {
                        runLength++;
                        continue;
                    }
                    InsertRepeat(m_Count, runLength, runValue);
                    runValue = enumerator.Current;
                    runLength = 1;
                }
                InsertRepeat(m_Count, runLength, runValue);
            }

            internal static SparseFrameList FromValues(IEnumerable<FrameValue> values)
            {
                if (values == null) throw new ArgumentNullException(nameof(values));
                var result = new SparseFrameList(0, Array.Empty<Span>());
                result.AddRange(values);
                return result;
            }

            internal static SparseFrameList FromRepeatedValue(FrameValue value, int count)
            {
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                var result = new SparseFrameList(0, Array.Empty<Span>());
                result.InsertRepeat(0, count, value);
                return result;
            }

            public void Clear()
            {
                m_Spans.Clear();
                m_Count = 0;
            }

            public bool Contains(FrameValue item) => IndexOf(item) >= 0;

            public void CopyTo(FrameValue[] array, int arrayIndex)
            {
                if (array == null) throw new ArgumentNullException(nameof(array));
                foreach (FrameValue value in this) array[arrayIndex++] = value;
            }

            public IEnumerator<FrameValue> GetEnumerator()
            {
                foreach (Span span in m_Spans)
                {
                    for (int frame = 0; frame < span.Duration; frame++) yield return span.Value;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public int IndexOf(FrameValue item)
            {
                foreach (Span span in m_Spans)
                {
                    if (span.Value.Equals(item)) return span.StartFrame;
                }
                return -1;
            }

            public int FindIndex(Predicate<FrameValue> match)
            {
                if (match == null) throw new ArgumentNullException(nameof(match));
                foreach (Span span in m_Spans)
                {
                    if (match(span.Value)) return span.StartFrame;
                }
                return -1;
            }

            public void Insert(int index, FrameValue item) => InsertRepeat(index, 1, item);

            public void InsertRepeat(int index, int count, FrameValue value)
            {
                if (index < 0 || index > m_Count) throw new ArgumentOutOfRangeException(nameof(index));
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                if (count == 0) return;

                var result = new List<Span>(m_Spans.Count + 2);
                foreach (Span span in m_Spans)
                {
                    if (span.EndFrameExclusive <= index)
                    {
                        AppendNormalized(result, span.StartFrame, span.Duration, span.Value);
                    }
                    else if (span.StartFrame >= index)
                    {
                        AppendNormalized(result, span.StartFrame + count, span.Duration, span.Value);
                    }
                    else
                    {
                        AppendNormalized(result, span.StartFrame, index - span.StartFrame, span.Value);
                        AppendNormalized(result, index + count,
                            span.EndFrameExclusive - index, span.Value);
                    }
                }
                AppendNormalized(result, index, count, value);
                result.Sort((left, right) => left.StartFrame.CompareTo(right.StartFrame));
                ReplaceSpansWithNormalized(result, m_Count + count);
            }

            public bool Remove(FrameValue item)
            {
                int index = IndexOf(item);
                if (index < 0) return false;
                RemoveAt(index);
                return true;
            }

            public void RemoveAt(int index) => RemoveRange(index, 1);

            public void RemoveRange(int index, int count)
            {
                ValidateRange(index, count);
                if (count == 0) return;
                int end = index + count;
                var result = new List<Span>(m_Spans.Count);
                foreach (Span span in m_Spans)
                {
                    if (span.EndFrameExclusive <= index)
                    {
                        AppendNormalized(result, span.StartFrame, span.Duration, span.Value);
                    }
                    else if (span.StartFrame >= end)
                    {
                        AppendNormalized(result, span.StartFrame - count, span.Duration, span.Value);
                    }
                    else
                    {
                        if (span.StartFrame < index)
                        {
                            AppendNormalized(result, span.StartFrame,
                                index - span.StartFrame, span.Value);
                        }
                        if (span.EndFrameExclusive > end)
                        {
                            AppendNormalized(result, index,
                                span.EndFrameExclusive - end, span.Value);
                        }
                    }
                }
                ReplaceSpansWithNormalized(result, m_Count - count);
            }

            public void ReplaceRange(int index, int count, FrameValue value)
            {
                ValidateRange(index, count);
                if (count == 0) return;
                int end = index + count;
                var result = new List<Span>(m_Spans.Count + 2);
                foreach (Span span in m_Spans)
                {
                    if (span.EndFrameExclusive <= index || span.StartFrame >= end)
                    {
                        AppendNormalized(result, span.StartFrame, span.Duration, span.Value);
                        continue;
                    }
                    if (span.StartFrame < index)
                    {
                        AppendNormalized(result, span.StartFrame,
                            index - span.StartFrame, span.Value);
                    }
                    if (span.EndFrameExclusive > end)
                    {
                        AppendNormalized(result, end,
                            span.EndFrameExclusive - end, span.Value);
                    }
                }
                AppendNormalized(result, index, count, value);
                result.Sort((left, right) => left.StartFrame.CompareTo(right.StartFrame));
                ReplaceSpansWithNormalized(result, m_Count);
            }

            private int FindSpanIndex(int index)
            {
                if (index < 0 || index >= m_Count) throw new ArgumentOutOfRangeException(nameof(index));
                int low = 0;
                int high = m_Spans.Count - 1;
                while (low <= high)
                {
                    int middle = low + ((high - low) / 2);
                    Span span = m_Spans[middle];
                    if (index < span.StartFrame) high = middle - 1;
                    else if (index >= span.EndFrameExclusive) low = middle + 1;
                    else return middle;
                }
                throw new InvalidOperationException($"Sparse frame index {index} has no span");
            }

            private void ValidateRange(int index, int count)
            {
                if (index < 0 || count < 0 || index + count > m_Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }

            private static void AppendNormalized(
                List<Span> spans, int start, int duration, FrameValue value)
            {
                if (duration <= 0) return;
                if (spans.Count > 0)
                {
                    Span previous = spans[spans.Count - 1];
                    if (previous.EndFrameExclusive == start && previous.Value.Equals(value))
                    {
                        spans[spans.Count - 1] = new Span(
                            previous.StartFrame, previous.Duration + duration, value);
                        return;
                    }
                }
                spans.Add(new Span(start, duration, value));
            }

            private void ReplaceSpansWithNormalized(IEnumerable<Span> spans, int count)
            {
                m_Spans.Clear();
                foreach (Span span in spans.OrderBy(span => span.StartFrame))
                {
                    AppendNormalized(m_Spans, span.StartFrame, span.Duration, span.Value);
                }
                m_Count = count;
                ValidateCoverage();
            }

            private void ValidateCoverage()
            {
                int nextStart = 0;
                foreach (Span span in m_Spans)
                {
                    if (span.StartFrame != nextStart)
                    {
                        throw new InvalidOperationException(
                            $"Sparse spans have a gap or overlap at frame {nextStart}");
                    }
                    nextStart = span.EndFrameExclusive;
                }
                if (nextStart != m_Count)
                {
                    throw new InvalidOperationException(
                        $"Sparse span coverage is {nextStart}, expected {m_Count}");
                }
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

        /// Applies an edit atomically through a frame-compatible sparse adapter. The callback can
        /// retain existing command logic without expanding the timeline into per-frame storage.
        public void ApplyEdit(Action<List<EditableTrack>> edit)
        {
            if (edit == null) throw new ArgumentNullException(nameof(edit));

            var editableTracks = new List<EditableTrack>(m_Tracks.Count);
            foreach (Track track in m_Tracks)
            {
                editableTracks.Add(new EditableTrack(
                    track.Id, track.Visible, track.Deleted,
                    new SparseFrameList(track.Length, track.Spans)));
            }

            edit(editableTracks);

            var uniqueTrackIds = new HashSet<int>();
            foreach (EditableTrack track in editableTracks)
            {
                if (track == null) throw new InvalidOperationException("An edited track cannot be null");
                if (!uniqueTrackIds.Add(track.Id))
                {
                    throw new InvalidOperationException($"Duplicate animation track ID {track.Id}");
                }
            }

            m_Tracks.Clear();
            m_TrackIdToIndex.Clear();
            m_DrawingLocations.Clear();
            Length = 0;
            for (int trackIndex = 0; trackIndex < editableTracks.Count; trackIndex++)
            {
                EditableTrack source = editableTracks[trackIndex];
                var spans = source.Frames.Spans.ToList();
                foreach (Span span in spans)
                {
                    IndexDrawing(span.Value.DrawingId, trackIndex, span.StartFrame);
                }
                m_TrackIdToIndex.Add(source.Id, trackIndex);
                m_Tracks.Add(new Track(
                    source.Id, source.Visible, source.Deleted, source.Frames.Count, spans));
                Length = Math.Max(Length, source.Frames.Count);
            }
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
