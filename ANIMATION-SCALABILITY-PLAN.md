# Animation Scalability Plan

## Status

Phases 0-3 are implemented on the animation-refactor branch. The repeatable Editor suites pass:
25 model/operation tests, 9 Canvas lifecycle/persistence tests, and the three-repeat manager
adapter/persistence benchmark. Phase 4A is in progress: `FrameDrawing` and its repository now own
drawing identity, Canvas adapter lookup, lifetime removal, and logical content revision while all
rendering remains Canvas-backed. Dormant Phase 4B infrastructure defines proxy contracts,
per-drawing compatibility reasons, explicit proxy-resource ownership, and render-comparison
metrics. Phase 4C is in progress behind a disabled-by-default playback flag: eligible pure batched
drawings can use one reusable proxy per visible track while meshes and materials remain shared with
the Canvas source; unsupported, widget-bearing, path-bearing, or comparison-mismatched drawings
fall back individually. Compatibility is cached by content revision, so an unchanged held frame
does not reclassify, resynchronize, or recompare its drawing. Image, XR, transform, and
target-performance gates remain before this path
can become a default. Phase 3 is not complete until the manual desktop/headset working-state gate
and the remaining target-device Phase 0 captures in `ANIMATION-TASKS.md` are complete. This
document does not supersede that checklist.

## Problem statement

The animation branch represents a drawing frame with a `CanvasScript`. A non-empty canvas
owns a `BatchManager`, one or more batch pools, and Unity GameObjects containing meshes,
renderers, and material instances. The timeline also creates canvases for empty cells.

This is convenient because existing Open Brush editing, selection, undo, batching, and save
code already understand canvases. It has scaling costs when a sketch contains many tracks,
many timeline frames, or many distinct drawings:

- timeline playback performs work across the full timeline rather than only across changed
  tracks;
- every empty timeline cell can become a Canvas GameObject;
- every non-empty key drawing retains its Unity batch hierarchy and mesh resources;
- several timeline operations repeatedly scan the full timeline or global stroke list;
- duplicate and split operations deep-copy stroke and batch geometry;
- inactive drawings have no explicit CPU/GPU residency policy.

The IMM player uses a different model. A timeline frame is an integer reference to a reusable
drawing. Playback selects the current drawing, and a centralized renderer handles its GPU
resources, culling, and submission. IMM is not a design to copy literally: its brush system is
more constrained than Open Brush's, and its renderer still contains per-chunk draw loops. Its
useful lessons are the separation of timeline data, drawing data, render state, and residency.

## Goals

1. Keep playback cost primarily proportional to the number and complexity of visible tracks,
   not total timeline length.
2. Stop representing empty timeline cells with Canvas GameObjects.
3. Allow inactive drawings to exist without retaining a complete active Unity render hierarchy.
4. Preserve Open Brush editing behavior, undo/redo, selection, widgets, animation paths, save
   compatibility, and existing brush rendering.
5. Introduce larger architectural changes only after measurements show that the preceding,
   lower-risk changes are insufficient.
6. Make CPU time, object counts, and CPU/GPU memory attributable to tracks and drawings.

## Non-goals

- Rewriting Open Brush or its animation system in C++.
- Replacing all Open Brush brush shaders with IMM's five-section brush model.
- Combining materials or brush geometries that are not render-state compatible.
- Building a streaming system before realistic scene measurements justify it.
- Breaking ordinary non-animation `.tilt` sketches. Animation-branch metadata has a lightweight
  legacy reader, but compatibility with experimental animation builds is not a reason to retain
  a second runtime representation.
- Optimizing editor-only operations that are already infrequent unless profiling identifies
  them as material user-facing stalls.

## Working-state delivery requirement

The application must remain usable at every phase boundary and at every named subphase boundary.
No phase is permission to replace a representation and all of its consumers in one change. Each
architectural migration must first add an adapter or parallel path, prove behavioral equivalence,
and only then redirect callers.

For this plan, a **working state** means:

- the project compiles and starts normally;
- existing non-animation drawing and editing workflows still work;
- animation sketches using the immediately preceding `frameLengths` metadata load, convert to
  sparse spans during open, and play with equivalent timing and visible content;
- animation creation, editing, undo/redo, save, and reload work for the content types supported
  before the phase began;
- unsupported content continues through the previous Canvas-backed path;
- a new rendering optimization can be disabled without converting or losing sketch data; the
  sparse timeline itself becomes the sole runtime representation in Phase 3;
