# Symmetry stroke linking and peer editing — handoff notes

Branch: `claude/symmetry-stroke-linking-6na5p8` (14 commits, `9d8e50f`..`2472efa`).

Strokes drawn with symmetry had no relationship to each other: deleting or editing one
left its copies untouched. This branch gives them one, records how they were made, and
makes edits propagate. It then goes further: mirrors become things with an identity, and
moving a mirror carries its strokes.

**Nothing here has been compiled by the author.** One part has been confirmed in the
editor by the project owner (see *Verification status*). Treat the rest as unproven.

---

## Turning it on

There is no switch. Linking comes from the mirror: strokes drawn with a **linked mirror**
are linked, strokes drawn with **plain** symmetry are ordinary strokes. See
*Design decisions* below.

- In VR: the "New linked mirror" button in the mirror options popup (`GlobalCommands.NewLinkedMirror`, 6008).
- API: `symmetry.mirror.new` (create a linked mirror), `symmetry.mirror.recall=<n>`
  (an earlier one, oldest first), `symmetry.mirror.fromselection` (the mirror owning the
  last selected stroke), `symmetry.mirror.plain` (back to plain).
- In code: `SymmetryMirrors.NewLinked/Recall/RecallFromStroke/UsePlain`. Each records an
  `ActivateMirrorCommand`, so undo restores the active mirror and the widget.

---

## Data model

```
Stroke ─┬─ m_SymmetryGroup ──→ SymmetryStrokeGroup ─┬─ Settings  (placement + settings AS DRAWN)
        └─ m_SymmetryPointerIndex                   └─ Mirror   ──→ SymmetryMirror ── Settings (LIVE)
```

**`SymmetryStrokeGroup`** — one per draw burst: the stroke the user drew plus the copies
the symmetry made. Strokes hold it by reference, so peer lookup is a field access. There
is no registry; a group dies with its strokes. Ids exist only inside a saved file.

**`SymmetryPointerIndex`** — which of the symmetry's pointers drew this stroke. Index 0 is
the pointer the user controls. This is the only asymmetry in a group; there is no "master
stroke" concept and none is needed (see *Rules*).

**`SymmetrySettingsSnapshot`** — mode, point/wallpaper parameters, widget pose, spin,
script name, and `PointerTransforms`: the transform mapping the drawn stroke onto each
pointer's stroke, **in the canvas space the strokes were drawn in**. Immutable and
interned by value, so a sketch holds one per distinct configuration, not one per stroke.
On a group it means *where these strokes actually are*; a mirror move rewrites it.

**`SymmetryMirror`** — a linked mirror: created only by explicit user action, with a
`Guid` that outlives any settings it happens to have. Its `Settings` are its current state;
changing them changes this mirror and carries its strokes, never forks a new one.
`SymmetryMirrors` is the registry: `Active` (what the widget stands for, null = plain),
`Showing` (Active, if the current mode is the mirror's), `LinkingMirror` (Showing, if
drawing into its canvas), `All` (oldest first), and `NoteSettingsChanged` (called from
`PointerManager.CalculateMirrors`: moves strokes for a count-preserving change, rebuilds
groups for a count-changing one).

Scripted and two-handed symmetry get a group but **no mirror** — the widget doesn't stand
for them, so nothing can move them after the fact.

---

## Rules that must not be broken

1. **Edits are relative, never absolute.** A peer gets the *mirrored delta* of what
   happened to the stroke, not placement onto an ideal mirror image. Colour keeps its HSV
   offset, size its ratio, position its displacement. So a peer that has drifted from
   perfect symmetry keeps its drift, and a group that matched stays matching. Snapping
   peers onto exact symmetry would destroy deliberate work.

2. **Point *i* maps to point *i*.** Peers are drawn point for point alongside each other.
   Tint and reshape rely on this and bail when the counts differ (a stroke the simplifier
   treated differently, or a scripted pointer that started a new stroke mid-line).

3. **Index 0 stays put.** For a mirror move, stroke *j* moves by `Mj_new · Mj_old⁻¹`.
   Index 0's transform is the identity at both ends, so the drawn stroke holds still and
   the copies rearrange around it. This falls out of the maths — don't add a master.

4. **Count-preserving vs count-changing.** Pose, wallpaper scale and skew are transforms
   of existing strokes. Order, wallpaper group, repeat counts and mode changes are not —
   moving 6 strokes to 8 positions needs regeneration. `MoveMirrorStrokesCommand` and
   `SymmetryMirrorMove` skip groups whose pointer count differs from the mirror's current
   count, so a group drawn at order 6 keeps behaving as order 6.

5. **Control points move with geometry.** `Stroke.TransformGeometryInPlace` has an
   `updateControlPoints: false` mode. Anything that rebuilds a stroke regenerates geometry
   *from control points*, so a stroke moved without them jumps back the moment another
   tool touches it. This caused a real bug (see *Traps*). Only use that mode where nothing
   else can reach the stroke.

