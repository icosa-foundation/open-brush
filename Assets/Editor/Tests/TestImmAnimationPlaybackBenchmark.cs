// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ImmPlayer;
using ImmPlayer.Authoring;
using Newtonsoft.Json;
using NUnit.Framework;
using TiltBrush.FrameAnimation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace TiltBrush.Tests
{
    internal class TestImmAnimationPlaybackBenchmark
    {
        private const int DrawingCount = 8;
        private const int StrokesPerDrawing = 64;
        private const int PointsPerStroke = 16;
        private const int FramesPerDrawing = 2;
        private const int Track = 1;
        private const int Resolution = 512;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

        [UnitySetUp]
        public IEnumerator EnterRuntime() => new TestAnimationCanvasLifecycle().EnterRuntime();

        [UnityTearDown]
        public IEnumerator ExitRuntime() => new TestAnimationCanvasLifecycle().ExitRuntime();

        [UnityTest]
        [Category("AnimationPerformance")]
        public IEnumerator CompareCanvasAndImm()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("IMM playback requires a graphics device.");

            var manager = App.Scene.animationUI_manager;
            var camera = App.VrSdk.GetVrCamera();
            manager.StartTimeline();
            manager.ConfigureDrawingRenderProxiesForTests(enabled: false);
            manager.ConfigureLegacyAnimationTracks(new List<IReadOnlyList<int>>
            {
                new[] { DrawingCount * FramesPerDrawing },
                Enumerable.Repeat(FramesPerDrawing, DrawingCount).ToArray()
            }, new List<bool> { true, true });

            var canvases = new CanvasScript[DrawingCount];
            Stroke editableStroke = null;
            for (int i = 0; i < DrawingCount; ++i)
            {
                canvases[i] = manager.GetOrCreateContentCanvas(Track, i * FramesPerDrawing);
                for (int j = 0; j < StrokesPerDrawing; ++j)
                {
                    var stroke = AddStroke(canvases[i], camera, i, j);
                    if (i == 0 && j == 0) editableStroke = stroke;
                }
            }

            var target = RenderTexture.GetTemporary(Resolution, Resolution, 24);
            var previousTarget = camera.targetTexture;
            // The test-runner scene can add a second EventSystem to the Main scene.
            // Its per-update warning and stack trace otherwise dominate the measured thread.
            var duplicates = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None)
                .Where(system => system.isActiveAndEnabled && system != EventSystem.current).ToArray();
            try
            {
                foreach (var duplicate in duplicates) duplicate.enabled = false;
                camera.targetTexture = target;
                using (var probe = new CameraProbe(camera))
                {
                    var modes = new[] { false, true, true, false };
                    for (int pass = 0; pass < modes.Length; ++pass)
                    {
                        bool imm = modes[pass];
                        var options = new SerializedObject(manager);
                        options.FindProperty("m_ImmPaintTrackIndex").intValue = Track;
                        options.FindProperty("m_UseImmPaintTrack").boolValue = imm;
                        options.ApplyModifiedPropertiesWithoutUndo();
                        manager.SelectTimelineFrame(Track, 0);
                        manager.TimelineSlideDown(true);
                        yield return probe.Until(() => canvases[0].gameObject.activeSelf != imm);

                        ImmDocument document = null;
                        int layer = -1;
                        if (imm)
                        {
                            var driver = typeof(AnimationUI_Manager).GetField("m_ImmPlayer", PrivateInstance).GetValue(manager);
                            document = (ImmDocument)driver.GetType().GetField("_document", PrivateInstance).GetValue(driver);
                            layer = document.GetLayersManaged().Single(info => info.Type == ImmDocument.LayerType.Paint).Id;
                        }
                        manager.TimelineSlideDown(false);
                        manager.StartAnimation();

                        // Verify native ownership and the evaluated drawing outside the timed window.
                        var verified = new HashSet<int>();
                        probe.AfterRender = stamp =>
                        {
                            int frame = manager.AppliedFrameForStats;
                            if (!imm || frame < 0 || canvases[frame / FramesPerDrawing].gameObject.activeSelf ||
                                verified.Contains(frame)) return;
                            var paint = document.GetEditableLayerStates().Single(state => state.id == layer);
                            Assert.AreEqual(0, ImmLiveDrawingEdits.GetFrameDrawing(document.DocumentId, layer, frame, out var expected));
                            Assert.AreEqual(0, ImmLiveDrawingEdits.GetDrawing(document.DocumentId, layer, paint.drawing, out var actual));
                            Assert.AreEqual(expected, actual, $"Native drawing at playback frame {frame}");
                            Assert.AreNotEqual(0, paint.visible);
                            verified.Add(frame);
                        };
                        double warmupEnd = Now + 4;
                        yield return probe.Until(() => Now >= warmupEnd);
                        if (imm) Assert.IsNotEmpty(verified, "Native paint never took over during playback.");

                        var wallMs = new List<double>(2048);
                        var renderMs = new List<double>(2048);
                        var gpuMs = new List<double>(2048);
                        var frameTimings = new FrameTiming[1];
                        ulong lastGpuSample = 0;
                        int native = 0, holds = 0, changes = 0, nativeHolds = 0, nativeChanges = 0;
                        int previousFrame = -1;
                        double previousStamp = 0;
                        double start = Now;
                        probe.AfterRender = stamp =>
                        {
                            int frame = manager.AppliedFrameForStats;
                            if (previousFrame >= 0)
                            {
                                wallMs.Add((stamp - previousStamp) * 1000);
                                renderMs.Add(probe.LastRenderMs);
                                bool taken = !canvases[frame / FramesPerDrawing].gameObject.activeSelf;
                                if (taken) ++native;
                                if (frame != previousFrame)
                                {
                                    if (frame / FramesPerDrawing == previousFrame / FramesPerDrawing)
                                    {
                                        ++holds;
                                        if (taken) ++nativeHolds;
                                    }
                                    else
                                    {
                                        ++changes;
                                        if (taken) ++nativeChanges;
                                    }
                                }
                            }
                            previousFrame = frame;
                            previousStamp = stamp;
                            FrameTimingManager.CaptureFrameTimings();
                            if (FrameTimingManager.GetLatestTimings(1, frameTimings) != 0 &&
                                frameTimings[0].frameStartTimestamp != lastGpuSample && frameTimings[0].gpuFrameTime > 0)
                            {
                                lastGpuSample = frameTimings[0].frameStartTimestamp;
                                gpuMs.Add(frameTimings[0].gpuFrameTime);
                            }
                        };
                        yield return probe.Until(() => Now - start >= 8);
                        probe.AfterRender = null;
                        manager.StopAnimation();
                        Assert.That(wallMs.Count, Is.GreaterThan(8));
                        Assert.That(changes, Is.GreaterThan(0));
                        if (!imm) Assert.AreEqual(0, native);

                        // Time a real repaint through its first completed camera.Render call.
                        // For IMM, also time the first native-owned render of the new revision.
                        var firstRenderMs = new List<double>();
                        var backendRenderMs = new List<double>();
                        for (int edit = 0; edit < 5; ++edit)
                        {
                            manager.SelectTimelineFrame(Track, 0);
                            manager.TimelineSlideDown(true);
                            yield return probe.Until(() => canvases[0].gameObject.activeSelf != imm);
                            ulong previousRevision = imm ? Presented(document) : 0;
                            manager.TimelineSlideDown(false);
                            Color color = editableStroke.m_Color == Color.red ? Color.blue : Color.red;
                            double editStart = Now;
                            SketchMemoryScript.m_Instance.PerformAndRecordCommand(new RepaintStrokeCommand(editableStroke,
                                color, editableStroke.m_BrushGuid, editableStroke.m_BrushSize));
                            manager.TimelineSlideDown(true);
                            double first = 0, backend = 0;
                            probe.AfterRender = stamp =>
                            {
                                bool onCanvas = canvases[0].gameObject.activeSelf;
                                bool updatedNative = imm && !onCanvas && Presented(document) > previousRevision;
                                if (first == 0 && (onCanvas || updatedNative)) first = (stamp - editStart) * 1000;
                                if (backend == 0 && (imm ? updatedNative : onCanvas)) backend = (stamp - editStart) * 1000;
                            };
                            yield return probe.Until(() => backend > 0);
                            Assert.AreEqual(color, editableStroke.m_Color);
                            probe.AfterRender = null;
                            manager.TimelineSlideDown(false);
                            if (edit > 0)
                            {
                                firstRenderMs.Add(first);
                                backendRenderMs.Add(backend);
                            }
                        }
                        Debug.Log("[OB_IMM_BENCH] " + JsonConvert.SerializeObject(new
                        {
                            pass, mode = imm ? "imm" : "canvas", gpu = SystemInfo.graphicsDeviceName,
                            graphicsApi = SystemInfo.graphicsDeviceType.ToString(), resolution = Resolution,
                            drawings = DrawingCount, strokesPerDrawing = StrokesPerDrawing, pointsPerStroke = PointsPerStroke,
                            framesPerDrawing = FramesPerDrawing, fps = 8, samples = wallMs.Count,
                            wallP50Ms = Percentile(wallMs, .5), wallP95Ms = Percentile(wallMs, .95),
                            renderP50Ms = Percentile(renderMs, .5), renderP95Ms = Percentile(renderMs, .95),
                            disabledDuplicateEventSystems = duplicates.Length,
                            gpuSamples = gpuMs.Count, gpuP50Ms = Percentile(gpuMs, .5), gpuP95Ms = Percentile(gpuMs, .95),
                            nativeFrames = native, canvasFrames = wallMs.Count - native,
                            holdTransitions = holds, holdTransitionsNative = nativeHolds,
                            drawingTransitions = changes, drawingTransitionsNative = nativeChanges,
                            verifiedNativeExposures = verified.Count, firstRenderMs, backendRenderMs
                        }));
                    }
                }
            }
            finally
            {
                foreach (var duplicate in duplicates)
                    if (duplicate != null) duplicate.enabled = true;
                manager.StopAnimation();
                manager.TimelineSlideDown(false);
                camera.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static double? Percentile(List<double> values, double fraction)
        {
            if (values.Count == 0) return null;
            var ordered = values.OrderBy(value => value).ToArray();
            return ordered[(int)Math.Ceiling(fraction * ordered.Length) - 1];
        }

        private static ulong Presented(ImmDocument doc)
        {
            Assert.AreEqual(0, ImmLiveDrawingEdits.GetRevisions(doc.DocumentId, out var revisions));
            return revisions.Presented;
        }

        private static Stroke AddStroke(CanvasScript canvas, Camera camera, int drawing, int index)
        {
            // Each canvas carries a different position and color. The grid fits in the registered camera's view.
            float x = (index % 8 - 3.5f) * .08f + drawing * .003f;
            float y = (index / 8 - 3.5f) * .08f;
            var local = canvas.transform;
            var forward = local.InverseTransformDirection(camera.transform.up);
            var normal = local.InverseTransformDirection(-camera.transform.forward);
            var rotation = Quaternion.LookRotation(forward, normal);
            var points = new PointerManager.ControlPoint[PointsPerStroke];
            for (int i = 0; i < points.Length; ++i)
            {
                var point = new Vector3(x, y + i * .004f, 1.5f);
                points[i] = new PointerManager.ControlPoint
                {
                    m_Pos = local.InverseTransformPoint(camera.transform.TransformPoint(point)),
                    m_Orient = rotation, m_Pressure = 1, m_TimestampMs = (uint)((drawing * StrokesPerDrawing + index) * 32 + i)
                };
            }
            var stroke = new Stroke
            {
                m_BrushGuid = new Guid(Quill.BRUSH_QUILL_CYLINDER), m_Color = Color.HSVToRGB(drawing / 8f, .8f, 1),
                m_BrushScale = 1, m_BrushSize = .012f / Mathf.Abs(local.lossyScale.x),
                m_ControlPoints = points, m_ControlPointsToDrop = new bool[points.Length]
            };
            stroke.Recreate(canvas: canvas);
            Assert.That(stroke.m_BatchSubset?.m_VertLength, Is.GreaterThan(0));
            SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
            return stroke;
        }

        // The test coroutine resumes before LateUpdate. Sample after the playhead, canvas visibility,
        // and native driver have all run; the batch Editor needs an explicit Game camera render.
        private sealed class CameraProbe : IDisposable
        {
            private readonly PlayerLoopSystem original;
            private readonly Camera camera;
            private Exception failure;
            public Action<double> AfterRender;
            public double LastRenderMs { get; private set; }

            internal CameraProbe(Camera camera)
            {
                this.camera = camera;
                original = PlayerLoop.GetCurrentPlayerLoop();
                var loop = original;
                loop.subSystemList = (PlayerLoopSystem[])original.subSystemList.Clone();
                int late = Array.FindIndex(loop.subSystemList, system => system.type == typeof(PreLateUpdate));
                Assert.That(late, Is.GreaterThanOrEqualTo(0));
                var systems = new List<PlayerLoopSystem>(loop.subSystemList[late].subSystemList)
                {
                    new PlayerLoopSystem { type = typeof(CameraProbe), updateDelegate = Tick }
                };
                loop.subSystemList[late].subSystemList = systems.ToArray();
                PlayerLoop.SetPlayerLoop(loop);
            }

            private void Tick()
            {
                if (failure != null) return;
                try
                {
                    double start = Now;
                    camera.Render();
                    double end = Now;
                    LastRenderMs = (end - start) * 1000;
                    AfterRender?.Invoke(end);
                }
                catch (Exception ex) { failure = ex; }
            }

            internal IEnumerator Until(Func<bool> done)
            {
                double deadline = Now + 45;
                do
                {
                    yield return null;
                    if (failure != null) throw failure;
                    Assert.Less(Now, deadline, "Timed out waiting for the playback benchmark.");
                } while (!done());
            }

            public void Dispose() => PlayerLoop.SetPlayerLoop(original);
        }
    }
}
