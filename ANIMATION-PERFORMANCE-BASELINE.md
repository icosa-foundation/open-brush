# Animation Performance Baseline and Comparisons

This is the durable record for Phase 0 baselines and later-phase comparisons. It currently
contains repeatable desktop Unity Editor frame-selection measurements. Phase 0 remains incomplete
until the same representative workloads have Player/headset render and memory captures.

## Capture 2026-07-19: desktop Editor

- Environment: Windows Editor, Unity 2022.3.62f2, D3D11, Open Brush Main scene.
- Driver: `TestAnimationPerformanceWorkloads`.
- Sampling: 33 synchronous frame selections per row. `Stopwatch` surrounds
  `ApplyPlaybackFrameForTests()`; it does not measure an ordinary rendered frame.
- Aggregation: three warm runs. Tables report the median of the three run medians and the median of
  the three run p95/worst values.
- Control run IDs: `20260719T102605162Z`, `20260719T102620813Z`,
  `20260719T102636865Z`.
- Real-stroke run IDs: `20260719T102352542Z`, `20260719T102429267Z`,
  `20260719T102458250Z`.
- Test results: all six recorded warm runs passed. The initial cold validation encountered the
  existing `TiltBrushStandardSpecular.shader` D3D11 compile error; its unchanged warm rerun passed.

### Held timeline length: 8 tracks, empty geometry

| Frames/track | Cells | Legacy median / p95 / worst (ms) | Differential median / p95 / worst (ms) | Median change | Hide visits legacy / differential |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 800 | 0.755 / 0.805 / 1.104 | 0.666 / 0.711 / 0.713 | -11.8% | 26,136 / 0 |
| 1,000 | 8,000 | 1.568 / 2.408 / 2.413 | 0.661 / 0.688 / 0.722 | -57.8% | 263,736 / 0 |
| 10,000 | 80,000 | 9.934 / 11.528 / 14.641 | 0.666 / 0.736 / 0.769 | -93.3% | 2,639,736 / 0 |

The legacy result scales with timeline cells. Differential median time remains 0.661-0.666 ms
while timeline size grows by 100 times. This assigns the removed cost to full-timeline traversal,
not brush geometry or rendering.

### Track count: 1,000 held frames/track, empty geometry

| Tracks | Cells | Legacy median / p95 / worst (ms) | Differential median / p95 / worst (ms) | Median change | Hide visits legacy / differential |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 1,000 | 0.358 / 0.615 / 0.668 | 0.197 / 0.218 / 0.220 | -45.0% | 32,967 / 0 |
| 8 | 8,000 | 1.582 / 1.702 / 1.930 | 0.662 / 0.717 / 0.737 | -58.2% | 263,736 / 0 |
| 32 | 32,000 | 4.190 / 5.129 / 5.432 | 0.666 / 0.758 / 0.769 | -84.1% | 1,054,944 / 0 |

### Unique empty-geometry drawings and selection pattern

| Workload | Legacy median / p95 / worst (ms) | Differential median / p95 / worst (ms) | Median change | Visibility requests legacy / differential |
| --- | ---: | ---: | ---: | ---: |
| 4 drawings, sequential | 2.286 / 18.182 / 29.001 | 2.265 / 2.618 / 3.115 | -0.9% | 132 / 66 |
| 16 drawings, sequential | 2.526 / 14.271 / 30.344 | 2.425 / 3.330 / 3.652 | -4.0% | 528 / 66 |
| 64 drawings, sequential | 2.551 / 5.642 / 39.564 | 2.441 / 3.071 / 3.547 | -4.3% | 2,112 / 66 |
| 64 drawings, deterministic random | 2.557 / 8.529 / 33.896 | 2.476 / 4.014 / 4.421 | -3.2% | 2,112 / 66 |

Median change is small when the timeline itself is short, but the differential path removes the
unique-drawing-dependent visibility requests. Editor tail results are less stable than medians.

## Real-stroke frame-selection matrix

Each row has four unique drawings. Geometry tiers use one brush at approximately 1k, 10k, and
100k vertices per drawing. Brush tiers instantiate each brush once per drawing, then use the same
Simple brush to hold total geometry near 10k vertices per drawing. Spatial tiers contain identical
geometry placed compactly or at 10-metre spacing.

| Workload | Total vertices | Batches | Legacy median / p95 (ms) | Differential median / p95 (ms) |
| --- | ---: | ---: | ---: | ---: |
| Geometry 1k/drawing | 4,032 | 4 | 2.276 / 2.584 | 2.252 / 16.079 |
| Geometry 10k/drawing | 40,032 | 4 | 2.267 / 3.166 | 2.324 / 8.430 |
| Geometry 100k/drawing | 400,032 | 28 | 2.268 / 20.487 | 2.297 / 4.355 |
| 1 brush group | 40,032 | 4 | 2.325 / 5.475 | 2.477 / 3.298 |
| 4 brush groups | 40,240 | 16 | 2.277 / 3.245 | 2.307 / 9.997 |
| 8 brush groups | 40,272 | 32 | 2.263 / 5.937 | 2.302 / 3.825 |
| Compact geometry | 40,032 | 4 | 2.274 / 2.673 | 2.324 / 5.538 |
| Spatially spread geometry | 40,032 | 4 | 2.276 / 11.684 | 2.290 / 2.844 |

These four-frame medians are effectively flat between modes and do not show a repeatable
geometry-dependent improvement. The p95 values contain intermittent Editor spikes in both modes.
The result does not measure GPU rendering, culling, or ordinary frame time.

The matrix also records batch-only and whole-Canvas hierarchy counts. At 100k vertices/drawing it
contains 400,032 vertices, 133,344 triangles, 28 batch meshes/renderers/material instances, and
43,248,480 bytes of Unity-reported batch Mesh memory. At 8 brush groups it holds geometry within
0.6% of the 1-group tier while increasing batch renderers/material instances from 4 to 32.

## What is established

- Phase 1 differential playback removes full-timeline traversal and provides the first measured
  performance improvement. The gain grows with held timeline length and track count.
- Sparse Phase 3 representation is evidenced by 8 spans for 80,000 cells, but no Phase 3 CPU/GPU
  or memory improvement is yet proven against a true pre-change Phase 0 capture.
- Real brush batches do not materially change four-frame selection medians in this Editor test.
  No GPU, draw-call, or visible-frame performance claim follows from these numbers.

## Required captures still missing

- Real-content timeline-length, track-count, unique-drawing, and random-selection scaling in the
  automated Editor matrix.
- Desktop Player and headset runs of every W1-W7 workload from
  `ANIMATION-PERFORMANCE-WORKLOADS.md`.
- CPU/GPU rendered frame time, draw calls, native/mesh/GPU memory, allocation/GC, uploads/rebuilds,
  culling, save/load latency, and first-display latency.
- Numeric pre-change Phase 0 baselines for metrics that cannot be recreated using the retained
  legacy/full-refresh diagnostic path.

Until those captures exist, Phase 0 and therefore the Phase 1-3 completion gates remain open.