- saved files are not written in a representation that the active loader cannot read;
- automated correctness tests and the phase's targeted smoke tests pass.

Commits within a subphase should also be kept buildable where practical. If temporary scaffolding
is required, it must be unreachable from normal app behavior until complete. Feature flags are
for controlled rollout and fallback, not for merging a known-broken default path.

The intended sequence of stable application states is:

```text
Existing Canvas system
  -> instrumented Canvas system
  -> Canvas system with differential playback
  -> Canvas system with indexed lookups
  -> sparse timeline with Canvas-backed non-empty drawings
  -> mixed Canvas/proxy rendering
  -> mixed rendering with bounded residency
  -> optimized submission for supported brush groups
```

The old path should be removed only in a later cleanup after its replacement has been the tested
default and no supported content still depends on it.

## Current architecture and likely scaling behavior

### Dense timeline

`AnimationUI_Manager.Timeline` is a list of tracks, each containing a list of frame structs.
Each frame refers directly to a `CanvasScript`. A held keyframe reuses the same Canvas reference,
which is already a useful form of drawing reuse. Empty cells, however, also receive canvases in
`AddLayerRefresh()`, `FillTimeline()`, and several edit operations.

Consequences:

- empty-object count grows approximately with `tracks * timeline length`;
- adding a long track creates many Unity objects before the track has content;
- timeline cleanup must correctly discover and destroy displaced canvases;
- Canvas identity is simultaneously used as drawing identity, edit target, render owner, and
  save/load location.

### Playback hot path

While playing, `Update()` calls `FocusFrame()` every Unity update, including updates during
which the integer animation frame has not changed. `FocusFrame()` iterates across every timeline
frame and calls `HideFrame()`, which iterates across every track. It then rebuilds the scene's
active canvas lists, changes active-canvas state, updates UI, and broadcasts layer updates.

The resulting control cost is approximately:

```text
Unity updates * timeline frames * tracks
```

This can become a bottleneck before inactive GameObject overhead does. Inactive canvases do not
run `Update()` and do not render, so steady-state draw cost is still mostly determined by the
currently visible drawings. Total frame count is more likely to affect switching cost, hierarchy
size, global scans, and retained memory.

### Render hierarchy

Every Canvas initializes a transform-gizmo child and a `BatchManager`. For each brush GUID used
by a drawing, batching creates at least one GameObject with a `MeshFilter`, `MeshRenderer`,
`Batch`, Mesh, and instantiated Material. Large drawings may require multiple batches per brush.

The retained cost therefore scales more closely with:

```text
unique drawings * brush types per drawing * batches required per brush
```

than with timeline frame count alone. This distinction must be preserved in benchmarks.

### Lookups and invalidation

Several common operations are linear or worse:

- `GetCanvasLocation()` scans every track and frame;
- `GetTimelineLength()` scans every track;
- `GetFrameFilled()` scans the global stroke memory and searches canvas widgets;
- `FocusFrame()` and timeline UI methods repeat timeline scans;
- layer update events can cause UI reconstruction even when only playback time changed.

These costs are avoidable without changing drawing representation.

### Persistence coupling

Save code currently builds a Canvas-to-`(frame, track)` map and stores frame/track extensions on
strokes. Load code asks the scene for a Canvas at a frame/track location and assigns it as the
stroke's intended canvas. Animation metadata stores track lengths and frame lengths. A sparse
runtime timeline must preserve this external meaning or introduce an explicit format version and
migration.

## IMM techniques that transfer

### Frame-to-drawing indirection

IMM stores a frame buffer of drawing indices. Repeated frames reference one drawing. Timeline
duration is cheap and independent of render-object count.

Open Brush should introduce an explicit drawing identity instead of using Canvas identity for
all purposes. Initially, that identity may still point to a Canvas for non-empty drawings.

### Current-drawing render selection

IMM asks each paint layer only for its current drawing. Non-current drawings are absent from the
render submission path rather than hidden by traversing a scene hierarchy.

Open Brush should first emulate the behavioral result with differential Canvas activation. A
later phase may use one or a small number of render proxies per track.

### Explicit residency

IMM records whether drawing data is CPU-loaded, assigns GPU IDs, uploads on demand, and can evict
non-current drawings. Open Brush currently has an optimization for discarding cached CPU batch
geometry once a mesh becomes immutable, but it does not have a timeline-aware policy for inactive
frame meshes.

