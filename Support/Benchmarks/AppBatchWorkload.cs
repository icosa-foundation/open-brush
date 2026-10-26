// Runs inside the owned Main-scene Play session; requires instrument_batch_workload.py.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Debug = UnityEngine.Debug;
namespace TiltBrush.Benchmarks
{
    public static class AppBatchWorkload
    {
        public sealed class Counter { public string name; public long calls, vertices, indices; public double cpuMs; }
        public sealed class Census
        {
            public int strokes, batchedStrokes, solitaryStrokes, notCreated, batches, residentBatches;
            public long vertices, indices, cachedIndexBytes, cachedVertexBytes;
        }
        public sealed class Phase
        {
            public string fixture, name, brushGuid, brushName;
            public int controlPoints, qualityLevel;
            public int sample, frameCount;
            public double wallMs, directCallCpuMs;
            public Counter[] counters;
            public Census census;
            public double[] frameIntervalsMs;
        }
        public sealed class Report
        {
            public int schemaVersion = 1;
            public string label, utc, status, error, unity, cpu, gpu, graphicsApi, sdkMode, sourceManifest, harnessSha256;
            public bool batching, eviction, geometryShaders, autosaveDisabled;
            public int screenWidth, screenHeight, qualityLevel, targetFrameRate, vSync;
            public string[] fixturePaths, fixtureSha256;
            public List<Phase> phases = new List<Phase>();
        }
        static readonly string[] Fixtures = { "Support/Sketches/PerfTest/Ink.tilt", "Support/Sketches/Promoted/Blue_Tit.tilt", "Support/Sketches/Promoted/TiltMech.tilt" };
        static Report s_Report;
        static string s_Output;
        static bool s_Running, s_Cancel, s_PreviousDisableAutosave;
        static Phase s_Phase;
        static long s_PhaseStart;
        static List<double> s_Intervals;
        static readonly double ToMs = 1000.0 / Stopwatch.Frequency;
        public static string Run(string label, string output)
        {
            if (s_Running || UnityEditor.SessionState.GetBool("BatchWorkloadRunning", false)) throw new InvalidOperationException("A workload is already running");
            if (!Application.isPlaying || App.CurrentState != App.AppState.Standard) throw new InvalidOperationException($"App not ready: {App.CurrentState}");
            if (!App.Config.m_UseBatchedBrushes || !App.Config.m_EnableBatchMemoryOptimization) throw new InvalidOperationException("Default batching and eviction must be enabled");
            s_PreviousDisableAutosave = App.UserConfig.Flags.DisableAutosave;
            App.UserConfig.Flags.DisableAutosave = true;
            s_Output = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(s_Output));
            s_Report = new Report
            {
                autosaveDisabled = true, label = label, utc = DateTime.UtcNow.ToString("o"), status = "running", unity = Application.unityVersion,
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                sdkMode = App.Config.m_SdkMode.ToString(), batching = App.Config.m_UseBatchedBrushes,
                eviction = App.Config.m_EnableBatchMemoryOptimization, geometryShaders = App.Config.GeometryShaderSuppported,
                screenWidth = Screen.width, screenHeight = Screen.height, qualityLevel = QualitySettings.GetQualityLevel(),
                targetFrameRate = Application.targetFrameRate, vSync = QualitySettings.vSyncCount,
                sourceManifest = File.ReadAllText("Logs/BatchWorkloadSession/state.json"),
                harnessSha256 = Hash(File.ReadAllBytes("Support/Benchmarks/AppBatchWorkload.cs")),
                fixturePaths = Fixtures, fixtureSha256 = Fixtures.Select(p => Hash(File.ReadAllBytes(p))).ToArray()
            };
            s_Running = true; s_Cancel = false; UnityEditor.SessionState.SetBool("BatchWorkloadRunning", true); UnityEditor.SessionState.SetBool("BatchWorkloadCancel", false); Save();
            App.Instance.StartCoroutine(Guarded(Work()));
            return $"[BatchWorkloadBenchmark] Started {label}; output {s_Output}";
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
                    more = stack.Peek().MoveNext();
                    if (more) value = stack.Peek().Current;
                    else { (stack.Pop() as IDisposable)?.Dispose(); }
                }
                catch (Exception e)
                {
                    BatchWorkloadCounters.Enabled = false; s_Report.status = "failed"; s_Report.error = e.ToString();
                    Debug.LogError($"[BatchWorkloadBenchmark] {e}"); Save(); s_Running = false; UnityEditor.SessionState.SetBool("BatchWorkloadRunning", false); App.UserConfig.Flags.DisableAutosave = s_PreviousDisableAutosave; yield break;
                }
                if (!more) continue;
                if (value is IEnumerator nested) { stack.Push(nested); continue; }
                yield return value;
                if (s_Phase != null) s_Intervals.Add(Time.unscaledDeltaTime * 1000.0);
            }
            BatchWorkloadCounters.Enabled = false; s_Report.status = "completed"; s_Running = false; UnityEditor.SessionState.SetBool("BatchWorkloadRunning", false); App.UserConfig.Flags.DisableAutosave = s_PreviousDisableAutosave; Save();
            Debug.Log($"[BatchWorkloadBenchmark] Completed {s_Report.label}: {s_Report.phases.Count} phases");
        }
        static IEnumerator Work()
        {
            foreach (string fixture in Fixtures)
            {
                // Warm the fixture and shaders before the three measured quick loads.
                yield return Load(fixture, -1);
                yield return Frames(90);
                for (int sample = 0; sample < 3; sample++)
                {
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    yield return Load(fixture, sample);
                    yield return Frames(30);
                }
                Begin(fixture, "idle", 0);
                yield return Frames(120);
                End();
                var all = SketchMemoryScript.AllStrokes().Where(s => s.m_Type == Stroke.Type.BatchedBrushStroke).ToArray();
                if (all.Length == 0) throw new InvalidOperationException("Fixture has no batched strokes");
                var selected = Enumerable.Range(0, Math.Min(32, all.Length)).Select(i => all[i * all.Length / Math.Min(32, all.Length)]).ToArray();
                var selectedBatches = selected.Select(s => s.m_BatchSubset.m_ParentBatch).Distinct().ToArray();
                for (int sample = 0; sample < 3; sample++)
                {
                    yield return Frames(90); // Natural batch-age eviction; newest batches stay mutable.
                    yield return Visibility(fixture, "visibility_after_idle", sample, selected, selectedBatches);
                    yield return Visibility(fixture, "visibility_warm", sample, selected, selectedBatches);
                }
                var sources = all.Where(s => s.m_ControlPoints.Length >= 8)
                    .GroupBy(s => BrushCatalog.m_Instance.GetBrush(s.m_BrushGuid).m_BrushPrefab.GetComponent<GeometryBrush>() != null)
                    .Select(g => g.OrderByDescending(s => s.m_ControlPoints.Length).First()).ToArray();
                foreach (var source in sources)
                    for (int sample = 0; sample < 3; sample++) yield return Draw(fixture, sample, source);
                for (int sample = 0; sample < 3; sample++)
                {
                    Begin(fixture, "undo_redo", sample);
                    Call(() => SketchMemoryScript.m_Instance.StepBack());
                    yield return null;
                    Call(() => SketchMemoryScript.m_Instance.StepForward());
                    yield return null;
                    End();
                }
            }
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
        static IEnumerator Visibility(string fixture, string name, int sample, Stroke[] strokes, Batch[] batches)
        {
            var meshes = batches.Select(b => b.GetComponent<MeshFilter>().sharedMesh).ToArray();
            var original = meshes.Select(m => m.triangles).ToArray();
            Begin(fixture, name, sample);
            Call(() => { foreach (var stroke in strokes) stroke.Hide(true); });
            yield return null;
            Call(() => { foreach (var stroke in strokes) stroke.Hide(false); });
            yield return null;
            End();
            for (int i = 0; i < meshes.Length; i++) if (!original[i].SequenceEqual(meshes[i].triangles)) throw new InvalidOperationException("Visibility failed to restore indices");
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
            s_Phase.brushGuid = source.m_BrushGuid.ToString(); s_Phase.brushName = brush.m_DurableName;
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
            s_Phase = new Phase { fixture = fixture, name = name, sample = sample, qualityLevel = QualitySettings.GetQualityLevel() };
            s_Intervals = new List<double>(256); BatchWorkloadCounters.Reset(); BatchWorkloadCounters.Enabled = true;
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
            s_Phase.census = Count(); s_Report.phases.Add(s_Phase);
            Debug.Log($"[BatchWorkloadBenchmark] {Path.GetFileName(s_Phase.fixture)} {s_Phase.name} {s_Phase.sample}: {s_Phase.wallMs:F2}ms, {s_Phase.census.batchedStrokes} batched strokes");
            s_Phase = null; Save();
        }
        static void Call(Action action)
        {
            long start = Stopwatch.GetTimestamp(); action();
            if (s_Phase != null) s_Phase.directCallCpuMs += (Stopwatch.GetTimestamp() - start) * ToMs;
        }
        static Census Count()
        {
            var strokes = SketchMemoryScript.AllStrokes().ToArray();
            var batches = strokes.Where(s => s.m_Type == Stroke.Type.BatchedBrushStroke).Select(s => s.m_BatchSubset.m_ParentBatch).Distinct().ToArray();
            return new Census
            {
                strokes = strokes.Length, batchedStrokes = strokes.Count(s => s.m_Type == Stroke.Type.BatchedBrushStroke),
                solitaryStrokes = strokes.Count(s => s.m_Type == Stroke.Type.BrushStroke), notCreated = strokes.Count(s => s.m_Type == Stroke.Type.NotCreated),
                batches = batches.Length, residentBatches = batches.Count(b => b.Geometry.IsGeometryResident),
                vertices = batches.Sum(b => (long)b.Geometry.NumVerts), indices = batches.Sum(b => (long)b.Geometry.NumTriIndices),
                cachedIndexBytes = batches.Sum(b => (b.Geometry.m_Tris?.Capacity ?? 0) * 4L),
                cachedVertexBytes = batches.Sum(b => (b.Geometry.m_Vertices?.Capacity ?? 0) * 12L + (b.Geometry.m_Normals?.Capacity ?? 0) * 12L + (b.Geometry.m_Tangents?.Capacity ?? 0) * 16L + (b.Geometry.m_Colors?.Capacity ?? 0) * 4L
                    + (b.Geometry.m_Texcoord0.v2?.Capacity ?? 0) * 8L + (b.Geometry.m_Texcoord0.v3?.Capacity ?? 0) * 12L + (b.Geometry.m_Texcoord0.v4?.Capacity ?? 0) * 16L
                    + (b.Geometry.m_Texcoord1.v2?.Capacity ?? 0) * 8L + (b.Geometry.m_Texcoord1.v3?.Capacity ?? 0) * 12L + (b.Geometry.m_Texcoord1.v4?.Capacity ?? 0) * 16L
                    + (b.Geometry.m_Texcoord2.v2?.Capacity ?? 0) * 8L + (b.Geometry.m_Texcoord2.v3?.Capacity ?? 0) * 12L + (b.Geometry.m_Texcoord2.v4?.Capacity ?? 0) * 16L)
            };
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
