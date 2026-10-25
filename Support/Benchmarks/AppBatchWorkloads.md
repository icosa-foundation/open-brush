**Default batch-mode application workloads**

The earlier optimizations help visibility changes on older batches and reduce their managed cache size. These application measurements do not establish a quick-load, drawing or steady-viewing speedup on desktop. They change the next optimization priority: inspect per-stroke visual updates and batch appending before expanding the batch-upload rewrite.

Measured 25 October 2026: pre-optimization source `39b088aab` versus optimized source `9beb3fdcb`, in alternating order before-01, after-01, before-02, after-02. Each session completed 51 phases in the actual Main-scene application. All geometry censuses match, and visibility edits restored their original mesh indices. Each non-idle fixture/phase has six samples across two sessions; these are descriptive measurements, not a statistical significance claim.

**What default batch mode actually exercised.**

| Sketch | Loaded strokes, all batched | Batches | Completed-batch uploads per quick load | Per-stroke visual updates per quick load | Completed-batch vertex resubmission |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ink | 67 | 1 | 1 | 67 | 1.00x final vertex count |
| Blue_Tit | 471 | 7 | 10 | 471 | 1.12x final vertex count |
| TiltMech | 869 | 19 | 25 | 869 | 1.22x final vertex count |

Both batching and managed-geometry eviction were enabled at the scene defaults. The 120-frame idle phases performed zero full batch uploads and zero topology uploads in all four sessions. Ink fits in one protected newest batch, so its visibility edits never exercise cold vertex reloads. Blue Tit and TiltMech do contain aged, evictable batches.

The desktop reports geometry-shader support. The fallback triangle-ID generator ran **zero times** in every measured phase, so its microbenchmark improvement contributes nothing to this desktop result. That path still needs a representative mobile/device workload.

**Quick-load CPU scopes, pooled median milliseconds per load.** These scopes are nested: do not add columns to infer total application CPU time.

| Sketch | Whole measured load wall time before / after | Completed-batch uploads before / after | Per-stroke visual updates before / after | Batch appending after |
| --- | ---: | ---: | ---: | ---: |
| Ink | 192.02 / 197.53 | 0.38 / 0.38 | 9.74 / 9.43 | 0.73 |
| Blue_Tit | 648.37 / 681.92 | 3.19 / 3.36 | 64.47 / 64.02 | 8.52 |
| TiltMech | 790.35 / 798.98 | 7.60 / 8.00 | 129.86 / 141.55 | 27.36 |

No load speedup is claimed: wall times and some CPU scopes are higher after optimization. These desktop loading paths mostly do not use the code that was optimized. The timings include Editor variability and time-sliced playback; they are not evidence of an equivalent regression in a player. Both directions of variation remain in the raw data.

For TiltMech, the optimized run medians were 141.55 ms in Pointer.VisualUpdate, 52.91 ms in Pointer.ControlPoint, 27.36 ms in Batch.Append and 8.00 ms in completed-batch uploads. GeometryBrush.VisualUpdate accounts for 89.71 ms inside Pointer.VisualUpdate. GeometryPool.FullCopy totals 17.61 ms across both the geometry-brush and completed-batch paths. The 141.55 ms visual-update figure therefore must not be described as all mesh-upload cost: it includes procedural geometry work.

Paced replay made one visual update per retained recorded control point, then only one completed-batch upload when the stroke finished. The legacy Paper and geometry-based Wire cases each replayed 128 points; Ink and Blue Tit used their shorter recorded strokes where applicable. Replay CPU did not improve: for TiltMech, total directly invoked replay work was 9.44 to 9.85 ms for Paper and 7.47 to 8.15 ms for Wire. The roughly 2.14-second wall duration is driven by replay pacing, not geometry throughput.

**Visibility changes after natural cache eviction.** Each operation hides then restores up to 32 evenly spaced existing strokes, with a frame between changes so the app performs its normal deferred uploads. The CPU figures below sum directly timed command work and deferred Batch.TopologyUpload work, which are disjoint in this workload. They exclude frame waiting, rendering and unrelated app work. Each entry is the median of three operations in that session.

