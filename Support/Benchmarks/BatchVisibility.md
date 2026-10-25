**Batch visibility: partial index uploads**

Measured 25 October 2026 against the Batch implementation at commit 2656a5248. This is the second implementation step after triangle-ID generation.

Small visibility edits now coalesce into one changed index span and use Mesh.SetIndexBufferData. UInt16 meshes use one shared conversion buffer capped at 16,384 indices (32 KiB payload); UInt32 buffers use the existing int list directly. Dense spans covering at least half the buffer, or UInt16 spans exceeding the cap, use the existing full SetTriangles path with bounds recalculation disabled. Full vertex rebuilds reset the dirty span.

Index positions, index count, submesh layout, triangle backups, and geometry eviction policy remain unchanged. Conservative bounds are retained across visibility changes. This costs two integer fields per batch and at most 32 KiB shared scratch, rather than a second index array per batch. Initial scratch growth allocates; the figures below are after warmup.

**Results: CPU milliseconds per hide + flush + show + flush pair.**

| Fixture | Before median | After median | Ratio | Before p95 | After p95 | Allocation before / after |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| small_single | 0.0079 | 0.0052 | 1.51x | 0.0084 | 0.0059 | 0 / 0 B |
| large_single | 0.0670 | 0.0052 | 12.79x | 0.0725 | 0.0097 | 0 / 0 B |
| large_adjacent | 0.0779 | 0.0234 | 3.33x | 0.1070 | 0.0402 | 0 / 0 B |
| large_spread | 0.0701 | 0.0478 | 1.47x | 0.0781 | 0.0621 | 0 / 0 B |
| large_all | 0.2559 | 0.2433 | 1.05x | 0.2689 | 0.2991 | 0 / 0 B |
| large_cold_single | 0.1749 | 0.1096 | 1.60x | 0.2381 | 0.1811 | 928,200 / 928,200 B |
| large_uint32_single | 0.0676 | 0.0041 | 16.49x | 0.0722 | 0.0043 | 0 / 0 B |

The large batch contains 15,996 vertices and 23,994 indices in strokes of 32 quads (the final stroke is shorter). A single stroke changes 192 indices, so a UInt16 partial update submits 384 bytes per flush instead of the old 47,988-byte index payload. These are API payload sizes, not measured graphics-driver transfers. Adjacent selects eight neighboring strokes; spread selects first/middle/last; all selects every stroke. The small batch has 1,024 vertices.

All warm resident cases allocated zero bytes before and after. Cold batches still allocate 928,200 bytes to rehydrate their complete GeometryPool; this step does not eliminate that cost. Estimated Unity Mesh memory is unchanged. Source ownership and peak process/graphics memory need separate device profiling.

Bounds were already preserved by the old path in this Unity 6.6 fixture. The new path explicitly preserves them, but these measurements do not establish a pre-existing bounds correctness failure. Index readback agrees with the expected hidden/restored data in all fixtures.

**Reproduce.**

1. Open the project in Unity 6000.6.0f1, wait for imports/compilation and test cleanup to settle, leave Play mode, and disable profiler recording. Avoid competing CPU work.
2. Run from the project root in host PowerShell:

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -File Support/Benchmarks/Run-BatchVisibilityBenchmark.ps1 -Label visibility-repeat-01
```

3. Repeat with distinct labels. Each run has six warmups and 41 samples per fixture. Resident samples average 16 pairs; cold samples contain one pair, with eviction performed before the timed region. Real Batch.DisableSubset, EnableSubset and FlushMeshUpdates methods are called; reflection is limited to constructing isolated test batches.
4. JSON output is saved under Logs/BrushMeshBenchmarks and survives Editor shutdown. It includes source and fixture hashes, the benchmark hash, hardware, Unity version, raw timings, three separate allocation probes, and correctness results. Allocation probes use Unity's calibrated global GC Allocated In Frame counter outside timed regions.
5. Regenerate the saved comparison:

```powershell
python Support/Benchmarks/analyze_batch_visibility.py --before Support/Benchmarks/Results/2026-10-25-visibility/visibility-baseline-01.json Support/Benchmarks/Results/2026-10-25-visibility/visibility-baseline-02.json --after Support/Benchmarks/Results/2026-10-25-visibility/visibility-final-01.json Support/Benchmarks/Results/2026-10-25-visibility/visibility-final-02.json Support/Benchmarks/Results/2026-10-25-visibility/visibility-final-03.json --output Logs/BrushMeshBenchmarks/visibility-summary.json
```

6. To establish a new actual baseline, run this same harness against the desired baseline source before editing it. The two saved baseline runs precede the Batch source change; the three saved final runs follow successful tests and compilation. Unlike the triangle-ID harness, this benchmark does not carry a duplicate implementation. Trial runs collected during test-runner cleanup/compilation are not included in the final comparison.

**Validation boundary.** Six EditMode cases pass: both index formats, resident and evicted geometry, repeated hide/show, successive dirty spans, scattered updates, empty subsets, and unchanged bounds/index count. The fixture explicitly owns and destroys the MeshFilter mesh clone; its first EditMode mesh access produces Unity's known clone warning outside measurement, which the tests expect. There is no retained mesh leak from fixture teardown.

The timings are from one Windows Editor on Ryzen 7 7800X3D / RTX 4090 / Direct3D12. CPU submission is measured, not GPU completion or application FPS. Editor SelfCheck calls are included. Desktop players, Android/IL2CPP, visual picking/highlighting, and peak device RAM have not been measured. No Android device was connected.

UInt32 tests cover raw buffer width with indices below 65,536. The existing ushort visibility backups still limit restoring larger absolute vertex indices; this pre-existing LargeMeshSupport issue is not fixed by the upload change. The existing 16-bit picking triangle-ID limit also remains. Do not treat this change as a validation of arbitrarily large batches.

The next memory-focused opportunity is avoiding full geometry rehydration for cold visibility edits. It needs its own ownership and correctness design and should preserve these cold-operation baselines.

API reference: [SetIndexBufferData](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh.SetIndexBufferData.html).
