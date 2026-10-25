For the second optimization, see [batch visibility benchmarks](BatchVisibility.md).

**Brush triangle-ID benchmark**

This benchmark accompanies the first brush batching optimization: fallback triangle-ID generation. It measures CPU ID generation plus `Mesh.SetUVs`, using deterministic shared-vertex quad fixtures. It does not measure rendered frames, GPU upload completion, selection end-to-end, or desktop/mobile player performance.

1. **Requirements.** Open this project in its specified Unity Editor, wait for compilation/imports to finish, leave Play mode, and close/disable profiler recording. The Unity Pipeline package and CLI must be available. Avoid other CPU-heavy jobs while collecting results. The runner checks that the Editor is ready and stopped.
2. **Run from host PowerShell.** From the project root, execute:

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -File Support/Benchmarks/Run-BrushTriangleIdsBenchmark.ps1 -Label after-01
```

3. **Repeat independently.** Use labels `after-02` and `after-03`. Raw samples and the command envelope go to `Temp/BrushMeshBenchmarks/<label>.json` and `<label>.command.json`. Keep successful raw measurement files; the command envelope alone is not a measurement. Inactive application windows must not be treated as an unattended device benchmark.
4. **Compare preserved runs.** The saved result files under `Results/2026-10-25-editor` include two actual pre-change runs and three post-change runs. Summarize them reproducibly with:

```powershell
python Support/Benchmarks/analyze_brush_triangle_ids.py --before Support/Benchmarks/Results/2026-10-25-editor/baseline-01.json Support/Benchmarks/Results/2026-10-25-editor/baseline-02.json --after Support/Benchmarks/Results/2026-10-25-editor/after-01.json Support/Benchmarks/Results/2026-10-25-editor/after-02.json Support/Benchmarks/Results/2026-10-25-editor/after-03.json --output Temp/BrushMeshBenchmarks/summary.json
```

5. **Retained baseline.** `LegacyFull` and `LegacySubset` are benchmark-only copies of the production algorithms at `39b088aab`. They deliberately preserve the old nonzero-offset indexing defect. The runner binds the loaded production method once, outside measurement, and alternates reference/current execution order. Before the implementation changed, both paths were measured to confirm matching outputs and allocation behavior. On later revisions, the retained reference allows same-session A/B comparisons without rolling back the checkout.
6. **Timing protocol.** Each fixture gets five warmups per variant, then 31 paired samples. Full-path samples average eight calls; subset samples use one call because the old implementation allocates heavily. Timing uses `Stopwatch.GetTimestamp`. Fixtures, mesh creation, delegates, correctness checks, serialization, allocation probes, and compilation are outside timed regions. One full collection is requested before the sample series; collections caused by the operations remain part of the workload. Interleaved variants share the GC heap, so old-path allocation pressure can affect current-path timings too.
7. **Memory protocol.** Allocation probes are separate from timing. Unity's `GC Allocated In Frame` counter is calibrated with an escaping 4 KiB array, with profiler state restored afterward. Three probes per variant are retained. It is a global counter: background allocations can contaminate it, so the analyzer reports the range rather than silently assuming every byte belongs to the tested call. The .NET allocation counter returned zero in this Editor and is not used. `GC.Alloc` recorder sample values represent timing, not allocated bytes, and are also not used as byte counts.
8. **Correctness and identity.** Results include fixture SHA-256, loaded production-source SHA-256, benchmark-source SHA-256, Unity version, hardware, graphics API, warmup/sample counts, wrong-ID counts, and estimated Mesh memory. The analyzer rejects environment/fixture mismatches and incorrect post-change IDs. The original nonzero-offset case has 1,024 wrong IDs; the optimized case must have none. Mesh memory uses `Profiler.GetRuntimeMemorySizeLong`; it is not process peak RSS or an exact GPU residency measurement.
9. **Recreating an actual pre-change baseline.** Use the same benchmark files against the original `GeometryPool.cs` revision, then repeat against the candidate. Do not replace source in a shared working tree while another task is active. Both old and new private signatures are supported. The baseline/after benchmark hashes in the stored run differ because C# whitespace formatting ran between them; the protocol and retained algorithms were unchanged.
10. **Scope before expanding rollout.** The Windows Editor explicitly invokes the fallback even though the desktop GPU supports geometry shaders. These figures establish the allocation/CPU mechanism, not mobile speedups. For device results, integrate this fixture into a dedicated benchmark player with direct method bindings or preserved reflection targets, build both comparisons with the same IL2CPP/development settings, record device thermals and power state, and measure peak native/process/graphics memory. A device player runner is not implemented in this phase. Do not label these Editor files as desktop-player or Android results.

The production change shares at most 16,384 Vector2 entries (128 KiB payload) across all pools. The measured zero allocations are **after warmup** and within that cap. Larger meshes allocate a temporary ID array and do not enlarge the retained cache. The GeometryPool test suite checks empty meshes, shared vertices, nonzero source ranges, smaller subsequent meshes, and oversized UInt32 geometry.

The test evidence is `Results/2026-10-25-editor/geometry-pool-tests.json`: 16 passed, including five new parameterized cases. The root `brush-batching-benchmark-results.md` contains the measured summary and current validation boundary.

