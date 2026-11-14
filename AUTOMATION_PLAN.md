# In-headset tutorials and shared automation

Status: design proposal for implementation planning. The existing automation has been inspected; an in-headset prototype has not been built or validated.

This is the local, untracked planning document. Current script-authoring instructions are in [PROCEDURAL_AUTOMATION.md](Support/Automation/PROCEDURAL_AUTOMATION.md). Release-demo candidates remain in [DEMO_VIDEO_CANDIDATES.md](DEMO_VIDEO_CANDIDATES.md).

## 1. Objective

Let a user launch a tutorial inside Open Brush and watch controllers demonstrate real UI and tool interactions in front of them. Reuse the same authored sequences to produce tutorial videos. Keep explanatory text in the shared tutorial sequence so it works in both presentations.

Preserve the user's current sketch in memory throughout the tutorial, including their Undo and Redo history. Existing layers provide the main content boundary. A scoped history and explicit restoration of shared settings provide the remaining isolation.

## 2. Operating model

1. Use one active editing pipeline: the existing panels, buttons, tools, pointer manager and command system.
2. During demonstration, scripted input owns the logical brush and wand. The viewer's headset remains normally tracked.
3. Place tutorial content on a temporary layer and temporarily hide the user's content using existing layer mechanisms.
4. Retain the user's live content and original history objects. Suspend their history while the tutorial uses temporary stacks.
5. Return to the original sketch state by undoing tutorial actions, releasing tutorial resources and restoring scoped settings and input ownership.
6. Reuse the invisible arm model to constrain the demonstrator. Its reference body belongs to the demonstration, independently of the viewer's moving head.
7. Package generated JSON plans for headset execution. Python remains the authoring tool on the development machine.

This is an exclusive demonstration mode. A subsequent practice stage can hand the same tutorial workspace to the user's physical controllers. Both stages have a single owner of the editing pipeline at any instant.

## 3. Findings from the current code

| Area | Evidence | Consequence for this design |
| --- | --- | --- |
| Tool buttons | [ToolButton](Assets/Scripts/GUI/ToolButton.cs) changes `SketchSurfacePanel.m_Instance`. | Use the real tool pipeline with controlled ownership. Copied buttons would still affect global tools. |
| Panel buttons | [PanelButton](Assets/Scripts/GUI/PanelButton.cs) uses global panel and sketch-control managers. | Tutorial panel interaction should use the registered panel set. |
| Panel registration | [PanelManager](Assets/Scripts/GUI/PanelManager.cs) looks up registered panels by type and rejects unregistered panels as unavailable. | Independent live panel copies would require additional routing and lifecycle changes. |
| Input ownership | [InputManager](Assets/Scripts/InputManager.cs) exposes one brush/wand pair. [ScriptedAutomation](Assets/Scripts/Input/ScriptedAutomation.cs) replaces that pair and suspends controller pose writers. | Extract an explicit input-ownership boundary while retaining the fixed logical roles. |
| Camera and gaze | `ScriptedAutomation` currently requires Virtual Input and uses its body, gaze, aspect and framing APIs. [ViewpointScript](Assets/Scripts/ViewpointScript.cs) supplies interaction gaze. | Separate actor reference pose and interaction gaze from the tracked viewer camera. |
| Layers | [SceneScript](Assets/Scripts/SceneScript.cs) provides layer creation, visibility and active-canvas management. | Reuse layers for content isolation. Include selection and widgets in the isolation audit. |
| History | [SketchMemoryScript](Assets/Scripts/SketchMemoryScript.cs) clears Redo before new actions and can merge with the previous Undo command. | Retain the original stacks and use fresh tutorial stacks. |
| Disposal | [BrushStrokeCommand](Assets/Scripts/Commands/BrushStrokeCommand.cs) removes and destroys its stroke on disposal. | Dispose tutorial commands while tutorial ownership is still active; retain original command objects undisposed. |
| Dirty state | `SketchMemoryScript.IsMemoryDirty()` compares history against a saved-stack baseline. | History swapping must include this bookkeeping and save/autosave ownership. |
| Scene reset | `ClearMemory()` destroys content, clears history, resets groups and resets canvas batches. | Tutorial cleanup must operate on owned resources and scoped history rather than the global sketch-reset path. |
| Save snapshots | [SketchSnapshot](Assets/Scripts/Save/SketchSnapshot.cs) captures save/export data. | A save snapshot does not preserve the complete live editing session or its command objects. |

