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
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace TiltBrush.Tests
{
    /// Repeatable control-path workloads for Phase 0/1 comparisons. These deliberately contain
    /// no brush geometry; geometry, material diversity, spatial bounds, and target GPU work must
    /// be captured with the separate representative-sketch protocol.
    internal class TestAnimationPerformanceWorkloads
    {
        private const string kLogPrefix = "[OB_ANIM_PHASE0]";
        private const string kRenderLogPrefix = "[OB_ANIM_RENDER]";
        private const string kSparseEditLogPrefix = "[OB_ANIM_SPARSE_EDIT]";
        private const string kPersistenceLogPrefix = "[OB_ANIM_SPARSE_PERSISTENCE]";
        private const int kTransitionCount = 33;
        private const int kRenderSampleCount = 60;
        private static string s_RunId;

        [UnitySetUp]
        public IEnumerator EnterRuntime()
        {
            typeof(LuaManager).GetField(
                "m_Instance", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60f;
            while ((App.Scene == null || App.Scene.animationUI_manager == null) &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.IsNotNull(App.Scene, "Open Brush scene did not initialize");
            Assert.IsNotNull(App.Scene.animationUI_manager,
                "Animation manager did not initialize");
            while (App.CurrentState != App.AppState.Standard &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.AreEqual(App.AppState.Standard, App.CurrentState);
        }

        [UnityTearDown]
        public IEnumerator ExitRuntime()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        [Category("AnimationPerformance")]
        public IEnumerator ControlPathWorkloadMatrix()
        {
            s_RunId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            Debug.Log($"{kLogPrefix} run={s_RunId} matrix=controlPath state=started " +
                $"transitions={kTransitionCount}");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();

            foreach (int frameCount in new[] { 100, 1000, 10000 })
            {
                ConfigureHeldTimeline(manager, trackCount: 8, frameCount: frameCount);
                yield return null;
                RunBothModes(
                    manager, workload: "timelineLength", trackCount: 8,
                    frameCount: frameCount,
                    uniqueDrawingCount: 0, pattern: "sequential");
            }

            foreach (int trackCount in new[] { 1, 8, 32 })
            {
                const int frameCount = 1000;
                ConfigureHeldTimeline(manager, trackCount, frameCount);
                yield return null;
                RunBothModes(
                    manager, workload: "trackCount", trackCount: trackCount,
                    frameCount: frameCount,
                    uniqueDrawingCount: 0, pattern: "sequential");
            }

            foreach (int uniqueDrawingCount in new[] { 4, 16, 64 })
            {
                ConfigureUniqueDrawingTimeline(manager, uniqueDrawingCount);
                yield return null;
                RunBothModes(
                    manager, workload: "uniqueDrawings", trackCount: 1,
                    frameCount: uniqueDrawingCount,
                    uniqueDrawingCount: uniqueDrawingCount,
                    pattern: "sequential");
            }

            const int scrubDrawingCount = 64;
            ConfigureUniqueDrawingTimeline(manager, scrubDrawingCount);
            yield return null;
            RunBothModes(
                manager, workload: "selectionPattern", trackCount: 1,
                frameCount: scrubDrawingCount, uniqueDrawingCount: scrubDrawingCount,
                pattern: "sequential");
            RunBothModes(
                manager, workload: "selectionPattern", trackCount: 1,
                frameCount: scrubDrawingCount, uniqueDrawingCount: scrubDrawingCount,
                pattern: "random");

            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: false, differential: true);
            Debug.Log($"{kLogPrefix} run={s_RunId} matrix=controlPath state=passed");
        }

        [UnityTest]
        [Category("AnimationPerformance")]
        public IEnumerator RealStrokeWorkloadMatrix()
        {
            s_RunId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            Debug.Log($"{kLogPrefix} run={s_RunId} matrix=realStroke state=started " +
                $"transitions={kTransitionCount}");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();

            Stroke simpleStroke = LoadFirstStroke("Simple.tilt");
            foreach (int targetVerticesPerDrawing in new[] { 1000, 10000, 100000 })
            {
                List<CanvasScript> canvases = ConfigureUniqueDrawingTimeline(manager, 4);
                PopulateCanvasesToVertexTarget(
                    canvases, new[] { simpleStroke }, targetVerticesPerDrawing,
                    offsetForCopy: copyIndex => Vector3.zero);
                yield return null;
                RunBothModes(
                    manager, workload: $"geometryVertices{targetVerticesPerDrawing}",
                    trackCount: 1,
                    frameCount: 4, uniqueDrawingCount: 4, pattern: "sequential");
            }

            Stroke[] diverseStrokes =
            {
                simpleStroke,
                LoadFirstStroke("Marker.tilt"),
                LoadFirstStroke("Ink.tilt"),
                LoadFirstStroke("Flat.tilt"),
                LoadFirstStroke("ThickPaint.tilt"),
                LoadFirstStroke("OilPaint.tilt"),
                LoadFirstStroke("Wire.tilt"),
                LoadFirstStroke("LightWire.tilt")
            };
            foreach (int brushCount in new[] { 1, 4, 8 })
            {
                List<CanvasScript> canvases = ConfigureUniqueDrawingTimeline(manager, 4);
                PopulateCanvasesToVertexTarget(
                    canvases, diverseStrokes.Take(brushCount).ToArray(),
                    targetVerticesPerDrawing: 10000,
                    offsetForCopy: copyIndex => Vector3.zero,
                    includeEachSourceOnceThenFillWithFirst: true);
                yield return null;
                RunBothModes(
                    manager, workload: $"brushGroups{brushCount}", trackCount: 1,
                    frameCount: 4, uniqueDrawingCount: 4, pattern: "sequential");
            }

            foreach ((string name, float spacing) in new[]
            {
                ("compact", 0.01f),
                ("spread", 10f)
            })
            {
                List<CanvasScript> canvases = ConfigureUniqueDrawingTimeline(manager, 4);
                PopulateCanvasesToVertexTarget(
                    canvases, new[] { simpleStroke }, targetVerticesPerDrawing: 10000,
                    offsetForCopy: copyIndex => new Vector3(
                        (copyIndex % 4) * spacing,
                        ((copyIndex / 4) % 4) * spacing,
                        (copyIndex / 16) * spacing));
                yield return null;
                RunBothModes(
                    manager, workload: $"spatial{name}", trackCount: 1,
                    frameCount: 4, uniqueDrawingCount: 4, pattern: "sequential");
            }

            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: false, differential: true);
            Debug.Log($"{kLogPrefix} run={s_RunId} matrix=realStroke state=passed");
        }

        [UnityTest]
        [Category("AnimationPerformance")]
        public IEnumerator RealStrokeTimelineScaleMatrix()
        {
            s_RunId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            Debug.Log($"{kLogPrefix} run={s_RunId} matrix=realStrokeTimelineScale " +
                $"state=started transitions={kTransitionCount}");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            Stroke simpleStroke = LoadFirstStroke("Simple.tilt");

            foreach (int frameCount in new[] { 100, 1000, 10000 })
            {
                ConfigureHeldTimeline(manager, trackCount: 8, frameCount: frameCount);
                List<CanvasScript> canvases = MaterializeHeldTrackDrawings(
                    manager, trackCount: 8);
                PopulateCanvasesToVertexTarget(
                    canvases, new[] { simpleStroke }, targetVerticesPerDrawing: 1000,
                    offsetForCopy: copyIndex => Vector3.zero);
                yield return null;
                RunBothModes(
                    manager, workload: "realTimelineLength", trackCount: 8,
                    frameCount: frameCount, uniqueDrawingCount: 8,
                    pattern: "sequential");
            }

            foreach (int trackCount in new[] { 1, 8, 32 })
            {
                const int frameCount = 1000;
                ConfigureHeldTimeline(manager, trackCount: trackCount, frameCount: frameCount);
                List<CanvasScript> canvases = MaterializeHeldTrackDrawings(
                    manager, trackCount);
                PopulateCanvasesToVertexTarget(
                    canvases, new[] { simpleStroke }, targetVerticesPerDrawing: 1000,
                    offsetForCopy: copyIndex => Vector3.zero);
                yield return null;
                RunBothModes(
                    manager, workload: "realTrackCount", trackCount: trackCount,
                    frameCount: frameCount, uniqueDrawingCount: trackCount,
                    pattern: "sequential");
            }

            foreach (int uniqueDrawingCount in new[] { 4, 16, 64 })
            {
                List<CanvasScript> canvases = ConfigureUniqueDrawingTimeline(
                    manager, uniqueDrawingCount);
                PopulateCanvasesToVertexTarget(
                    canvases, new[] { simpleStroke }, targetVerticesPerDrawing: 1000,
                    offsetForCopy: copyIndex => Vector3.zero);
                yield return null;
                RunBothModes(
                    manager, workload: "realUniqueDrawings", trackCount: 1,
                    frameCount: uniqueDrawingCount,
                    uniqueDrawingCount: uniqueDrawingCount, pattern: "sequential");
            }

            RunBothModes(
                manager, workload: "realSelectionPattern", trackCount: 1,
                frameCount: 64, uniqueDrawingCount: 64, pattern: "sequential");
            RunBothModes(
                manager, workload: "realSelectionPattern", trackCount: 1,
                frameCount: 64, uniqueDrawingCount: 64, pattern: "random");

            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: false, differential: true);
            Debug.Log($"{kLogPrefix} run={s_RunId} matrix=realStrokeTimelineScale " +
                "state=passed");
        }

        [UnityTest]
        [Category("AnimationPerformance")]
        public IEnumerator ManagerEditAdapterMatrix()
        {
            const int runCount = 3;
            for (int runIndex = 1; runIndex <= runCount; runIndex++)
            {
                yield return MeasureManagerEditAdapterRun(runIndex, runCount);
            }
        }

        private static IEnumerator MeasureManagerEditAdapterRun(int runIndex, int runCount)
        {
            s_RunId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            Debug.Log(
                $"{kSparseEditLogPrefix} run={s_RunId} matrix=managerAdapter " +
                $"state=started samples=11 tracks=8 repetition={runIndex}/{runCount}");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();

            foreach (int frameCount in new[] { 100, 1000, 10000 })
            {
                ConfigureHeldTimeline(manager, trackCount: 8, frameCount: frameCount);
                yield return null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long managedBefore = GC.GetTotalMemory(false);
                long monoUsedBefore = Profiler.GetMonoUsedSizeLong();
                var elapsedMilliseconds = new List<double>();
                var stopwatch = new Stopwatch();
                for (int sample = 0; sample < 11; sample++)
                {
                    stopwatch.Restart();
                    manager.SetTrackVisibility(0, sample % 2 == 0);
                    stopwatch.Stop();
                    elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
                }

                manager.GetSparseTimelineCounts(out int spanCount, out int emptySpanCount);
                Assert.AreEqual(8, spanCount);
                Assert.AreEqual(8, emptySpanCount);
                Debug.Log(
                    $"{kSparseEditLogPrefix} run={s_RunId} matrix=managerAdapter " +
                    $"frames={frameCount} tracks=8 cells={frameCount * 8} samples=11 " +
                    $"medianMs={Median(elapsedMilliseconds):F4} " +
                    $"p95Ms={Percentile(elapsedMilliseconds, 0.95):F4} " +
                    $"worstMs={elapsedMilliseconds.Max():F4} spans={spanCount} " +
                    $"managedDeltaBytes=" +
                    $"{Math.Max(0, GC.GetTotalMemory(false) - managedBefore)} " +
                    $"monoUsedDeltaBytes=" +
                    $"{Math.Max(0, Profiler.GetMonoUsedSizeLong() - monoUsedBefore)}");

                var saveMilliseconds = new List<double>();
                var loadAndFirstDisplayMilliseconds = new List<double>();
                string json = null;
                for (int sample = 0; sample < 11; sample++)
                {
                    stopwatch.Restart();
                    json = JsonConvert.SerializeObject(CreateSparseMetadata(manager));
                    stopwatch.Stop();
                    saveMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);

                    AnimationMetadata metadata =
                        JsonConvert.DeserializeObject<AnimationMetadata>(json);
                    IReadOnlyList<IReadOnlyList<int>> durations = metadata.Tracks
                        .Select(track => (IReadOnlyList<int>)track.Spans
                            .Select(span => span.Duration).ToList())
                        .ToList();
                    IReadOnlyList<bool> visibility = metadata.Tracks
                        .Select(track => track.Visible).ToList();
                    stopwatch.Restart();
                    manager.ConfigureAnimationTracks(durations, visibility);
                    manager.SelectTimelineFrame(0, 0);
                    stopwatch.Stop();
                    loadAndFirstDisplayMilliseconds.Add(
                        stopwatch.Elapsed.TotalMilliseconds);
                }
                Debug.Log(
                    $"{kPersistenceLogPrefix} run={s_RunId} matrix=metadataAdapter " +
                    $"frames={frameCount} tracks=8 cells={frameCount * 8} samples=11 " +
                    $"jsonBytes={System.Text.Encoding.UTF8.GetByteCount(json)} " +
                    $"saveMedianMs={Median(saveMilliseconds):F4} " +
                    $"saveP95Ms={Percentile(saveMilliseconds, 0.95):F4} " +
                    $"loadFirstDisplayMedianMs=" +
                    $"{Median(loadAndFirstDisplayMilliseconds):F4} " +
                    $"loadFirstDisplayP95Ms=" +
                    $"{Percentile(loadAndFirstDisplayMilliseconds, 0.95):F4}");
            }

            Debug.Log(
                $"{kSparseEditLogPrefix} run={s_RunId} matrix=managerAdapter state=passed " +
                $"repetition={runIndex}/{runCount}");
        }

        [UnityTest]
        [Category("AnimationPerformance")]
        public IEnumerator RenderedFrameWorkloadMatrix()
        {
            s_RunId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            Debug.Log($"{kRenderLogPrefix} run={s_RunId} matrix=renderedFrame " +
                $"state=started samples={kRenderSampleCount}");
            AnimationUI_Manager manager = App.Scene.animationUI_manager;
            manager.StopAnimation();
            Stroke simpleStroke = LoadFirstStroke("Simple.tilt");

            ConfigureHeldTimeline(manager, trackCount: 8, frameCount: 10000);
            List<CanvasScript> heldCanvases = MaterializeHeldTrackDrawings(manager, 8);
            PopulateCanvasesToVertexTarget(
                heldCanvases, new[] { simpleStroke }, targetVerticesPerDrawing: 10000,
                offsetForCopy: copyIndex => Vector3.zero);
            yield return MeasureRenderedModes(
                manager, workload: "longHeld", frameCount: 10000,
                pattern: "sequential");

            List<CanvasScript> uniqueCanvases = ConfigureUniqueDrawingTimeline(manager, 16);
            PopulateCanvasesToVertexTarget(
                uniqueCanvases, new[] { simpleStroke }, targetVerticesPerDrawing: 10000,
                offsetForCopy: copyIndex => Vector3.zero);
            yield return MeasureRenderedModes(
                manager, workload: "uniqueComplex", frameCount: 16,
                pattern: "sequential");
            yield return MeasureRenderedModes(
                manager, workload: "uniqueComplex", frameCount: 16,
                pattern: "random");

            Stroke[] diverseStrokes =
            {
                simpleStroke,
                LoadFirstStroke("Marker.tilt"),
                LoadFirstStroke("Ink.tilt"),
                LoadFirstStroke("Flat.tilt"),
                LoadFirstStroke("ThickPaint.tilt"),
                LoadFirstStroke("OilPaint.tilt"),
                LoadFirstStroke("Wire.tilt"),
                LoadFirstStroke("LightWire.tilt")
            };
            List<CanvasScript> materialCanvases = ConfigureUniqueDrawingTimeline(manager, 4);
            PopulateCanvasesToVertexTarget(
                materialCanvases, diverseStrokes, targetVerticesPerDrawing: 10000,
                offsetForCopy: copyIndex => Vector3.zero,
                includeEachSourceOnceThenFillWithFirst: true);
            yield return MeasureRenderedModes(
                manager, workload: "materialDiverse", frameCount: 4,
                pattern: "sequential");

            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: false, differential: true);
            Debug.Log($"{kRenderLogPrefix} run={s_RunId} matrix=renderedFrame " +
                "state=passed");
        }

        private static void ConfigureHeldTimeline(
            AnimationUI_Manager manager, int trackCount, int frameCount)
        {
            manager.StartTimeline();
            var frameLengths = new List<IReadOnlyList<int>>(trackCount);
            var visibility = new List<bool>(trackCount);
            for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                frameLengths.Add(new[] { frameCount });
                visibility.Add(true);
            }
            manager.ConfigureLegacyAnimationTracks(frameLengths, visibility);
        }

        private static AnimationMetadata CreateSparseMetadata(AnimationUI_Manager manager)
        {
            List<int> activeTrackIndexes = manager.ActiveTrackIndexes();
            return new AnimationMetadata
            {
                Version = AnimationMetadata.CurrentVersion,
                Tracks = activeTrackIndexes.Select(trackIndex => new AnimationTrackMetadata
                {
                    Visible = manager.Timeline[trackIndex].Visible,
                    Spans = manager.GetTrackFrameLengths(trackIndex)
                        .Select(duration => new AnimationSpanMetadata { Duration = duration })
                        .ToList()
                }).ToArray(),
                numFrames = activeTrackIndexes.Count
            };
        }

        private static List<CanvasScript> ConfigureUniqueDrawingTimeline(
            AnimationUI_Manager manager, int uniqueDrawingCount)
        {
            manager.StartTimeline();
            manager.ConfigureLegacyAnimationTracks(
                new List<IReadOnlyList<int>>
                {
                    Enumerable.Repeat(1, uniqueDrawingCount).ToArray()
                },
                new List<bool> { true });
            var canvases = new List<CanvasScript>(uniqueDrawingCount);
            for (int frameIndex = 0; frameIndex < uniqueDrawingCount; frameIndex++)
            {
                canvases.Add(manager.GetOrCreateContentCanvas(0, frameIndex));
            }
            return canvases;
        }

        private static List<CanvasScript> MaterializeHeldTrackDrawings(
            AnimationUI_Manager manager, int trackCount)
        {
            var canvases = new List<CanvasScript>(trackCount);
            for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                canvases.Add(manager.GetOrCreateContentCanvas(trackIndex, 0));
            }
            return canvases;
        }

        private static Stroke LoadFirstStroke(string fileName)
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath, "../Support/Sketches/PerfTest", fileName));
            List<Stroke> strokes = TestBrush.GetStrokesFromTilt(path);
            Assert.IsNotEmpty(strokes, $"Performance sketch contains no strokes: {fileName}");
            return strokes[0];
        }

        private static void PopulateCanvasesToVertexTarget(
            IEnumerable<CanvasScript> canvases, IReadOnlyList<Stroke> sourceStrokes,
            int targetVerticesPerDrawing, Func<int, Vector3> offsetForCopy,
            bool includeEachSourceOnceThenFillWithFirst = false)
        {
            Assert.IsNotEmpty(sourceStrokes);
            foreach (CanvasScript canvas in canvases)
            {
                int copyIndex = 0;
                while (canvas.BatchManager.CountAllBatchVertices() < targetVerticesPerDrawing)
                {
                    Assert.Less(copyIndex, 10000,
                        $"Failed to reach {targetVerticesPerDrawing} vertices");
                    int sourceIndex = includeEachSourceOnceThenFillWithFirst &&
                        copyIndex >= sourceStrokes.Count
                        ? 0
                        : copyIndex % sourceStrokes.Count;
                    Stroke stroke = new Stroke(sourceStrokes[sourceIndex]);
                    Vector3 offset = offsetForCopy(copyIndex);
                    for (int pointIndex = 0;
                        pointIndex < stroke.m_ControlPoints.Length;
                        pointIndex++)
                    {
                        stroke.m_ControlPoints[pointIndex].m_Pos += offset;
                    }
                    BatchSubset subset = TestBrush.CreateSubsetFromStroke(canvas, stroke);
                    Assert.IsNotNull(subset,
                        $"Brush {stroke.m_BrushGuid} did not produce batched geometry");
                    stroke.m_Type = Stroke.Type.BatchedBrushStroke;
                    stroke.m_BatchSubset = subset;
                    subset.m_Stroke = stroke;
                    App.Scene.animationUI_manager.NotifyStrokeAdded(stroke);
                    copyIndex++;
                }
                canvas.BatchManager.FlushMeshUpdates();
            }
        }

        private static void RunBothModes(
            AnimationUI_Manager manager, string workload, int trackCount, int frameCount,
            int uniqueDrawingCount, string pattern)
        {
            int[] frames = CreateSelectionPattern(frameCount, pattern);
            Measurement legacy = Measure(manager, differential: false, frames: frames);
            Measurement differential = Measure(manager, differential: true, frames: frames);
            LogMeasurement(
                manager, workload, "legacy", trackCount, frameCount,
                uniqueDrawingCount, pattern, legacy);
            LogMeasurement(
                manager, workload, "differential", trackCount, frameCount,
                uniqueDrawingCount, pattern, differential);
        }

        private static int[] CreateSelectionPattern(int frameCount, string pattern)
        {
            var frames = new int[kTransitionCount];
            for (int transition = 0; transition < frames.Length; transition++)
            {
                frames[transition] = pattern == "random"
                    ? (transition * 37 + 11) % frameCount
                    : (transition + 1) % frameCount;
            }
            return frames;
        }

        private static Measurement Measure(
            AnimationUI_Manager manager, bool differential, IReadOnlyList<int> frames)
        {
            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: true, differential: differential);
            manager.SelectTimelineFrame(0, 0);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            manager.ResetPlaybackDiagnosticsForTests();
            var elapsedMilliseconds = new List<double>(frames.Count);
            var stopwatch = new Stopwatch();
            foreach (int frame in frames)
            {
                stopwatch.Restart();
                manager.ApplyPlaybackFrameForTests(frame);
                stopwatch.Stop();
                elapsedMilliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            elapsedMilliseconds.Sort();
            int p95Index = Mathf.Clamp(
                Mathf.CeilToInt(elapsedMilliseconds.Count * 0.95f) - 1,
                0, elapsedMilliseconds.Count - 1);
            return new Measurement(
                manager.CapturePlaybackDiagnosticsForTests(),
                elapsedMilliseconds[elapsedMilliseconds.Count / 2],
                elapsedMilliseconds[p95Index],
                elapsedMilliseconds[elapsedMilliseconds.Count - 1]);
        }

        private static IEnumerator MeasureRenderedModes(
            AnimationUI_Manager manager, string workload, int frameCount, string pattern)
        {
            int[] frames = CreateSelectionPattern(frameCount, pattern);
            RenderMeasurement legacy = null;
            yield return MeasureRenderedFrames(
                manager, differential: false, frames: frames,
                completed: measurement => legacy = measurement);
            LogRenderMeasurement(workload, "legacy", pattern, legacy);

            RenderMeasurement differential = null;
            yield return MeasureRenderedFrames(
                manager, differential: true, frames: frames,
                completed: measurement => differential = measurement);
            LogRenderMeasurement(workload, "differential", pattern, differential);

            RenderMeasurement proxy = null;
            yield return MeasureRenderedFrames(
                manager, differential: true, frames: frames,
                completed: measurement => proxy = measurement, drawingProxies: true);
            LogRenderMeasurement(workload, "proxy", pattern, proxy);
            manager.ConfigureDrawingRenderProxiesForTests(enabled: false);
        }

        private static IEnumerator MeasureRenderedFrames(
            AnimationUI_Manager manager, bool differential, IReadOnlyList<int> frames,
            Action<RenderMeasurement> completed, bool drawingProxies = false)
        {
            manager.ConfigurePlaybackDiagnosticsForTests(
                enabled: true, differential: differential);
            manager.ConfigureDrawingRenderProxiesForTests(enabled: drawingProxies);
            manager.SelectTimelineFrame(0, 0);
            for (int warmup = 0; warmup < 10; warmup++)
            {
                manager.ApplyPlaybackFrameForTests(frames[warmup % frames.Count]);
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            manager.ResetPlaybackDiagnosticsForTests();

            var measurement = new RenderMeasurement();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var frameTimings = new FrameTiming[1];
            for (int sample = 0; sample < kRenderSampleCount; sample++)
            {
                manager.ApplyPlaybackFrameForTests(frames[sample % frames.Count]);
                FrameTimingManager.CaptureFrameTimings();
                yield return null;

                measurement.DeltaMilliseconds.Add(Time.unscaledDeltaTime * 1000.0);
                measurement.EditorFrameMilliseconds.Add(UnityStats.frameTime * 1000.0);
                measurement.EditorRenderMilliseconds.Add(UnityStats.renderTime * 1000.0);
                measurement.DrawCalls.Add(UnityStats.drawCalls);
                measurement.Batches.Add(UnityStats.batches);
                measurement.SetPassCalls.Add(UnityStats.setPassCalls);
                measurement.Vertices.Add(UnityStats.vertices);
                measurement.Triangles.Add(UnityStats.triangles);
                measurement.VboUploads.Add(UnityStats.vboUploads);
                measurement.VboUploadBytes.Add(UnityStats.vboUploadBytes);
                uint timingCount = FrameTimingManager.GetLatestTimings(1, frameTimings);
                if (timingCount > 0)
                {
                    measurement.CpuFrameMilliseconds.Add(frameTimings[0].cpuFrameTime);
                    if (frameTimings[0].gpuFrameTime > 0)
                    {
                        measurement.GpuFrameMilliseconds.Add(frameTimings[0].gpuFrameTime);
                    }
                }
            }
            measurement.ManagedAllocatedBytes = Math.Max(
                0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
            List<CanvasScript> drawingCanvases = manager.Timeline
                .SelectMany(track => track.Frames)
                .Select(frame => frame.Canvas)
                .Where(canvas => canvas != null)
                .Distinct()
                .ToList();
            measurement.RetainedCanvasObjects = drawingCanvases.Count;
            measurement.ActiveCanvasObjects = drawingCanvases.Count(
                canvas => canvas.gameObject.activeInHierarchy);
            measurement.RetainedCanvasHierarchyObjects = drawingCanvases.Sum(
                canvas => canvas.GetComponentsInChildren<Transform>(true).Length);
            measurement.ProxyObjects = manager.GetDrawingRenderProxyObjectCountForTests();
            measurement.VisibleProxies = manager.GetVisibleDrawingRenderProxyCountForTests();
            measurement.Counters = manager.CapturePlaybackDiagnosticsForTests();
            completed(measurement);
        }

        private static void LogRenderMeasurement(
            string workload, string mode, string pattern, RenderMeasurement measurement)
        {
            Debug.Log(
                $"{kRenderLogPrefix} run={s_RunId} workload={workload} mode={mode} " +
                $"pattern={pattern} samples={kRenderSampleCount} " +
                $"deltaMedianMs={Median(measurement.DeltaMilliseconds):F3} " +
                $"deltaP95Ms={Percentile(measurement.DeltaMilliseconds, 0.95):F3} " +
                $"editorFrameMedianMs={Median(measurement.EditorFrameMilliseconds):F3} " +
                $"editorRenderMedianMs={Median(measurement.EditorRenderMilliseconds):F3} " +
                $"cpuTimingSamples={measurement.CpuFrameMilliseconds.Count} " +
                $"cpuMedianMs={Median(measurement.CpuFrameMilliseconds):F3} " +
                $"gpuTimingSamples={measurement.GpuFrameMilliseconds.Count} " +
                $"gpuMedianMs={Median(measurement.GpuFrameMilliseconds):F3} " +
                $"drawCalls={Median(measurement.DrawCalls):F0} " +
                $"batches={Median(measurement.Batches):F0} " +
                $"setPassCalls={Median(measurement.SetPassCalls):F0} " +
                $"vertices={Median(measurement.Vertices):F0} " +
                $"triangles={Median(measurement.Triangles):F0} " +
                $"vboUploads={Median(measurement.VboUploads):F0} " +
                $"vboUploadBytes={Median(measurement.VboUploadBytes):F0} " +
                $"hideVisits={measurement.Counters.HideFrameVisits} " +
                $"visibilityRequests={measurement.Counters.CanvasVisibilityRequests} " +
                $"retainedCanvases={measurement.RetainedCanvasObjects} " +
                $"activeCanvases={measurement.ActiveCanvasObjects} " +
                $"retainedCanvasHierarchyObjects={measurement.RetainedCanvasHierarchyObjects} " +
                $"proxyObjects={measurement.ProxyObjects} " +
                $"visibleProxies={measurement.VisibleProxies} " +
                $"managedAllocatedBytes={measurement.ManagedAllocatedBytes} " +
                $"allocatedBytes={Profiler.GetTotalAllocatedMemoryLong()} " +
                $"reservedBytes={Profiler.GetTotalReservedMemoryLong()}");
        }

        private static double Median<T>(IEnumerable<T> values)
        {
            double[] sorted = values
                .Select(value => Convert.ToDouble(value))
                .OrderBy(value => value)
                .ToArray();
            return sorted.Length == 0 ? 0 : sorted[sorted.Length / 2];
        }

        private static double Percentile<T>(IEnumerable<T> values, double percentile)
        {
            double[] sorted = values
                .Select(value => Convert.ToDouble(value))
                .OrderBy(value => value)
                .ToArray();
            if (sorted.Length == 0) return 0;
            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(sorted.Length * percentile)) - 1,
                0, sorted.Length - 1);
            return sorted[index];
        }

        private static void LogMeasurement(
            AnimationUI_Manager manager, string workload, string mode, int trackCount,
            int frameCount, int uniqueDrawingCount, string pattern, Measurement measurement)
        {
            manager.GetSparseTimelineCounts(out int spanCount, out int emptySpanCount);
            List<CanvasScript> canvases = manager.Timeline
                .SelectMany(track => track.Frames)
                .Select(frame => frame.Canvas)
                .Where(canvas => canvas != null)
                .Distinct()
                .ToList();
            int batchCount = canvases.Sum(canvas => canvas.BatchManager?.CountBatches() ?? 0);
            int vertexCount = canvases.Sum(
                canvas => canvas.BatchManager?.CountAllBatchVertices() ?? 0);
            List<Batch> batches = canvases
                .SelectMany(canvas => canvas.BatchManager.AllBatches())
                .ToList();
            List<Mesh> batchMeshes = batches
                .Select(batch => batch.GetComponent<MeshFilter>()?.sharedMesh)
                .Where(mesh => mesh != null)
                .Distinct()
                .ToList();
            List<Renderer> batchRenderers = batches
                .Select(batch => batch.GetComponent<Renderer>())
                .Where(renderer => renderer != null)
                .ToList();
            int batchBrushCount = batches
                .Select(batch => batch.Brush.m_Guid)
                .Distinct()
                .Count();
            int batchMaterialCount = batchRenderers
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .Count();
            int triangleCount = canvases.Sum(
                canvas => canvas.BatchManager?.CountAllBatchTriangles() ?? 0);
            long batchMeshBytes = batchMeshes.Sum(
                mesh => Profiler.GetRuntimeMemorySizeLong(mesh));
            List<MeshFilter> hierarchyMeshFilters = canvases
                .SelectMany(canvas => canvas.GetComponentsInChildren<MeshFilter>(true))
                .Where(meshFilter => meshFilter.sharedMesh != null)
                .ToList();
            List<Mesh> hierarchyMeshes = hierarchyMeshFilters
                .Select(meshFilter => meshFilter.sharedMesh)
                .Distinct()
                .ToList();
            List<Renderer> hierarchyRenderers = canvases
                .SelectMany(canvas => canvas.GetComponentsInChildren<Renderer>(true))
                .ToList();
            long hierarchyMeshBytes = hierarchyMeshes.Sum(
                mesh => Profiler.GetRuntimeMemorySizeLong(mesh));
            int hierarchyMaterialCount = hierarchyRenderers
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .Count();
            Debug.Log(
                $"{kLogPrefix} run={s_RunId} workload={workload} mode={mode} " +
                $"pattern={pattern} " +
                $"tracks={trackCount} frames={frameCount} cells={trackCount * frameCount} " +
                $"uniqueDrawings={uniqueDrawingCount} spans={spanCount} " +
                $"emptySpans={emptySpanCount} canvases={canvases.Count} " +
                $"batches={batchCount} batchBrushes={batchBrushCount} " +
                $"batchMeshes={batchMeshes.Count} batchRenderers={batchRenderers.Count} " +
                $"batchMaterials={batchMaterialCount} vertices={vertexCount} " +
                $"triangles={triangleCount} batchMeshBytes={batchMeshBytes} " +
                $"hierarchyMeshes={hierarchyMeshes.Count} " +
                $"hierarchyRenderers={hierarchyRenderers.Count} " +
                $"hierarchyMaterials={hierarchyMaterialCount} " +
                $"hierarchyMeshBytes={hierarchyMeshBytes} transitions={kTransitionCount} " +
                $"medianMs={measurement.MedianMilliseconds:F3} " +
                $"p95Ms={measurement.P95Milliseconds:F3} " +
                $"worstMs={measurement.WorstMilliseconds:F3} " +
                $"hideVisits={measurement.Counters.HideFrameVisits} " +
                $"visibilityRequests={measurement.Counters.CanvasVisibilityRequests} " +
                $"locationCells={measurement.Counters.LocationCellsVisited} " +
                $"layerEvents={measurement.Counters.LayerEvents} " +
                $"allocatedBytes={Profiler.GetTotalAllocatedMemoryLong()} " +
                $"reservedBytes={Profiler.GetTotalReservedMemoryLong()} " +
                $"managedBytes={GC.GetTotalMemory(false)}");
        }

        private readonly struct Measurement
        {
            internal AnimationPerformanceStats.CounterSnapshot Counters { get; }
            internal double MedianMilliseconds { get; }
            internal double P95Milliseconds { get; }
            internal double WorstMilliseconds { get; }

            internal Measurement(
                AnimationPerformanceStats.CounterSnapshot counters,
                double medianMilliseconds, double p95Milliseconds,
                double worstMilliseconds)
            {
                Counters = counters;
                MedianMilliseconds = medianMilliseconds;
                P95Milliseconds = p95Milliseconds;
                WorstMilliseconds = worstMilliseconds;
            }
        }

        private sealed class RenderMeasurement
        {
            internal readonly List<double> DeltaMilliseconds = new();
            internal readonly List<double> EditorFrameMilliseconds = new();
            internal readonly List<double> EditorRenderMilliseconds = new();
            internal readonly List<double> CpuFrameMilliseconds = new();
            internal readonly List<double> GpuFrameMilliseconds = new();
            internal readonly List<int> DrawCalls = new();
            internal readonly List<int> Batches = new();
            internal readonly List<int> SetPassCalls = new();
            internal readonly List<int> Vertices = new();
            internal readonly List<int> Triangles = new();
            internal readonly List<int> VboUploads = new();
            internal readonly List<int> VboUploadBytes = new();
            internal AnimationPerformanceStats.CounterSnapshot Counters;
            internal long ManagedAllocatedBytes;
            internal int RetainedCanvasObjects;
            internal int ActiveCanvasObjects;
            internal int RetainedCanvasHierarchyObjects;
            internal int ProxyObjects;
            internal int VisibleProxies;
        }
    }
}
