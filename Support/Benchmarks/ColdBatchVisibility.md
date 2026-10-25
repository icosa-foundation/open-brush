**Cold batch visibility: index-only reloads**

25 October 2026. Baseline production commit: `3827b8e7d`. This follows the partial-index-upload optimization.

Visibility edits on evicted batches now load only triangle indices with `Mesh.GetTriangles(List<int>, 0)`. Vertex channels stay evicted. A later full reload preserves pending index edits; existing age-based batch eviction flushes and releases the index-only cache. File-backed pools retain full reload behavior. No new per-batch scratch buffer or native persistent allocation is added.

**Windows Editor CPU time per hide + flush + show + flush pair.**

| Fixture | Before median ms | After median ms | Ratio | Before p95 ms | After p95 ms | Allocation before / after |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| small_single | 0.0051 | 0.0052 | 0.98x | 0.0059 | 0.0056 | 0 / 0 B |
| large_single | 0.0051 | 0.0052 | 0.99x | 0.0056 | 0.0059 | 0 / 0 B |
| large_adjacent | 0.0221 | 0.0225 | 0.98x | 0.0235 | 0.0295 | 0 / 0 B |
| large_spread | 0.0471 | 0.0475 | 0.99x | 0.0499 | 0.0507 | 0 / 0 B |
| large_all | 0.2365 | 0.2420 | 0.98x | 0.2431 | 0.3572 | 0 / 0 B |
| large_cold_single | 0.1074 | 0.0158 | 6.79x | 2.8091 | 0.0207 | 928,200 / 96,048 B |
| large_uint32_single | 0.0041 | 0.0041 | 0.99x | 0.0043 | 0.0046 | 0 / 0 B |
| large_cold_adjacent | 0.1191 | 0.0309 | 3.85x | 0.1578 | 0.0594 | 928,200 / 96,048 B |
| large_cold_all | 0.3387 | 0.2260 | 1.50x | 0.3730 | 0.4052 | 928,200 / 96,048 B |
| large_cold_uint32_single | 0.0985 | 0.0102 | 9.66x | 0.1278 | 0.0188 | 928,200 / 96,048 B |

The large fixture has 15,996 vertices and 23,994 indices; one stroke changes 192 indices. All four cold cases allocate 96,048 bytes instead of 928,200 bytes per pair (89.65% less). Allocation probes agree across all saved runs. This remains an allocation on each reload after eviction, not a zero-allocation cold path. Warm resident cases remain allocation-free.

After a cold pair, the managed index list retains 95,976 bytes of array payload in both versions. Retained vertex-channel array payload drops from 831,792 bytes to zero. These values derive from the actual list capacities for this fixture, exclude object headers, and are not process peak memory. Unity's reported Mesh memory remains 1,761,152 bytes in this fixture. The index cache is eligible for the existing batch-age eviction policy; the protected newest batches retain the existing exception.

Resident cases measured roughly 1–2% slower in this session. No resident speedup is claimed. Whole-batch cold p95 also increased from 0.3730 to 0.4052 ms despite its improved median; no universal tail-latency gain is claimed. Cold adjacent/all cases vary more between runs: the report retains every final run and raw sample, including the slower second after run. Two actual baseline runs preceded all production edits; three after runs followed successful regression tests. Comparisons use the identical extended harness, fixture, hardware and Unity version.

**Reproduce.**

1. Open this project in Unity 6000.6.0f1. Wait for compilation and test cleanup, leave Play mode, turn off profiler recording, and avoid competing CPU workloads.
2. Run from the project root in host PowerShell:

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -File Support/Benchmarks/Run-BatchVisibilityBenchmark.ps1 -Label cold-repeat-01
```

3. Repeat with distinct labels. The runner saves JSON and command responses under `Logs/BrushMeshBenchmarks`. Each of ten fixtures uses six warmups and 41 samples. Resident samples average 16 pairs; cold samples contain one pair, with eviction outside the timed region. Fixture construction, readback validation, memory inspection and three calibrated allocation probes are outside timing. The runner calls real Batch methods; there is no duplicated production algorithm.
4. To repeat the before/after experiment, retain this commit's three benchmark tools and run them with the three production files (`Batch.cs`, `BatchPool.cs`, `GeometryPool.cs`) from baseline `3827b8e7d`, then with the changed production files. Use a disposable benchmark checkout or save your working changes first. Both versions must use the same harness; the original seven-case harness from the baseline commit is a different protocol. Output records SHA-256 hashes of all three production files and the benchmark, plus fixture hashes and environment metadata.
5. Regenerate this comparison using the saved raw data:

```powershell
python Support/Benchmarks/analyze_batch_visibility.py --before Support/Benchmarks/Results/2026-10-25-cold-visibility/cold-baseline-01.json Support/Benchmarks/Results/2026-10-25-cold-visibility/cold-baseline-02.json --after Support/Benchmarks/Results/2026-10-25-cold-visibility/cold-after-01.json Support/Benchmarks/Results/2026-10-25-cold-visibility/cold-after-02.json Support/Benchmarks/Results/2026-10-25-cold-visibility/cold-after-03.json --output Logs/BrushMeshBenchmarks/cold-summary.json
```

6. Run EditMode classes `TiltBrush.TestBatchVisibility` and `TiltBrush.TestGeometryPool` separately. The Unity CLI test-name filter used here does not combine class names with `|`. Saved results contain 11/11 visibility tests and 17/17 GeometryPool tests passing.

**Validation and limits.** All benchmark index comparisons and bounds checks pass. New tests cover both index widths, pending edits followed by a full reload, eviction before an explicit flush, restoration after eviction, age-based index-cache eviction, and file-backed fallback. Existing triangle-ID and GeometryPool tests also pass. Test fixtures own and destroy their temporary meshes.

Measurements come from one Windows Editor on Ryzen 7 7800X3D / RTX 4090 / Direct3D12. Editor-only Batch.SelfCheck work is included and follows a different branch for nonresident vertex data, so player speedups can differ. The allocation and list-capacity results establish what this fixture reloads, not mobile peak RAM or application FPS. No desktop-player, Android/IL2CPP, GPU-completion, XR visual/picking or physical-device measurements were taken. Existing ushort subset backups and 16-bit triangle-ID limits remain unchanged; UInt32 fixtures use indices below 65,536.

[Raw measurements and test results](Results/2026-10-25-cold-visibility/) include pooled and per-run medians. [Unity GetTriangles API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.GetTriangles.html) documents the list overload used for the index-only readback.