These findings support a prototype based on one active editing context. They do not establish complete state restoration or headset compatibility yet.

## 4. Layer-based tutorial workspace

1. Record the original active layer, layer identities, order, visibility and transforms before creating tutorial content.
2. Create a dedicated tutorial layer through the existing layer/canvas system. Treat its identity as session-owned, rather than assuming a fixed layer index.
3. Hide the user's layers while the demonstration runs. Preserve layers that were already hidden exactly as they were.
4. Audit user selection separately: selected strokes can reside on the shared selection canvas. Capture and restore selection through a proven reversible path, and ensure selected user content cannot become a tutorial target.
5. Include layer-owned models, images, guides and other widgets in the visibility and interaction tests. Preserve the normal lifecycle of their assets when layers are hidden and shown.
6. Direct prepared strokes, drawing, Tint, Reshape and imported tutorial fixtures to the tutorial layer. Each operation must have a checked target-layer policy.
7. Record every tutorial-owned layer, stroke and widget in a resource ledger. This covers preparation and resources whose lifecycle is not fully represented by Undo commands.
8. Restore the original active layer and visibility after tutorial cleanup. Delete only session-owned layers and resources.

Scene rotation remains application-wide in the current code. Hidden user content can share that transform during a tutorial provided the original scene pose is restored before the content is revealed. Restore poses in their defining coordinate frames, with parent transforms restored before dependent widget poses.

## 5. Scoped Undo and Redo

### Why an ordinary Undo loop is insufficient

1. A new tutorial action currently calls `ClearRedo()`, disposing the user's existing Redo commands.
2. A tutorial command can merge with the top user command.
3. Undoing tutorial actions leaves them on Redo unless they are explicitly discarded.
4. History also participates in dirty-state tracking, UI notifications and command publication.

### Proposed history scope

Introduce a controlled history-scope API in `SketchMemoryScript`; `TutorialHistoryScope` is a proposed name. Keep access to history internals inside the history owner.

1. At entry, retain references to the original Undo and Redo stacks and capture related bookkeeping. Preserve the command objects themselves.
2. Install fresh tutorial Undo and Redo stacks before any tutorial setup creates commands. Starting with an empty Undo stack prevents merges across the user/tutorial boundary.
3. Preserve and reset the saved-stack baseline used by `IsMemoryDirty()`, the surface panel's last-command reference, and any command-group bookkeeping identified by the audit.
4. Let native drawing and editing commands operate normally on the temporary history. Tutorial Undo/Redo steps use those same stacks.
5. On exit, undo every remaining applied tutorial command. Commands already undone by the tutorial remain in its temporary Redo history.
6. Dispose the temporary history while the tutorial scope and required canvas resources are still active. Use the existing command disposal rules to remove undone tutorial strokes from sketch memory and release owned objects.
7. Reconcile the resource ledger, then restore the original stacks and their bookkeeping. Restore the original last-command reference before releasing user input.
8. Publish the appropriate history/UI refresh after restoration so Undo/Redo controls describe the user session again.

The original Redo commands must never pass through `ClearRedo()` during the tutorial. Verify both original command identity and subsequent usability, not just stack counts.

### Commands and notifications

1. Entry requires an idle editing state: finish or reject an active stroke, widget grab, command group or external API Undo transaction before changing ownership.
2. Audit both `PerformAndRecordCommand` and the older `RecordCommand` path, plus brush memorization paths that clear Redo directly.
3. Keep ordinary UI notifications useful within tutorial mode while separating user-document dirtiness, autosave and external command publication.
4. `StepBack(invoke: false)` is not a complete notification boundary: operation-stack notifications still occur. Define notification ownership explicitly.
5. Audit network-stack and collaboration behavior before allowing tutorials in an active collaborative session. The initial prototype should run in a standalone session; broader support needs a deliberate command-routing policy.

