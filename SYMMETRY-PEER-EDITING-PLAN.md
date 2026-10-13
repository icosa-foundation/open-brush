# Symmetry peer editing stabilization plan

## Objective

Stabilize symmetry peer editing before adding count-preserving settings controls, VR mirror-management UI, or stroke regeneration.

The current implementation has a sound core idea: strokes created in one symmetry draw burst form a group, and edits propagate as transformed deltas rather than snapping peers back to ideal symmetry. The immediate work is to make placement state, canvas conversions, command ownership, persistence, and multi-stroke gestures obey that idea consistently.

## Scope decisions

1. Keep `SymmetryStrokeGroup`, per-stroke pointer indices, stable mirror identity, and relative edit propagation.
2. Treat correctness of existing operations as the priority: mirror movement, transform, selection movement, repaint, tint, reshape, deletion, undo/redo, and save/load.
3. Defer count-changing regeneration. Changing six copies into eight raises separate product questions about hand-edited peers.
4. Defer VR controls for creating and recalling mirrors until mirror lifecycle semantics and persistence are stable.
5. Treat multiplayer peer linkage as unsupported until group identity survives live messages and scene-sync chunk boundaries. Multiplayer is a separate follow-up, not a prerequisite for local UI or settings controls.
6. This plan incorporates a static review of `feature/symmetry-stroke-linking` at `d5ec05f0f`. Code findings are not runtime verification; keep the baseline checks focused on confirming the identified risks.

## Required invariants

The implementation should enforce these rules centrally:

1. An edit-propagation basis changes only through a defined operation that also commits the corresponding geometry or coordinate-space transition. It is not a reconstruction of the stroke's actual shape or every hand-edited offset.
2. Every transform is tagged with, or explicitly converted through, the canvas in which it is expressed.
3. A failed, skipped, selected, erased, or untransformable stroke must not silently receive updated placement metadata.
4. Command constructors do not mutate sketch state.
5. One user gesture produces one undo step containing geometry, control-point, mirror, and placement-state changes.
6. Undo and redo restore geometry and metadata to matching states, including when a stroke is temporarily selected, erased, or unbatched.
7. Direct edits and propagated edits within the same tool update have deterministic conflict resolution independent of target enumeration order. Different physical tool trajectories need not produce identical results.
8. Save/load reconstructs groups and mirrors without changing which visible mirror settings are active unexpectedly.
9. Additive loading does not unintentionally link independent imports through reused mirror identities.

## Phase 1: Establish a reproducible baseline

### Work

1. Compile the branch and resolve any compiler errors without broad refactoring.
2. Add a temporary, unique diagnostic prefix for symmetry operations so current editor-log entries can be distinguished from old results.
3. Record the current behavior of this compact scenario matrix:
   1. Two-way mirror move, then tint and reshape either peer.
   2. Multi-mirror move, then transform one peer.
   3. Mirror move with one peer selected.
   4. Mirror move with one peer erased.
   5. Mirror move with an unbatched brush.
   6. Mirror move for strokes on a non-active layer.
   7. Selection move of one peer and of a complete group.
   8. Undo and redo after mirror and selection movement.
   9. Save/load, followed by another peer edit.
   10. Additive loading of the same sketch twice.
4. Turn each confirmed failure into a short reproduction note. Avoid expanding this into exhaustive UI testing.

### Acceptance criteria

1. The branch compiles.
2. Each known failure has a repeatable trigger and an identified expected result.
3. Log evidence is current and uses the unique diagnostic prefix.

## Phase 2: Separate generation settings from placement state

`SymmetrySettingsSnapshot` currently combines values with different lifetimes. Split its responsibilities conceptually, then in code:

1. `SymmetryGenerationSettings` describes how new copies are generated:
   1. Symmetry mode and custom type.
   2. Point family and order.
   3. Wallpaper group, repeats, scale, and skew.
   4. Per-copy transform settings such as `m_SymmetryTransformEach` and `m_SymmetryTransformEachAfter`.
   5. Color-shift settings.
   6. Script identity where it can be restored safely.
2. `SymmetryMirror` owns stable identity and its current generation settings, widget pose, and spin.
3. `SymmetryGroupPlacement` records an edit-propagation basis for the group's pointer indices and the canvas in which that basis is expressed. Peer edits use `C = basis[peer] * basis[source].inverse`; geometry and control points retain intentional differences that this basis does not describe.
4. Make snapshots genuinely immutable: use read-only properties and copy incoming transform collections.
5. Keep file-format migration explicit. The existing version remains readable while the new version records the separated values.

### Define basis semantics before changing the representation