The transferable feature is an explicit residency state machine and budget. The exact IMM policy
of retaining only the current drawing may be too aggressive for an 8 fps flipbook because it can
cause upload/rebuild churn.

### Bounds and chunk culling

IMM rejects whole drawings by bounds, rejects drawings that are insignificant in screen space,
and culls bounded geometry chunks before submission. Open Brush already batches strokes, but a
large batch is generally submitted as a unit.

Open Brush can retain sub-batch bounds and visibility metadata without changing brush appearance.
Any multi-draw, indirect, or procedural submission work should remain grouped by compatible
material, shader, vertex layout, render queue, blend mode, and other required render state.

### Centralized performance accounting

IMM exposes draw-call and triangle counters and has an optional rendering budget. Open Brush's
animation work should similarly expose per-frame-switch and per-drawing counters before it adds
adaptive behavior.

## Target architecture

The long-term model should separate four concepts:

```text
AnimationDocument
  Track[]
    DrawingSpan[] { startFrame, duration, drawingId, animationPathId, flags }

DrawingRepository
  DrawingId -> FrameDrawing
    editable stroke/widget data
    bounds and content state
    optional authoring Canvas
    optional runtime render resources

PlaybackState
  current frame
  current drawing per track
  previous drawing per track

DrawingRenderer
  visible drawing set
  residency and prefetch policy
  culling and render submission
```

An empty span uses a reserved empty drawing ID and owns no Canvas. Adjacent spans with the same
drawing and compatible path/flags are merged. Timeline UI may expose individual frame cells, but
the model does not have to store one object per displayed cell.

The implementation should reach this architecture in stages. It should not begin by replacing
Canvas ownership throughout the application.

## Phase 0: measurement and representative workloads

### Work

Add development-only animation instrumentation with a unique log/profiler prefix. Record:

- timeline frames, tracks, spans, unique canvases, non-empty drawings, and empty canvases;
- total Canvas, Batch, Mesh, Renderer, and instantiated Material counts;
- strokes, vertices, indices, and batches per drawing and per visible frame;
- managed memory, Unity native memory, mesh memory where available, and estimated GPU mesh data;
- time spent in animation `Update`, frame selection, visibility changes, active-canvas rebuilding,
  UI refresh, layer event subscribers, and transform animation;
- counts of `SetActive`, layer-event, global stroke-scan, mesh upload, and mesh rebuild operations;
- frame-time spikes during scrubbing, sequential playback, reverse stepping, duplicate, split,
  save, and load.

Create or identify representative workloads that independently vary:

1. timeline length with few held drawings;
2. number of unique non-empty drawings;
3. number of tracks;
4. strokes and vertices per drawing;
5. brush/material diversity per drawing;
6. large spatial drawings where chunk culling could matter;
7. sequential playback versus random scrubbing.

Do not use one synthetic sketch to represent all scaling dimensions.

### Exit criteria

- Baseline captures are repeatable on the intended desktop and headset targets.
- The dominant costs can be assigned to control traversal, UI/events, GameObject hierarchy,
  CPU memory, GPU memory, uploads, or visible draw complexity.
- Later phases have numeric before/after comparisons rather than only object-count arguments.
- Instrumentation is development-only or cheap when disabled and does not change sketch behavior,
  persistence, or release performance materially.

## Phase 1: make playback differential

This is the first implementation phase because it is low risk and addresses an unconditional
full-timeline hot path.

### Work

1. Store the last applied integer animation frame separately from the continuously advancing
   playback time.
2. Return immediately when playback time still resolves to the applied frame.
3. Replace full-timeline hiding with one comparison per track:
   - resolve previous drawing;
   - resolve next drawing;
   - if they are identical, do nothing;
   - otherwise decrement visibility/use count for the old drawing and increment it for the new
     drawing;
   - activate or deactivate only when the use count crosses zero.
4. Rebuild `App.Scene.m_MainCanvas` and `m_LayerCanvases` only when the selected frame changes.
5. Separate playback-time notification from structural layer/timeline notification. Do not call
   `LayerCanvasesUpdate` for an ordinary playback tick unless a documented consumer requires it.
6. Update the timeline playhead without rebuilding track nodes, occupancy indicators, or layer
   controls.
7. Preserve active-track selection across frame changes without scanning for the Canvas location.
8. Ensure scrubbing and explicit frame selection can force an update even when selecting the same
   frame after a structural edit.
