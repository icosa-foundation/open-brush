// Run in the initialized Main-scene Play session. Results are local; this file is test code.
// PowerShell, from the project root (after AppWorkloadSetup.Start has reached Standard):
// $arguments = ConvertTo-Json -InputObject @('Logs/BrushMeshBenchmarks/finalization-parity.json') -Compress
// unity command run_script --file Support/Benchmarks/BatchFinalizationParity.cs --entry TiltBrush.Benchmarks.BatchFinalizationParity.Run --args $arguments --project-path $PWD --json
// Wait for the output JSON status to become passed or failed; the command starts a coroutine.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
namespace TiltBrush.Benchmarks
{
    public static class BatchFinalizationParity
    {
        public sealed class Case
        {
            public string fixture, brush, brushType, mode, beforeHash, afterHash, error;
            public int points, vertices, indices;
            public bool geometryEqual, temporaryMeshUnchanged;
        }
        public sealed class Result
        {
            public string status = "running", error, unity;
            public List<Case> cases = new List<Case>();
        }
        static Result s_Result;
        static string s_Output;
        static bool s_PreviousBirthTime;
        public static string Run(string output)
        {
            if (!Application.isPlaying || App.CurrentState != App.AppState.Standard) throw new InvalidOperationException("App must be ready in Play mode");
            if (UnityEditor.SessionState.GetBool("BatchWorkloadRunning", false)) throw new InvalidOperationException("Timing workload still running");
            if (UnityEditor.SessionState.GetBool("BatchFinalizationParityRunning", false)) throw new InvalidOperationException("Parity check already running");
            UnityEditor.SessionState.SetBool("BatchFinalizationParityRunning", true);
            s_Output = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(s_Output));
            s_Result = new Result { unity = Application.unityVersion };
            s_PreviousBirthTime = App.Config.m_ForceDeterministicBirthTimeForExport;
            App.Config.m_ForceDeterministicBirthTimeForExport = true;
            Save(); App.Instance.StartCoroutine(Guarded());
            return $"[BatchFinalizationParity] Started; output {s_Output}";
        }
        static IEnumerator Guarded()
        {
            using (var work = Work())
            {
                while (true)
                {
                    bool next;
                    try { next = work.MoveNext(); }
                    catch (Exception e) { s_Result.error = e.ToString(); s_Result.status = "failed"; Finish(); yield break; }
                    if (!next) break;
                    yield return work.Current;
                }
            }
            s_Result.status = s_Result.cases.All(c => c.geometryEqual && c.temporaryMeshUnchanged && c.error == null) ? "passed" : "failed";
            Finish();
        }
        static void Finish()
        {
            App.Config.m_ForceDeterministicBirthTimeForExport = s_PreviousBirthTime;
            UnityEditor.SessionState.SetBool("BatchFinalizationParityRunning", false);
            Save(); Debug.Log($"[BatchFinalizationParity] {s_Result.status}: {s_Result.cases.Count} cases; {s_Result.error}");
        }
        static IEnumerator<object> Work()
        {
            // The existing Editor helper parses recorded strokes without changing the sketch.
            var helper = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TiltBrush.TestBrush")).FirstOrDefault(t => t != null);
            if (helper == null) throw new InvalidOperationException("TestBrush fixture parser unavailable");
            var read = helper.GetMethod("GetStrokesFromTilt", BindingFlags.Public | BindingFlags.Static);
            var files = Directory.GetFiles("Support/Sketches/PerfTest", "*.tilt").OrderBy(p => p).Concat(new[] { "Support/Sketches/Promoted/TiltMech.tilt" });
            foreach (var path in files)
            {
                var strokes = (List<Stroke>)read.Invoke(null, new object[] { Path.GetFullPath(path) });
                foreach (var group in strokes.Where(s => s.m_ControlPoints.Length >= 2).GroupBy(s => s.m_BrushGuid))
                {
                    var ordered = group.OrderBy(s => s.m_ControlPoints.Length).ToArray();
                    foreach (var source in new[] { ordered[0], ordered[ordered.Length / 2], ordered[ordered.Length - 1] }.Distinct())
                    {
                        var desc = BrushCatalog.m_Instance.GetBrush(source.m_BrushGuid);
                        if (desc == null || !desc.m_BrushPrefab.GetComponent<BaseBrushScript>().m_bCanBatch) continue;
                        foreach (bool progressive in new[] { false, true })
                        {
                            var record = new Case { fixture = path, brush = desc.m_DurableName, brushType = desc.m_BrushPrefab.GetComponent<BaseBrushScript>().GetType().Name, mode = progressive ? "progressive-final-step" : "whole-stroke", points = Math.Min(128, source.m_ControlPoints.Length) };
                            try
                            {
                                bool unchanged;
                                record.beforeHash = Build(source, desc, false, progressive, out _, out _, out _);
                                record.afterHash = Build(source, desc, true, progressive, out unchanged, out int vertices, out int indices);
                                record.vertices = vertices; record.indices = indices;
                                record.geometryEqual = record.beforeHash == record.afterHash; record.temporaryMeshUnchanged = unchanged;
                            }
                            catch (Exception e) { record.error = e.ToString(); }
                            s_Result.cases.Add(record); Save();
                            // Release temporary native meshes between cases, outside any timing workload.
                            yield return null;
                        }
                    }
                }
            }
        }
        static string Build(Stroke source, BrushDescriptor desc, bool fast, bool progressive, out bool temporaryUnchanged, out int vertices, out int indices)
        {
            var root = new GameObject("BatchFinalizationParity canvas");
            root.transform.SetParent(App.Scene.transform, false);
            var canvas = root.AddComponent<CanvasScript>();
            BaseBrushScript brush = null;
            try
            {
                var cp0 = source.m_ControlPoints[0];
                brush = BaseBrushScript.Create(canvas.transform, TrTransform.TRS(cp0.m_Pos, cp0.m_Orient, source.m_BrushScale), desc, source.m_Color, source.m_BrushSize);
                brush.SetIsLoading(); brush.RandomSeed = source.m_Seed; brush.SetStrokeData(source);
                int count = Math.Min(128, source.m_ControlPoints.Length);
                for (int i = 0; i < count; i++)
                {
                    if (brush.IsOutOfVerts()) break;
                    var cp = source.m_ControlPoints[i];
                    brush.UpdatePosition_LS(TrTransform.TRS(cp.m_Pos, cp.m_Orient, source.m_BrushScale), cp.m_Pressure, source.GetColor(i));
                    if (progressive && i < count - 1) brush.ApplyChangesToVisuals();
                }
                var temporary = brush.GetComponent<MeshFilter>().sharedMesh;
                string before = Snapshot(temporary);
                if (fast) brush.PrepareForBatching(); else brush.ApplyChangesToVisuals();
                temporaryUnchanged = !fast || before == Snapshot(temporary);
                var subset = brush.FinalizeBatchedBrush();
                if (subset == null) throw new InvalidOperationException("Missing finalized subset");
                canvas.BatchManager.FlushMeshUpdates();
                var mesh = subset.m_ParentBatch.GetComponent<MeshFilter>().sharedMesh;
                vertices = mesh.vertexCount; indices = (int)mesh.GetIndexCount(0);
                return Snapshot(mesh);
            }
            finally
            {
                if (brush != null) { brush.DestroyMesh(); Object.DestroyImmediate(brush.gameObject); }
                canvas.BatchManager.ResetPools(); Object.DestroyImmediate(root);
            }
        }
        static string Snapshot(Mesh mesh)
        {
            if (mesh == null) return "null";
            using (var sha = SHA256.Create())
            {
                var description = $"{mesh.vertexCount}/{mesh.indexFormat}/{mesh.subMeshCount}/{mesh.bounds.ToString("R")}/";
                description += string.Join(";", mesh.GetVertexAttributes().Select(a => $"{a.attribute}/{a.format}/{a.dimension}/{a.stream}"));
                byte[] meta = Encoding.UTF8.GetBytes(description); sha.TransformBlock(meta, 0, meta.Length, meta, 0);
                // Dispose each snapshot before any mesh is changed: no retained native shadow copy.
                using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    byte[] chunk = new byte[4096];
                    for (int stream = 0; stream < data[0].vertexBufferCount; stream++)
                    {
                        var bytes = data[0].GetVertexData<byte>(stream);
                        for (int first = 0; first < bytes.Length; first += chunk.Length)
                        {
                            int length = Math.Min(chunk.Length, bytes.Length - first);
                            for (int i = 0; i < length; i++) chunk[i] = bytes[first + i];
                            sha.TransformBlock(chunk, 0, length, chunk, 0);
                        }
                    }
                }
                // Index getters are only used by this untimed correctness check.
                byte[] indexBytes = new byte[4];
                foreach (int index in mesh.triangles)
                {
                    for (int i = 0; i < 4; i++) indexBytes[i] = (byte)(index >> (i * 8));
                    sha.TransformBlock(indexBytes, 0, 4, indexBytes, 0);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
        static void Save()
        {
            var serializer = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject", new[] { typeof(object) });
            File.WriteAllText(s_Output, (string)serializer.Invoke(null, new object[] { s_Result }));
        }
    }
}