1. Mirror movement updates each participating basis using the transform actually committed to that member. Do not replace it with the active mirror's ideal transform.
2. Specify separately whether individual transforms and whole-group selection transforms transport the editing frame or preserve its previous directions. Do not infer this policy from the phrase "actual placement." Preserve the existing relative-edit behavior until this choice is explicit.
3. Use a concrete decision case: move and rotate a complete group, then reshape one peer. State whether the other peers follow the rotated group's symmetry directions or the original directions. Also specify the result after moving just one peer with propagation disabled.
4. Canvas transfers must convert the recorded basis into the destination coordinate space, or deliberately suspend/detach linkage. Reshape and tint do not reconstruct a basis from the modified stroke shape.
5. Write the chosen rules for mirror movement, individual transforms, whole-group movement, and canvas transfer alongside their focused checks. Split only the data needed to implement these rules and the transaction fixes; avoid making a broad model rewrite a prerequisite for every repair.

### Acceptance criteria

1. Changing a mirror's current generation settings cannot mutate a group's recorded placement.
2. Recalling a mirror restores every recorded setting that the application supports, including spin and per-copy transforms.
3. Equality and hashing operate on immutable values and agree with one another.

## Phase 3: Replace mirror movement with a per-group transaction

### Begin

1. Capture the active mirror and its visible settings.
2. Build an immutable move plan per group containing:
   1. The group's canvas.
   2. Its old placement basis.
   3. The mirror basis at drag start, converted into that canvas.
   4. The eligible strokes and their pointer indices.
3. For the first stable implementation, reject the entire group if any member that must move is selected, erased, cannot be transformed, has an invalid pointer index, or otherwise cannot participate consistently. Erased members must not acquire new basis metadata without corresponding movement. An identity-delta member may remain unchanged, but validate its index and canvas. Supporting partially participating groups later requires explicit per-member state.
4. Do not use pointer count alone as a compatibility test. Also require a compatible symmetry topology or generation signature.

### Update

1. Calculate the mirror basis separately in each planned group's canvas.
2. Apply the incremental transform to geometry and control points.
3. Record a transform only after it succeeds.
4. Keep the accumulated transform per pointer index or stroke, rather than assuming every group followed the same ideal transforms.
5. Preflight each group update. If a member fails after another member has moved, roll back that group's update and retain its previous basis; do not advance a shared previous transform past a failed update. Provide a restoration path that does not depend solely on the failed in-place geometry operation.

### End

1. Derive each new placement basis from the old basis and the transform actually applied:

   `newBasis[i] = appliedDelta[i] * oldBasis[i]`

2. Do not replace group placement with the active mirror's ideal current transforms.
3. Record the widget move, stroke movement, mirror state, and placement transition as one composite command.
4. If no stroke moved, still keep mirror state synchronized with the widget and its undo command.

### Undo and redo

1. Restore a group's geometry, control points, and basis as one transaction. Preflight undo/redo and roll back partial application on failure; conditionally skipping metadata alone is not a successful undo.
2. Use a safe fallback for supported unbatched strokes, or reject them when constructing the move plan.
3. Remove the dependency on `MoveWidgetCommand.Merge()` accepting a command after `m_Final` is set.

### Cancellation and movement entry points

1. Define cancellation as restoring the drag-start widget, mirror settings, geometry, control points, and bases. Scene teardown may discard state only when the affected sketch is being discarded.
2. Feature disablement, topology changes, or mirror recall during a drag must finish or cancel the current transaction before changing its inputs. Do not silently forget already-applied movement.
3. Audit grab movement, `BringToUser`, command-driven `ResetToHome`, automatic spin, and API-driven pose/settings changes. Declare for each whether it carries artwork or only positions the widget; route every artwork-moving path through this transaction.
4. Recall positions the widget and restores settings without carrying artwork. Separate that restoration path from user edits so future lifecycle hooks cannot accidentally move strokes or fork mirrors during recall.

### Acceptance criteria

1. Moving a mirror on any layer produces the same scene-space result.
2. Skipped groups retain their previous placement metadata.
3. Tinting or reshaping after any mirror move does not scatter peers.
4. A mirror drag and the strokes moved by that drag undo and redo in one step.
5. A failure or cancellation cannot leave a partially committed group. Include a focused failure-path check as well as a normal drag.

## Phase 4: Repair selection preview and selection baking

### Work

1. Remove `SymmetryPeerPreview.Hide()` and every other state mutation from `SelectCommand` construction.
2. Introduce explicit preview lifecycle calls owned by selection interaction:
   1. Start or rebuild preview when the selected set changes.
   2. Update preview while the selection transform changes.
   3. Restore previewed strokes before constructing the bake command.
   4. Forget preview state only during scene teardown.
