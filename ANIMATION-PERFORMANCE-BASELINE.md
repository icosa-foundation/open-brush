# Animation Performance Baseline and Comparisons

This is the durable record for Phase 0 baselines and later-phase comparisons. It currently
contains repeatable desktop Unity Editor frame-selection measurements. Phase 0 remains incomplete
until the same representative workloads have Player/headset render and memory captures.

## Capture 2026-07-19: desktop Editor

- Environment: Windows Editor, Unity 2022.3.62f2, D3D11, Open Brush Main scene.
- Driver: `TestAnimationPerformanceWorkloads`.
- Sampling: 33 synchronous frame selections per row. Each mode receives a full managed collection
  before sampling so the always-second differential path is not charged for construction garbage.
  `Stopwatch` surrounds `ApplyPlaybackFrameForTests()`; it does not measure an ordinary rendered
  frame.
- Aggregation: three warm runs. Tables report the median of the three run medians and the median of
  the three run p95/worst values.
- Control run IDs: `20260719T103323168Z`, `20260719T103516216Z`,
  `20260719T103743802Z`.
- Real-stroke geometry run IDs: `20260719T103409150Z`, `20260719T103601150Z`,
  `20260719T103830191Z`.
- Real-stroke scale run IDs: `20260719T103345047Z`, `20260719T103537445Z`,
  `20260719T103805173Z`.
- Rendered-frame run IDs: `20260719T104734176Z`, `20260719T104857076Z`,
  `20260719T105121285Z`.
- Test results: three combined jobs passed 3/3 and three render jobs passed 1/1 (12/12 total).
  The initial cold validation encountered the
  existing `TiltBrushStandardSpecular.shader` D3D11 compile error; its unchanged warm rerun passed.

### Held timeline length: 8 tracks, empty geometry

| Frames/track | Cells | Legacy median / p95 / worst (ms) | Differential median / p95 / worst (ms) | Median change | Hide visits legacy / differential |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 800 | 0.768 / 1.374 / 1.396 | 0.669 / 0.833 / 1.089 | -12.9% | 26,136 / 0 |
| 1,000 | 8,000 | 1.645 / 2.108 / 2.530 | 0.669 / 1.195 / 1.439 | -59.3% | 263,736 / 0 |
| 10,000 | 80,000 | 10.094 / 15.847 / 16.612 | 0.664 / 0.887 / 0.955 | -93.4% | 2,639,736 / 0 |

The legacy result scales with timeline cells. Differential median time remains 0.664-0.669 ms
while timeline size grows by 100 times. This assigns the removed cost to full-timeline traversal,
not brush geometry or rendering.

### Track count: 1,000 held frames/track, empty geometry

| Tracks | Cells | Legacy median / p95 / worst (ms) | Differential median / p95 / worst (ms) | Median change | Hide visits legacy / differential |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1,000 | 0.366 / 0.518 / 0.569 | 0.195 / 0.264 / 0.389 | -46.7% | 32,967 / 0 |
| 8 | 8,000 | 1.583 / 1.873 / 2.281 | 0.670 / 0.959 / 1.218 | -57.7% | 263,736 / 0 |
| 32 | 32,000 | 4.408 / 5.908 / 7.042 | 0.688 / 0.785 / 0.974 | -84.4% | 1,054,944 / 0 |

### Unique empty-geometry drawings and selection pattern

| Workload | Legacy median / p95 / worst (ms) | Differential median / p95 / worst (ms) | Median change | Visibility requests legacy / differential |
| --- | ---: | ---: | ---: | ---: |
| 4 drawings, sequential | 2.433 / 4.133 / 4.289 | 2.407 / 3.455 / 3.615 | -1.1% | 132 / 66 |
| 16 drawings, sequential | 2.619 / 3.950 / 4.303 | 2.562 / 3.207 / 3.587 | -2.2% | 528 / 66 |
| 64 drawings, sequential | 2.717 / 4.068 / 4.311 | 2.679 / 4.008 / 4.427 | -1.4% | 2,112 / 66 |
| 64 drawings, deterministic random | 2.722 / 3.571 / 3.864 | 2.627 / 4.302 / 4.431 | -3.5% | 2,112 / 66 |

Median change is small when the timeline itself is short, but the differential path removes the
unique-drawing-dependent visibility requests. Editor tail results are less stable than medians.

## Real-stroke frame-selection matrix

Each row has four unique drawings. Geometry tiers use one brush at approximately 1k, 10k, and
100k vertices per drawing. Brush tiers instantiate each brush once per drawing, then use the same
Simple brush to hold total geometry near 10k vertices per drawing. Spatial tiers contain identical
geometry placed compactly or at 10-metre spacing.