9. Retain the existing full-refresh operation as an explicit structural-edit and diagnostic path.
   During rollout, allow differential playback to be disabled without changing timeline data.

### Tests

- Holding on one 8 fps animation frame across multiple Unity updates causes no repeated Canvas
  visibility or layer rebuild work.
- Moving by one frame touches at most the changed drawings for each track.
- Held drawings are not deactivated and reactivated at span boundaries where the drawing is the
  same.
- Shared drawings, hidden/deleted tracks, animation paths, selection, and active Canvas behavior
  remain correct.
- Forward playback, looping, reverse stepping, home/end, and random scrubbing remain correct.

### Exit criteria

- Playback control cost is approximately proportional to track count on an actual frame change
  and close to zero between animation frame changes.
- Timeline UI reconstruction is absent from the ordinary playback hot path.
- No save-format change is required.
- With differential playback disabled, the existing Canvas-backed behavior remains available as a
  temporary fallback.

## Phase 2: indexes and precise invalidation

### Work

1. Maintain a Canvas-to-drawing/location index when timeline structure changes.
2. Cache timeline length and update it only on structural operations.
3. Track drawing occupancy from stroke/widget/path lifecycle events instead of scanning the global
   stroke list in `GetFrameFilled()`.
4. Split the current broad `ResetTimeline()` operation into narrow invalidations:
   - structure changed;
   - span length changed;
   - drawing occupancy changed;
   - track visibility changed;
   - selection changed;
   - playhead changed;
   - scroll viewport changed.
5. Update only visible timeline widgets and consider pooling them if object churn remains material.
6. Avoid `Timeline.IndexOf(track)` inside iteration; use stable track IDs or explicit indices.
7. Keep the source timeline authoritative while indexes are introduced. In development builds,
   allow suspect or invalid indexes to be rebuilt and the operation retried through the existing
   scan-based path.

### Invariants

- Indexes are updated atomically with timeline edits and undo/redo.
- A development validation method can rebuild indexes from source data and compare the result.
- Deleted or displaced canvases are removed from every index before destruction.

### Exit criteria

- Common canvas/location and occupancy queries are constant-time or local to a track.
- Structural edits produce bounded UI work for the visible timeline region.
- Disabling or rebuilding derived indexes changes performance but not sketch semantics.

## Phase 3: sparse timeline and removal of empty canvases

### Data model

Introduce stable `TrackId` and `DrawingId` values and represent each track with ordered spans.
A span contains at minimum:

```text
startFrame
duration
drawingId       // EmptyDrawingId for no content
animationPathId // optional
flags           // visible/deleted or their eventual replacements
```

Track visibility and deletion belong on the track rather than being copied into every frame.
Drawing content/deletion state belongs on the drawing or span according to actual semantics.

### Stable subphases

Each subphase below is a required working-app checkpoint:

#### Phase 3A: introduce the model behind adapters

- Add stable Track and Drawing IDs and sparse span classes without changing the active timeline.
- Add a span-backed frame-coordinate adapter that can resolve the current frame/Canvas-oriented
  API without allocating one compatibility object per timeline cell.
- In development builds, build a shadow sparse model from the current timeline and compare every
  resolved track/frame against the existing representation.
- Keep the existing timeline authoritative and keep all saves in the existing format.

**Working state:** behavior is still supplied by the current Canvas timeline. The new model is
unreachable from normal app behavior except for equivalence validation.

#### Phase 3B: make the sparse model authoritative

- Redirect read operations through the adapter, then redirect one edit command family at a time.
- Keep a span-backed coordinate adapter for UI and unconverted callers. Do not generate or retain
  an eager dense compatibility view; explicit full enumeration is allowed only at cold boundaries
  that genuinely require individual coordinates.
- Convert add/delete, move, extend/reduce, and duplicate/split separately, with command and
  undo/redo tests at each step.
- Continue to use Canvas-backed drawings for every non-empty drawing and continue writing the
  existing save representation.

**Working state:** the sparse model is authoritative, but rendering, editing, UI, and persistence
still observe the same Canvas/frame contract through the span-backed coordinate adapter. Each edit
family is validated before the next is redirected; rollback is by build/commit, not by retaining a
second authoritative runtime model.

#### Phase 3C: remove empty canvases