3. Capture each stroke's join transform and peer before-state at gesture time, rather than consulting mutable selection state later.
4. Define behavior for target-canvas changes. If a selected stroke and its peer finish in different canvases, either detach the relationship deliberately or retain it as inactive with explicit placement information.
5. Ensure preview restoration remains correct if a stroke is repainted, rebuilt, erased, or selected while previewing.
6. Use the same join-relative calculation for preview and bake: `movement = selectionTransform * joinTransform.inverse`. Store the source's join transform in the preview plan. The current preview uses the full selection transform while the bake subtracts the join transform.
7. Do not skip a bake merely because the final selection transform is identity: a stroke that joined under a nonidentity transform can still have nonidentity movement.

### Acceptance criteria

1. Constructing and discarding a `SelectCommand` changes no geometry or control points.
2. Adding a stroke to an already moved selection propagates only the movement after it joined.
3. Selection movement and peer movement form one undo step.
4. Repeated select, move, deselect, undo, and redo cycles do not accumulate drift.
5. Move a selection, add another group's stroke, move again, then deselect: preview and baked peers match without a jump. Repeat with the final selection transform returned to identity.

## Phase 5: Centralize peer-edit planning

Create a shared operation-level planner used by tint, reshape, repaint, transform, and deletion.

### Responsibilities

1. Preserve gesture-start state for undo. Separately freeze source and peer state for each tool update before applying any edits in that update. Discrete commands need one such update.
2. Gather all direct targets first, then traverse each affected symmetry group once per update.
3. Assign each target stroke one resulting edit per update. Preserve accumulated tint behavior and reshape's captured-transform behavior rather than resetting both to gesture-start values every frame.
4. Apply a documented conflict rule:
   1. A direct edit wins over a propagated edit.
   2. Multiple propagated edits to the same target use that update's frozen before-state and must produce equivalent results under a documented operation-appropriate comparison, or the target is skipped with a diagnostic.
   3. Define how a target contacted directly later in a gesture incorporates or replaces earlier propagated changes. Reusing a command dictionary alone does not establish direct-edit precedence; do not discard intentional accumulated changes implicitly.
5. Build a single command tree for the gesture.
6. Centralize eligibility checks for erased strokes, canvas compatibility, point-count compatibility, and invalid pointer indices.

### Tool-specific behavior

1. Tint and reshape retain point-to-point mapping and skip incompatible point counts.
2. Repaint preserves color offset, size ratio, and intentional per-pointer brush differences.
3. Transform propagates canvas-space deltas after explicit canvas conversion.
4. Delete includes every currently visible peer exactly once.
5. Cropping and other stroke-replacement operations must choose explicitly between:
   1. Applying corresponding replacements to every compatible peer.
   2. Detaching the replacement strokes from the symmetry group.

### Acceptance criteria

1. The same contacts and tool input in one update produce the same result regardless of enumeration order. Different physical sweep trajectories may produce different results.
2. No stroke receives competing edits within one update; its command retains the gesture-start undo state across subsequent updates.
3. Undo returns all directly and indirectly edited strokes to their gesture-start state.

## Phase 6: Define mirror lifecycle semantics

The following is a proposed product policy, not a settled requirement from the original handoff. Resolve the fork behavior before implementing automatic lifecycle changes:

1. Artwork-moving widget pose changes edit the active mirror transactionally. Wallpaper scale and skew are intended to use the same behavior when settings movement is implemented in Phase 8; until then they remain generation-only changes and must not rewrite group bases.
2. Mode, point family, wallpaper group, order, repeat count, and other topology changes create a new mirror automatically.
3. Turning symmetry off and back on creates a new mirror.
4. Explicit recall is the only operation that resumes an older mirror.
5. Recall restores the widget and generation settings but moves no strokes until the user edits that mirror.
6. Retain mirrors for recall by default, matching the current registry. Garbage collection is a separate policy choice: account for references from undo/redo history as well as live strokes, and do not silently change oldest-first recall indices.
7. Recall and load restore settings through an explicit restoration path that suppresses automatic forking. Define whether user-created/forked mirrors and active-mirror changes participate in undo before wiring those transitions.

This prevents an older object from moving merely because the user returned to symmetry elsewhere, and prevents equal pointer counts from being mistaken for compatible topology.

### Acceptance criteria

1. Starting a separate symmetry session cannot move earlier artwork accidentally.
2. Recalling an older mirror restores its visible state and makes only its groups eligible for subsequent movement.
3. Structural changes never reinterpret old pointer indices under an unrelated topology.

## Phase 7: Harden persistence

### Work

1. Version the separated generation, mirror, and placement records.
2. Persist every mirror that should remain recallable, including the active mirror when it has no groups.
3. On ordinary load, choose one explicit behavior:
   1. Recall the chosen loaded mirror on the main thread and apply its settings to the widget.
   2. Leave loaded mirrors inactive and retain the user's current widget state.
