# Symmetry stroke linking and peer editing — handoff notes

Branch: `ccr-177bbb3d-nv3tl6` (based on `main`; the earlier work came from
`claude/symmetry-stroke-linking-6na5p8`).

Strokes drawn with symmetry had no relationship to each other: deleting or editing one
left its copies untouched. This branch adds **linked mirrors**. A linked mirror is a thing
with an identity that owns the strokes drawn with it. An edit to any copy carries to every
copy, and moving the mirror carries its strokes. Plain symmetry is unchanged.

**Not yet built as a player.** Changed files pass a Roslyn semantic check against Unity's
engine assemblies. The project owner has tried parts in the editor (see *Verification
status*). Treat everything else as unproven.

---

## Turning it on

There is no global switch. Linking comes from the mirror: strokes drawn with a **linked
mirror** are linked, and strokes drawn with **plain** symmetry are ordinary strokes. See
*Design decisions* below.

- **In VR:** the "Linked mirror" toggle in the mirror options popup
  (`GlobalCommands.ToggleLinkedMirror`, 6008).
  - On starts a fresh linked mirror; off switches to plain.
  - The toggle and the widget title ("Mirror / Linked" in light cyan) follow
    `SymmetryMirrors.Showing`. So they read "on" only when new strokes would actually link.
  - The popup is an ordinary popup, not a long-press one. It stays open while you use it.
- **API:**
  - `symmetry.mirror.new` creates a linked mirror.
  - `symmetry.mirror.recall=<n>` recalls an earlier one, oldest first.
  - `symmetry.mirror.fromselection` recalls the mirror owning the last selected stroke.
  - `symmetry.mirror.plain` goes back to plain.
- **In code:** `SymmetryMirrors.NewLinked/Recall/RecallFromStroke/UsePlain`. Each records an
  `ActivateMirrorCommand`, so undo restores the active mirror and the widget.
- **Mode switches:** switching to another symmetry mode goes back to plain, in the same undo
  step (`PointerManager.SetSymmetryMode`). Turning symmetry off keeps the linked mirror, so
  turning it on again resumes it.
- **Multiplayer:** runs in beginner mode, so linked mirrors aren't supported there. Strokes go
  over the network unlinked (`SymmetrySaveState.None()`).

---

## Data model

```
Stroke ─┬─ m_SymmetryGroup ──→ SymmetryStrokeGroup ─┬─ Instances (per copy: colour shift, size ratio)
        └─ m_SymmetryPointerIndex                   └─ Mirror ──→ SymmetryMirror ── Settings (live)
```

**`SymmetryMirror`.** A linked mirror is created only by explicit user action. It has a
`Guid` that outlives any settings it happens to have. Its `Settings` are its current state.
Changing them changes this mirror and carries its strokes; it never forks a new mirror. It
also records the `Canvas` (layer) its transforms are expressed in.

**`SymmetryMirrors`** is the registry:
- `Active`: what the widget stands for (null means plain).
- `Showing`: Active, if the current mode is the mirror's.
- `LinkingMirror`: Showing, if drawing into the mirror's canvas.
- `All`: the mirrors, oldest first.
- `NoteSettingsChanged`: called from `PointerManager.CalculateMirrors`.
  - A change that keeps the copy count runs `MoveMirrorSettingsCommand`. Slider drags merge
    into one undo step.
  - A change that alters the copy count runs `RegenerateMirrorStrokesCommand`.

**`SymmetryStrokeGroup`.** One per draw burst: the stroke the user drew plus the copies the
mirror made. Strokes hold it by reference, there is no registry, and a group dies with its
strokes. Ids exist only inside a saved file.
- `Canonical` is the lowest pointer index.
- `DerivationSource` is the lowest *visible* pointer index, used for rebuilds.
- `Instance` holds per-copy appearance relative to the canonical stroke: an HSV
  `ColorShift` and a `SizeRatio`. Position comes from the mirror.
- `CaptureInstances` measures the instances. It runs when a group is drawn, loaded or
  duplicated.

**`SymmetryPointerIndex`.** Which of the mirror's pointers drew this stroke. Index 0 is the
pointer the user controls.