- Introduce the empty drawing sentinel and transient empty authoring Canvas.
- Stop allocating persistent canvases for empty spans in newly created or edited timelines.
- Load old sketches into sparse empty spans without manufacturing canvases for every empty cell.
- Validate the span-backed coordinate adapter directly. Do not retain a diagnostic mode that
  materializes a dense timeline, because that mode recreates the scaling failure and has no
  meaningful deployed compatibility audience.

**Working state:** all non-empty drawings remain on the existing Canvas render/edit path. Only
empty-cell representation changes. Empty cells still acquire a transient authoring Canvas when
selected, so drawing, undo/redo, and save/reload remain usable without a dense fallback mode.

#### Phase 3D: finalize persistence and remove temporary dual writes

- Validate old-file load, new-file save/reload, autosave, thumbnail, export, and API consumers.
- Write versioned sparse-span metadata and stop writing the experimental `frameLengths` field.
- Keep a small `frameLengths` fallback reader for older animation-branch files. Convert those
  durations immediately into the authoritative sparse model during open; do not carry a legacy
  mode or dual representation beyond the load boundary.
- Do not require new animation files to open in older experimental animation builds. The expected
  legacy user count is close to zero, so golden-file coverage is limited to the preceding format
  and no broader migration framework is justified.

**Working state:** files written by the default path can be read immediately by that build, and
legacy files still migrate without requiring the old runtime timeline.

### Work

1. Add a model layer independent of timeline GameObjects and UI widgets.
2. Provide frame-resolution APIs so callers do not access `List<Frame>` directly.
3. Convert add, delete, move, duplicate, split, extend, and reduce operations to span edits.
4. Normalize after edits:
   - remove zero-length spans;
   - merge compatible adjacent spans;
   - maintain complete coverage through the track duration using the empty sentinel;
   - do not allocate a Canvas for an empty sentinel.
5. Initially keep one Canvas for every non-empty unique drawing. This isolates sparse-timeline
   benefits from the later render-proxy refactor.
6. Use one reusable empty authoring Canvas per track, or a single transient empty Canvas, only
   when the editing workflow requires a target before the first stroke is created.
7. Promote that transient canvas to a real drawing only when content is added.

### Undo/redo

Commands should store model deltas or immutable before/after span snapshots, not cloned lists of
frame structs plus incidental Canvas lists. Drawing lifetime should be reference-counted across:

- active timeline spans;
- undo/redo history;
- current selection or transient editing state;
- save operations in progress.

Destroy a drawing Canvas only when no owner remains. Redo must either restore the same stable
Drawing ID or update all references in one operation.

### Save/load compatibility

1. Keep ordinary `.tilt` content and the preceding animation `frameLengths` metadata readable.
2. On load, convert legacy durations directly into spans before normal runtime work begins.
3. On save, emit versioned sparse-span metadata while continuing to emit the stroke frame/track
   locations used to associate drawing content with span starts.
4. Define how empty spans, held drawings, duplicate-but-independent drawings, animation paths,
   hidden tracks, and deleted tracks round-trip.
5. Add one legacy animation fixture plus new-format round-trip coverage. Do not add old-reader
   support for new animation files unless real usage later establishes that requirement.

### Exit criteria

- Empty timeline duration adds small model records rather than Canvas GameObjects.
- Canvas count tracks non-empty unique drawings, not `tracks * timeline length`.
- All animation commands and repeated undo/redo pass lifecycle tests without leaked or prematurely
  destroyed canvases.
- Existing animation sketches load with equivalent visible frames and timing.
- Phases 3A through 3D each satisfy the global working-state delivery requirement before work
  advances to the next subphase.

## Phase 4: drawing repository and authoring/render separation

Proceed only if Phase 0 and post-Phase-3 measurements show that non-empty drawing canvases remain
a limiting source of hierarchy cost or retained memory.

### Work

1. Introduce `FrameDrawing` as the owner of drawing identity and logical content.
2. Move APIs away from requiring `CanvasScript` as the persistent identity. Use a drawing context
   or interface for stroke ownership, coordinate space, grouping, bounds, and content changes.
3. Keep Canvas adapters so existing tools can edit the selected drawing during migration.
4. Instantiate or bind an authoring Canvas only for selected/actively edited drawings.
5. Create playback render proxies per track, with enough buffering for current and prefetched
   drawings.
6. Move widgets through separate animation-aware proxies or explicitly retain GameObjects for
   widget-bearing drawings until their semantics are designed.
