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
using System.Linq;

namespace TiltBrush.FrameAnimation
{
    /// Value-only timeline transformations shared by runtime commands and unit tests.
    public static class AnimationTimelineOperations
    {
        public static int GetContentLength(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            Func<AnimationTimelineModel.FrameValue, bool> isFilled)
        {
            if (tracks == null) throw new ArgumentNullException(nameof(tracks));
            if (isFilled == null) throw new ArgumentNullException(nameof(isFilled));

            int lastFilledFrame = 0;
            foreach (AnimationTimelineModel.EditableTrack track in tracks)
            {
                for (int frameIndex = 0; frameIndex < track.Frames.Count; frameIndex++)
                {
                    if (isFilled(track.Frames[frameIndex]))
                    {
                        lastFilledFrame = Math.Max(lastFilledFrame, frameIndex);
                    }
                }
            }
            return lastFilledFrame + 1;
        }

        public static void PadTracks(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks, int length,
            Func<AnimationTimelineModel.FrameValue> createEmpty)
        {
            if (tracks == null) throw new ArgumentNullException(nameof(tracks));
            if (createEmpty == null) throw new ArgumentNullException(nameof(createEmpty));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

            foreach (AnimationTimelineModel.EditableTrack track in tracks)
            {
                if (track.Frames.Count >= length) continue;
                AnimationTimelineModel.FrameValue emptyValue = createEmpty();
                while (track.Frames.Count < length) track.Frames.Add(emptyValue);
            }
        }

        public static List<AnimationTimelineModel.FrameValue> ExpandLegacyFrameLengths(
            IReadOnlyList<int> frameLengths,
            Func<AnimationTimelineModel.FrameValue> createEmpty)
        {
            if (frameLengths == null) throw new ArgumentNullException(nameof(frameLengths));
            if (createEmpty == null) throw new ArgumentNullException(nameof(createEmpty));

            var frames = new List<AnimationTimelineModel.FrameValue>();
            foreach (int serializedLength in frameLengths)
            {
                int duration = Math.Max(1, serializedLength);
                AnimationTimelineModel.FrameValue empty = createEmpty();
                for (int frame = 0; frame < duration; frame++) frames.Add(empty);
            }
            return frames;
        }

        public static void NormalizeLength(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            Func<AnimationTimelineModel.FrameValue, bool> isFilled,
            Func<AnimationTimelineModel.FrameValue> createEmpty)
        {
            int contentLength = GetContentLength(tracks, isFilled);
            foreach (AnimationTimelineModel.EditableTrack track in tracks)
            {
                if (track.Frames.Count > contentLength)
                {
                    track.Frames.RemoveRange(contentLength, track.Frames.Count - contentLength);
                }
            }
            PadTracks(tracks, contentLength, createEmpty);
        }

        public static void RemoveSpan(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            int trackIndex, int startFrame, int duration,
            Func<AnimationTimelineModel.FrameValue> createEmpty,
            Func<AnimationTimelineModel.FrameValue, bool> isFilled)
        {
            ValidateSpan(tracks, trackIndex, startFrame, duration);
            AnimationTimelineModel.FrameValue emptyValue = createEmpty();
            for (int frame = startFrame; frame < startFrame + duration; frame++)
            {
                tracks[trackIndex].Frames[frame] = emptyValue;
            }
            NormalizeLength(tracks, isFilled, createEmpty);
        }

        public static bool MoveSpan(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            int trackIndex, int startFrame, int duration, bool moveRight,
            Func<AnimationTimelineModel.FrameValue> createEmpty,
            Func<AnimationTimelineModel.FrameValue, bool> isFilled,
            out int newStartFrame)
        {
            ValidateSpan(tracks, trackIndex, startFrame, duration);
            List<AnimationTimelineModel.FrameValue> frames = tracks[trackIndex].Frames;
            if (moveRight)
            {
                int followingFrame = startFrame + duration;
                if (followingFrame < frames.Count && isFilled(frames[followingFrame]))
                {
                    newStartFrame = startFrame;
                    return false;
                }

                AnimationTimelineModel.FrameValue moved = frames[startFrame];
                if (followingFrame >= frames.Count)
                {
                    frames[startFrame] = createEmpty();
                    frames.Add(moved);
                }
                else
                {
                    AnimationTimelineModel.FrameValue empty = frames[followingFrame];
                    frames[followingFrame] = moved;
                    frames[startFrame] = empty;
                }
                newStartFrame = startFrame + 1;
            }
            else
            {
                if (startFrame == 0 || isFilled(frames[startFrame - 1]))
                {
                    newStartFrame = startFrame;
                    return false;
                }

                int lastFrame = startFrame + duration - 1;
                AnimationTimelineModel.FrameValue empty = frames[startFrame - 1];
                frames[startFrame - 1] = frames[lastFrame];
                frames[lastFrame] = empty;
                newStartFrame = startFrame - 1;
            }

            NormalizeLength(tracks, isFilled, createEmpty);
            return true;
        }

        public static void InsertEmptyKey(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            int trackIndex, int frameIndex, bool insert,
            Func<AnimationTimelineModel.FrameValue> createEmpty,
            bool alignTracks)
        {
            ValidateTrack(tracks, trackIndex);
            List<AnimationTimelineModel.FrameValue> frames = tracks[trackIndex].Frames;
            if (frameIndex < 0 || frameIndex > frames.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex));
            }
            if (frameIndex == frames.Count) frames.Add(createEmpty());
            else if (insert) frames.Insert(frameIndex, createEmpty());

