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

- [x] Add automated model coverage for repeated redo/undo, failed operations, multi-track alignment, track visibility, and drawing ownership reference counts.
- [x] Run Unity integration coverage for real Canvas creation, promotion, destruction, save leases, and command-history disposal.
- [ ] Run the real `.tilt` snapshot write/load integration test and confirm sparse timing, track visibility, and empty-Canvas scaling round-trip.
- [ ] Run the 80,000-cell held-timeline workload and record legacy versus differential traversal and frame-selection timings.

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