7. Preserve coordinate transforms: track transform, animated path transform, drawing-local
   transform, and scene transform must remain separately testable.

### Stable subphases

Each subphase is independently releasable and leaves unsupported content Canvas-backed:

#### Phase 4A: drawing identity with universal Canvas backing

- Introduce `FrameDrawing` and repository ownership while every drawing still owns or resolves to
  its current Canvas.
- Redirect identity, lifetime, and lookup code to Drawing IDs without changing rendering.
- Validate selection, grouping, undo/redo, save/load, and Canvas-facing APIs through adapters.

**Working state:** this is an ownership refactor only. Visuals, editing, and rendering still use
the established Canvas hierarchy.

#### Phase 4B: add dormant render-proxy infrastructure

- Add proxy interfaces, compatibility classification, resource ownership, and comparison metrics.
- Do not use proxies by default and do not change saved data.
- Where feasible, render diagnostic comparisons in controlled tests rather than normal sessions.

**Working state:** the proxy path is unreachable in normal app use; all drawings remain
Canvas-rendered.

#### Phase 4C: proxy-render pure brush drawings

- Enable proxies behind a feature flag for pure batched-stroke drawings with supported brushes.
- Fall back per drawing, not per sketch, when content or render state is unsupported.
- Preserve the Canvas path as the default until image, XR, and performance validation passes.

**Working state:** mixed proxy/Canvas playback is supported, and disabling the flag immediately
returns all drawings to Canvas rendering without data conversion.

#### Phase 4D: editing transitions

- Materialize or bind an authoring Canvas when a proxy-backed drawing is selected for editing.
- Return it to proxy playback only after edits are committed and render resources are synchronized.
- Cover selection, repaint, delete, grouping, duplication, undo/redo, and unsaved dirty state.

**Working state:** a proxy-backed drawing can be opened, edited, played, undone, saved, and
reloaded. If materialization fails, the drawing remains Canvas-backed rather than becoming
uneditable.

#### Phase 4E: animated paths and transforms

- Add track, drawing, path, and scene transforms to the proxy path with explicit coordinate-space
  tests.
- Fall back affected drawings to Canvas rendering until their path semantics are supported.

**Working state:** supported path drawings use proxies; all others retain their previous behavior.

#### Phase 4F: widgets and remaining content

- Migrate one widget/content category at a time, including activation, ownership, save/load, and
  undo semantics.
- Keep mixed stroke/widget drawings Canvas-backed until the complete combination is supported.
- Update import/export and Canvas-exposing APIs through compatibility adapters.

**Working state:** every supported pre-migration sketch remains usable even if only some drawings
are eligible for proxy rendering.

#### Phase 4G: make proxy rendering the default for eligible drawings

- Change the default only after mixed-mode validation and target-device measurements pass.
- Keep a runtime/development fallback to Canvas rendering for diagnosis and unsupported cases.
- Remove obsolete Canvas-only proxy bypasses only after no supported eligible content depends on
  them.

**Working state:** proxy rendering is an optimization policy, not a new sketch representation;
turning it off preserves content and returns to a known rendering path.

Do not migrate every content type at once. The mixed mode is part of the architecture, not merely
a temporary implementation accident.

### Exit criteria

- In playback mode, retained active render GameObjects scale with visible tracks and the prefetch
  window rather than all non-empty drawings.
- Entering edit mode for a drawing preserves stroke identity, grouping, selection, and undo.
- Mixed legacy/proxy drawings render in the correct order with equivalent appearance.
- Phases 4A through 4G each satisfy the global working-state delivery requirement before work
  advances to the next subphase.

## Phase 5: timeline-aware resource residency

Proceed after the drawing repository provides a resource boundary and measurements identify
retained mesh memory as a problem.

### Working-state rollout

1. Add residency accounting in observe-only mode while retaining all resources as before.
2. Add prefetch and eviction behind a disabled-by-default feature flag.
3. Enable eviction only for clean drawings with a verified authoritative representation.
4. On a cache miss, use a correctness-first synchronous reconstruction fallback until asynchronous
   prefetch is proven reliable; record the stall rather than showing a missing drawing.
5. If reconstruction fails, pin the affected drawing to the Canvas/resident path and report a
   diagnostic error without changing sketch data.
6. Make residency default only after sequential playback, random scrubbing, editing, save/load,
   pause/resume, and target-memory tests pass.

