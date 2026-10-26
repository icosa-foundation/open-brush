// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
// Frozen reference methods are from GeometryPool.cs at 39b088aab.
// Outside Assets: run with Pipeline run_script; also usable in an opt-in benchmark player.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace TiltBrush.Benchmarks
{
    public static class BrushTriangleIdsBenchmark
    {
        const int Samples = 31, Warmups = 5;
        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [Serializable]
        public class Measurement
        {
            public double milliseconds;
            public int collections;
        }

        [Serializable]
        public class CaseResult
        {
            public string name, fixtureSha256;
            public int vertices, indices, sourceVertexOffset, sourceIndexOffset, callsPerSample;
            public int legacyIncorrectIds, currentIncorrectIds;
            public long legacyMeshBytes, currentMeshBytes, legacyAllocatedBytes, currentAllocatedBytes;
            public long[] legacyAllocationProbes, currentAllocationProbes;
            public Measurement[] legacy, current;
        }

        [Serializable]
        public class Result
        {
            public int schemaVersion = 1;
            public string label, utc, unity, platform, os, cpu, graphicsDevice, graphicsApi;
            public string baselineRevision = "39b088aab";
            public string productionSha256, benchmarkSha256;
            public bool isEditor, developmentBuild, incrementalGc;
            public int cpuCount, systemMemoryMb, samples = Samples, warmups = Warmups;
            public long stopwatchFrequency;
            public string scope = "CPU triangle-ID generation and Mesh.SetUVs only. No draw, GPU fence, or app frame timing. Allocation probes use Unity's global GC Allocated In Frame counter outside timing (three repeats expose background noise); mesh bytes are Unity's estimate, not peak RSS.";
            public CaseResult[] cases;
        }

        public static string Run(string label, string output)
        {
            if (AllocatedBytes(() => GC.KeepAlive(new byte[4096])) < 4096)
                throw new InvalidOperationException("Allocation counter calibration failed; do not report zero allocation.");
            var result = new Result
            {
                label = label, utc = DateTime.UtcNow.ToString("o"),
                unity = Application.unityVersion, platform = Application.platform.ToString(),
                os = SystemInfo.operatingSystem, cpu = SystemInfo.processorType,
                cpuCount = SystemInfo.processorCount, systemMemoryMb = SystemInfo.systemMemorySize,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                isEditor = Application.isEditor, developmentBuild = UnityEngine.Debug.isDebugBuild,
                incrementalGc = UnityEngine.Scripting.GarbageCollector.isIncremental,
                stopwatchFrequency = Stopwatch.Frequency,
                productionSha256 = HashFile("Assets/Scripts/Brushes/GeometryPool.cs"),
                benchmarkSha256 = HashFile("Support/Benchmarks/BrushTriangleIdsBenchmark.cs"),
                cases = new[]
                {
                    RunCase("full_256_quads", 256, 0, false, 8),
                    RunCase("full_3999_quads", 3999, 0, false, 8),
                    RunCase("subset_64_quads_zero_offset", 64, 0, true, 1),
                    RunCase("subset_256_quads_zero_offset", 256, 0, true, 1),
                    RunCase("subset_256_quads_offset_12", 256, 12, true, 1),
                }
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, (string)Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject", new[] { typeof(object) }).Invoke(null, new object[] { result }));
            string summary = string.Join("; ", result.cases.Select(c =>
                $"{c.name}: old={Median(c.legacy):F4}ms current={Median(c.current):F4}ms, alloc={c.legacyAllocatedBytes}/{c.currentAllocatedBytes}B, incorrect IDs={c.legacyIncorrectIds}/{c.currentIncorrectIds}"));
            UnityEngine.Debug.Log($"[BrushBatchBenchmark] {label}: {summary}; output={output}");
            return summary;
        }

        static CaseResult RunCase(string name, int quads, int prefixVertices, bool subset, int calls)
        {
            var pool = new GeometryPool();
            pool.Layout = new GeometryPool.VertexLayout();
            int vertexCount = quads * 4;
            for (int i = 0; i < prefixVertices + vertexCount; ++i)
                pool.m_Vertices.Add(new Vector3(i % 97, (i / 97) % 31, i * 0.001f));
            for (int i = 0; i < prefixVertices; i += 3)
                pool.m_Tris.AddRange(new[] { i, i + 1, i + 2 });
            int firstIndex = pool.m_Tris.Count;
            for (int q = 0; q < quads; ++q)
            {
                int v = prefixVertices + q * 4;
                pool.m_Tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            int indexCount = quads * 6;
            var vertices = pool.m_Vertices.GetRange(prefixVertices, vertexCount).ToArray();
            var indices = pool.m_Tris.GetRange(firstIndex, indexCount).Select(i => i - prefixVertices).ToArray();
            var legacyMesh = CreateMesh(vertices, indices);
            var currentMesh = CreateMesh(vertices, indices);
            try
            {
                Action legacy = subset
                    ? (Action)(() => LegacySubset(legacyMesh, prefixVertices, vertexCount))
                    : () => LegacyFull(legacyMesh);
                Action current = BindCurrent(pool, currentMesh, prefixVertices, vertexCount, firstIndex, indexCount, subset);
                for (int i = 0; i < Warmups; ++i) { legacy(); current(); }
                var result = new CaseResult
                {
                    name = name, vertices = vertexCount, indices = indexCount,
                    sourceVertexOffset = prefixVertices, sourceIndexOffset = firstIndex,
                    fixtureSha256 = HashFixture(vertices, indices, prefixVertices, firstIndex),
                    callsPerSample = calls,
                    legacyIncorrectIds = CountIncorrectIds(legacyMesh, indices),
                    currentIncorrectIds = CountIncorrectIds(currentMesh, indices),
                    legacyMeshBytes = Profiler.GetRuntimeMemorySizeLong(legacyMesh),
                    currentMeshBytes = Profiler.GetRuntimeMemorySizeLong(currentMesh),
                    legacy = new Measurement[Samples], current = new Measurement[Samples]
                };
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                // Include collection pressure. No forced collections within the timed series.
                // Alternate paired order to reduce systematic drift.
                for (int i = 0; i < Samples; ++i)
                {
                    if ((i & 1) == 0)
                    {
                        result.legacy[i] = Measure(legacy, calls);
                        result.current[i] = Measure(current, calls);
                    }
                    else
                    {
                        result.current[i] = Measure(current, calls);
                        result.legacy[i] = Measure(legacy, calls);
                    }
                }
                result.legacyAllocationProbes = new[] { AllocatedBytes(legacy), AllocatedBytes(legacy), AllocatedBytes(legacy) };
                result.currentAllocationProbes = new[] { AllocatedBytes(current), AllocatedBytes(current), AllocatedBytes(current) };
                result.legacyAllocatedBytes = result.legacyAllocationProbes.Min();
                result.currentAllocatedBytes = result.currentAllocationProbes.Min();
                return result;
            }
            finally
            {
                Object.DestroyImmediate(legacyMesh);
                Object.DestroyImmediate(currentMesh);
                pool.Destroy();
            }
        }

        static Action BindCurrent(GeometryPool pool, Mesh mesh, int vertexStart, int vertexCount,
            int indexStart, int indexCount, bool subset)
        {
            if (!subset)
            {
                var method = typeof(GeometryPool).GetMethod("GenerateTriangleIds", PrivateInstance,
                    null, new[] { typeof(Mesh) }, null);
                var action = (Action<Mesh>)Delegate.CreateDelegate(typeof(Action<Mesh>), pool, method);
                return () => action(mesh);
            }
            var updated = typeof(GeometryPool).GetMethod("GenerateTriangleIds", PrivateInstance,
                null, new[] { typeof(Mesh), typeof(int), typeof(int), typeof(int), typeof(int) }, null);
            if (updated != null)
            {
                var action = (Action<Mesh, int, int, int, int>)Delegate.CreateDelegate(
                    typeof(Action<Mesh, int, int, int, int>), pool, updated);
                return () => action(mesh, vertexStart, vertexCount, indexStart, indexCount);
            }
            var original = typeof(GeometryPool).GetMethod("GenerateTriangleIds", PrivateInstance,
                null, new[] { typeof(Mesh), typeof(int), typeof(int) }, null);
            var oldAction = (Action<Mesh, int, int>)Delegate.CreateDelegate(typeof(Action<Mesh, int, int>), pool, original);
            return () => oldAction(mesh, vertexStart, vertexCount);
        }

        static Measurement Measure(Action action, int count)
        {
            int collections = GC.CollectionCount(0);
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < count; ++i) action();
            long elapsed = Stopwatch.GetTimestamp() - start;
            return new Measurement
            {
                milliseconds = elapsed * 1000.0 / Stopwatch.Frequency / count,
                collections = GC.CollectionCount(0) - collections
            };
        }

        static long AllocatedBytes(Action action)
        {
            // GC.GetAllocatedBytesForCurrentThread returns zero on the tested Unity Mono runtime.
            // Probe Unity's byte counter separately from timing, and restore profiling state.
            bool enabled = Profiler.enabled;
#if UNITY_EDITOR
            bool profileEditor = UnityEditorInternal.ProfilerDriver.profileEditor;
#endif
            try
            {
#if UNITY_EDITOR
                UnityEditorInternal.ProfilerDriver.profileEditor = true;
#endif
                Profiler.enabled = true;
                using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
                {
                    if (!recorder.Valid) throw new InvalidOperationException("Allocation counter unavailable.");
                    long before = recorder.CurrentValue;
                    action();
                    return recorder.CurrentValue - before;
                }
            }
            finally
            {
                Profiler.enabled = enabled;
#if UNITY_EDITOR
                UnityEditorInternal.ProfilerDriver.profileEditor = profileEditor;
#endif
            }
        }

        static Mesh CreateMesh(Vector3[] vertices, int[] indices)
        {
            var mesh = new Mesh();
            mesh.MarkDynamic(); mesh.vertices = vertices; mesh.triangles = indices;
            return mesh;
        }

        static int CountIncorrectIds(Mesh mesh, int[] indices)
        {
            var expected = new Vector2[mesh.vertexCount];
            for (int i = 0; i < indices.Length; ++i) expected[indices[i]] = new Vector2(i / 3, 0);
            var actual = new List<Vector2>();
            mesh.GetUVs(4, actual);
            if (actual.Count != expected.Length) return expected.Length;
            int errors = 0;
            for (int i = 0; i < expected.Length; ++i) if (actual[i] != expected[i]) ++errors;
            return errors;
        }

        static double Median(Measurement[] data) => data.Select(x => x.milliseconds).OrderBy(x => x).ElementAt(data.Length / 2);
        static string HashFile(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : "unavailable-in-player";

        static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static string HashFixture(Vector3[] vertices, int[] indices, int vertexStart, int indexStart)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(vertexStart); writer.Write(indexStart);
                foreach (var v in vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                foreach (var i in indices) writer.Write(i);
                writer.Flush();
                return Hash(stream.ToArray());
            }
        }

        // Frozen baseline: intentionally retains array getters and the source/local-index defect.
        static void LegacyFull(Mesh mesh)
        {
            var triangleIds = new Vector2[mesh.vertexCount];
            int triCount = mesh.triangles.Length / 3;
            var tris = mesh.triangles;
            for (int i = 0; i < triCount; i++)
            {
                Vector2 uv = new Vector2(i, 0);
                int triIndex = i * 3;
                triangleIds[tris[triIndex]] = uv;
                triangleIds[tris[triIndex + 1]] = uv;
                triangleIds[tris[triIndex + 2]] = uv;
            }
            mesh.SetUVs(4, triangleIds);
        }

        static void LegacySubset(Mesh mesh, int iVert, int nVert)
        {
            var triangleIds = new Vector2[nVert];
            for (int i = 0; i < mesh.triangles.Length; i += 3)
            {
                int triIndex = Mathf.FloorToInt(i / 3);
                Vector2 uv = new Vector2(triIndex, 0);
                if (mesh.triangles[i] >= iVert && mesh.triangles[i] < iVert + nVert)
                    triangleIds[mesh.triangles[i] - iVert] = uv;
                if (mesh.triangles[i + 1] >= iVert && mesh.triangles[i + 1] < iVert + nVert)
                    triangleIds[mesh.triangles[i + 1] - iVert] = uv;
                if (mesh.triangles[i + 2] >= iVert && mesh.triangles[i + 2] < iVert + nVert)
                    triangleIds[mesh.triangles[i + 2] - iVert] = uv;
            }
            mesh.SetUVs(4, triangleIds);
        }
    }
}