| Sketch | Before session 1 ms | After session 1 ms | Before session 2 ms | After session 2 ms |
| --- | ---: | ---: | ---: | ---: |
| Ink | 0.233 | 0.259 | 0.244 | 0.204 |
| Blue_Tit | 2.817 | 0.439 | 1.414 | 1.464 |
| TiltMech | 17.802 | 1.657 | 3.605 | 1.580 |

TiltMech improved in both sessions, but the baseline varies greatly. Blue Tit improved in the first pair and was slightly slower in the second pair despite its faster upload/readback scopes. There is no defensible single application-wide speedup factor from these runs. The stable memory effect is more decisive.

| Sketch | Managed vertex-channel array payload after visibility, before | After | Reduction |
| --- | ---: | ---: | ---: |
| Ink | 758,784 B | 758,784 B | 0 B |
| Blue_Tit | 5,957,952 B | 998,400 B | 4,959,552 B |
| TiltMech | 12,131,240 B | 3,528,128 B | 8,603,112 B |

These are actual managed-list capacity payloads for the batches in the sketch. They exclude object headers, per-stroke backups, Unity Mesh/native/graphics memory and unrelated app memory, and are not peak RAM measurements. Protected newest batches stay resident. The changed path retains indices for older batches without repopulating their vertex attributes. Raw results also retain index-cache payloads, resident-batch counts, warm edits and undo/redo samples.

**Next priorities supported by this measurement.**

1. Investigate per-stroke visual updates during quick loading and paced drawing. `QuadStripBrush.ApplyChangesToVisuals` resubmits vertex attributes and recalculates bounds; `GeometryBrush.ApplyChangesToVisuals` combines procedural regeneration with mesh writes. Split those costs before selecting an API rewrite.
2. Test whether intermediate solitary-mesh uploads can be omitted when a stroke will be finalized straight into a batch. Geometry generation must still happen. This is a candidate for a separate correctness-checked A/B experiment, not an implemented optimization or promised gain. It should not require a second persistent geometry cache.
3. Profile `Batch.Append` before further prioritizing completed-batch uploads: in the mixed sketch its measured CPU scope was over three times the completed-batch upload scope. Neither warrants a large RAM tradeoff based on these results.
4. Retain the visibility optimizations, but evaluate further work against the frequency of editing older batches. Idle rendering, GPU draw-call cost, loading and live-stroke generation are separate workloads. Native mobile/IL2CPP measurements remain necessary before drawing mobile conclusions.

**Protocol and validation boundary.** Unity 6000.6.0f1, Windows Editor Play mode, Ryzen 7 7800X3D, RTX 4090, Direct3D12, Monoscopic SDK, quality level 2, 266x609 Game view, target 60 fps and vSync 0. The small Editor view is not representative of a desktop headset or mobile GPU workload; no FPS or GPU-completion claim is made.

Each session used the bundled Ink, Blue_Tit and TiltMech sketches. An untimed quick load and 90 frames warmed each fixture. Three measured quick loads followed, with 30 settling frames between them. Metadata lookup and the file picker are outside the quick-load timer. Idle viewing spans 120 frames. Each of three cold/warm visibility pairs follows 90 frames of natural cache aging. Recorded-stroke replay selects the longest available stroke in each brush family, caps it at 128 control points and feeds the normal transient-pointer path one point per frame. It uses `BeginLineFromMemory`, including its loading flag; it is a drawing-path proxy, not measured hand/controller input latency. Three undo/redo pairs follow. Autosave is disabled only in memory during a workload and restored afterward.