Every step is a working checkpoint. Disabling residency changes memory use and performance only;
it must not change timeline resolution, editing capability, or file contents.

### State model

Each drawing should expose states such as:

```text
Unloaded
LogicalDataResident
GeometryResidentCPU
GeometryResidentGPU
Active
Dirty
```

The exact states may differ, but transitions and ownership must be explicit.

### Policy

- Always keep actively edited and visible drawings resident.
- Prefetch at least the next sequential drawing; consider previous/current/next as the initial
  window.
- Expand the window based on measured rebuild/upload latency and available memory.
- Treat random scrubbing differently from sequential playback to avoid continuous speculative
  loading.
- Evict least-recently-used drawings outside the protected window when a measured memory budget is
  exceeded.
- Never evict the only recoverable representation of unsaved edits.
- Keep compact stroke/control-point data or another authoritative representation from which mesh
  geometry can be rebuilt.
- Schedule expensive generation and upload work across frames where Unity APIs permit it.

### Tests and metrics

- No missing frame during sequential playback under a supported workload.
- Scrubbing does not leak meshes or materials.
- Repeated loops reach a stable resource count.
- Dirty edited frames survive eviction attempts, save/load, undo, and application pause/resume.
- Upload and rebuild spikes remain within an explicitly chosen frame-time budget.

### Exit criteria

- Retained GPU/mesh memory stays within a configurable budget on target hardware.
- Residency produces a measured benefit larger than its scheduling and rebuild overhead.
- Observe-only, enabled, and disabled modes all preserve equivalent visible and editable content.

## Phase 6: culling and render submission

Proceed based on visible-frame profiling. These changes target complex individual drawings, not
long timelines by themselves.

### Working-state rollout

Treat drawing culling, chunk culling, material sharing, alternate Unity submission APIs,
multi-draw/indirect submission, and GPU-driven culling as separate changes. Each change must:

- be gated independently while under validation;
- fall back per compatibility group or drawing to the previous renderer;
- leave unsupported brushes and content on the previous path;
- pass visual, ordering, stereo/XR, and editing-transition tests before becoming default;
- be removable or disableable without converting sketch data.

The application must be working after each individual rendering optimization; Phase 6 is not a
single renderer replacement.

### Hierarchical culling

1. Retain drawing bounds independent of an active Canvas.
2. Retain bounds for subdivisions of large compatible batches.
3. Reject invisible tracks and drawings before inspecting chunks.
4. Frustum-cull chunks for large drawings.
5. Evaluate screen-size rejection only with visual-quality requirements and hysteresis; do not
   introduce popping merely because IMM uses a fixed threshold.

### Submission reduction

1. Classify compatibility using full render state, not only brush GUID.
2. Measure CPU render-submission cost and draw-call count before choosing an API.
3. Evaluate, in increasing implementation cost:
   - fewer/larger existing Unity Mesh batches;
   - shared materials plus `MaterialPropertyBlock` where shader semantics allow it;
   - `Graphics.RenderMesh` or equivalent version-appropriate APIs;
   - multi-draw or indirect submission for compatible chunks;
   - GPU-driven culling only if CPU culling/submission remains material.
4. Preserve transparency ordering, blend modes, render queues, depth behavior, audio-reactive
   properties, per-brush keywords, and stereo/XR rendering.

### Caveat from IMM

IMM centralizes buffers by five geometric brush-section types. Open Brush supports a wider and
more varied shader/material library. The expected Open Brush result is several compatibility
groups, not one universal stroke buffer.

### Exit criteria

- Changes reduce measured CPU submission time or GPU work on target workloads.
- Image comparisons and representative XR inspection show no unacceptable brush, ordering, or
  stereo regressions.
- Each enabled optimization has a tested previous-path fallback for unsupported content and
  diagnosis.

## Cross-cutting correctness requirements

Every phase must preserve or explicitly test:

- add, delete, move, duplicate, split, extend, and reduce frame operations;
- repeated undo/redo and disposal of undo history;
- held drawings and independent duplicated drawings;
- track visibility, deletion, focus, rename, and squash behavior;
- active Canvas and drawing-tool targeting;
- selection Canvas transfers and restoration of previous ownership;
- stroke groups and batched subset lifetime;
- widgets, especially their activation and Canvas association;
- animation paths and per-frame transform interpolation;
- save/load, autosave, thumbnail generation, export, and import;
- API and Lua calls that accept layer/frame coordinates or expose canvases;
- sketch-meter accounting and resource destruction;
- desktop, XR single-pass/multiview, and supported render pipelines.