**`SymmetrySettingsSnapshot`.** Records:
- mode, point/wallpaper parameters, widget pose, spin and script name;
- transform-each (`TransformEach`, `TransformEachAfter`);
- `PointerTransforms`: the transform mapping pointer 0's stroke onto each pointer's stroke,
  in the mirror's canvas space.

Snapshots are replaced, never changed in place.

Only linked mirrors make groups. Plain, scripted and two-handed symmetry produce ordinary
strokes.

---

## Rules that must not be broken

1. **Edit the copy that was touched, then derive the rest from it** (decision 8 as
   clarified). Tools no longer mirror their own edits. `RederiveSymmetryGroupCommand`
   rebuilds the other copies from the edited one:
   - geometry goes through the mirror;
   - appearance (colour, point colours, brush, size) goes through the instance data.

   Each copy keeps its own colour shift and size ratio.
2. **Rigid moves don't rebuild.** When copy s moves by xf, copy j moves by C·xf·C⁻¹, where
   C is the mirror transform from s to j (`TransformSymmetryCopiesCommand`). Batched copies
   move in place. Others get their control points moved and are rebuilt. Repeated in-place
   moves can drift by float error; the next rebuilding edit makes the group exact again.
3. **Point *i* maps to point *i*.** Copies are drawn point for point alongside each other.
   Derivation relies on this.
4. **Control points move with geometry.** Anything that rebuilds a stroke regenerates it
   from control points, so a stroke moved without them jumps back. Only use
   `TransformGeometryInPlace(updateControlPoints: false)` where nothing else can reach the
   stroke.
5. **Erased copies stay erased, and are never a source.** Edits skip erased copies. Rebuilds
   use `DerivationSource`. When a mirror change rebuilds from a source other than pointer 0,
   the source first moves by its own pointer's change.
6. **Selected copies are moved by the selection.** Only one copy of a group is ever selected
   (decision 10). The others follow it live through `SymmetryPeerPreview`. The deselect bake
   adopts what the preview already moved.
7. **A mirror change ends a selection that holds one of its copies** before it applies
   (`SymmetryMirrors.EndSelectionOwnedBy`).

---

## File map

| File | What it holds |
|---|---|
| `SymmetryMirror.cs` | Mirror identity + `SymmetryMirrors` registry |
| `SymmetryStrokeGroup.cs` | The group: members, canonical/derivation source, instance data |
| `SymmetrySettingsSnapshot.cs` | Settings + `PointerTransforms`, value equality, binary blob (v2) |
| `SymmetryPeerEditing.cs` | Peer queries: `IsLinked`, `PeersOf`, `PeersOutside`, `TryGetPeerSymmetryTransform` |
| `SymmetryPeerPreview.cs` | Unselected copies following a selection live; `TryCommit` |
| `SymmetryMirrorMove.cs` | Strokes following the mirror live while the widget is held |
| `Commands/SymmetryMirrorCommands.cs` | `ActivateMirrorCommand`, `MoveMirrorSettingsCommand`, `RegenerateMirrorStrokesCommand`, `MirrorStrokeEdits` |
| `Commands/RederiveSymmetryGroupCommand.cs` | `SymmetryDerivation` + derive a group from one copy |
| `Commands/TransformSymmetryCopiesCommand.cs` | Rigid copy moves; `ForSelectionEdit` |
| `Commands/MoveSymmetryGroupsToLayerCommand.cs` | Whole groups to another layer under a new mirror |
| `Commands/MoveMirrorStrokesCommand.cs` | Records a mirror move (recorded, not performed) |
| `Stroke.cs` / `StrokeData.cs` | Group membership, `ReplaceDerivedData`, `TransformGeometryInPlace` |
| `Batching/Batch.cs` | `TransformSubset`: in-place geometry move |
| `Save/SketchWriter.cs` | Stroke extensions, `SymmetrySaveState`, the symmetry table |
| `Widgets/SymmetryWidget.cs` | No spin/drift while linked; linked title |
| `GUI/MirrorOptionsPopUpWindow.cs`, `Prefabs/PopUps/PopupWindow_MirrorOptions.prefab` | Linked toggle |

---

## File format

Per stroke, two **single-word** extensions. Older readers skip each with a 4-byte read:

