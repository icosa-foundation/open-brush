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
            while (App.CurrentState != App.AppState.Standard &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(
                App.AppState.Standard, App.CurrentState,
                "Open Brush did not finish entering its normal interactive state");
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
            Assert.IsTrue(manager.TryGetFrameDrawingForTests(
                authoringCanvas, out FrameDrawing promotedDrawing));
            long revisionBeforeChange = promotedDrawing.ContentRevision;
            manager.NotifyDrawingContentChanged(authoringCanvas);
            Assert.AreEqual(revisionBeforeChange + 1, promotedDrawing.ContentRevision,
                "Canvas content notifications must update the logical drawing revision");

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
        public IEnumerator FrameCoordinateConsumerMaterializesOnlyRequestedSpan()
        {
            Debug.Log($"{kLogPrefix} test=frameCoordinateConsumer state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(
                new List<IReadOnlyList<int>> { new List<int> { 2, 3 } },
                new List<bool> { true });

            CanvasScript initialEmptyCanvas = manager.GetTimelineCanvas(0, 0);
            Assert.AreSame(initialEmptyCanvas, manager.GetTimelineCanvas(0, 4));

            CanvasScript importedContentCanvas = App.Scene.GetOrCreateLayer(0, 3);
            Assert.AreNotSame(initialEmptyCanvas, importedContentCanvas);
            Assert.AreSame(initialEmptyCanvas, manager.GetTimelineCanvas(0, 0));
            Assert.AreSame(initialEmptyCanvas, manager.GetTimelineCanvas(0, 1));
            Assert.AreSame(importedContentCanvas, manager.GetTimelineCanvas(0, 2));
            Assert.AreSame(importedContentCanvas, manager.GetTimelineCanvas(0, 3));
            Assert.AreSame(importedContentCanvas, manager.GetTimelineCanvas(0, 4));
            Assert.AreEqual((0, 2), manager.GetCanvasLocation(importedContentCanvas));

            var widgetObject = new GameObject("Animation coordinate consumer test widget");
            widgetObject.transform.SetParent(importedContentCanvas.transform, false);
            widgetObject.AddComponent<GrabWidget>();
            manager.NotifyDrawingContentChanged(importedContentCanvas);
            Assert.IsTrue(manager.GetFrameFilled(0, 3));

            manager.GetSparseTimelineCounts(out int spanCount, out int emptySpanCount);
            Assert.AreEqual(2, spanCount);
            Assert.AreEqual(1, emptySpanCount);
            Assert.AreEqual(2, manager.Timeline[0].Frames
                .Select(frame => frame.Canvas).Distinct().Count());

            AnimationMetadata metadata = App.Scene.AnimationTracksSerialized();
            Assert.AreEqual(AnimationMetadata.CurrentVersion, metadata.Version);
            Assert.AreEqual(1, metadata.Tracks.Length);
            Assert.IsNull(metadata.Tracks[0].frameLengths);
            CollectionAssert.AreEqual(new[] { 2, 3 },
                metadata.Tracks[0].Spans.Select(span => span.Duration));

            WidgetManager.m_Instance.UnregisterGrabWidget(widgetObject);
            UnityEngine.Object.Destroy(widgetObject);
            yield return null;
            Debug.Log($"{kLogPrefix} test=frameCoordinateConsumer state=passed");
        }

        [UnityTest]
        public IEnumerator DenseEmptyFallbackPreservesSparseAndPersistenceSemantics()
        {
            Debug.Log($"{kLogPrefix} test=denseEmptyFallback state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            var frameLengths = new List<IReadOnlyList<int>>
            {
                new List<int> { 2, 3 },
                new List<int> { 1, 4 }
            };
            var visibility = new List<bool> { true, false };

            manager.ConfigureEmptyCanvasSharingForTests(true);
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(frameLengths, visibility);
            AnimationMetadata sharedMetadata = App.Scene.AnimationTracksSerialized();
            manager.GetSparseTimelineCounts(
                out int sharedSpanCount, out int sharedEmptySpanCount);
            int sharedCanvasCount = manager.Timeline
                .SelectMany(track => track.Frames)
                .Select(frame => frame.Canvas)
                .Distinct()
                .Count();

            try
            {
                manager.ConfigureEmptyCanvasSharingForTests(false);
                manager.StartTimeline();
                manager.ConfigureLegacyAnimationTracks(frameLengths, visibility);
                AnimationMetadata denseMetadata = App.Scene.AnimationTracksSerialized();
                manager.GetSparseTimelineCounts(
                    out int denseSpanCount, out int denseEmptySpanCount);
                int denseCanvasCount = manager.Timeline
                    .SelectMany(track => track.Frames)
                    .Select(frame => frame.Canvas)
                    .Distinct()
                    .Count();

                Assert.AreEqual(2, sharedCanvasCount,
                    "Shared empty mode should retain one authoring Canvas per track");
                Assert.AreEqual(4, denseCanvasCount,
                    "Diagnostic fallback should materialize one Canvas per legacy key span");
                Assert.AreEqual(sharedSpanCount, denseSpanCount);
                Assert.AreEqual(sharedEmptySpanCount, denseEmptySpanCount);
                Assert.AreEqual(sharedMetadata.Tracks.Length, denseMetadata.Tracks.Length);
                for (int trackIndex = 0;
                    trackIndex < sharedMetadata.Tracks.Length; trackIndex++)
                {
                    CollectionAssert.AreEqual(
                        sharedMetadata.Tracks[trackIndex].Spans.Select(span => span.Duration),
                        denseMetadata.Tracks[trackIndex].Spans.Select(span => span.Duration));
                    Assert.AreEqual(
                        sharedMetadata.Tracks[trackIndex].Visible,
                        denseMetadata.Tracks[trackIndex].Visible);
                }
            }
            finally
            {
                manager.ConfigureEmptyCanvasSharingForTests(true);
            }

            yield return null;
            Debug.Log($"{kLogPrefix} test=denseEmptyFallback state=passed");
        }

        [UnityTest]
        public IEnumerator DifferentialAndLegacyPlaybackProduceEquivalentVisibleSets()
        {
            Debug.Log($"{kLogPrefix} test=playbackFallbackEquivalence state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(
                new List<IReadOnlyList<int>>
                {
                    new List<int> { 1, 1, 1 },
                    new List<int> { 1, 1, 1 }
                },
                new List<bool> { true, false });

            for (int trackIndex = 0; trackIndex < 2; trackIndex++)
            {
                for (int frameIndex = 0; frameIndex < 3; frameIndex++)
                {
                    manager.GetOrCreateContentCanvas(trackIndex, frameIndex);
                }
            }

            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: false, differential: false);
            manager.ApplyPlaybackFrameForTests(2);
            var legacyVisibleSets = new List<HashSet<CanvasScript>>();
            for (int frameIndex = 0; frameIndex < 3; frameIndex++)
            {
                manager.ApplyPlaybackFrameForTests(frameIndex);
                legacyVisibleSets.Add(CaptureActiveTimelineCanvases(manager));
            }

            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: false, differential: true);
            manager.SelectTimelineFrame(0, 0);
            var differentialVisibleSets = new List<HashSet<CanvasScript>>();
            for (int frameIndex = 0; frameIndex < 3; frameIndex++)
            {
                manager.ApplyPlaybackFrameForTests(frameIndex);
                differentialVisibleSets.Add(CaptureActiveTimelineCanvases(manager));
            }

            for (int frameIndex = 0; frameIndex < 3; frameIndex++)
            {
                CollectionAssert.AreEquivalent(
                    legacyVisibleSets[frameIndex], differentialVisibleSets[frameIndex],
                    $"Playback paths disagreed at frame {frameIndex}");
                Assert.AreEqual(1, differentialVisibleSets[frameIndex].Count,
                    $"Only the visible track drawing should be active at frame {frameIndex}");
                Assert.Contains(
                    manager.GetTimelineCanvas(0, frameIndex),
                    differentialVisibleSets[frameIndex].ToList());
                Assert.IsFalse(manager.GetTimelineCanvas(1, frameIndex).gameObject.activeSelf,
                    $"Hidden track drawing became active at frame {frameIndex}");
            }

            yield return null;
            Debug.Log($"{kLogPrefix} test=playbackFallbackEquivalence state=passed");
        }

        [UnityTest]
        public IEnumerator MissingDrawingIndexRecoversFromFrameAdapterOnce()
        {
            Debug.Log($"{kLogPrefix} test=indexRecovery state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(
                new List<IReadOnlyList<int>> { new List<int> { 1, 1 } },
                new List<bool> { true });
            CanvasScript drawingCanvas = manager.GetOrCreateContentCanvas(0, 1);
            Assert.AreEqual((0, 1), manager.GetCanvasLocation(drawingCanvas));

            Assert.IsTrue(manager.RemoveDrawingCanvasIndexForTests(drawingCanvas));

            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: true, differential: true);
            manager.ResetPlaybackDiagnosticsForTests();
            Assert.AreEqual((0, 1), manager.GetCanvasLocation(drawingCanvas));
            AnimationPerformanceStats.CounterSnapshot recoveryCounters =
                manager.CapturePlaybackDiagnosticsForTests();
            Assert.Greater(recoveryCounters.LocationCellsVisited, 0,
                "A missing index entry should use the development compatibility scan");

            manager.ResetPlaybackDiagnosticsForTests();
            Assert.AreEqual((0, 1), manager.GetCanvasLocation(drawingCanvas));
            AnimationPerformanceStats.CounterSnapshot indexedCounters =
                manager.CapturePlaybackDiagnosticsForTests();
            Assert.AreEqual(0, indexedCounters.LocationCellsVisited,
                "The repaired index should make the next query constant-time");
            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: false, differential: true);

            yield return null;
            Debug.Log($"{kLogPrefix} test=indexRecovery state=passed");
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
            Assert.AreEqual(0, differential.Counters.TimelineResets,
                "Ordinary playback must not rebuild timeline structure");
            Assert.AreEqual(0, differential.Counters.LayerEvents,
                "Ordinary playback must not broadcast structural layer events");
            Assert.AreEqual(0, differential.Counters.GlobalStrokeScans,
                "Ordinary playback must not scan the global stroke list");

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
        public IEnumerator TimelineWidgetPoolDoesNotGrowWithTimelineDuration()
        {
            const int trackCount = 8;
            Debug.Log($"{kLogPrefix} test=boundedTimelineWidgets state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();
            manager.ConfigurePlaybackDiagnosticsForTests(enabled: true, differential: true);

            ConfigureHeldTracks(manager, trackCount, frameCount: 100);
            manager.ResetTimeline();
            yield return null;
            int shortNotches = manager.frameNotchesWidget.transform.childCount;
            int shortFrameButtons = manager.trackNodesWidget.Sum(
                trackNodes => trackNodes.transform.childCount);

            ConfigureHeldTracks(manager, trackCount, frameCount: 10000);
            manager.ResetPlaybackDiagnosticsForTests();
            manager.ResetTimeline();
            yield return null;
            int longNotches = manager.frameNotchesWidget.transform.childCount;
            int longFrameButtons = manager.trackNodesWidget.Sum(
                trackNodes => trackNodes.transform.childCount);
            AnimationPerformanceStats.CounterSnapshot counters =
                manager.CapturePlaybackDiagnosticsForTests();

            Assert.AreEqual(10000, manager.GetTimelineLength(),
                "The long-duration fixture must remain configured during verification");
            Assert.Greater(shortNotches, 0,
                "The test must observe an instantiated timeline-notch pool");
            Assert.Greater(shortFrameButtons, 0,
                "The test must observe instantiated frame-button pools");
            Assert.AreEqual(shortNotches, longNotches,
                "Timeline notch allocation must be bounded by the visible frame pool");
            Assert.AreEqual(shortFrameButtons, longFrameButtons,
                "Frame-button allocation must be bounded by visible tracks and frames");
            Assert.AreEqual(0, counters.GlobalStrokeScans,
                "A duration-only structural refresh must use the occupancy index");
            Debug.Log(
                $"{kLogPrefix} test=boundedTimelineWidgets state=passed " +
                $"tracks={trackCount} shortFrames=100 longFrames=10000 " +
                $"notches={longNotches} frameButtons={longFrameButtons}");
        }

        [UnityTest]
        public IEnumerator PureBrushPlaybackProxyFallsBackPerDrawingAndRestoresCanvasRendering()
        {
            const string proxyLogPrefix = "[OB_ANIM_P4_PROXY]";
            Debug.Log($"{proxyLogPrefix} test=mixedPlayback state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(
                new List<IReadOnlyList<int>> { new[] { 1, 1 } },
                new List<bool> { true });
            CanvasScript drawingCanvas = manager.GetOrCreateContentCanvas(0, 1);
            Stroke stroke = LoadFirstPerformanceStroke("Simple.tilt");
            BatchSubset subset = TestBrush.CreateSubsetFromStroke(drawingCanvas, stroke);
            Assert.IsNotNull(subset);
            stroke.m_Type = Stroke.Type.BatchedBrushStroke;
            stroke.m_BatchSubset = subset;
            subset.m_Stroke = stroke;
            drawingCanvas.BatchManager.FlushMeshUpdates();
            manager.NotifyStrokeAdded(stroke);

            manager.ConfigureDrawingRenderProxiesForTests(enabled: true);
            manager.ApplyPlaybackFrameForTests(1);
            Assert.IsFalse(drawingCanvas.gameObject.activeSelf,
                "An eligible playback drawing should render through its track proxy");
            Assert.AreEqual(1, manager.GetVisibleDrawingRenderProxyCountForTests());
            Assert.IsTrue(manager.TryGetDrawingRenderProxyForTests(
                manager.Timeline[0].Id, out CanvasBatchRenderProxy proxy));
            Assert.AreEqual(
                FrameDrawingRenderMetrics.CaptureBatches(drawingCanvas), proxy.Metrics);
            var initialWork = manager.GetDrawingRenderProxyWorkCountsForTests();
            manager.ApplyPlaybackFrameForTests(1);
            manager.ApplyPlaybackFrameForTests(1);
            Assert.AreEqual(initialWork, manager.GetDrawingRenderProxyWorkCountsForTests(),
                "An unchanged held frame must not reclassify or rebuild its drawing proxy");

            manager.ConfigureDrawingRenderProxiesForTests(enabled: false);
            Assert.IsTrue(drawingCanvas.gameObject.activeSelf,
                "Disabling proxies must immediately restore Canvas rendering");
            Assert.AreEqual(0, manager.GetVisibleDrawingRenderProxyCountForTests());

            var widgetObject = new GameObject("Proxy fallback widget");
            widgetObject.transform.SetParent(drawingCanvas.transform, false);
            GrabWidget widget = widgetObject.AddComponent<GrabWidget>();
            manager.NotifyWidgetAdded(widget);
            manager.ConfigureDrawingRenderProxiesForTests(enabled: true);
            manager.ApplyPlaybackFrameForTests(1);
            Assert.IsTrue(drawingCanvas.gameObject.activeSelf,
                "Widget-bearing drawings must remain on the Canvas path");
            Assert.AreEqual(0, manager.GetVisibleDrawingRenderProxyCountForTests());
            manager.ConfigureDrawingRenderProxiesForTests(enabled: false);

            yield return null;
            Debug.Log(
                $"{proxyLogPrefix} test=mixedPlayback state=passed " +
                $"batches={proxy.Metrics.Batches} meshes={proxy.Metrics.Meshes}");
        }

        [UnityTest]
        public IEnumerator SnapshotWriteAndLoadRoundTripsSparseTrackTimingAndVisibility()
        {
            Debug.Log($"{kLogPrefix} test=snapshotRoundTrip state=started");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            manager.StartTimeline();
            App.Scene.AddLayerNow();
            manager.ConfigureLegacyAnimationTracks(
                new List<IReadOnlyList<int>>
                {
                    new List<int> { 2, 3 },
                    new List<int> { 1, 4 }
                },
                new List<bool> { true, false });
            AnimationMetadata before = App.Scene.AnimationTracksSerialized();
            manager.GetSparseTimelineCounts(
                out int beforeSpanCount, out int beforeEmptySpanCount);
            int beforeUniqueTimelineCanvases = manager.Timeline
                .SelectMany(track => track.Frames)
                .Select(frame => frame.Canvas)
                .Where(canvas => canvas != null)
                .Distinct()
                .Count();

            var serializer = new JsonSerializer
            {
                ContractResolver = new CustomJsonContractResolver()
            };
            var snapshot = new SketchSnapshot(
                serializer, saveIconCapture: null,
                out IEnumerator<Timeslice> snapshotConstructor, selectedOnly: false);
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = snapshotConstructor.MoveNext();
                }
                catch (Exception exception)
                {
                    Assert.Fail(
                        $"Snapshot construction threw before save: {exception}");
                    yield break;
                }
                if (!hasNext) break;
                yield return null;
            }

            string path = Path.Combine(
                Application.temporaryCachePath,
                $"ob-animation-phase3-{Guid.NewGuid():N}.tilt");
            try
            {
                string writeError = snapshot.WriteSnapshotToFile(path);
                Assert.IsNull(writeError, $"Snapshot write failed: {writeError}");
                Assert.IsTrue(File.Exists(path));

                bool loaded;
                List<Stroke> loadedStrokes;
                var loadFailures = new List<string>();
                bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
                Application.LogCallback captureLoadFailure = (condition, stackTrace, type) =>
                {
                    if (type == LogType.Assert || type == LogType.Error ||
                        type == LogType.Exception)
                    {
                        loadFailures.Add($"{type}: {condition}");
                    }
                };
                try
                {
                    // ResetLayers assigns the active Canvas without raising ActiveCanvasChanged,
                    // so PointerScript emits this existing harmless assertion on the next layer.
                    // LogAssert expectations do not survive this fixture's play/domain boundary,
                    // so capture and verify every suppressed failing log explicitly.
                    Application.logMessageReceived += captureLoadFailure;
                    LogAssert.ignoreFailingMessages = true;
                    try
                    {
                        loaded = SaveLoadScript.m_Instance.Load(
                            new DiskSceneFileInfo(path, readOnly: true),
                            bAdditive: false, targetLayer: -1, out loadedStrokes);
                    }
                    finally
                    {
                        LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                        Application.logMessageReceived -= captureLoadFailure;
                    }
                }
                catch (Exception exception)
                {
                    Assert.Fail($"Snapshot load threw: {exception}");
                    yield break;
                }
                string distinctLoadFailures = string.Join(", ", loadFailures.Distinct());
                Assert.IsTrue(
                    loadFailures.All(failure => failure == "Assert: Assertion failed"),
                    $"Snapshot load emitted an unexpected failing log: {distinctLoadFailures}");
                Assert.IsTrue(loaded, "The just-written animation snapshot failed to load");
                Assert.IsNotNull(loadedStrokes);
                AnimationMetadata after = App.Scene.AnimationTracksSerialized();

                Assert.IsNotNull(before);
                Assert.IsNotNull(before.Tracks);
                Assert.IsNotNull(after);
                Assert.IsNotNull(after.Tracks);
                Assert.AreEqual(AnimationMetadata.CurrentVersion, after.Version);
                Assert.AreEqual(before.Tracks.Length, after.Tracks.Length);
                for (int trackIndex = 0; trackIndex < before.Tracks.Length; trackIndex++)
                {
                    Assert.IsNull(after.Tracks[trackIndex].frameLengths);
                    CollectionAssert.AreEqual(
                        before.Tracks[trackIndex].Spans.Select(span => span.Duration),
                        after.Tracks[trackIndex].Spans.Select(span => span.Duration),
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
                Assert.AreEqual(beforeSpanCount, spanCount,
                    "Sparse span count changed during save/load");
                Assert.AreEqual(beforeEmptySpanCount, emptySpanCount,
                    "Empty sparse span count changed during save/load");
                Assert.AreEqual(beforeUniqueTimelineCanvases, uniqueTimelineCanvases,
                    "Unique animation canvas count changed during save/load");
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

        private static void ConfigureHeldTracks(
            AnimationUI_Manager manager, int trackCount, int frameCount)
        {
            var durations = new List<IReadOnlyList<int>>(trackCount);
            var visibility = new List<bool>(trackCount);
            for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                durations.Add(new[] { frameCount });
                visibility.Add(true);
            }
            manager.ConfigureAnimationTracks(durations, visibility);
        }

        private static Stroke LoadFirstPerformanceStroke(string fileName)
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "../Support/Sketches/PerfTest", fileName));
            List<Stroke> strokes = TestBrush.GetStrokesFromTilt(path);
            Assert.IsNotEmpty(strokes, $"Performance sketch contains no strokes: {fileName}");
            return new Stroke(strokes[0]);
        }

        private static HashSet<CanvasScript> CaptureActiveTimelineCanvases(
            AnimationUI_Manager manager)
        {
            return manager.Timeline
                .SelectMany(track => track.Frames)
                .Select(frame => frame.Canvas)
                .Where(canvas => canvas != null && canvas.gameObject.activeSelf)
                .ToHashSet();
        }
    }
}
