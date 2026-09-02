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
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using TiltBrush.FrameAnimation;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace TiltBrush.Tests
{
    internal class TestAnimationSparseEditPerformance
    {
        private const string kLogPrefix = "[OB_ANIM_SPARSE_EDIT]";
        private const int kSamples = 3;

        [Test]
        [Category("AnimationPerformance")]
        public void HeldTimelineEditMatrix()
        {
            // Warm the JIT and generic collection paths outside the recorded samples.
            AnimationTimelineModel warmLegacy = CreateHeldModel(100);
            AnimationTimelineModel warmSparse = CreateHeldModel(100);
            ApplyExpandedEdit(warmLegacy);
            ApplySparseEdit(warmSparse);

            foreach (int duration in new[] { 100, 1000, 10000, 1000000 })
            {
                var expanded = new List<EditMeasurement>(kSamples);
                var sparse = new List<EditMeasurement>(kSamples);
                for (int sample = 0; sample < kSamples; sample++)
                {
                    AnimationTimelineModel expandedModel = CreateHeldModel(duration);
                    AnimationTimelineModel sparseModel = CreateHeldModel(duration);
                    if (sample % 2 == 0)
                    {
                        expanded.Add(Measure(() => ApplyExpandedEdit(expandedModel)));
                        sparse.Add(Measure(() => ApplySparseEdit(sparseModel)));
                    }
                    else
                    {
                        sparse.Add(Measure(() => ApplySparseEdit(sparseModel)));
                        expanded.Add(Measure(() => ApplyExpandedEdit(expandedModel)));
                    }

                    AssertEditedModel(expandedModel, duration);
                    AssertEditedModel(sparseModel, duration);
                }

                Log(duration, "expanded", expanded);
                Log(duration, "sparse", sparse);
                if (duration == 1000000)
                {
                    Assert.Less(
                        Median(sparse.Select(result => result.ManagedDeltaBytes)),
                        Median(expanded.Select(result => result.ManagedDeltaBytes)) / 10,
                        "A million-frame sparse edit should not allocate a dense frame list");
                }
            }
        }

        private static AnimationTimelineModel CreateHeldModel(int duration)
        {
            var held = new AnimationTimelineModel.FrameValue(
                AnimationDrawingId.Empty, spanIdentity: 1);
            var model = new AnimationTimelineModel();
            model.Rebuild(
                new[] { 1 }, new[] { true }, new[] { false },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>
                {
                    new RepeatedFrameValues(held, duration)
                });
            return model;
        }

        private static void ApplyExpandedEdit(AnimationTimelineModel model)
        {
            AnimationTimelineModel.Track track = model.Tracks[0];
            var frames = new List<AnimationTimelineModel.FrameValue>(track.Length);
            foreach (AnimationTimelineModel.Span span in track.Spans)
            {
                for (int frame = span.StartFrame; frame < span.EndFrameExclusive; frame++)
                {
                    frames.Add(span.Value);
                }
            }
            frames[track.Length / 2] = new AnimationTimelineModel.FrameValue(
                new AnimationDrawingId(9));
            model.Rebuild(
                new[] { track.Id }, new[] { track.Visible }, new[] { track.Deleted },
                new List<IReadOnlyList<AnimationTimelineModel.FrameValue>> { frames });
        }

        private static void ApplySparseEdit(AnimationTimelineModel model)
        {
            int frame = model.Tracks[0].Length / 2;
            model.ApplyEdit(tracks => tracks[0].Frames.ReplaceRange(
                frame, 1,
                new AnimationTimelineModel.FrameValue(new AnimationDrawingId(9))));
        }

        private static EditMeasurement Measure(Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long threadAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long managedBefore = GC.GetTotalMemory(false);
            long monoUsedBefore = Profiler.GetMonoUsedSizeLong();
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return new EditMeasurement(
                stopwatch.Elapsed.TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - threadAllocatedBefore,
                Math.Max(0, GC.GetTotalMemory(false) - managedBefore),
                Math.Max(0, Profiler.GetMonoUsedSizeLong() - monoUsedBefore));
        }

        private static void AssertEditedModel(AnimationTimelineModel model, int duration)
        {
            Assert.AreEqual(duration, model.Tracks[0].Length);
            Assert.AreEqual(3, model.Tracks[0].Spans.Count);
            Assert.AreEqual(
                new AnimationDrawingId(9), model.Tracks[0].Spans[1].Value.DrawingId);
        }

        private static void Log(
            int duration, string mode, IReadOnlyList<EditMeasurement> measurements)
        {
            Debug.Log(
                $"{kLogPrefix} workload=heldEdit mode={mode} frames={duration} " +
                $"samples={measurements.Count} " +
                $"medianMs={Median(measurements.Select(result => result.Milliseconds)):F4} " +
                $"worstMs={measurements.Max(result => result.Milliseconds):F4} " +
                $"medianThreadAllocatedBytes=" +
                $"{Median(measurements.Select(result => result.ThreadAllocatedBytes)):F0} " +
                $"medianManagedDeltaBytes=" +
                $"{Median(measurements.Select(result => result.ManagedDeltaBytes)):F0} " +
                $"medianMonoUsedDeltaBytes=" +
                $"{Median(measurements.Select(result => result.MonoUsedDeltaBytes)):F0}");
        }

        private static double Median(IEnumerable<double> values)
        {
            double[] ordered = values.OrderBy(value => value).ToArray();
            return ordered[ordered.Length / 2];
        }

        private static double Median(IEnumerable<long> values) =>
            Median(values.Select(value => (double)value));

        private readonly struct EditMeasurement
        {
            internal double Milliseconds { get; }
            internal long ThreadAllocatedBytes { get; }
            internal long ManagedDeltaBytes { get; }
            internal long MonoUsedDeltaBytes { get; }

            internal EditMeasurement(
                double milliseconds, long threadAllocatedBytes,
                long managedDeltaBytes, long monoUsedDeltaBytes)
            {
                Milliseconds = milliseconds;
                ThreadAllocatedBytes = threadAllocatedBytes;
                ManagedDeltaBytes = managedDeltaBytes;
                MonoUsedDeltaBytes = monoUsedDeltaBytes;
            }
        }

        private sealed class RepeatedFrameValues : IReadOnlyList<AnimationTimelineModel.FrameValue>
        {
            private readonly AnimationTimelineModel.FrameValue m_Value;
            public int Count { get; }

            internal RepeatedFrameValues(
                AnimationTimelineModel.FrameValue value, int count)
            {
                m_Value = value;
                Count = count;
            }

            public AnimationTimelineModel.FrameValue this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }
                    return m_Value;
                }
            }

            public IEnumerator<AnimationTimelineModel.FrameValue> GetEnumerator()
            {
                for (int index = 0; index < Count; index++) yield return m_Value;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