- `StrokeExtension.SymmetryGroup = 1 << 5`: uint32, dense file-local group id, 0 = none.
- `StrokeExtension.SymmetryPointerIndex = 1 << 6`: int32.

After the last stroke comes a **trailer** that older readers never reach:

```
uint32 'SYMT' (0x53594d54)
int32  version (3)
int32  mirrorCount,  [Guid + uint32 len + settings blob] *
int32  groupCount,   [uint32 mirrorIndex] *        (group id = index + 1, mirror index 1-based)
uint32 activeMirror                                 (1-based, 0 = plain)
```

How it is written and read:
- **Capture.** `SymmetrySaveState.Capture` runs on the main thread together with the stroke
  copies. The background save thread therefore never reads live mirrors.
- **What is saved.** A whole-sketch save keeps every registered mirror, even ones with no
  strokes. Saving selected strokes keeps only the mirrors they use, and includes the
  selected copies' peers.
- **Additive loads** give every mirror a fresh identity and leave the active mirror alone.
- **Old tables.** Other trailer versions are ignored, so their strokes load plain.
- **Instance data isn't stored.** It is re-measured on load (decision 9's trailer is not
  done).
- **Multiplayer** uses the same stroke stream but writes no table.

---

## What is wired, and where

| Edit | Hook |
|---|---|
| Eraser / delete selection | `SketchMemoryScript.MemorizeDeleteSelection`, `DeleteSelectionCommand` |
| Transform (+ `strokes.move/rotate/scale` API) | `TransformItemsCommand` → `TransformSymmetryCopiesCommand` |
| Selection grab + bake | `SymmetryPeerPreview` (live), `SelectCommand` (`m_CopyMoves`, adopted from the preview) |
| Selection-wide ops (transform/align/distribute) | `TransformSymmetryCopiesCommand.ForSelectionEdit` |
| Duplicate | `DuplicateSelectionCommand.DuplicateLinkedGroup` (new linked group) |
| Repaint / recolour / rebrush / resize | `SketchMemoryScript` → appearance rederive (first copy touched drives) |
| Tint | `TintColorTool` → appearance rederive |
| Reshape (all sub-tools) | `ReshapeTool` → geometry rederive, refreshed during the drag |
| Snip / crop / join | `SnipStrokeCommand`, `StrokeCropping.DeriveCropPiece`, `JoinStrokeCommand.ClassifySymmetryJoin` |
| Layer move / squash | `MoveSymmetryGroupsToLayerCommand` (selection, `SquashLayerCommand`, Lua) |
| Mirror move | `SymmetryWidget` interaction → `SymmetryMirrorMove`; Bring to User / Reset to Home too |
| Settings change | `PointerManager.CalculateMirrors` → `SymmetryMirrors.NoteSettingsChanged` |

Two non-obvious ones:

- **The deselect is where a selection move becomes real.** Strokes ride the selection canvas
  and are only rewritten on deselect, so `SelectCommand` (deselect) is the undoable moment.
- **A mirror move is applied live and *recorded* afterwards.** `RecordCommand` puts a
  command on the stack without executing it.

---

## Performance

Per-frame geometry moves are cheap if done in place. `Batch.TransformSubset` moves a stroke
where it sits: one pass over its vertices plus a mesh update. Cost scales with the number
of **batches touched**. Avoid `Uncreate`/`Recreate` and reparenting between canvases in
anything that runs every frame. That is why rigid edits take the in-place path and only
shape and appearance edits rederive.

Nobody has profiled this on a sketch with thousands of mirrored strokes.

---

## Verification status

| | |
|---|---|
| Confirmed in the editor | Mirror move following strokes live; selection move, release and undo on linked copies |
| Fixed, needs retest | Linked toggle initial state and mode switching (`feb1b36`) |
| Never exercised | Repaint/tint/reshape rederive, snip/crop/join, layer moves, duplicate, settings changes (move and regenerate), save/load round trip, additive load |

Changed files pass the Roslyn semantic check. Editor tests exist
(`Assets/Editor/Tests/TestSymmetryMirrorMove.cs`, `TestSymmetryPeerSelection.cs`) but
haven't been run here.

---

## Traps found the hard way

- **Geometry-only preview.** Moving a peer's geometry without its control points broke as
  soon as another tool rebuilt the stroke. Rule 4 exists because of this.
