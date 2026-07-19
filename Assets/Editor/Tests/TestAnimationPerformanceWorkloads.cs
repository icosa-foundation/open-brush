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
using NUnit.Framework;
using TiltBrush.FrameAnimation;
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
        private const int kTransitionCount = 33;
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
    }
}