| Workload | Total vertices | Batches | Legacy median / p95 (ms) | Differential median / p95 (ms) |
| --- | ---: | ---: | ---: | ---: |
| Geometry 1k/drawing | 4,032 | 4 | 2.469 / 3.236 | 2.415 / 3.298 |
| Geometry 10k/drawing | 40,032 | 4 | 2.383 / 2.828 | 2.428 / 2.959 |
| Geometry 100k/drawing | 400,032 | 28 | 2.783 / 4.333 | 2.406 / 3.219 |
| 1 brush group | 40,032 | 4 | 2.413 / 3.102 | 2.416 / 2.989 |
| 4 brush groups | 40,240 | 16 | 2.480 / 3.190 | 2.431 / 2.776 |
| 8 brush groups | 40,272 | 32 | 2.407 / 3.367 | 2.452 / 3.386 |
| Compact geometry | 40,032 | 4 | 2.454 / 3.676 | 2.365 / 2.871 |
| Spatially spread geometry | 40,032 | 4 | 2.505 / 3.511 | 2.408 / 3.011 |

These four-frame medians are effectively flat between modes and do not show a consistent
geometry-dependent improvement. Equivalent pre-sample collection removed the previous second-mode
tail bias.
The result does not measure GPU rendering, culling, or ordinary frame time.

The matrix also records batch-only and whole-Canvas hierarchy counts. At 100k vertices/drawing it
contains 400,032 vertices, 133,344 triangles, 28 batch meshes/renderers/material instances, and
43,248,480 bytes of Unity-reported batch Mesh memory. At 8 brush groups it holds geometry within
0.6% of the 1-group tier while increasing batch renderers/material instances from 4 to 32.

## Real-stroke scale matrix

Held-track rows use about 1k vertices in each non-empty drawing. Unique-drawing rows use about 1k
vertices per frame. These are frame-selection results, not rendered-frame results.

| Workload | Total vertices | Legacy median / p95 / worst (ms) | Differential median / p95 / worst (ms) | Median change |
| --- | ---: | ---: | ---: | ---: |
| 8 tracks × 100 held frames | 8,064 | 0.744 / 0.881 / 1.224 | 0.666 / 0.906 / 1.012 | -10.5% |
| 8 tracks × 1,000 held frames | 8,064 | 1.590 / 2.719 / 2.748 | 0.685 / 0.905 / 1.138 | -56.9% |
| 8 tracks × 10,000 held frames | 8,064 | 10.322 / 13.197 / 13.197 | 0.663 / 1.171 / 1.177 | -93.6% |
| 1 track × 1,000 held frames | 1,008 | 0.369 / 0.487 / 0.550 | 0.204 / 0.348 / 0.393 | -44.7% |
| 8 tracks × 1,000 held frames | 8,064 | 1.631 / 2.727 / 2.739 | 0.672 / 1.102 / 1.233 | -58.8% |
| 32 tracks × 1,000 held frames | 32,256 | 4.251 / 4.865 / 6.111 | 0.688 / 0.788 / 0.975 | -83.8% |
| 4 unique drawings | 4,032 | 2.562 / 4.121 / 4.175 | 2.490 / 3.290 / 3.532 | -2.8% |
| 16 unique drawings | 16,128 | 2.553 / 3.822 / 3.881 | 2.566 / 3.723 / 4.182 | +0.5% |
| 64 unique drawings | 64,512 | 2.660 / 3.669 / 4.191 | 2.619 / 3.411 / 3.741 | -1.5% |
| 64 drawings, sequential | 64,512 | 2.641 / 3.954 / 4.372 | 2.697 / 3.213 / 3.220 | +2.1% |
| 64 drawings, deterministic random | 64,512 | 2.675 / 3.800 / 4.601 | 2.662 / 4.560 / 4.722 | -0.5% |

Real batches preserve the timeline-length and track-count gains. Increasing unique drawings or
changing selection order on a short timeline does not produce a material median improvement.

## Editor rendered-frame matrix

Each mode warms for 10 frames and samples 60 normal player-loop frames. The long-held workload has
8 tracks × 10,000 frames and about 10k vertices per visible track. Unique-complex has 16 unique
10k-vertex drawings. Material-diverse has 4 unique drawings, each with 8 brush groups and about
10k vertices.

The EditMode-driven Play Mode loop runs at about 10 Hz on this machine; approximately 95.5 ms is
reported as Editor render time. These absolute CPU/frame values are not representative of a Player
build or headset. They are retained as repeatable same-environment comparisons and to validate
render counters. GPU timing was supported for all 60 samples per row.