4. Do not assign `SymmetryMirrors.Active` without reconciling visible widget settings.
5. Remap mirror identity during additive imports so loading the same sketch twice creates independent instances. Preserve identity only for protocols that explicitly intend to merge the same mirror.
6. Validate table counts and lengths before allocating or advancing through the trailer.
7. Ensure save-thread snapshots do not retain mutable live mirror or group objects.
8. Store mirror recall order and active identity explicitly rather than deriving them from stroke encounter order. Mirror records must be writable and readable without any grouped strokes: currently table creation depends on stroke references and `ReadSymmetryTable` returns immediately when `pending.Count == 0`.
9. Parse and validate into temporary records before mutating `SymmetryMirrors`. On invalid counts, lengths, indices, or truncation, abandon the trailer cleanly using the documented fallback; do not continue reading later tables from a misaligned position or leave partially registered mirrors.
10. Pass an explicit load context for ordinary load, additive import, and network reconstruction. Apply identity remapping consistently within each import, and register validated records on the owning thread. Do not infer import intent from whether an active mirror happens to exist.

### Acceptance criteria

1. Save/load preserves group membership, pointer indices, placement, mirror settings, and recall order.
2. Editing immediately after load behaves the same as editing immediately before save.
3. Two additive imports of the same file do not move each other's strokes.
4. Older files load without symmetry data loss outside the unsupported new fields.
5. An empty active mirror and the retained recall list round-trip without grouped strokes. A malformed trailer leaves no partially registered mirrors.

## Separate follow-up: Design multiplayer identity explicitly

Do not rely on file-local group IDs for network identity.

This follow-up does not block Phase 8 or local stabilization. Before local release, define and enforce the unsupported multiplayer behavior: audit both live messages and scene sync, which already shares sketch serialization, and prevent incomplete peer linkage from enabling inconsistent propagated edits. Document the chosen guard and cover it with a focused check; a label in this plan alone is insufficient. Full multiplayer support remains outside the local milestone.

### Work

1. Give networked symmetry groups a stable group GUID.
2. Include group GUID, mirror GUID, pointer index, generation settings reference, and placement data in live stroke creation messages or a dedicated group message.
3. Preserve group identity across long-stroke message chunks and full-scene sync chunks.
4. Decide whether mirror edits are transmitted as high-level mirror transactions or as resolved per-stroke transforms. Prefer one authoritative representation to avoid applying movement twice.
5. Keep network mirror registries scoped to the remote sketch/session and resolve GUID collisions deliberately.

### Acceptance criteria

1. A symmetry group split across scene-sync chunks is reconstructed as one group.
2. Live peers create the same group membership on every participant.
3. Mirror movement, peer edits, undo, and late join produce consistent results across clients.

## Phase 8: Add UI and count-preserving settings movement

After local stabilization, persistence, and the required lifecycle decisions are complete (without waiting for multiplayer support):

1. Add VR controls for new and recalled mirrors.
2. Route wallpaper scale and skew through the same per-group movement transaction used by widget movement.
3. Merge continuous slider changes into one command per gesture.
4. Keep topology-changing controls on the automatic-fork path.
5. Profile live movement on sketches with many mirrored strokes and batches before adding caching or other optimization complexity.

## Verification strategy

Use a small set of focused automated checks plus editor scenarios.

1. Pure tests:
   1. Conjugated transform calculation, including reflected transforms.
   2. Placement-basis updates from actual applied deltas.
   3. Topology compatibility checks.
   4. Immutable snapshot equality and hashing.
   5. Serialization round trips and old-version reads.
2. Editor checks:
   1. Mirror and selection interaction lifecycle.
   2. Batched and supported unbatched brush behavior.
   3. Multiple canvases and non-active layers.
   4. Undo/redo command composition.
3. Full network checks remain separate until the multiplayer follow-up supplies persistent group identity. Verify the interim unsupported-mode guard before local release.

## Implementation order

1. Baseline reproductions and logging.
2. Define basis semantics and make the minimal placement/settings separation needed for the fixes.
3. Transactional mirror movement and one-step undo.
4. Selection preview lifecycle.
5. Persistence and additive loading, including empty mirrors and safe parsing. Resolve retained-mirror and load-state policy here; defer automatic fork behavior if it is still undecided.
6. Shared edit planner and tool migration.
7. Resolve and implement the remaining mirror lifecycle policy.
8. VR UI, settings sliders, and performance profiling.
9. Separate follow-up: multiplayer identity and full network behavior.

Phase numbers group the work by subject; the order above expresses implementation dependencies. Do not build a dependent feature on a path that can leave geometry and basis metadata disagreeing. Independent persistence or tool fixes may proceed without completing unrelated phases, and multiplayer does not gate local UI. Keep verification focused on the listed risks rather than expanding this into exhaustive testing.