The workload is a controlled recipe, not telemetry establishing how users divide their time. Custom allocation-free timing scopes record inclusive CPU time and call counts; nested scopes cannot be summed. Empty-scope calibration measured a median 55.5 ns added per scope in this Editor. No overhead was subtracted. Counter and harness code, source revision/hashes, fixture hashes, raw samples, frame intervals and censuses are retained. The topology counter's `indices` field records the owning mesh's total index count, not actual partial-upload bytes. Startup emitted KTX/Draco package-removal timeouts before the measured phases; those unrelated package issues were not changed. Full visual parity, player builds, physical devices and process peak memory were not validated.

**Reproduce in an owned Editor session.** The installer temporarily replaces seven source/test files with an instrumented revision, refuses unexpected source changes and backs up original bytes under Logs/BatchWorkloadSession. Do not save the temporary scene configuration or commit the installed instrumentation.

1. Start from clean production/test source files in this branch, outside Play mode. Open this project with Unity 6000.6.0f1. Preserve any unsaved scene work; the setup script refuses to replace dirty scenes.
2. Install the baseline scopes:

```powershell
$project = (Get-Location).Path
python Support/Benchmarks/instrument_batch_workload.py --revision 39b088aab
unity command run_script --file Support/Benchmarks/PrepareAppBatchWorkload.cs --entry AppWorkloadSetup.Refresh --project-path $project --json
```

3. Wait for compilation, then enter the Main scene with temporary monoscopic settings:

```powershell
unity command run_script --file Support/Benchmarks/PrepareAppBatchWorkload.cs --entry AppWorkloadSetup.Start --project-path $project --json
unity command run_script --file Support/Benchmarks/PrepareAppBatchWorkload.cs --entry AppWorkloadSetup.Status --project-path $project --json
```

4. Once app status is Standard, run from host PowerShell:

```powershell
& 'C:\Program Files\PowerShell\7\pwsh.exe' -NoProfile -File Support/Benchmarks/Run-AppBatchWorkload.ps1 -Label workload-before-01
```

5. The command starts an asynchronous workload. Inspect only the output JSON's `status`, `error` and phase count until it reports `completed` with 51 phases. Stop Play with `AppWorkloadSetup.Stop` and wait until the Editor is stopped before changing sources. A failed or cancelled run must also leave Play mode before retrying. Repeat steps 2–4 with revision `9beb3fdcb` and label `workload-after-01`, then alternate baseline/current again with labels ending `02`. Use the same current benchmark tools on both revisions. Never start a second workload while one is running.
6. After the last completed run, stop Play and restore the original source files, then refresh and allow compilation:

```powershell
unity command run_script --file Support/Benchmarks/PrepareAppBatchWorkload.cs --entry AppWorkloadSetup.Stop --project-path $project --json
python Support/Benchmarks/instrument_batch_workload.py --restore
unity command run_script --file Support/Benchmarks/PrepareAppBatchWorkload.cs --entry AppWorkloadSetup.Refresh --project-path $project --json
```

After compilation finishes:

```powershell
unity command run_script --file Support/Benchmarks/PrepareAppBatchWorkload.cs --entry AppWorkloadSetup.RestoreScene --project-path $project --json
```

7. Regenerate the saved comparison:

```powershell
python Support/Benchmarks/analyze_app_batch_workload.py --before Support/Benchmarks/Results/2026-10-25-app-workloads/workload-before-01.json Support/Benchmarks/Results/2026-10-25-app-workloads/workload-before-02.json --after Support/Benchmarks/Results/2026-10-25-app-workloads/workload-after-01.json Support/Benchmarks/Results/2026-10-25-app-workloads/workload-after-02.json --output Logs/BrushMeshBenchmarks/workload-summary.json
```

The analyzer rejects incomplete runs, mismatched protocol/environment/source hashes within each group, different replay inputs, or geometry-census changes. Raw evidence is in [Results/2026-10-25-app-workloads](Results/2026-10-25-app-workloads/). Hashes cover measured working-file bytes, including line endings; Git may normalize CRLF when checking out the harness. Repeat both revisions with the same checkout of the benchmark tools. Temporary source changes were restored byte-for-byte after these measurements; no new production optimization is included in this benchmark step.