- **Settings as identity.** Linking strokes to a snapshot of the settings dissolves the link
  exactly when it matters: move the mirror and the values no longer match. Hence
  `SymmetryMirror`.
- **Sweeping a tool across a group.** Repaint, tint and reshape can touch several copies of
  one group in a frame. The first copy touched drives the group for that sweep, and the
  others are ignored.
- **Erased peers.** Recreating geometry or reparenting resurrects an erased stroke. Rule 5.
- **Rebuilding every move.** Deriving geometry for translations was too slow; hence rule 2.
- **Popup restore.** The mirror popup used to re-issue MultiMirror on close, to undo a
  long-press glitch. That workaround fought deliberate mode changes once the popup stayed
  open, so it was removed.
- **Active but not showing.** A linked mirror from another mode used to stay active, so the
  toggle read "on" while strokes were plain. UI state now uses `Showing`, and mode switches
  go plain.

---

## Design decisions

Answered by the project owner on 2026-10-01; these replace the open questions that stood here.

**Implementation status.** Decisions 1, 3 (API only), 4, 5, 7-8 (as clarified), 10, 11 and
12 are implemented. Decision 2's "Plain" entry and decision 3's VR routes wait on the mirror
list UI. For now the popup has a single linked/plain toggle. Decision 9 (instance data in the
file trailer) is not started. A linked mirror can't spin or drift: when released, it stays
where it was let go. Tossing it away to hide it still works, and `Spin` is ignored. Spinning
would need strokes following a mirror in continuous motion, with no point at which to record
a move.

**Canonical-stroke model (decisions 7-8).** Each group records per-member instance data
(`SymmetryStrokeGroup.Instance`: an HSV colour shift and a size ratio relative to the canonical
stroke, the lowest pointer index); position comes from the mirror. `RederiveSymmetryGroupCommand`
makes every other member an exact copy of one member - geometry through the mirror, appearance
(colour, point colours, brush, size) through the instance data - keeping erased members erased.
`RederiveSymmetryGroupCommand.Appearance` derives appearance only, which works on selected or
preview-displaced copies. Every edit to a linked stroke is made to the copy touched and the group
is derived from it; tools no longer mirror their own edits:
- repaint (selection and tool sweep), tint and reshape: the first copy a drag or sweep touches
  drives its group, refreshed as the drag goes;
- transform and the selection bake: rigid moves don't rebuild. `TransformSymmetryCopiesCommand`
  moves each other copy by C·xf·C⁻¹ where it lies (in place for batched copies, control points
  plus rebuild otherwise); undo applies the inverse. The bake adopts the copies the selection
  preview has already moved (`SymmetryPeerPreview.TryCommit`), so release costs nothing for
  them. Copies in the moved list themselves get only the correction to their mirrored move.
  Repeated in-place moves can drift by float error; the next rebuilding edit makes the group
  exact again;
- snip (copies derived first so one index cuts them all), crop (the source is cropped, copies
  take its pieces), join (pairwise, then derived);
- mirror moves and settings changes move strokes in place, falling back to derivation from a
  visible member moved with its own pointer;
- copy-count changes regenerate each group from its derivation source. Each pointer keeps its
  instance appearance, and new pointers get the mirror's colour shift for their index
  (`RegenerateMirrorStrokesCommand.NewCopyInstance`);
- selection-wide operations (transform, align, distribute, duplicate) carry whole groups.

Checked with Roslyn semantic analysis against Unity's engine assemblies (no errors in changed
files); not yet built or run in Unity.

**Group selection (decision 10).** A linked group is selected through one copy, the first
picked, which drives it; the other copies are never selected themselves and follow it live as
its mirror images (`SymmetryPeerPreview`). Selecting another copy of a selected group does
nothing; deselecting any copy deselects the group. Enforced in `SelectionManager.SelectStrokes`
(so API, Lua and invert-selection paths follow it), `SelectCommand` and `SelectionTool`. The
following copies aren't drawn with the selection highlight.