## Testing strategy

### Unit tests

- frame-to-span resolution at boundaries and loop points;
- span normalization after each edit operation;
- reference counts and drawing lifetime through undo/redo disposal;
- Canvas/drawing/location indexes versus a full rebuild;
- occupancy changes caused by strokes, widgets, and paths;
- save-model expansion and load-model compaction.

### Integration tests

- construct multi-track animations through commands rather than directly mutating model lists;
- run long sequences of edits and undo/redo while validating object/resource counts;
- save, clear, reload, and compare resolved drawings for every track/frame;
- sequential play, loop, scrub, reverse, and jump while checking active drawing sets;
- edit a previously evicted or proxy-rendered drawing and verify exact round-trip behavior.

### Performance tests

For every representative workload, capture:

- median and worst frame-selection CPU time;
- ordinary render-update CPU/GPU frame time;
- hierarchy and component counts;
- managed, native, Mesh, and estimated GPU memory;
- draw calls, batches, vertices, triangles, culled chunks, uploads, and rebuilds;
- allocation and garbage-collection activity;
- save/load and first-display latency.

Report results against Phase 0 baselines. A phase is not complete merely because it reduces
GameObject count; it must improve the limiting metric without an unacceptable regression in
another one.

## Rollout and compatibility

Use feature flags for render-proxy stages until editing behavior is established. The Phase 3
sparse model is a one-way runtime migration rather than a permanently selectable legacy mode.
During migration:

- validate the sparse model against value snapshots and the span-backed coordinate adapter; do not
  retain an eager dense runtime representation merely for comparison;
- keep saved-format changes versioned and one-way migrations explicit;
- prefer a mixed runtime capable of retaining legacy Canvas-backed drawings;
- add diagnostic summaries to bug reports without logging stroke content or user data;
- provide a safe fallback to Canvas-backed rendering when a brush or widget is unsupported by a
  new proxy path.

### Phase-boundary working states

| Boundary | Required working application state |
| --- | --- |
| Phase 0 | Existing behavior with development measurements; persistence is unchanged. |
| Phase 1 | Existing Canvas timeline with differential playback; full refresh remains available for structural edits and diagnosis. |
| Phase 2 | Existing Canvas timeline with derived indexes; indexes can be rebuilt or bypassed without semantic changes. |
| Phase 3 | Sparse authoritative timeline with adapters, current external file compatibility, and Canvas backing for every non-empty drawing. |
| Phase 4 | Mixed proxy/Canvas playback; eligible drawings can be edited through Canvas materialization and unsupported drawings remain entirely Canvas-backed. |
| Phase 5 | The Phase 4 app with bounded optional residency; disabling it retains resources and preserves all behavior. |
| Phase 6 | The Phase 5 app with independently gated rendering optimizations and previous-renderer fallback per unsupported drawing or compatibility group. |

If a phase cannot meet its row, it is incomplete rather than a transitional success. Work should
remain behind its inactive adapter or feature flag until the working-state requirement is met.

## Decision gates

### Gate A: after Phase 1

If playback CPU cost becomes acceptable and memory is within target limits, stop before changing
the data model. The GameObject approach may be adequate for actual workloads once full-timeline
work is removed.

### Gate B: after Phase 3

If empty canvases were the dominant hierarchy problem and unique drawing canvases remain within
budget, retain Canvas-backed non-empty drawings. Do not build render proxies solely for
architectural neatness.

### Gate C: before Phase 5

Add residency only if retained mesh/GPU memory is a measured limit. Compare the memory saved with
the latency and power cost of regeneration and upload.

### Gate D: before Phase 6

Add chunk culling or indirect rendering only when complex visible drawings, not timeline control
or residency, dominate frame time.

## Recommended initial implementation slice

The first reviewable slice should contain only:

1. development counters and profiler markers for the playback path;
2. a last-applied integer frame guard;
3. previous/current drawing comparison per track;
4. differential Canvas activation;
5. separation of playhead-only UI updates from structural timeline updates;
6. tests proving that held frames and repeated Unity updates perform no redundant visibility work.

This slice does not change the save format or Canvas ownership. It establishes measurements and
removes the known `timeline frames * tracks` playback traversal before the sparse timeline work
begins.