6. **Peers that are erased, selected, or in another canvas are skipped.** Recreating
   geometry or reparenting would resurrect an erased stroke; a selected stroke is being
   moved by something else; the canvas-space transforms don't hold across canvases.
   `SymmetryPeerEditing.PeersOf` filters erased centrally.

---

## File map

| File | What it holds |
|---|---|
| `SymmetryStrokeGroup.cs` | The group: peers, placement snapshot, mirror |
| `SymmetrySettingsSnapshot.cs` | Settings + `PointerTransforms`, value equality, binary blob |
| `SymmetryMirror.cs` | Mirror identity + `SymmetryMirrors` registry |
| `SymmetryPeerEditing.cs` | **The one place tools ask about peers.** Every rule above lives here |
| `SymmetryPeerPreview.cs` | Peers following a selection live |
| `SymmetryMirrorMove.cs` | Strokes following the mirror live |
| `Commands/MoveMirrorStrokesCommand.cs` | Records a mirror move (recorded, not performed) |
| `Stroke.cs` / `StrokeData.cs` | Peer API, `TransformGeometryInPlace` |
| `Batching/Batch.cs` | `TransformSubset` — in-place geometry move |
| `Save/SketchWriter.cs` | Stroke extensions + the symmetry table |

`SymmetryPeerEditing` is the extension point. Teaching a new tool about peers should be
one call to `WithPeers`/`PeersOutside` (set-shaped edits) or one of the
`TryGetPeer*`/`GatherPeer*` helpers (edits that transform or recolour).

---

## File format

Per stroke, two **single-word** extensions — older readers skip each with a 4-byte read:

- `StrokeExtension.SymmetryGroup = 1 << 5` — uint32, dense file-local group id, 0 = none
- `StrokeExtension.SymmetryPointerIndex = 1 << 6` — int32

After the last stroke, a **trailer** older readers never reach:

```
uint32 'SYMT' (0x53594d54)
int32  version (2)
int32  settingsCount,  [uint32 len + blob] *   (deduped by value)
int32  groupCount,     [uint32 settingsIndex] *   (group id = index + 1)
int32  mirrorCount,    [Guid + uint32 len + blob] *
                       [uint32 mirrorIndex] *      (one per group)
```

Version 1 (no mirrors) still loads. Group ids are file-local, so an additive load cannot
collide. **Multiplayer uses the same binary stream** (`MultiplayerStrokeSerialization`),
which is why the table lives here and not in the metadata JSON.

---

## What is wired, and where

| Edit | Hook |
|---|---|
| Eraser | `SketchMemoryScript.MemorizeDeleteSelection` |
| Delete selection | `DeleteSelectionCommand` ctor → child `DeleteStrokeCommand`s |
| Transform (+ `strokes.move/rotate/scale` API) | `TransformItemsCommand` ctor + `TransformItems.TransformEach` |
| Repaint / recolor / rebrush / resize | `SketchMemoryScript.RepaintSelected` and `MemorizeStrokeRepaint` |
| Tint | `TintColorTool.HandleIntersectionWithBatchedStroke` |
| Reshape (all sub-tools) | `ReshapeTool.ApplyStrokeModification` |
| Selection grab | `SymmetryPeerPreview` (live) + `SelectCommand` deselect bake |
| Mirror move | `SymmetryWidget.OnUserBegin/Update/EndInteracting` → `SymmetryMirrorMove` |

Two non-obvious ones:

- **The deselect is where a selection move becomes real.** Strokes ride the selection
  canvas and are only rewritten on deselect, so `SelectCommand` (deselect) is the
  undoable moment, not `MoveWidgetCommand`. `SelectionTransform` is already in canvas
  space — no conversion needed. `SelectionManager` records the transform each stroke
  joined the selection under, so a stroke added to an already-moved selection mirrors only
  what happened after it joined.
- **A mirror move is applied live and *recorded* afterwards.** `SketchMemoryScript.RecordCommand`
  puts a command on the stack without executing it, which is what "already applied" needs.

---

## Performance

Per-frame geometry moves are cheap if done in place. `GeometryPool.ApplyTransform` already
transforms a vertex range, so `Batch.TransformSubset` moves a stroke where it sits: a pass
over its own vertices plus a mesh update the batch would do anyway. No regeneration, no
re-batching, no allocation.

Cost scales with **batches touched**, not strokes touched — one dirty stroke dirties its
whole batch, and strokes drawn together share batches, which is exactly what symmetry
produces. The expensive paths to avoid per frame are `Uncreate`/`Recreate` (regenerates
from control points) and reparenting between canvases (copies subsets).

This was measured only qualitatively: the owner confirmed dragging a mirror "works great".
Nobody has profiled it on a sketch with thousands of mirrored strokes.

---