**Layer moves (decision 11).** `MoveSymmetryGroupsToLayerCommand` takes whole groups into
another layer: every copy goes (erased ones stay erased), and the groups are re-homed under a
new linked mirror there, made from the old one with its transforms re-expressed in the new
layer's space (one new mirror per old mirror per move). The new mirror is registered but not
made active. Used when a selection is moved to another layer (the driving copy's peers get the
mirrored move first, then go with it), when a layer is squashed into another, and by Lua's
`stroke.layer` and `group:Add` (unrecorded, as Lua edits are).

**Erased copies.** Edits skip erased copies, which is safe: an erased copy only comes back
through undo, after every later edit has been undone. What isn't safe is rebuilding from one,
so rebuilds use `SymmetryStrokeGroup.DerivationSource` (the lowest visible pointer index).
When a mirror move or settings change rebuilds a group from a source other than pointer 0,
`RederiveSymmetryGroupCommand` first moves the source by its own pointer's change.

**Join (decision 12).** `JoinStrokeCommand.ClassifySymmetryJoin` decides. Two strokes from
different groups of one mirror: every copy of A joins its counterpart in B's group (the copy at
the pointer whose transform is P[j]·P[a]⁻¹·P[b]), A's group becomes the joined group, and it is
derived from A afterwards. Two copies of one group: the joined stroke leaves the group, as
before. Linked with unlinked, or different mirrors: refused by the join tool and `stroke.join`.
The range join `strokes.join` still unlinks what it joins.

**Selection and mirror changes.** A mirror move or settings change on a mirror that owns a
selected copy ends the selection first (`SymmetryMirrors.EndSelectionOwnedBy`), baking it;
the two are separate undo steps.

**Range join.** `strokes.join` joins unlinked strokes, or copies of one group (the result
leaves it); a range mixing groups or mirrors is refused.

Links now break only where a group has no visible member to rebuild from during a
count-changing settings change (it is invisible, and undo restores the link with it) or where
something outside these paths splits a group across canvases. Not started: instance data in
the file trailer (decision 9). Lua and some API stroke edits still bypass derivation (see
*Next steps*).

### Principle

Mirror creation is controlled only by explicit user action; everything else follows from that.
A mirror has a persistent identity and owns the peer groups drawn with it. Peer relationships
come from that ownership, never from comparing symmetry settings. Peer editing is not a global
setting: it is a property of the mirror (linked vs plain).

- **Plain mirror:** today's symmetry. Strokes drawn with it are ordinary strokes.
- **Linked mirror:** owns its strokes. Edits propagate across each group, and moving the mirror
  moves its strokes.

### Decisions

1. **What creates a linked mirror:** a dedicated "New linked mirror" button. It always creates
   a fresh linked mirror, even while another linked mirror is active. Nothing creates one
   implicitly (drawing, widget moves, settings changes, navigation and undo never do).

2. **Returning to plain symmetry:** the mirror list in the popup has a "Plain" entry alongside
   the linked mirrors. Choosing it switches the widget to a plain mirror; linked mirrors keep
   their strokes.

3. **Getting back to an earlier linked mirror:** both routes:
   - the mirror list in the mirror popup, and
   - activating a mirror from one of its strokes.

   Either makes that mirror active and moves the widget to its pose.

4. **Turning symmetry off and on:** turning symmetry off only hides the mirror. Turning it back
   on resumes the same linked mirror. *(Implementation, 2026-10-04: switching to a different
   symmetry mode goes back to plain, undoably, so the toggle never shows a linked mirror that
   isn't linking.)*

5. **Settings that change the number of copies** (point order, wallpaper group, repeat counts)
   on a linked mirror that already owns strokes: **regenerate the copies** to the new count.

6. **Hand edits and regeneration:** within a linked group, every edit to one member always
   applies to all other members. Copies are therefore always exact symmetric images of each
   other, so regeneration simply rebuilds the group from one member under the new settings.
   (Superseded in mechanism by decisions 7-12: copies are derived from one canonical stroke.)

### Edits and links (2026-10-02)

Background: links were broken as a side effect when an edit couldn't be mirrored (reshape
with mismatched point counts, snip, crop, multi-copy selection moves, layer moves, mirror
moves over partly-selected groups, join). That kept the shared mirror transforms truthful in
the old model, where peer editing could be off and groups drift. With edits always applying
to every member, it silently breaks the promise that a linked group stays symmetric.

