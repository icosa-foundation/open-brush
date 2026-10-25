// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
// Production Batch hide/show + flush benchmark. Compile via Pipeline run_script outside Assets.
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
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TiltBrush.Benchmarks
{
    public static class BatchVisibilityBenchmark
    {
        const int Samples = 41, Warmups = 6;
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public sealed class Fixture : IDisposable
        {
            public readonly GameObject GameObject;
            public readonly Batch Batch;
            public readonly GeometryPool Geometry;
            public readonly Mesh Mesh;
            public readonly BatchSubset[] Selected;
            public readonly int[] OriginalIndices;
            public readonly Bounds PaddedBounds;

            public Fixture(int quads, string selection, bool uint32)
            {
                GameObject = new GameObject("BrushBatchBenchmark");
                GameObject.hideFlags = HideFlags.HideAndDontSave;
                var filter = GameObject.AddComponent<MeshFilter>();
                Batch = GameObject.AddComponent<Batch>();
                Geometry = new GeometryPool
                {
                    Layout = new GeometryPool.VertexLayout
                    {
                        bUseNormals = true, bUseColors = true, bUseTangents = true,
                        texcoord0 = new GeometryPool.TexcoordInfo { size = 2 }
                    }
                };
                for (int i = 0; i < quads * 4; ++i)
                {
                    Geometry.m_Vertices.Add(new Vector3(i % 97, i / 97, i * 0.001f));
                    Geometry.m_Normals.Add(Vector3.up);
                    Geometry.m_Tangents.Add(new Vector4(1, 0, 0, 1));
                    Geometry.m_Colors.Add(new Color32(100, 200, 255, 255));
                    Geometry.m_Texcoord0.v2.Add(new Vector2(i % 2, (i / 2) % 2));
                }
                for (int q = 0; q < quads; ++q)
                {
                    int v = q * 4;
                    Geometry.m_Tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                }
                OriginalIndices = Geometry.m_Tris.ToArray();
                Mesh = new Mesh { indexFormat = uint32 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                Mesh.MarkDynamic();
                Mesh.SetVertices(Geometry.m_Vertices);
                Mesh.SetNormals(Geometry.m_Normals);
                Mesh.SetTangents(Geometry.m_Tangents);
                Mesh.SetColors(Geometry.m_Colors);
                Mesh.SetUVs(0, Geometry.m_Texcoord0.v2);
                Mesh.SetTriangles(Geometry.m_Tris, 0);
                var paddedBounds = Mesh.bounds;
                paddedBounds.Expand(10);
                PaddedBounds = paddedBounds;
                Mesh.bounds = PaddedBounds;
                filter.sharedMesh = Mesh;
                // Consume MeshFilter.mesh's first-access clone before measurement.
                var ownedMesh = filter.mesh;
                if (ownedMesh != Mesh)
                {
                    Object.DestroyImmediate(Mesh);
                    Mesh = ownedMesh;
                }
                var manager = new BatchManager();
                var pool = new BatchPool(manager) { m_Batches = new List<Batch> { Batch } };
                SetField(Batch, "m_ParentPool", pool);
                SetField(Batch, "m_MeshFilter", filter);
                SetField(Batch, "m_Geometry", Geometry);
                Batch.m_Groups = new List<BatchSubset>();
                for (int firstQuad = 0; firstQuad < quads; firstQuad += 32)
                {
                    int count = Math.Min(32, quads - firstQuad);
                    Batch.m_Groups.Add(new BatchSubset
                    {
                        m_ParentBatch = Batch, m_Active = true,
                        m_StartVertIndex = firstQuad * 4, m_VertLength = count * 4,
                        m_iTriIndex = firstQuad * 6, m_nTriIndex = count * 6
                    });
                }
                var groups = Batch.m_Groups;
                switch (selection)
                {
                    case "all": Selected = groups.ToArray(); break;
                    case "spread": Selected = new[] { groups[0], groups[groups.Count / 2], groups[groups.Count - 1] }; break;
                    case "adjacent": Selected = groups.Skip(groups.Count / 2).Take(Math.Min(8, groups.Count / 2)).ToArray(); break;
                    default: Selected = new[] { groups[groups.Count / 2] }; break;
                }
            }

            public void Prepare(bool cold)
            {
                Mesh.bounds = PaddedBounds;
                if (cold) Batch.ClearCachedGeometry();
            }

            public void Hide()
            {
                foreach (var subset in Selected) Batch.DisableSubset(subset);
                Batch.FlushMeshUpdates();
            }

            public void Show()
            {
                foreach (var subset in Selected) Batch.EnableSubset(subset);
                Batch.FlushMeshUpdates();
            }

            public void Cycle() { Hide(); Show(); }

            public int ValidateHidden()
            {
                var expected = (int[])OriginalIndices.Clone();
                foreach (var subset in Selected)
                    Array.Clear(expected, subset.m_iTriIndex, subset.m_nTriIndex);
                return expected.Zip(Mesh.triangles, (a, b) => a == b ? 0 : 1).Sum();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(GameObject);
                Object.DestroyImmediate(Mesh);
                Geometry.Destroy();
            }
        }

        public sealed class CaseResult
        {
            public string name, fixtureSha256;
            public bool cold, uint32;
            public int vertexCount, indexCount, changedIndices, callsPerSample, hiddenErrors, restoredErrors;
            public bool paddedBoundsPreserved;
            public long meshBytes;
            public double[] milliseconds;
            public long[] allocatedBytes;
        }

        public static string Run(string label, string output)
        {
            if (AllocatedBytes(() => GC.KeepAlive(new byte[4096])) < 4096)
                throw new InvalidOperationException("Allocation counter calibration failed.");
            var cases = new[]
            {
                RunCase("small_single", 256, "single", false, false),
                RunCase("large_single", 3999, "single", false, false),
                RunCase("large_adjacent", 3999, "adjacent", false, false),
                RunCase("large_spread", 3999, "spread", false, false),
                RunCase("large_all", 3999, "all", false, false),
                RunCase("large_cold_single", 3999, "single", true, false),
                RunCase("large_uint32_single", 3999, "single", false, true),
            };
            var result = new
            {
                schemaVersion = 1,
                label,
                utc = DateTime.UtcNow.ToString("o"),
                unity = Application.unityVersion,
                platform = Application.platform.ToString(),
                cpu = SystemInfo.processorType,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                incrementalGc = UnityEngine.Scripting.GarbageCollector.isIncremental,
                baselineRevision = "2656a5248",
                samples = Samples,
                warmups = Warmups,
                productionSha256 = Hash(File.ReadAllBytes("Assets/Scripts/Batching/Batch.cs")),
                benchmarkSha256 = Hash(File.ReadAllBytes("Support/Benchmarks/BatchVisibilityBenchmark.cs")),
                scope = "Production Batch.DisableSubset/EnableSubset and FlushMeshUpdates CPU time per hide+show pair. No rendering/GPU fence. Cold prepares eviction outside timing. Allocation probes use a global Unity counter outside timing.",
                cases
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var serializer = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json").GetMethod("SerializeObject", new[] { typeof(object) });
            File.WriteAllText(output, (string)serializer.Invoke(null, new object[] { result }));
            return string.Join("; ", cases.Select(c => $"{c.name}: {c.milliseconds.OrderBy(x => x).ElementAt(Samples / 2):F4}ms, {c.allocatedBytes.Min()}B, hidden/restored errors={c.hiddenErrors}/{c.restoredErrors}, bounds={c.paddedBoundsPreserved}"));
        }

        static CaseResult RunCase(string name, int quads, string selection, bool cold, bool uint32)
        {
            using (var fixture = new Fixture(quads, selection, uint32))
            {
                int calls = cold ? 1 : 16;
                Action cycle = fixture.Cycle;
                for (int i = 0; i < Warmups; ++i) { fixture.Prepare(cold); cycle(); }
                var result = new CaseResult
                {
                    name = name, cold = cold, uint32 = uint32,
                    vertexCount = fixture.Mesh.vertexCount, indexCount = fixture.OriginalIndices.Length,
                    changedIndices = fixture.Selected.Sum(s => s.m_nTriIndex),
                    callsPerSample = calls, meshBytes = Profiler.GetRuntimeMemorySizeLong(fixture.Mesh),
                    fixtureSha256 = Hash(System.Text.Encoding.UTF8.GetBytes($"{quads}/{selection}/{cold}/{uint32}/xyz-mod97/32quads-per-stroke/v1")),
                    milliseconds = new double[Samples], allocatedBytes = new long[3]
                };
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                for (int i = 0; i < Samples; ++i)
                {
                    fixture.Prepare(cold);
                    long start = Stopwatch.GetTimestamp();
                    for (int call = 0; call < calls; ++call) cycle();
                    result.milliseconds[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / calls;
                }
                for (int i = 0; i < result.allocatedBytes.Length; ++i)
                {
                    fixture.Prepare(cold);
                    result.allocatedBytes[i] = AllocatedBytes(cycle);
                }
                fixture.Prepare(cold);
                fixture.Hide();
                result.hiddenErrors = fixture.ValidateHidden();
                result.paddedBoundsPreserved = fixture.Mesh.bounds == fixture.PaddedBounds;
                fixture.Show();
                result.restoredErrors = fixture.OriginalIndices.Zip(fixture.Mesh.triangles, (a, b) => a == b ? 0 : 1).Sum();
                result.paddedBoundsPreserved &= fixture.Mesh.bounds == fixture.PaddedBounds;
                return result;
            }
        }

        static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

        static long AllocatedBytes(Action action)
        {
            bool enabled = Profiler.enabled;
            bool profileEditor = UnityEditorInternal.ProfilerDriver.profileEditor;
            try
            {
                UnityEditorInternal.ProfilerDriver.profileEditor = true;
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
                UnityEditorInternal.ProfilerDriver.profileEditor = profileEditor;
            }
        }

        static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}

