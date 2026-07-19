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

## What is established

- Phase 1 differential playback removes full-timeline traversal and provides the first measured
  performance improvement. The gain grows with held timeline length and track count.
- Sparse Phase 3 representation is evidenced by 8 spans for 80,000 cells, but no Phase 3 CPU/GPU
  or memory improvement is yet proven against a true pre-change Phase 0 capture.
- Real brush batches do not materially change four-frame selection medians in this Editor test.
  No GPU, draw-call, or visible-frame performance claim follows from these numbers.

## Required captures still missing

- Desktop Player and headset runs of every W1-W7 workload from
  `ANIMATION-PERFORMANCE-WORKLOADS.md`.
- Target CPU/GPU rendered frame time, native/mesh/GPU memory, allocation/GC, uploads/rebuilds,
  culling, save/load latency, and first-display latency. Editor draw-call/GPU samples above do not
  replace target captures.
- Numeric pre-change Phase 0 baselines for metrics that cannot be recreated using the retained
  legacy/full-refresh diagnostic path.

Until those captures exist, Phase 0 and therefore the Phase 1-3 completion gates remain open.
