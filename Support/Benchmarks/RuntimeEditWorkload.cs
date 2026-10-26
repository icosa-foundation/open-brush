// Runtime edit protocol; load is setup only. Run through Run-RuntimeEditWorkload.ps1.
// Runs inside the owned Main-scene Play session; requires instrument_batch_workload.py.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Debug = UnityEngine.Debug;
namespace TiltBrush.Benchmarks
{
    public static class RuntimeEditWorkload
    {
        public sealed class Counter { public string name; public long calls, vertices, indices; public double cpuMs; }
        public sealed class Census
        {
            public int strokes, batchedStrokes, solitaryStrokes, notCreated, batches, residentBatches;
            public long vertices, indices, cachedIndexBytes, cachedVertexBytes, meshVertexBytes, unusedMeshVertexBytes;
        }
        public sealed class Phase
        {
            public string fixture, name, brushGuid, brushName;
            public int controlPoints, qualityLevel;
            public int sample, frameCount;
            public double wallMs, directCallCpuMs;
            public long directCallAllocatedBytes, managedBytesBefore, managedBytesAfter, nativeBytesBefore, nativeBytesAfter, sampledPeakNativeBytes;
            public int affectedStrokes, directCallCollections;
            public string cameraPose;
            public List<Frame> frames = new List<Frame>(512);
            public Counter[] counters;
            public Census census;
            public double[] frameIntervalsMs;
        }
        public sealed class Frame { public int frame; public double intervalMs, mainThreadMs, renderThreadMs, gpuMs, cpuMainFrameMs, cpuPresentWaitMs, cpuRenderFrameMs, fullUploadMs, topologyMs; public long gcBytes, fullUploadCalls, uploadedVertices, drawCalls, batches, triangles; }
        public sealed class Report
        {
            public int schemaVersion = 2;
            public string label, utc, status, error, unity, cpu, gpu, graphicsApi, sdkMode, sourceManifest, harnessSha256;
            public bool batching, eviction, geometryShaders, autosaveDisabled, cameraControlsDisabled, desktopStateInputSuppressed;
            public bool mainRecorderValid, renderRecorderValid, gcRecorderValid, frameTimingEnabled, threadAllocationCounterValid;
            public string measurementScope = "Editor Play mode; loaded-sketch editing. Main/render timings include waits and Editor overhead. GPU samples only from settled continuous phases; zero means unavailable. Input hit-testing/controller latency excluded. Frame GC includes Editor and harness overhead. Per-call allocation is -1 when the Mono allocation counter is unavailable. Zero draw/batch counters are unavailable on some Editor backends. Native bytes are Unity tracked allocations, not process peak RAM.";
            public int screenWidth, screenHeight, qualityLevel, targetFrameRate, vSync;
            public string[] fixturePaths, fixtureSha256;
            public int[] selectionSizes;
            public List<Phase> phases = new List<Phase>();
        }
        static readonly string[] DefaultFixtures = { "Support/Sketches/Promoted/Blue_Tit.tilt", "Support/Sketches/Promoted/TiltMech.tilt" };
        [Serializable]
        public class FixtureList { public string[] paths; public int[] selectionSizes; }
        static string[] s_Fixtures;
        static Report s_Report;
        static string s_Output;
        static bool s_Running, s_Cancel, s_PreviousDisableAutosave;
        static Phase s_Phase;
        static long s_PhaseStart;
        static List<double> s_Intervals;
        static Unity.Profiling.ProfilerRecorder s_Main, s_Render, s_Gc, s_DrawCalls, s_Batches, s_Triangles;
        static long s_LastCalls, s_LastVertices, s_LastFullTicks, s_LastTopologyTicks;
        static readonly FrameTiming[] s_Timing = new FrameTiming[1];
        static ulong s_LastTiming;
        static bool s_Continuous;
        static MonoCameraControlScript[] s_CameraControls;
        static bool[] s_CameraControlsEnabled;
        static Vector3 s_FixedCameraPosition;
        static Quaternion s_FixedCameraRotation;
        static float s_FixedCameraFov;
        static readonly double ToMs = 1000.0 / Stopwatch.Frequency;
        public static string Run(string label, string output)
        {
            return RunWithFixtures(label, output, null);
        }