7. **One canonical stroke per group.** A linked group stores a single canonical stroke; every
   other copy is an *instance*: a transform plus an optional colour override. Edits apply
   once, to the canonical stroke, so links can't break as a side effect and there is nothing
   to propagate. Per-copy jitter is carried by the instance (colour jitter in the colour
   override, size jitter in the transform's scale, position jitter in its translation).

8. **Runtime representation: derived now, instancing later.** Copies remain real `Stroke`
   objects generated from the canonical stroke and their instance data, so tools, exporters,
   Lua and multiplayer keep working. They are read-only: an edit aimed at a copy is redirected
   to the canonical stroke through that copy's transform, and the copies are re-derived. Live
   tools (drags) may update copies cheaply per frame and re-derive once on release. GPU
   instancing is a later rendering optimisation behind the same data model.

   *Clarified 2026-10-03:* edits are made to whichever copy was touched, not redirected to
   the canonical stroke; the group is then derived from that copy. Only the canonical stroke
   and the instance data are authoritative, so the result is the same, but a selection stays
   where the user grabbed it.

9. **File format: canonical + instances, and expanded copies.** The trailer stores the
   canonical stroke's group and each instance (transform + colour override). Every copy is
   also written as an ordinary stroke, marked as derived, so older readers and other .tilt
   consumers see the full sketch. The loader discards the written copies and re-derives them.

10. **Several copies of one group in a selection:** one copy drives. Selecting a second copy
    of a group selects the group; the first-selected copy drives the move, the canonical
    stroke follows it, and every copy moves as its mirror image would.

11. **Moving a linked copy to another layer:** the whole group moves to that layer, under a
    new linked mirror created there. (This is a mirror created as a consequence of a user
    action rather than by "New linked mirror" — an intended exception to decision 1.)

12. **Join:** depends on the pair.
    - Two strokes from different groups of the same mirror: every pair of copies joins,
      giving one linked group.
    - A copy joined to another copy of the same group (across the mirror): the result is one
      self-symmetric stroke, which leaves the group.
    - Anything else (linked with unlinked, or different mirrors): refused.

**Postponed:** an explicit "Unlink" action (per group or per copy), as a later expansion once
the basics are complete and consistent.

**Assumed, not yet asked:** the canonical stroke is the lowest pointer index in the group,
normally 0 (the one the user drew). A duplicated or regenerated group's canonical stroke is
chosen the same way.

### Consequences agreed in discussion

- Peer edits use the owning mirror's own current state, so they work whether or not that
  mirror is active or in the current symmetry mode.
- While a linked mirror is active, every widget move is a mirror move (including Bring to User,
  snap home and spin). A plain mirror's widget moves freely.
- Creating a mirror, switching the active mirror and moving a mirror are undoable commands;
  undo restores recorded state rather than inferring it.
- Old sketches load as plain. Only linked mirrors are saved, so no "linked" flag is needed.
- d8b7fab "Reactivate the matching symmetry mirror after undo and redo" (settings matching and
  tolerance) was reverted; this design supersedes it.

---

## Next steps, in order

1. **Retest in the editor.** Start with the linked toggle and mode switching, then
   everything still listed as never exercised under *Verification status*.
2. **Lua and API edits that bypass derivation:**
   - Lua stroke setters;
   - API `strokes.delete` by index;
   - `_ModifyStrokeControlPoints`.

   Each should edit the touched copy and rederive, as the tools do.
3. **Copy-count change rough edges.** Groups that can't be regenerated (no visible member)
   become invisible rather than unlinked. Check undo through a regenerate.
4. **Drift.** Repeated in-place rigid moves accumulate float error until the next rebuild.
   Consider rederiving on save or after N moves.
5. **Non-batched brushes during a mirror drag.** `TransformGeometryInPlace` only handles
   batched strokes, so these follow on release rather than live.
6. **Mirror registry clutter.** Every "on" press makes a mirror, and empty mirrors are saved.
   Decide when unused mirrors are pruned.
7. **Recall UI.** The mirror list (with a "Plain" entry) and activating a mirror from a
   stroke in VR. Needs prefab work.
8. **Decision 9's file trailer:** instance data and derived-copy markers.