## 6. Shared-state inventory

Complete this inventory against the first tutorial before implementing broad snapshot code. Each state item needs a named owner, capture method, restore method and validation check. Copy mutable settings values so tutorial writes cannot modify the saved snapshot through shared references.

| State | Proposed handling | Required check |
| --- | --- | --- |
| User layers and active canvas | Retain objects; capture identities, order, visibility and transforms. | Same objects, active layer and visibility after exit. |
| Selection and selection canvas | Suspend through an audited reversible path. | Same selected strokes/widgets, groups and previous-canvas relationships. |
| Undo/Redo and saved-state baseline | Retain original stacks; use temporary history. | Original command identity, order, dirty state and Redo behavior survive. |
| Mirror and symmetry | Capture mode, settings, mutable transform arrays, widget pose/visibility and relevant pointer configuration. | Same mirror behavior and widget placement after exit. |
| Brush and tool state | Capture active tool/subtool, brush, colour, size, radius and tool-specific options. | User resumes with the same settings and no tutorial command cached by a tool. |
| Straight edge and snapping | Capture enabled modes, angle/grid settings and guide/endpoint state that affects subsequent input. | Next user stroke behaves as it did before entry. |
| Scene pose and scale | Capture the original transform. | Tutorial scene manipulation leaves the user's scene unchanged on return. |
| Panels and popups | Capture layout, mode, attachments and supported popup state; coordinate with existing automation restoration. | Same editing layout returns without tutorial preference changes. |
| Scripted symmetry and Lua | Audit selected scripts, parameters, enabled modes and mutable runtime state. | Restore or explicitly suspend active behavior; names alone may not describe its state. |
| Save/autosave state | Preserve document identity, saved-state markers and autosave ownership. | Tutorial geometry is never saved into the user's document or recovery slot. |
| Input and controller visuals | Retain physical bindings and original visibility; own the temporary override. | Current hardware poses and input return correctly. |
| Pending work | Drain or cancel GPU intersections and other tool callbacks before restoration. | Late tutorial callbacks cannot mutate the resumed user session. |

Use focused state adapters where ownership is clear. This does not require turning every application manager into a second instance. Persistent writes made by demonstrated controls must be identified and suppressed or restored through the relevant adapter.

## 7. Separate the executor from its presentation host

Retain the plan compiler, native action sequence, arm solver and assertions. Introduce a small execution-host boundary; names below are proposals, not existing APIs.

1. `VideoAutomationHost` supplies the simulated body/controllers, synthetic interaction gaze, fixed shot framing and optional capture already used by video generation.
2. `HeadsetTutorialHost` supplies a staged actor reference pose and scripted controller ownership while leaving the real XR camera and headset tracking active.
3. The host owns interaction-gaze overrides, controller binding, presentation-specific setup and restoration.
4. The executor emits step/caption events and exposes safe pause, completion, failure and cancellation state.
5. A tutorial-session coordinator owns layer isolation, history scope, shared-state adapters and resource cleanup. Assign one restoration owner per setting to avoid overlapping executor and coordinator cleanup.

Current `ScriptedAutomation` dependencies on `VirtualSimulation.BodyPose`, `InteractionGaze`, view aspect and `SetFramingPose` are the main seams to extract. Preserve existing desktop/Mac recording behavior through the video host.

## 8. Headset presentation and input