        // A local JSON object {"paths":["path/to/sketch.tilt",...]} keeps user fixtures out of Git.
        public static string RunWithFixtures(string label, string output, string fixturesFile)
        {
            if (Screen.width != 1280 || Screen.height != 720) throw new InvalidOperationException("Configure RuntimeEditSetup for 1280 x 720 before measuring");
            if (s_Running || UnityEditor.SessionState.GetBool("BatchWorkloadRunning", false)) throw new InvalidOperationException("A workload is already running");
            if (!Application.isPlaying || App.CurrentState != App.AppState.Standard) throw new InvalidOperationException($"App not ready: {App.CurrentState}");
            if (!App.Config.m_UseBatchedBrushes || !App.Config.m_EnableBatchMemoryOptimization) throw new InvalidOperationException("Default batching and eviction must be enabled");
            var fixtures = string.IsNullOrEmpty(fixturesFile) ? new FixtureList { paths = DefaultFixtures } :
                JsonUtility.FromJson<FixtureList>(File.ReadAllText(fixturesFile));
            s_Fixtures = fixtures?.paths;
            if (s_Fixtures == null || s_Fixtures.Length == 0 || s_Fixtures.Distinct().Count() != s_Fixtures.Length ||
                s_Fixtures.Any(path => !File.Exists(path))) throw new ArgumentException("Fixture list must contain unique existing sketch files");
            if (fixtures.selectionSizes != null && fixtures.selectionSizes.Any(size => size < 0))
                throw new ArgumentException("Selection sizes must be positive, or zero for select-all");
            s_PreviousDisableAutosave = App.UserConfig.Flags.DisableAutosave;
            App.UserConfig.Flags.DisableAutosave = true;
            // Mouse camera control otherwise overwrites FrameSketch's rotation every Update.
            // Isolate the benchmark from desktop input and restore the original state afterward.
            s_CameraControls = UnityEngine.Object.FindObjectsByType<MonoCameraControlScript>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            s_CameraControlsEnabled = s_CameraControls.Select(control => control.enabled).ToArray();
            foreach (var control in s_CameraControls) control.enabled = false;
            foreach (var device in InputSystem.devices)
                if (device is Mouse || device is Keyboard) InputSystem.ResetDevice(device, alsoResetDontResetControls: true);
            InputSystem.onEvent += SuppressDesktopStateInput;
            s_Output = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(s_Output));
            s_Report = new Report
            {
                autosaveDisabled = true, cameraControlsDisabled = true, desktopStateInputSuppressed = true, label = label, utc = DateTime.UtcNow.ToString("o"), status = "running", unity = Application.unityVersion,
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                sdkMode = App.Config.m_SdkMode.ToString(), batching = App.Config.m_UseBatchedBrushes,
                eviction = App.Config.m_EnableBatchMemoryOptimization, geometryShaders = App.Config.GeometryShaderSuppported,
                screenWidth = Screen.width, screenHeight = Screen.height, qualityLevel = QualitySettings.GetQualityLevel(),
                targetFrameRate = Application.targetFrameRate, vSync = QualitySettings.vSyncCount,
                sourceManifest = File.ReadAllText("Logs/BatchWorkloadSession/state.json"),
                harnessSha256 = Hash(File.ReadAllBytes("Support/Benchmarks/RuntimeEditWorkload.cs")),
                fixturePaths = s_Fixtures, fixtureSha256 = s_Fixtures.Select(p => Hash(File.ReadAllBytes(p))).ToArray(),
                selectionSizes = fixtures.selectionSizes ?? Array.Empty<int>()
            };
            s_Main = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Main Thread", 1);
            s_Render = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "Render Thread", 1);
            s_Gc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            s_DrawCalls = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count", 1);
            s_Batches = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Batches Count", 1);
            s_Triangles = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Triangles Count", 1);
            s_Report.mainRecorderValid = s_Main.Valid; s_Report.renderRecorderValid = s_Render.Valid; s_Report.gcRecorderValid = s_Gc.Valid;
            s_Report.frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            long beforeProbe = GC.GetAllocatedBytesForCurrentThread(); var allocationProbe = new byte[4096];
            s_Report.threadAllocationCounterValid = GC.GetAllocatedBytesForCurrentThread() > beforeProbe;
            GC.KeepAlive(allocationProbe);
            s_Running = true; s_Cancel = false; UnityEditor.SessionState.SetBool("BatchWorkloadRunning", true); UnityEditor.SessionState.SetBool("BatchWorkloadCancel", false); Save();
            App.Instance.StartCoroutine(Guarded(s_Report.selectionSizes.Length == 0 ? Work() : SelectionSweep()));
            return $"[RuntimeEditBenchmark] Started {label}; output {s_Output}";
        }
        public static string Cancel() { s_Cancel = true; UnityEditor.SessionState.SetBool("BatchWorkloadCancel", true); return "Cancellation requested"; }
        static IEnumerator Guarded(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            while (stack.Count > 0)
            {
                object value = null; bool more = false;
                try
                {
                    if (s_Cancel || UnityEditor.SessionState.GetBool("BatchWorkloadCancel", false)) throw new OperationCanceledException();
                    if (s_Phase != null)
                    {
                        var camera = App.VrSdk.GetVrCamera();
                        if (!camera.transform.position.Equals(s_FixedCameraPosition) || !camera.transform.rotation.Equals(s_FixedCameraRotation) || camera.fieldOfView != s_FixedCameraFov)
                            throw new InvalidOperationException("Benchmark camera moved during a measured phase");
                    }
                    more = stack.Peek().MoveNext();
                    if (more) value = stack.Peek().Current;
                    else { (stack.Pop() as IDisposable)?.Dispose(); }
                }
                catch (Exception e)
                {
                    BatchWorkloadCounters.Enabled = false; s_Report.status = "failed"; s_Report.error = e.ToString();
                    Debug.LogError($"[RuntimeEditBenchmark] {e}"); Save(); s_Running = false; UnityEditor.SessionState.SetBool("BatchWorkloadRunning", false); App.UserConfig.Flags.DisableAutosave = s_PreviousDisableAutosave; RestoreCameraControls(); s_Main.Dispose(); s_Render.Dispose(); s_Gc.Dispose(); s_DrawCalls.Dispose(); s_Batches.Dispose(); s_Triangles.Dispose(); yield break;
                }
                if (!more) continue;
                if (value is IEnumerator nested) { stack.Push(nested); continue; }
                yield return value;
                if (s_Phase != null) SampleFrame();
            }
            BatchWorkloadCounters.Enabled = false; s_Report.status = "completed"; s_Running = false; UnityEditor.SessionState.SetBool("BatchWorkloadRunning", false); App.UserConfig.Flags.DisableAutosave = s_PreviousDisableAutosave; RestoreCameraControls(); s_Main.Dispose(); s_Render.Dispose(); s_Gc.Dispose(); s_DrawCalls.Dispose(); s_Batches.Dispose(); s_Triangles.Dispose(); Save();
            Debug.Log($"[RuntimeEditBenchmark] Completed {s_Report.label}: {s_Report.phases.Count} phases");
        }
        static void RestoreCameraControls()
        {
            InputSystem.onEvent -= SuppressDesktopStateInput;
            for (int i = 0; i < s_CameraControls.Length; i++)
                if (s_CameraControls[i] != null) s_CameraControls[i].enabled = s_CameraControlsEnabled[i];
        }
        static void SuppressDesktopStateInput(InputEventPtr input, InputDevice device)
        {
            // Commands are supplied by this workload. Ignore physical button/motion state while
            // measuring, without disabling OS input or removing devices from the Editor.
            if ((device is Mouse || device is Keyboard) && (input.IsA<StateEvent>() || input.IsA<DeltaStateEvent>())) input.handled = true;
        }
        // Optional fixture JSON selectionSizes (e.g. [256,1024,4096,0]) runs a selection-only sweep.
        // Reload for every repetition: selection restores source caches on older implementations.
        // Reusing that sketch would incorrectly make subsequent baseline selections appear cheap.
        static IEnumerator SelectionSweep()
        {
            foreach (string fixture in s_Fixtures)
            {
                yield return Load(fixture, -1);
                int total = SketchMemoryScript.AllStrokes().Count(s => s.m_Type == Stroke.Type.BatchedBrushStroke);
                if (total == 0) throw new InvalidOperationException($"No batched strokes: {fixture}");
                var sizes = s_Report.selectionSizes.Select(size => size == 0 ? total : Math.Min(size, total)).Distinct().ToArray();
                foreach (int count in sizes)
                {
                    for (int sample = -1; sample < 3; sample++)
                    {
                        yield return Load(fixture, -1); FrameSketch(); yield return Frames(120);
                        var all = SketchMemoryScript.AllStrokes().Where(s => s.m_Type == Stroke.Type.BatchedBrushStroke).ToArray();
                        var selected = Enumerable.Range(0, count).Select(i => all[(long)i * all.Length / count]).ToArray();
                        Begin(fixture, $"stationary_view_{count}", sample); s_Continuous = true;
                        yield return Frames(120); End();
                        yield return Frames(12);
                        var select = new SelectCommand(selected, null, SelectionManager.m_Instance.SelectionTransform);
                        Begin(fixture, $"select_{count}", sample);
                        Call(() => SketchMemoryScript.m_Instance.PerformAndRecordCommand(select));
                        s_Phase.affectedStrokes = SelectionManager.m_Instance.SelectedStrokes.Count();
                        yield return Frames(16); End();
                        yield return Frames(12);
                        var startPose = SelectionManager.m_Instance.SelectionTransform;
                        Begin(fixture, $"drag_selection_{count}", sample); s_Continuous = true;
                        for (int frame = 0; frame < 120; frame++)
                        {
                            var pose = startPose; pose.translation += new Vector3(0.5f * Mathf.Sin((frame + 1) * Mathf.PI / 120), 0, 0);
                            Call(() => SelectionManager.m_Instance.SelectionTransform = pose);
                            yield return null;
                        }
                        End();
                        var releasePose = startPose; releasePose.translation += new Vector3(0.25f, 0, 0);
                        SelectionManager.m_Instance.SelectionTransform = releasePose;
                        yield return Frames(12);
                        var deselect = new SelectCommand(SelectionManager.m_Instance.SelectedStrokes.ToArray(), null, releasePose, deselect: true);
                        Begin(fixture, $"release_selection_{count}", sample);
                        Call(() => SketchMemoryScript.m_Instance.PerformAndRecordCommand(deselect));
                        yield return Frames(16); End();
                        if (SelectionManager.m_Instance.HasSelection) throw new InvalidOperationException("Release did not clear selection");
                    }
                }
            }
        }
        static IEnumerator Work()
        {
            foreach (string fixture in s_Fixtures)
            {
                // One unmeasured warm-up of every operation, then three fresh-sketch repetitions.
                for (int sample = -1; sample < 3; sample++)
                {
                    yield return Load(fixture, -1);
                    FrameSketch();
                    yield return Frames(120);
                    var all = SketchMemoryScript.AllStrokes().Where(s => s.m_Type == Stroke.Type.BatchedBrushStroke).ToArray();
                    var selected = Enumerable.Range(0, Math.Min(32, all.Length)).Select(i => all[i * all.Length / Math.Min(32, all.Length)]).ToArray();
                    Begin(fixture, "stationary_view", sample); s_Continuous = true;
                    yield return Frames(120); End();
                    yield return Frames(12);
                    var select = new SelectCommand(selected, null, SelectionManager.m_Instance.SelectionTransform);
                    Begin(fixture, "select_32", sample);
                    Call(() => SketchMemoryScript.m_Instance.PerformAndRecordCommand(select));
                    s_Phase.affectedStrokes = SelectionManager.m_Instance.SelectedStrokes.Count();
                    yield return Frames(16); End();
                    yield return Frames(12);
                    var startPose = SelectionManager.m_Instance.SelectionTransform;
                    Begin(fixture, "drag_selection", sample); s_Continuous = true;
                    for (int frame = 0; frame < 120; frame++)
                    {
                        var pose = startPose; pose.translation += new Vector3(0.5f * Mathf.Sin((frame + 1) * Mathf.PI / 120), 0, 0);
                        Call(() => SelectionManager.m_Instance.SelectionTransform = pose);
                        yield return null;
                    }
                    End();
                    // Leave a non-identity translation so releasing exercises transformed geometry.
                    var releasePose = startPose; releasePose.translation += new Vector3(0.25f, 0, 0);
                    SelectionManager.m_Instance.SelectionTransform = releasePose;
                    yield return Frames(12);
                    var deselect = new SelectCommand(SelectionManager.m_Instance.SelectedStrokes.ToArray(), null, releasePose, deselect: true);
                    Begin(fixture, "release_selection", sample);
                    Call(() => SketchMemoryScript.m_Instance.PerformAndRecordCommand(deselect));
                    yield return Frames(16); End();
                    if (SelectionManager.m_Instance.HasSelection) throw new InvalidOperationException("Release did not clear selection");
                    // DeselectCommand intentionally absorbs subsequent deletes. Reset the sketch so
                    // this scenario measures independent erase commands, rather than undoing selection too.
                    yield return Load(fixture, -1); FrameSketch(); yield return Frames(90);
                    all = SketchMemoryScript.AllStrokes().Where(s => s.m_Type == Stroke.Type.BatchedBrushStroke).ToArray();
                    selected = Enumerable.Range(0, Math.Min(32, all.Length)).Select(i => all[i * all.Length / Math.Min(32, all.Length)]).ToArray();
                    // Each erase is a real command on a separate rendered frame; undo/redo use the app stack.
                    Begin(fixture, "erase_32", sample);
                    foreach (var stroke in selected) { Call(() => SketchMemoryScript.m_Instance.PerformAndRecordCommand(new DeleteStrokeCommand(stroke))); yield return null; }
                    yield return Frames(8); End();
                    foreach (string phase in new[] { "undo_erase_32", "redo_erase_32", "restore_erase_32" })
                    {
                        yield return Frames(12); Begin(fixture, phase, sample);
                        for (int i = 0; i < selected.Length; i++) { if (phase == "redo_erase_32") Call(() => SketchMemoryScript.m_Instance.StepForward()); else Call(() => SketchMemoryScript.m_Instance.StepBack()); yield return null; }
                        yield return Frames(8); End();
                    }
                    if (selected.Any(s => !s.m_BatchSubset.m_Active)) throw new InvalidOperationException("Undo did not restore strokes");
                    yield return Frames(90);
                    var repaint = new RepaintStrokeCommand(selected.ToList(), selected.Select(s => new Color(s.m_Color.b, s.m_Color.r, s.m_Color.g, s.m_Color.a)).ToList(), selected.Select(s => s.m_BrushGuid).ToList(), selected.Select(s => s.m_BrushSize).ToList());
                    Begin(fixture, "repaint_32", sample);
                    Call(() => SketchMemoryScript.m_Instance.PerformAndRecordCommand(repaint));
                    yield return Frames(16); End();
                    yield return Frames(12); Begin(fixture, "undo_repaint_32", sample);
                    Call(() => SketchMemoryScript.m_Instance.StepBack()); yield return Frames(16); End();
                    var sources = all.Where(s => s.m_ControlPoints.Length >= 8)
                        .GroupBy(s => BrushCatalog.m_Instance.GetBrush(s.m_BrushGuid).m_BrushPrefab.GetComponent<GeometryBrush>() != null)
                        .Select(g => g.OrderByDescending(s => s.m_ControlPoints.Length).First()).ToArray();
                    foreach (var source in sources) { yield return Frames(12); yield return Draw(fixture, sample, source); }
                }
            }
        }
        static void FrameSketch()
        {
            var bounds = App.Scene.ActiveCanvas.BatchManager.GetBoundsOfAllStrokes(true);
            var camera = App.VrSdk.GetVrCamera();
            var center = App.Scene.ActiveCanvas.transform.TransformPoint(bounds.center);
            float radius = bounds.extents.magnitude * App.Scene.ActiveCanvas.transform.lossyScale.x;
            float distance = Mathf.Max(1, radius / Mathf.Sin(camera.fieldOfView * Mathf.Deg2Rad * 0.5f));
            camera.transform.SetPositionAndRotation(center + Vector3.back * distance, Quaternion.identity);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, distance + radius * 2);
            s_FixedCameraPosition = camera.transform.position;
            s_FixedCameraRotation = camera.transform.rotation;
            s_FixedCameraFov = camera.fieldOfView;
        }
        static IEnumerator Load(string path, int sample)
        {
            var info = new DiskSceneFileInfo(Path.GetFullPath(path)); info.ReadMetadata();
            if (SaveLoadScript.m_Instance.LastMetadataError != null) throw new InvalidOperationException(SaveLoadScript.m_Instance.LastMetadataError);
            if (sample >= 0) Begin(path, "quick_load", sample);
            Call(() => SketchControlsScript.m_Instance.LoadSketch(info, true));
            yield return null;
            int frames = 0;
            while (App.CurrentState != App.AppState.Standard || SketchMemoryScript.m_Instance.IsPlayingBack)
            {
                if (++frames > 7200) throw new TimeoutException($"Load failed to settle: {path}, {App.CurrentState}");
                yield return null;
            }
            // LateUpdate/render callbacks flush pending batch uploads before the next sample ends.
            yield return null;
            if (sample >= 0) End();
        }
        static IEnumerator Draw(string fixture, int sample, Stroke source)
        {
            // Recorded control points through the app's pointer path, one point per rendered frame.
            var stroke = new Stroke(source);
            stroke.m_ControlPoints = source.m_ControlPoints.Take(128).ToArray();
            stroke.m_ControlPointsToDrop = source.m_ControlPointsToDrop.Take(stroke.m_ControlPoints.Length).ToArray();
            stroke.Group = SketchGroupTag.None;
            var pointer = PointerManager.m_Instance.GetTransientPointer(0);
            var brush = BrushCatalog.m_Instance.GetBrush(source.m_BrushGuid);
            Begin(fixture, brush.m_BrushPrefab.GetComponent<GeometryBrush>() != null ? "paced_geometry_replay" : "paced_legacy_replay", sample);
            s_Continuous = true; s_Phase.brushGuid = source.m_BrushGuid.ToString(); s_Phase.brushName = brush.m_DurableName;
            s_Phase.controlPoints = stroke.m_ControlPoints.Length;
            Call(() => { stroke.m_Object = pointer.BeginLineFromMemory(stroke, App.Scene.ActiveCanvas); stroke.m_Type = Stroke.Type.BrushStroke; });
            if (stroke.m_Object == null) throw new InvalidOperationException("Replay brush missing");
            for (int i = 0; i < stroke.m_ControlPoints.Length; i++)
            {
                int cp = i;
                if (!stroke.m_ControlPointsToDrop[i]) Call(() => { pointer.UpdateLineFromControlPoint(stroke.m_ControlPoints[cp], stroke.GetColor(cp)); pointer.UpdateLineVisuals(); });
                yield return null;
            }
            Call(() =>
            {
                pointer.EndLineFromMemory(stroke);
                SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(new BrushStrokeCommand(stroke));
            });
            yield return null;
            End();
            if (stroke.m_Type != Stroke.Type.BatchedBrushStroke) throw new InvalidOperationException("Completed replay did not enter a batch");
        }
        static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        static void Begin(string fixture, string name, int sample)
        {
            s_Continuous = false; s_LastCalls = s_LastVertices = s_LastFullTicks = s_LastTopologyTicks = 0;
            s_Phase = new Phase { fixture = fixture, name = name, sample = sample, qualityLevel = QualitySettings.GetQualityLevel() };
            s_Intervals = new List<double>(256); BatchWorkloadCounters.Reset(); BatchWorkloadCounters.Enabled = true;
            s_Phase.cameraPose = $"{App.VrSdk.GetVrCamera().transform.position.ToString("R")}/{App.VrSdk.GetVrCamera().transform.rotation.ToString("R")}/{App.VrSdk.GetVrCamera().fieldOfView:R}";
            s_Phase.directCallAllocatedBytes = s_Report.threadAllocationCounterValid ? 0 : -1;
            s_Phase.managedBytesBefore = GC.GetTotalMemory(false);
            s_Phase.nativeBytesBefore = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            s_Phase.sampledPeakNativeBytes = s_Phase.nativeBytesBefore;
            s_PhaseStart = Stopwatch.GetTimestamp();
        }
        static void End()
        {
            long end = Stopwatch.GetTimestamp(); BatchWorkloadCounters.Enabled = false;
            s_Phase.wallMs = (end - s_PhaseStart) * ToMs; s_Phase.frameCount = s_Intervals.Count;
            s_Phase.frameIntervalsMs = s_Intervals.ToArray();
            s_Phase.counters = Enumerable.Range(0, BatchWorkloadCounters.Names.Length).Select(i => new Counter
            {
                name = BatchWorkloadCounters.Names[i], calls = BatchWorkloadCounters.Calls[i], cpuMs = BatchWorkloadCounters.Ticks[i] * ToMs,
                vertices = BatchWorkloadCounters.Vertices[i], indices = BatchWorkloadCounters.Indices[i]
            }).ToArray();
            s_Phase.managedBytesAfter = GC.GetTotalMemory(false);
            s_Phase.nativeBytesAfter = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            s_Phase.census = Count(); if (s_Phase.sample >= 0) s_Report.phases.Add(s_Phase);
            Debug.Log($"[RuntimeEditBenchmark] {Path.GetFileName(s_Phase.fixture)} {s_Phase.name} {s_Phase.sample}: {s_Phase.wallMs:F2}ms, {s_Phase.census.batchedStrokes} batched strokes");
            s_Phase = null; Save();
        }
        static void Call(Action action)
        {
            int collections = GC.CollectionCount(0);
            long bytes = GC.GetAllocatedBytesForCurrentThread(); long start = Stopwatch.GetTimestamp(); action();
            if (s_Phase != null) { s_Phase.directCallCpuMs += (Stopwatch.GetTimestamp() - start) * ToMs; if (s_Report.threadAllocationCounterValid) s_Phase.directCallAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - bytes; }
            if (s_Phase != null) s_Phase.directCallCollections += GC.CollectionCount(0) - collections;
        }
        static void SampleFrame()
        {
            double interval = Time.unscaledDeltaTime * 1000.0; s_Intervals.Add(interval);
            double gpu = 0, cpuMain = 0, cpuWait = 0, cpuRender = 0;
            if (FrameTimingManager.GetLatestTimings(1, s_Timing) > 0 && s_Timing[0].frameStartTimestamp != s_LastTiming)
            {
                s_LastTiming = s_Timing[0].frameStartTimestamp;
                // FrameTiming data arrives delayed. Only use settled continuous phases, never
                // attribute a previous phase's GPU time to a single-frame edit.
                if (s_Continuous && s_Phase.frames.Count >= 8)
                {
                    gpu = s_Timing[0].gpuFrameTime; cpuMain = s_Timing[0].cpuMainThreadFrameTime;
                    cpuWait = s_Timing[0].cpuMainThreadPresentWaitTime; cpuRender = s_Timing[0].cpuRenderThreadFrameTime;
                }
            }
            FrameTimingManager.CaptureFrameTimings();
            long full = BatchWorkloadCounters.Calls[0], verts = BatchWorkloadCounters.Vertices[0];
            long fullTicks = BatchWorkloadCounters.Ticks[0], topologyTicks = BatchWorkloadCounters.Ticks[1];
            double fullMs = (fullTicks - s_LastFullTicks) * ToMs, topologyMs = (topologyTicks - s_LastTopologyTicks) * ToMs;
            s_Phase.frames.Add(new Frame
            {
                frame = Time.frameCount - 1, intervalMs = interval,
                mainThreadMs = s_Main.Valid ? s_Main.LastValue * 1e-6 : 0, renderThreadMs = s_Render.Valid ? s_Render.LastValue * 1e-6 : 0,
                gcBytes = s_Gc.Valid ? s_Gc.LastValue : 0, gpuMs = gpu, cpuMainFrameMs = cpuMain, cpuPresentWaitMs = cpuWait, cpuRenderFrameMs = cpuRender, fullUploadCalls = full - s_LastCalls, uploadedVertices = verts - s_LastVertices,
                drawCalls = s_DrawCalls.Valid ? s_DrawCalls.LastValue : -1, batches = s_Batches.Valid ? s_Batches.LastValue : -1, triangles = s_Triangles.Valid ? s_Triangles.LastValue : -1, fullUploadMs = fullMs, topologyMs = topologyMs
            });
            s_LastCalls = full; s_LastVertices = verts; s_LastFullTicks = fullTicks; s_LastTopologyTicks = topologyTicks;
            s_Phase.sampledPeakNativeBytes = Math.Max(s_Phase.sampledPeakNativeBytes, UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong());
        }
        static Census Count()
        {
            var strokes = SketchMemoryScript.AllStrokes().ToArray();
            var batches = App.Scene.GetComponentsInChildren<CanvasScript>(true).Where(c => c.BatchManager != null).SelectMany(c => c.BatchManager.AllBatches()).Where(b => b.Geometry != null).Distinct().ToArray();
            return new Census
            {
                strokes = strokes.Length, batchedStrokes = strokes.Count(s => s.m_Type == Stroke.Type.BatchedBrushStroke),
                solitaryStrokes = strokes.Count(s => s.m_Type == Stroke.Type.BrushStroke), notCreated = strokes.Count(s => s.m_Type == Stroke.Type.NotCreated),
                batches = batches.Length, residentBatches = batches.Count(b => b.Geometry.IsGeometryResident),
                vertices = batches.Sum(b => (long)b.Geometry.NumVerts), indices = batches.Sum(b => (long)b.Geometry.NumTriIndices),
                meshVertexBytes = batches.Sum(b => MeshVertexBytes(b, false)),
                unusedMeshVertexBytes = batches.Sum(b => MeshVertexBytes(b, true)),
                cachedIndexBytes = batches.Sum(b => (b.Geometry.m_Tris?.Capacity ?? 0) * 4L),
                cachedVertexBytes = batches.Sum(b => (b.Geometry.m_Vertices?.Capacity ?? 0) * 12L + (b.Geometry.m_Normals?.Capacity ?? 0) * 12L + (b.Geometry.m_Tangents?.Capacity ?? 0) * 16L + (b.Geometry.m_Colors?.Capacity ?? 0) * 4L
                    + (b.Geometry.m_Texcoord0.v2?.Capacity ?? 0) * 8L + (b.Geometry.m_Texcoord0.v3?.Capacity ?? 0) * 12L + (b.Geometry.m_Texcoord0.v4?.Capacity ?? 0) * 16L
                    + (b.Geometry.m_Texcoord1.v2?.Capacity ?? 0) * 8L + (b.Geometry.m_Texcoord1.v3?.Capacity ?? 0) * 12L + (b.Geometry.m_Texcoord1.v4?.Capacity ?? 0) * 16L
                    + (b.Geometry.m_Texcoord2.v2?.Capacity ?? 0) * 8L + (b.Geometry.m_Texcoord2.v3?.Capacity ?? 0) * 12L + (b.Geometry.m_Texcoord2.v4?.Capacity ?? 0) * 16L)
            };
        }
        // Vertex-buffer payload, not driver allocation size or total process/GPU memory.
        static long MeshVertexBytes(Batch batch, bool unusedOnly)
        {
            var mesh = batch.GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null) return 0;
            long stride = 0;
            for (int stream = 0; stream < mesh.vertexBufferCount; stream++) stride += mesh.GetVertexBufferStride(stream);
            return stride * (unusedOnly ? Math.Max(0, mesh.vertexCount - batch.Geometry.NumVerts) : mesh.vertexCount);
        }
        static void Save()
        {
            var serializer = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject", new[] { typeof(object) });
            File.WriteAllText(s_Output, (string)serializer.Invoke(null, new object[] { s_Report }));
        }
        static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