| Workload | Mode | Delta median / p95 (ms) | Editor render (ms) | GPU (ms) | Draws / batches / SetPass | Hide visits / visibility requests |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Long held, sequential | Legacy | 102.495 / 110.127 | 95.605 | 0.145 | 109 / 109 / 109 | 4,799,520 / 480 |
| Long held, sequential | Differential | 101.399 / 107.283 | 95.510 | 0.084 | 109 / 109 / 109 | 0 / 0 |
| Unique complex, sequential | Legacy | 101.342 / 108.762 | 95.685 | 0.144 | 109 / 109 / 109 | 900 / 959 |
| Unique complex, sequential | Differential | 101.898 / 108.380 | 95.841 | 0.146 | 109 / 109 / 109 | 0 / 118 |
| Unique complex, random | Legacy | 102.905 / 108.853 | 95.653 | 0.138 | 109 / 109 / 109 | 900 / 959 |
| Unique complex, random | Differential | 103.112 / 108.584 | 95.834 | 0.137 | 109 / 109 / 109 | 0 / 118 |
| Material diverse | Legacy | 101.050 / 109.601 | 95.573 | 0.131 | 177 / 177 / 177 | 180 / 239 |
| Material diverse | Differential | 101.784 / 107.049 | 95.769 | 0.138 | 177 / 177 / 177 | 0 / 118 |

The differential path does not change visible rendering work: draw calls, batches, SetPass calls,
vertices, and triangles match legacy for each workload. The long-timeline median improves by
1.096 ms in this render-dominated Editor loop, while short unique/material workloads show no
consistent frame-time change. This supports a control-traversal claim, not a GPU optimization.

## Phase 4 proxy rendered-frame matrix

Run `20260719T154451692Z` added the proxy policy to the same 60-frame Editor matrix. The test
passed 1/1. Values below are single-run medians, intended to decide whether Phase 4G can be enabled,
not to replace the still-missing Player/headset captures.

| Workload | Mode | Delta median / p95 (ms) | CPU / GPU median (ms) | Draws / VBO uploads | Active Canvases | Proxy objects / visible | Retained Canvas hierarchy |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Long held, sequential | Legacy | 108.729 / 118.452 | 107.959 / 0.129 | 109 / 61 | 8 | 0 / 0 | 304 |
| Long held, sequential | Differential | 107.594 / 113.444 | 108.202 / 0.130 | 109 / 61 | 8 | 0 / 0 | 304 |
| Long held, sequential | Proxy | 108.058 / 114.613 | 107.847 / 0.130 | 109 / 61 | 0 | 16 / 8 | 304 |
| Unique complex, sequential | Legacy | 108.345 / 115.717 | 108.002 / 0.131 | 109 / 61 | 1 | 0 / 0 | 608 |
| Unique complex, sequential | Differential | 108.399 / 120.049 | 108.046 / 0.130 | 109 / 61 | 1 | 0 / 0 | 608 |
| Unique complex, sequential | Proxy | 107.059 / 114.303 | 107.946 / 0.130 | 109 / 61 | 0 | 2 / 1 | 608 |
| Unique complex, random | Legacy | 108.938 / 113.307 | 107.859 / 0.128 | 109 / 61 | 1 | 2 / 0 | 608 |
| Unique complex, random | Differential | 107.267 / 115.613 | 108.001 / 0.091 | 109 / 61 | 1 | 2 / 0 | 608 |
| Unique complex, random | Proxy | 108.149 / 113.753 | 107.937 / 0.122 | 109 / 61 | 0 | 2 / 1 | 608 |
| Material diverse | Legacy | 108.802 / 112.850 | 108.056 / 0.130 | 177 / 61 | 1 | 0 / 0 | 180 |
| Material diverse | Differential | 107.896 / 115.034 | 107.997 / 0.130 | 177 / 61 | 1 | 0 / 0 | 180 |
| Material diverse | Proxy | 107.878 / 115.266 | 108.001 / 0.129 | 177 / 61 | 0 | 9 / 1 | 180 |

All rows reported zero managed bytes allocated by the sampled current thread. Proxies achieved the
Phase 4 active-hierarchy boundary: no source drawing Canvas remained active, and visible proxy
count tracked visible tracks/drawings. They did not reduce draw calls, VBO uploads, retained Canvas
hierarchy, or measured CPU/GPU frame time. This implementation reuses the same meshes, materials,
and renderers through a lighter active hierarchy; it is not a batching or submission reduction.
These data do not satisfy the Phase 4G improvement gate, so proxy rendering remains opt-in.

## Sparse model-edit matrix

`TestAnimationSparseEditPerformance.HeldTimelineEditMatrix` compares the former
expand/edit/recompress implementation with the authoritative span edit on the same held timeline.
Each of three passing jobs records three samples per row and alternates which mode runs first. The
table reports the median of the three run medians and the median of their worst samples.