1. Choose a world-space demonstration anchor from the user's pose at launch. Position the artwork and controller actor at a comfortable, visible distance, validated in-headset.
2. Keep that anchor stable as the viewer turns or moves. Use the actor's reference body for the invisible arm model.
3. Preserve the viewer's real head tracking throughout the demonstration. Cinematic framing and video aspect-ratio controls belong to the video host.
4. Use the existing controller roles and registered panel set while scripted input owns editing. Any physical-controller visual treatment is a presentation decision to review in the prototype.
5. Provide pause, replay and exit through an independent hardware input path. That path must remain available when the normal logical brush/wand inputs are procedural.
6. Debounce launch: wait for the user's initiating button/trigger to be released before the actor starts.
7. On exit, restore current hardware tracking poses, not controller positions captured at tutorial launch. Gate editing input until any physically held controls are released.

Audit `VrControllers.SuspendPoseTracking()` on the chosen XR setup to confirm only the intended controller pose writers are suspended. Validate this before extending beyond a controller-based OpenXR prototype.

## 9. Session lifecycle

Proposed states: `Idle -> Entering -> Demonstrating -> Paused -> Exiting -> Idle`. `Practising` is an extension. Failure and cancellation enter the same cleanup path as normal completion.

### Entry order

1. Check the requested tutorial's capabilities and current app state. Wait for launch controls to be released.
2. Acquire exclusive editing ownership and quiesce the current tool, pointer and widget interaction. Complete preparation checks that can run without modifying the sketch.
3. Capture the shared-state inventory and original history references before changing layers or settings.
4. Begin the temporary history and establish save/autosave and command-publication ownership.
5. Suspend selection as required, hide user content, create the tutorial layer and make it active.
6. Prepare fixtures and register tutorial-owned resources. Apply the tutorial's starting settings and panel layout.
7. Acquire actor-controller and interaction-gaze ownership through the selected host.
8. Run the compiled plan and publish viewer-facing cues. Any failure after acquisition invokes the same session cleanup, including partial entry.

### Pause and replay

1. Initially pause at safe action boundaries: finish an active atomic gesture, release controls and wait for native tools to settle before stopping progression.
2. Keep the current explanatory cue visible while paused. Show a pending-pause indication if an active gesture must finish first.
3. Replay a section by restoring its owned state through a validated checkpoint/rollback path. Simply rewinding the controller trajectory would leave tool results and settings inconsistent.
4. Treat immediate pause within a stroke or widget grab as an additional capability to design and test; releasing input mid-gesture can change stroke count and grouping.

### Exit and cleanup order

1. Stop scheduling new actions and release procedural triggers, grips and buttons.
2. Let active native gestures close and finish their commands. Drain/cancel outstanding tool work; reject stale completions if a subsystem cannot cancel them directly.
3. Disable or settle the active tutorial tool so it cannot add commands during rollback.
4. Undo the applied tutorial commands with the temporary history still active.
5. Dispose tutorial history while its referenced resources are valid. Reconcile and release any remaining tutorial-owned resources; remove temporary layers after their dependent commands/strokes have been handled.
6. Restore scene and shared tool/settings state in the correct coordinate and ownership order. Restore selection and the original active layer.
7. Restore the original history stacks, saved-state bookkeeping and last-command references before normal command notifications resume.
8. Restore the original layer visibility and panel layout, remove the actor gaze override, and return controller tracking/input ownership. Keep the real headset tracked throughout.
9. Confirm the postconditions, then release exclusive editing ownership.

The coordinator must support partial entry, repeated cleanup requests, normal completion, user cancellation and controlled exceptions. Its lifetime must cover cleanup even if the action runner is disabled. Use independent restoration guards so a failed optional visual restoration does not prevent hardware input from returning.

If rollback fails, retain the original history references, stop tutorial writes and report the failure. Recover owned tutorial resources through the resource ledger rather than invoking a global sketch reset. Define the recovery behavior during the prototype before exposing the feature generally.

## 10. Captions and labels

### Shared authoring model

1. Add optional viewer-facing caption text to authored steps or reusable sections. Existing diagnostic labels continue to identify execution and framing operations.
2. A caption change emits a semantic cue event from the running tutorial. Keep the current cue active across actions until replaced or explicitly cleared.
3. Use explicit script waits for reading time. Preserve the current speed rule: only script-authored durations are scaled; native animations retain their own timing.
4. Examples for Tint: "Apply a light tint at 0.3", "Compare full strength on another section", and "Press the secondary button to enable clearing."