## Verification status

| | |
|---|---|
| Confirmed in the editor | Mirror move following strokes live |
| Fixed, needs retest | Tint after a move scattering peers (`2472efa`) |
| Never exercised | Everything else — delete, transforms, repaint, tint, reshape, selection preview, save/load round-trip, undo/redo |

First thing worth doing: a compile, then a two-way mirror with one drawn stroke — move the
widget, confirm the drawn stroke holds still and only the reflection moves. If that's
right, the core maths is right.

---

## Traps found the hard way

- **Geometry-only preview.** Moving a peer's geometry without its control points looked
  tidier (data never lies about an uncommitted move) but broke as soon as another tool
  rebuilt the stroke: it snapped back, and the preview still owed it an inverse transform
  that would have displaced it permanently. Rule 5 exists because of this.
- **Settings as identity.** Linking strokes to a snapshot of the settings dissolves the
  link exactly when it matters — move the mirror and the values no longer match. Hence
  `SymmetryMirror`.
- **Sweeping a tool across a group.** Repaint and tint queue both a direct edit and a peer
  edit for the same stroke in one frame; the later one won arbitrarily. Both now track
  the strokes already handled in the batch, and a direct edit wins.
- **Erased peers.** Repaint/tint/reshape recreate geometry and transforms reparent — both
  resurrect an erased stroke. Filtered centrally in `PeersOf`.

---

## Design decisions

Answered by the project owner on 2026-10-01; these replace the open questions that stood here.

**Implementation status.** First pass done (not yet compiled): explicit creation, plain as
"no active mirror", peers from ownership alone (no active-mirror or mode gating), undoable
activation, settings changes moving or regenerating a linked mirror's strokes, and Bring to
User / Reset to Home carrying them. A linked mirror can't spin or drift: released, it stays
where it was let go (tossing it away to hide it still works), and `Spin` is ignored. Spinning
would need strokes following a mirror in continuous motion, with no point to record a move. Still to do: the mirror list UI (with its "Plain" entry)
and activating a mirror from a stroke in VR — both need prefab work, and are API-only
for now; the rest of the canonical-stroke model (below); per-pointer colours when
regenerating (copies take the source stroke's colour). Sections above that describe the
global toggle or settings-based forking are out of date.

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
- transform and the selection bake: the first moved copy drives (after any layer move);
- snip (copies derived first so one index cuts them all), crop (the source is cropped, copies
  take its pieces), join (pairwise, then derived);
- mirror moves and settings changes move strokes in place, falling back to derivation from a
  visible member moved with its own pointer.

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
something outside these paths splits a group across canvases. Not started: copies as read-only derived strokes with edits redirected to the canonical
stroke, instance data in the file trailer (decisions 7-9).

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
   on resumes the same linked mirror.

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

**Assumed, not yet asked:** the canonical stroke is the copy at pointer index 0 (the one the
user drew); a duplicated or regenerated group's canonical stroke is its index-0 copy.

### Consequences agreed in discussion

- Peer edits use the owning mirror's own current state, so they work whether or not that
  mirror is active or in the current symmetry mode.
- While a linked mirror is active, every widget move is a mirror move (including Bring to User,
  snap home and spin). A plain mirror's widget moves freely.
- Creating a mirror, switching the active mirror and moving a mirror are undoable commands;
  undo restores recorded state rather than inferring it.
- Old sketches load as plain. The saved mirror record gains a "linked" flag.
- Revert d8b7fab "Reactivate the matching symmetry mirror after undo and redo"
  (settings matching and tolerance), which this design supersedes.

---

## Next steps, in order

1. **Compile, and retest tint after a move.** Nothing else matters until the branch builds.
2. **Count-preserving settings changes should move strokes** (wallpaper scale/skew).
   Same `Begin`/`Update`/`End` shape as `SymmetryMirrorMove`, hooked at
   `PointerManager.CalculateMirrors` (already the settings-changed choke point, already
   calls into `SymmetryMirrors`). Needs a drag begin/end signal from the panel, or command
   merging, so a slider doesn't emit a command per frame.
3. **VR UI for new/recall mirror.** Only API commands exist. Without a control binding the
   fork trap above is unavoidable in normal use. Requires prefab/scene work.
4. **Undo pairing for mirror moves.** `MoveWidgetCommand.Merge` adopts
   `MoveMirrorStrokesCommand` as a child, but refuses all merges once `m_Final`, which is
   usually the case on release — so undo can take two steps.
5. **Smaller gaps:** `BringToUser` and command-driven `ResetToHome` don't carry strokes
   (only a real grab does); `m_SymmetryTransformEach`/`m_SymmetryTransformEachAfter` are
   not recorded in the snapshot, so API-set per-copy transforms won't reproduce;
   `TransformGeometryInPlace` only handles batched strokes, so unbatched brushes don't
   follow live (they still follow the bake).
