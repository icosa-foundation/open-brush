# Animation Performance Workloads

This protocol supplies the representative workloads required by Phase 0 of
`ANIMATION-SCALABILITY-PLAN.md`. A Phase 0 capture is complete only when every workload has been
run on the intended desktop and headset targets. Control-path-only tests are useful comparisons,
but do not substitute for drawings with real brush geometry.

## Common capture protocol

For every workload:

1. Record the commit, Unity version, target/device, graphics API, render pipeline, XR mode,
   resolution, refresh rate, and whether the run is an Editor or Player run.
2. Start from a freshly loaded representative sketch and allow shaders and assets to warm up.
3. Capture at least three runs. Report the median run and retain the individual results so run
   variance is visible.
4. Exercise at least 33 frame transitions for frame-selection measurements. Capture sequential
   playback and deterministic random scrubbing separately.
5. Record the `[OB_ANIM_SCALE]` diagnostic snapshot and profiler markers. For the automated
   control matrix, retain every `[OB_ANIM_PHASE0]` row.
6. Record median, p95, and worst frame-selection CPU time; ordinary CPU/GPU frame time; hierarchy,
   Canvas, Batch, Mesh, Renderer, and material counts; managed/native/mesh/estimated-GPU memory;
   draw calls, batches, vertices, triangles, uploads, rebuilds, and allocations.
7. Compare the legacy/full-refresh path with the differential/sparse path at the same workload
   and settings. Do not compare different sketches or render configurations.

Editor `Stopwatch` results isolate the synchronous frame-selection call. They are not ordinary
render frame time and must be labelled as Editor control-path measurements.

## Workload matrix

| ID | Scaling dimension | Required tiers | Status and driver |
| --- | --- | --- | --- |
| W1 | Timeline length with held drawings | 100, 1,000, and 10,000 frames across 8 tracks | Automated empty and real-stroke Editor frame-selection coverage; Player/headset captures remain required. |
| W2 | Unique non-empty drawings | 4, 16, and 64 unique drawings | Automated empty and 1k-vertex/drawing Editor coverage; Player/headset captures remain required. |
| W3 | Track count | 1, 8, and 32 tracks with 1,000 held frames | Automated empty and real-stroke Editor frame-selection coverage; repeat on target devices. |
| W4 | Geometry complexity per drawing | About 1k, 10k, and 100k vertices using one brush | Automated real-stroke Editor frame-selection coverage; Player/headset rendering remains required. |
| W5 | Brush/material diversity | 1, 4, and 8 compatible brush groups at about 10k vertices/drawing | Automated real-stroke Editor frame-selection coverage; render/batch cost on targets remains required. |
| W6 | Large spatial drawing | Compact and 10-metre-spaced versions of identical geometry | Automated construction/frame-selection coverage; culling and GPU measurements remain required. |
| W7 | Selection pattern | Sequential and deterministic random selection of 64 unique drawings | Automated empty and 1k-vertex/drawing Editor coverage; Player/headset captures remain required. |

## Representative content construction

For W4, create one source drawing with a release-supported brush, then make deterministic copies
at increasing stroke/vertex counts. For W5, construct drawings with comparable visible vertex
counts while increasing the number of brush/material groups. For W6, duplicate one geometry set:
keep one copy compact and translate stroke groups in the other across a large world-space volume.

Place each tier in its own animation frame using normal application operations. Save the resulting
`.tilt` files as reusable test assets only after verifying their counts through the diagnostic
snapshot. Do not use a single large sketch as evidence for all three dimensions.

## Completion rule

Automated rows close the repeatable Editor frame-selection portion of W1-W7. Phase 0 remains
incomplete until W1-W7 have repeatable desktop Player and headset captures and the dominant costs
can be assigned to traversal, UI/events, hierarchy, CPU/GPU memory, uploads, or visible rendering.
