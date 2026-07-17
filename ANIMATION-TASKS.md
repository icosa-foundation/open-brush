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

- [ ] Add automated coverage for repeated redo/undo, failed operations, multi-track alignment, track visibility, and canvas ownership.