The Mono runtime returns zero from `GC.GetAllocatedBytesForCurrentThread()`, so that counter is
explicitly reported as unsupported. Managed-memory and Unity Mono-used deltas agree and measure
the dense temporary list retained at the end of the measured call.

| Held frames | Expanded median / worst (ms) | Sparse median / worst (ms) | Median change | Managed heap delta expanded / sparse |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 0.0384 / 0.0387 | 0.0416 / 0.0597 | +8.3% | 4,096 / 0 bytes |
| 1,000 | 0.1172 / 0.1175 | 0.0426 / 0.0460 | -63.7% | 32,768 / 0 bytes |
| 10,000 | 0.9218 / 0.9310 | 0.0433 / 0.0521 | -95.3% | 323,584 / 0 bytes |
| 1,000,000 | 90.0203 / 92.5262 | 0.0419 / 0.0447 | -99.95% | 32,002,048 / 0 bytes |

Both paths finish with the same three normalized spans and resolved drawing. A separate
million-frame correctness test enforces bounded managed-memory growth so a future change cannot
silently restore per-frame edit expansion. This is direct Phase 3 model-edit evidence; it is not a
claim about compatibility-view reconstruction, save/load, rendering, or target-device frame time.

## Manager adapter and sparse-metadata matrix

`TestAnimationPerformanceWorkloads.ManagerEditAdapterMatrix` measures the complete manager edit:
span mutation, derived-index rebuild, and reconstruction of the public frame-coordinate adapter.
The pre-change row retained an eager `Frame` record for every timeline coordinate. The post-change
adapter retains one projected record per sparse span. Three passing post-change repetitions each
contain 11 samples per tier; the table reports the median of their medians and p95 values.

| Held frames × 8 tracks | Eager projection median (ms) | Span adapter median / p95 (ms) | Median change | Managed heap delta eager / span-adapter median |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 0.4792 | 0.0213 / 0.0829 | -95.6% | 385,024 / 0 bytes |
| 1,000 | 3.0113 | 0.0223 / 0.0979 | -99.3% | 2,883,584 / 0 bytes |
| 10,000 | 38.8202 | 0.0227 / 0.0874 | -99.94% | 28,999,680 / 0 bytes |

The same repetitions serialize versioned sparse-span metadata, deserialize it, configure the
manager, rebuild the adapter, and select the first frame. This is an in-memory metadata-boundary
measurement, not full `.tilt` disk I/O or brush-geometry reconstruction.

| Held frames × 8 tracks | JSON bytes | Save median / p95 (ms) | Load + first display median / p95 (ms) |
| ---: | ---: | ---: | ---: |
| 100 | 390 | 0.0727 / 0.1058 | 0.9065 / 3.9171 |
| 1,000 | 398 | 0.0713 / 0.1410 | 0.8695 / 4.2912 |
| 10,000 | 406 | 0.0961 / 0.1344 | 1.2279 / 4.2755 |

The metadata grows by 16 bytes while timeline coordinates grow from 800 to 80,000. Save time is
flat at this scale. Load plus first display remains close to one millisecond, with no
duration-proportional trend established by these three tiers. The existing snapshot integration
test separately covers real `.tilt` write/load correctness for two tracks and four spans; target
Player/headset file latency remains an open Phase 0 measurement.

## What is established

- Phase 1 differential playback removes full-timeline traversal and provides the first measured
  performance improvement. The gain grows with held timeline length and track count.
- Phase 3 span-native editing removes the former duration-proportional temporary frame list. At
  one million held frames the isolated Editor model edit falls from 90.0203 ms and a 32,002,048-byte
  heap delta to 0.0419 ms with no measurable heap increase.
- The span-backed manager adapter removes the duration-proportional projection cost: at 8 tracks ×
  10,000 held frames the measured edit falls from 38.8202 ms and a 28,999,680-byte heap delta to
  0.0227 ms with a zero median heap delta.
- Sparse Phase 3 rendering/device improvements are not established by the model or manager-edit
  results. Target CPU/GPU/memory gates remain to be measured.
- Real brush batches do not materially change four-frame selection medians in this Editor test.
  No GPU, draw-call, or visible-frame performance claim follows from these numbers.

## Required captures still missing

- Desktop Player and headset runs of every W1-W7 workload from
  `ANIMATION-PERFORMANCE-WORKLOADS.md`.
- Target CPU/GPU rendered frame time, native/mesh/GPU memory, allocation/GC, uploads/rebuilds,
  culling, save/load latency, and first-display latency. Editor draw-call/GPU samples above do not
  replace target captures.
- Numeric pre-change Phase 0 baselines for metrics that cannot be recreated using the retained
  legacy/full-refresh or expand/edit/recompress comparison paths.

Until those captures exist, Phase 0 and therefore the Phase 1-3 completion gates remain open.