An illustrative step could include `"caption": "Set Tint amount to 0.3"` alongside its normal slider action. The exact schema and clearing semantics should be settled before implementing the parser extension.

### Video presentation

1. Record actual capture-frame boundaries when cues change, rather than calculating timestamps from nominal script durations.
2. Account for a cue already active when capture begins, explicit clears and capture ending while a cue is active.
3. Generate subtitle timing from those boundaries and burn captions into the canonical MP4 during the existing FFmpeg encoding step. SRT export can be optional.
4. Review short text on a translucent background in a consistent caption area. Validate placement against the artwork, panels and controllers before changing framing margins.
5. Keep generated subtitle/rendering intermediates temporary and retain the current automatic capture-frame cleanup.

### Headset presentation

1. Present the same cue state as readable in-world text beside the demonstration.
2. Choose text distance, size, orientation and placement through a headset preview. Keep the demonstration stable while allowing the text to remain readable from the intended viewing area.
3. Keep captions synchronized with pause, replay, completion and cancellation through runtime cue state, independently of a video frame clock.
4. Any controller/button highlight is a separate visual treatment requiring explicit user approval. The current approved work uses tilt and pauses only.

Tint is the first caption/style preview for both presentations. Caption semantics should be shared from the outset, even if video output is delivered first.

## 11. Authoring and headset packaging

1. Continue composing actions and reusable sections in the external Python authoring layer.
2. Generate and validate JSON plans during development/build preparation, then bundle them with tutorial metadata for headset loading.
3. Keep the headset runtime independent of a Python installation or an external HTTP client. HTTP remains useful for development and recording.
4. Add metadata for title, description, fixtures, supported actions and presentation/capability requirements. Validate resources and selectors before committing to a session where possible.
5. Use semantic selectors where unambiguous and exact runtime hierarchy selectors where needed. UI reorganization can still require script updates.
6. Keep preparation, reusable sections and standalone tutorials distinct. Draw and Open Labs remain reusable components rather than canonical standalone videos.

## 12. Implementation milestones

### 1. Ownership audit and design decisions

1. Complete the state inventory for Tint and an Open Labs interaction.
2. Identify history bookkeeping, command notifications, selection suspension, mirror state and persistent writes.
3. Decide the first prototype's launch policy, safe-pause behavior and collaboration policy.
4. Deliver a concrete ownership/restore map and a small regression fixture. Acceptance: every state touched by the first tutorial has a defined owner and exit check.

### 2. History scope

1. Add the temporary history API without changing ordinary editing behavior.
2. Exercise native commands with pre-existing user Undo and Redo, tutorial Undo/Redo, merging and disposal.
3. Validate saved-state bookkeeping and notifications.
4. Acceptance: original commands retain identity/order and remain usable; tutorial history is fully released after exit or cancellation.

### 3. Layer workspace and restoration

1. Add tutorial-layer ownership and the minimum state adapters needed for Tint, including mirror/symmetry preservation.
2. Validate selection, mixed visible/hidden layers, user content protection and resource cleanup in the existing Virtual Input environment.
3. Exercise completion, cancellation and failure before attempting headset presentation.
4. Acceptance: the original sketch's content, editing state and history match their entry state.

### 4. Shared explanatory cues and video output

1. Add cue schema/events and actual-frame capture metadata.
2. Add temporary subtitle generation and FFmpeg rendering.
3. Produce one Tint preview to agree on wording, font, position and reading time.
4. Acceptance: cue timing follows actual execution at supported script speeds and video frame rates; canonical recording and cleanup behavior remain stable.

### 5. Headset host prototype