            if (alignTracks)
            {
                PadTracks(tracks, tracks.Max(track => track.Frames.Count), createEmpty);
            }
        }

        public static void ExtendSpan(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            int trackIndex, int startFrame, int duration,
            Func<AnimationTimelineModel.FrameValue> createEmpty,
            Func<AnimationTimelineModel.FrameValue, bool> isFilled)
        {
            ValidateSpan(tracks, trackIndex, startFrame, duration);
            int insertIndex = startFrame + duration;
            List<AnimationTimelineModel.FrameValue> targetFrames = tracks[trackIndex].Frames;
            bool insertAcrossTracks = insertIndex >= targetFrames.Count ||
                isFilled(targetFrames[insertIndex]);
            if (!insertAcrossTracks)
            {
                targetFrames[insertIndex] = targetFrames[startFrame];
                return;
            }

            for (int currentTrack = 0; currentTrack < tracks.Count; currentTrack++)
            {
                List<AnimationTimelineModel.FrameValue> frames = tracks[currentTrack].Frames;
                AnimationTimelineModel.FrameValue addingValue = currentTrack == trackIndex
                    ? frames[startFrame]
                    : createEmpty();
                AnimationTimelineModel.FrameValue paddingValue = createEmpty();
                while (frames.Count < insertIndex) frames.Add(paddingValue);
                frames.Insert(insertIndex, addingValue);
            }
        }

        public static bool ReduceSpan(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            int trackIndex, int startFrame, int duration,
            Func<AnimationTimelineModel.FrameValue> createEmpty,
            Func<AnimationTimelineModel.FrameValue, bool> isFilled)
        {
            ValidateSpan(tracks, trackIndex, startFrame, duration);
            if (duration <= 1) return false;
            tracks[trackIndex].Frames[startFrame + duration - 1] = createEmpty();
            NormalizeLength(tracks, isFilled, createEmpty);
            return true;
        }

        public static void ReplaceRange(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            int trackIndex, int startFrame, int duration,
            AnimationTimelineModel.FrameValue value)
        {
            ValidateSpan(tracks, trackIndex, startFrame, duration);
            for (int frame = startFrame; frame < startFrame + duration; frame++)
            {
                tracks[trackIndex].Frames[frame] = value;
            }
        }

        public static int ReplaceDrawingWithEmptySpans(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            AnimationDrawingId drawingId,
            Func<AnimationTimelineModel.FrameValue> createEmpty)
        {
            if (tracks == null) throw new ArgumentNullException(nameof(tracks));
            if (drawingId.IsEmpty) throw new ArgumentException(
                "Cannot demote the empty drawing sentinel", nameof(drawingId));
            if (createEmpty == null) throw new ArgumentNullException(nameof(createEmpty));

            int replacements = 0;
            foreach (AnimationTimelineModel.EditableTrack track in tracks)
            {
                AnimationTimelineModel.FrameValue emptyTemplate = default;
                bool replacingPreviousFrame = false;
                for (int frameIndex = 0; frameIndex < track.Frames.Count; frameIndex++)
                {
                    AnimationTimelineModel.FrameValue value = track.Frames[frameIndex];
                    bool replacing = value.DrawingId == drawingId;
                    if (replacing && !replacingPreviousFrame)
                    {
                        emptyTemplate = createEmpty();
                        if (!emptyTemplate.DrawingId.IsEmpty)
                        {
                            throw new InvalidOperationException(
                                "The empty frame factory returned a drawing");
                        }
                    }
                    if (replacing)
                    {
                        track.Frames[frameIndex] = new AnimationTimelineModel.FrameValue(
                            AnimationDrawingId.Empty, value.Deleted, value.FrameExists,
                            value.PathToken, emptyTemplate.SpanIdentity);
                        replacements++;
                    }
                    replacingPreviousFrame = replacing;
                }
            }
            return replacements;
        }

        public static void DuplicateRange(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            int trackIndex, int destinationFrame, int duration,
            AnimationTimelineModel.FrameValue value,
            Func<AnimationTimelineModel.FrameValue> createEmpty,
            Func<AnimationTimelineModel.FrameValue, bool> isFilled)
        {
            ValidateTrack(tracks, trackIndex);
            if (destinationFrame < 0 || duration <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            List<AnimationTimelineModel.FrameValue> frames = tracks[trackIndex].Frames;
            for (int frameOffset = 0; frameOffset < duration; frameOffset++)
            {
                int destination = destinationFrame + frameOffset;
                if (destination < frames.Count && !isFilled(frames[destination]))
                {
                    frames[destination] = value;
                }
                else
                {
                    frames.Insert(destination, value);
                }
            }
            PadTracks(tracks, tracks.Max(track => track.Frames.Count), createEmpty);
        }

        private static void ValidateTrack(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks, int trackIndex)
        {
            if (tracks == null) throw new ArgumentNullException(nameof(tracks));
            if (trackIndex < 0 || trackIndex >= tracks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(trackIndex));
            }
        }

        private static void ValidateSpan(
            IReadOnlyList<AnimationTimelineModel.EditableTrack> tracks,
            int trackIndex, int startFrame, int duration)
        {
            ValidateTrack(tracks, trackIndex);
            if (startFrame < 0 || duration <= 0 ||
                startFrame + duration > tracks[trackIndex].Frames.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startFrame));
            }
        }
    }
}
