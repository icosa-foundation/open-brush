# Animation Tasks

## High priority

- [x] Make extend/reduce operations restore exact timeline state on undo and avoid recording no-op reductions.
- [x] Make delete-frame operations clean up temporary canvases on undo and permanently removed content when undo history is discarded.
- [x] Preserve track visibility in `FillTimeline()` and `CleanTimeline()`, and destroy canvases removed by timeline cleanup.
- [x] Keep other tracks time-aligned when extending a held frame.

## Correctness and lifecycle

- [x] Make split and duplicate source strokes come from the requested timeline frame rather than `App.Scene.ActiveCanvas`.
- [x] Replace transform-child-based frame occupancy with a logical content check and guard canvases not present in the timeline.
- [x] Stop `AddLayerRefresh()` from creating an unused canvas for the first frame.
- [x] Prevent failed or no-op animation operations from entering undo history and marking the sketch dirty.
- [x] Destroy the full animation-path GameObject when rejecting a path on an empty frame.

## Verification

- [x] Add and run a repeatable desktop Editor control-path matrix for timeline length, track count,
  unique empty-geometry drawings, and sequential/random selection; record all results and limits in
  `ANIMATION-PERFORMANCE-BASELINE.md`.
- [x] Add and run three desktop Editor frame-selection samples with real brush batches, independently
  varying vertices/drawing, brush groups at comparable geometry, and compact/spread geometry.
- [x] Add and run three desktop Editor real-stroke scale samples for timeline length, track count,
  unique drawings, and sequential/random selection, with equivalent pre-sample GC for both modes.
- [x] Add and run three 60-frame Editor render samples with supported CPU/GPU timing, draw calls,
  batches, SetPass, geometry, VBO upload, memory, and animation traversal counters.
- [x] Add automated model coverage for repeated redo/undo, failed operations, multi-track alignment, track visibility, and drawing ownership reference counts.
- [x] Run Unity integration coverage for real Canvas creation, promotion, destruction, save leases, and command-history disposal.
- [x] Run the real `.tilt` snapshot write/load integration test and confirm sparse timing, track visibility, and empty-Canvas scaling round-trip (2 tracks, 4 drawing spans, 2 unique Canvases).
- [x] Run the 80,000-cell held-timeline workload and record legacy versus differential traversal and frame-selection timings (719,928 legacy hide visits versus 0 differential; 19.273 ms versus 9.067 ms median across 9 transitions on the validation machine).
- [x] Verify that layer/frame coordinate consumers materialize only the requested sparse span and preserve legacy frame-length metadata.
- [x] Verify that disabling empty-Canvas sharing changes only compatibility hierarchy count while sparse spans, track visibility, and persistence metadata remain equivalent.
- [x] Verify differential and legacy playback produce identical active drawing sets across distinct drawings and hidden tracks.
- [x] Verify a missing Canvas/drawing index entry recovers through the development compatibility scan and subsequent queries return to indexed lookup.

## Phase 0 baseline gate

The control-path matrix is only partial Phase 0 evidence. These items must be complete before any
Phase 0-3 phase is declared complete.

- [x] Repeat the control-path matrix at least three times and report run-to-run variance.
- [ ] Capture all W1-W7 workloads from `ANIMATION-PERFORMANCE-WORKLOADS.md` with representative
  brush geometry on the intended desktop target.
- [ ] Capture the same representative workload set on the intended headset target.
- [ ] Record ordinary CPU/GPU frame time, hierarchy/component counts, managed/native/mesh/GPU
  memory, rendering counts, uploads/rebuilds, allocations/GC, save/load, and first-display latency.
- [ ] Assign each dominant cost to traversal, UI/events, hierarchy, CPU/GPU memory, uploads, or
  visible rendering and report numeric legacy/baseline versus Phase 1-3 comparisons.

## Phase 3 manual working-state gate

Run this pass after the automated Phase 0-3 suite is green. This is the final Phase 3 gate, not an
early implementation check.

- [ ] Create a multi-track animation and exercise add, delete, move, duplicate, split, extend, and reduce; verify undo and redo for each operation.
- [ ] Verify held frames, independent duplicated frames, track visibility/deletion/focus/rename/squash, active-track selection, and timeline scrolling.
- [ ] Verify sequential playback, looping, reverse stepping, home/end, and random scrubbing without drawing flashes or repeated UI reconstruction.
- [ ] Verify strokes, widgets, and animation paths remain associated with the correct frame and Canvas after editing and selection transfers.
- [ ] Save, reload, autosave, generate a thumbnail, export, and import; compare timing, visibility, content, and active-frame targeting.
- [ ] Exercise API and Lua calls that use layer/frame coordinates, then check sketch-meter accounting and that discarded drawings release their resources.
- [ ] Repeat the playback and editing smoke pass in desktop and headset/XR modes and check the Unity log for current `[OB_ANIM_SCALE]` errors or index-recovery warnings.
- [ ] Check the supported render-pipeline and XR single-pass/multiview configurations used for release targets.
