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
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using TiltBrush.FrameAnimation;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace TiltBrush.Tests
{
    internal class TestAnimationCanvasLifecycle
    {
        private const string kLogPrefix = "[OB_ANIM_P3_INTEGRATION]";

        [UnitySetUp]
        public IEnumerator EnterRuntime()
        {
            // LuaManager does not currently clear its private singleton when Play mode exits.
            // Isolate repeated Play-mode integration cases from that existing lifecycle bug.
            typeof(LuaManager).GetField(
                "m_Instance", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60f;
            while ((App.Scene == null || App.Scene.animationUI_manager == null ||
                    WidgetManager.m_Instance == null) &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsNotNull(App.Scene, "Open Brush scene did not initialize");
            Assert.IsNotNull(App.Scene.animationUI_manager,
                "Animation manager did not initialize");
            Assert.IsNotNull(WidgetManager.m_Instance,
                "Widget manager did not initialize");
        }

        [UnityTearDown]
        public IEnumerator ExitRuntime()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EmptyCanvasPromotionUndoAndSaveLeasePreserveCanvasLifetime()
        {
            Debug.Log($"{kLogPrefix} test=canvasLifetime state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(
                new List<IReadOnlyList<int>> { new List<int> { 1, 2 } },
                new List<bool> { true });

            Assert.AreEqual(3, manager.GetTimelineLength());
            CanvasScript frameZeroEmpty = manager.GetTimelineCanvas(0, 0);
            CanvasScript authoringCanvas = manager.GetTimelineCanvas(0, 1);
            Assert.AreSame(frameZeroEmpty, authoringCanvas,
                "One track should share one transient empty authoring Canvas");
            Assert.AreEqual(1, manager.Timeline[0].Frames
                .Select(frame => frame.Canvas).Distinct().Count());

            manager.SelectTimelineFrame(0, 1);
            var widgetObject = new GameObject("Animation lifecycle test widget");
            widgetObject.transform.SetParent(authoringCanvas.transform, false);
            GrabWidget widget = widgetObject.AddComponent<GrabWidget>();
            manager.NotifyDrawingContentChanged(authoringCanvas);

            Assert.IsTrue(manager.GetFrameFilled(0, 1));
            Assert.AreSame(authoringCanvas, manager.GetTimelineCanvas(0, 1));
            Assert.AreSame(authoringCanvas, manager.GetTimelineCanvas(0, 2));
            Assert.AreNotSame(authoringCanvas, manager.GetTimelineCanvas(0, 0));

            AnimationUI_Manager.DeleteFrameOperation undoOperation =
                manager.RemoveKeyFrame(0, 1);
            Assert.IsTrue(undoOperation.Succeeded);
            Assert.IsFalse(authoringCanvas == null,
                "Undo history must retain the displaced drawing Canvas");
            manager.UndoDeleteFrameOperation(undoOperation);
            Assert.AreSame(authoringCanvas, manager.GetTimelineCanvas(0, 1));
            Assert.IsTrue(manager.GetFrameFilled(0, 1));

            IDisposable saveLease = manager.RetainTimelineDrawingsForSave();
            AnimationUI_Manager.DeleteFrameOperation disposeOperation =
                manager.RemoveKeyFrame(0, 1);
            Assert.IsTrue(disposeOperation.Succeeded);
            manager.DiscardDeleteFrameOperationUndoState(disposeOperation);
            yield return null;
            Assert.IsFalse(authoringCanvas == null,
                "An in-progress save must retain a displaced drawing Canvas");

            WidgetManager.m_Instance.UnregisterGrabWidget(widgetObject);
            UnityEngine.Object.Destroy(widgetObject);
            saveLease.Dispose();
            yield return null;
            Assert.IsTrue(authoringCanvas == null,
                "The Canvas should be destroyed after its final owner releases it");
            Debug.Log($"{kLogPrefix} test=canvasLifetime state=passed");
        }

        [UnityTest]
        public IEnumerator LongHeldTimelineUsesSparseSpansAndDifferentialTraversal()
        {
            const int trackCount = 8;
            const int frameCount = 10000;
            const int transitionCount = 9;
            Debug.Log($"{kLogPrefix} test=longHeldPerformance state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();

            var frameLengths = new List<IReadOnlyList<int>>(trackCount);
            var visibility = new List<bool>(trackCount);
            for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                frameLengths.Add(new List<int> { frameCount });
                visibility.Add(true);
            }
            manager.ConfigureLegacyAnimationTracks(frameLengths, visibility);
            yield return null;

            manager.GetSparseTimelineCounts(out int spanCount, out int emptySpanCount);
            int uniqueTimelineCanvases = manager.Timeline
                .SelectMany(track => track.Frames)
                .Select(frame => frame.Canvas)
                .Where(canvas => canvas != null)
                .Distinct()
                .Count();
            Assert.AreEqual(trackCount * frameCount,
                manager.Timeline.Sum(track => track.Frames.Count));
            Assert.AreEqual(trackCount, spanCount,
                "Each long held track should normalize to one sparse span");
            Assert.AreEqual(trackCount, emptySpanCount);
            Assert.AreEqual(trackCount, uniqueTimelineCanvases,
                "Empty Canvas count must scale with tracks, not timeline cells");

            PlaybackMeasurement legacy = MeasurePlaybackTransitions(
                manager, differential: false, transitionCount: transitionCount);
            PlaybackMeasurement differential = MeasurePlaybackTransitions(
                manager, differential: true, transitionCount: transitionCount);

            long expectedLegacyVisits =
                (long)(frameCount - 1) * trackCount * transitionCount;
            Assert.AreEqual(expectedLegacyVisits, legacy.Counters.HideFrameVisits);
            Assert.AreEqual(0, differential.Counters.HideFrameVisits,
                "Differential playback must not traverse hidden timeline cells");
            Assert.AreEqual(transitionCount, differential.Counters.FocusFrameCalls);
            Assert.AreEqual(0, differential.Counters.CanvasVisibilityRequests,
                "Held drawings must not be deactivated and reactivated");

            Debug.Log(
                $"{kLogPrefix} test=longHeldPerformance state=passed tracks={trackCount} " +
                $"frames={frameCount} cells={trackCount * frameCount} spans={spanCount} " +
                $"uniqueTimelineCanvases={uniqueTimelineCanvases} transitions={transitionCount} " +
                $"legacyHideVisits={legacy.Counters.HideFrameVisits} " +
                $"differentialHideVisits={differential.Counters.HideFrameVisits} " +
                $"legacyMedianMs={legacy.MedianMilliseconds:F3} " +
                $"legacyWorstMs={legacy.WorstMilliseconds:F3} " +
                $"differentialMedianMs={differential.MedianMilliseconds:F3} " +
                $"differentialWorstMs={differential.WorstMilliseconds:F3}");
        }

        [UnityTest]
        public IEnumerator SnapshotWriteAndLoadRoundTripsSparseTrackTimingAndVisibility()
        {
            Debug.Log($"{kLogPrefix} test=snapshotRoundTrip state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(
                new List<IReadOnlyList<int>>
                {
                    new List<int> { 2, 3 },
                    new List<int> { 1, 4 }
                },
                new List<bool> { true, false });
            AnimationMetadata before = App.Scene.AnimationTracksSerialized();

            var serializer = new JsonSerializer
            {
                ContractResolver = new CustomJsonContractResolver()
            };
            var snapshot = new SketchSnapshot(
                serializer, saveIconCapture: null,
                out IEnumerator<Timeslice> snapshotConstructor, selectedOnly: false);
            while (snapshotConstructor.MoveNext()) yield return null;

            string path = Path.Combine(
                Application.temporaryCachePath,
                $"ob-animation-phase3-{Guid.NewGuid():N}.tilt");
            try
            {
                string writeError = snapshot.WriteSnapshotToFile(path);
                Assert.IsNull(writeError, $"Snapshot write failed: {writeError}");
                Assert.IsTrue(File.Exists(path));

                bool loaded = SaveLoadScript.m_Instance.Load(
                    new DiskSceneFileInfo(path, readOnly: true),
                    bAdditive: false, targetLayer: -1, out List<Stroke> loadedStrokes);
                Assert.IsTrue(loaded, "The just-written animation snapshot failed to load");
                Assert.IsNotNull(loadedStrokes);
                AnimationMetadata after = App.Scene.AnimationTracksSerialized();

                Assert.AreEqual(before.Tracks.Length, after.Tracks.Length);
                for (int trackIndex = 0; trackIndex < before.Tracks.Length; trackIndex++)
                {
                    CollectionAssert.AreEqual(
                        before.Tracks[trackIndex].frameLengths,
                        after.Tracks[trackIndex].frameLengths,
                        $"Track {trackIndex} timing changed during save/load");
                    Assert.AreEqual(
                        before.Tracks[trackIndex].Visible,
                        after.Tracks[trackIndex].Visible,
                        $"Track {trackIndex} visibility changed during save/load");
                }

                manager.GetSparseTimelineCounts(out int spanCount, out int emptySpanCount);
                int uniqueTimelineCanvases = manager.Timeline
                    .SelectMany(track => track.Frames)
                    .Select(frame => frame.Canvas)
                    .Where(canvas => canvas != null)
                    .Distinct()
                    .Count();
                Assert.AreEqual(4, spanCount);
                Assert.AreEqual(4, emptySpanCount);
                Assert.AreEqual(before.Tracks.Length, uniqueTimelineCanvases);
                Debug.Log(
                    $"{kLogPrefix} test=snapshotRoundTrip state=passed " +
                    $"tracks={after.Tracks.Length} spans={spanCount} " +
                    $"uniqueTimelineCanvases={uniqueTimelineCanvases}");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private readonly struct PlaybackMeasurement
        {
            internal AnimationPerformanceStats.CounterSnapshot Counters { get; }
            internal double MedianMilliseconds { get; }
            internal double WorstMilliseconds { get; }

            internal PlaybackMeasurement(
                AnimationPerformanceStats.CounterSnapshot counters,
                double medianMilliseconds, double worstMilliseconds)
            {
                Counters = counters;
                MedianMilliseconds = medianMilliseconds;
                WorstMilliseconds = worstMilliseconds;
            }
        }

        private static PlaybackMeasurement MeasurePlaybackTransitions(
            AnimationUI_Manager manager, bool differential, int transitionCount)
        {
            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: true, differential: differential);
            manager.ApplyPlaybackFrameForTests(0);
            manager.ResetPlaybackDiagnosticsForTests();
            var elapsedMilliseconds = new List<double>(transitionCount);
            var stopwatch = new Stopwatch();
            for (int transition = 0; transition < transitionCount; transition++)
            {
                int frame = transition % 2 == 0 ? 1 : 0;
                stopwatch.Restart();
                manager.ApplyPlaybackFrameForTests(frame);
                stopwatch.Stop();
                elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            elapsedMilliseconds.Sort();
            return new PlaybackMeasurement(
                manager.CapturePlaybackDiagnosticsForTests(),
                elapsedMilliseconds[elapsedMilliseconds.Count / 2],
                elapsedMilliseconds[elapsedMilliseconds.Count - 1]);
        }
    }
}