1. Extract the executor's camera/gaze/input host boundary while preserving the recording host.
2. Start with a real Open Labs button interaction, then a Tint gesture on the tutorial layer.
3. Implement independent hardware pause/exit and safe input handoff.
4. Validate real headset tracking, stage placement, controller poses, gaze-independent actor operation and full exit restoration.
5. Acceptance: one complete tutorial can be watched, cancelled and replayed on the chosen OpenXR headset without disturbing the user's sketch.

### 6. Tutorial launch experience and practice

1. Add a tutorial catalogue/launch UI backed by bundled plans.
2. Add reviewed explanatory cues to the initial tutorial set.
3. Introduce practice as a controlled ownership handoff on the tutorial layer, with explicit success conditions and cleanup behavior.
4. Add step replay/checkpoints only after their state restoration is validated.

Each milestone should have focused implementation commits and proportionate checks. Runtime refactoring and presentation changes should remain independently reviewable.

## 13. Validation matrix

| Case | Required result |
| --- | --- |
| User has both Undo and Redo entries | Original command objects and both histories survive; original Redo still works. |
| User sketch is already dirty | Same dirty state, document identity and save target after exit. |
| User sketch is clean | Tutorial entry/actions/cleanup do not leave it dirty. |
| Tutorial performs Undo and Redo | Only tutorial commands move between the temporary stacks. |
| Last user command could merge with the first tutorial command | No cross-boundary merge or mutation of the original command. |
| Different layer visibility, active layer and selection | Exact original state returns, including selection-canvas content. |
| Models/images/widgets on user layers | Layer hide/show and tutorial cleanup preserve their content and lifecycle. |
| Mirror, custom/scripted symmetry and tool options changed | Original settings, mutable values and relevant widget state return. |
| Cancel during entry, stroke, panel grab or scene manipulation | Inputs are released, cleanup completes, and user content/history remain usable. |
| Delayed GPU/tool completion | No callback edits the resumed user session. |
| Tutorial creates resources outside a normal drawing command | Resource ledger releases only owned resources. |
| Viewer moves or looks away | Head tracking stays live; actor interactions and stage placement remain coherent. |
| Exit while a physical control is held | Hardware visuals recover without an unintended user editing action. |
| Scaled scene/canvas | Authored dimensions and restoration remain correct. |
| Caption changes, clears, pauses and capture boundaries | Video timing and live cue state agree with actual execution. |
| Existing video scripts | Native actions, framing and outputs retain their established behavior. |

For history tests, compare object identity and data as well as counts. For content, compare stroke/control-point data, colours, transforms, layer relationships and relevant widget state. Use targeted live Editor checks before headset validation; record the exact platform/device and validation limits.

## 14. Recording and preview conventions to preserve

1. Current canonical tutorials use stable filenames under `Open Brush/Videos/Tutorials`, 4:3 at 1440 x 1080 and 30 FPS.
2. The usual 1.5x speed affects authored script durations only.
3. Captured PNG frames are automatically removed after encoding, including failed recordings where cleanup can complete safely.
4. Review presentation changes in one or two previews before rerecording the full library.
5. Keep controller movement anatomically plausible, preserve working shot composition and avoid unnecessary returns to neutral.
6. The shared secondary-button presentation currently has a Tint preview with a 0.3-strength first pass, separate left-to-right comparison sections, and before/after button pauses. Applying the reviewed presentation to angle snapping remains follow-up work.

## 15. Decisions to settle before implementation

1. Confirm the exclusive demonstration model and the first supported headset/controller configuration.
2. Confirm how user selection is suspended and whether all user layers are hidden in the first prototype.
3. Define the exact history/dirty-state notification boundary and standalone/collaboration launch policy.
4. Choose the independent pause/exit controls and confirm safe-boundary pause behavior.
5. Decide the first handling policy for active Lua/background scripts and other mutable state that cannot be restored from simple settings values.
6. Agree on caption wording, layout and timing through the first preview; request approval separately for any highlight treatment.

The next implementation step, once authorized, is milestone 1: turn this inventory into a source-backed ownership/restore map and a minimal history-scope regression fixture. That establishes the reversible editing boundary before the headset host is introduced.
