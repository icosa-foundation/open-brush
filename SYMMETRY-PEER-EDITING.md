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

Off by default. The *linking data* is always recorded; only the *editing behaviour* is
opt-in, so enabling it later still works on strokes drawn earlier.

- `Open Brush.cfg`: `"Flags": { "SymmetryPeerEditing": true }`
- At runtime: `http://localhost:40074/api/v1?symmetry.peerediting=true`
- In code: `SymmetryPeerEditing.Enabled`

Related commands: `symmetry.mirror.new` (fork a mirror), `symmetry.mirror.recall=<n>`
(bring an earlier one back, oldest first).

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

**`SymmetryMirror`** — the thing the widget stands for, with a `Guid` that outlives any
settings it happens to have. `SymmetryMirrors` is the registry: `Active` (what the widget
shows, what new strokes link to), `All` (oldest first, kept forever — no deletion),
`Create` (fork), `Recall` (make active + restore its settings, moves nothing),
`NoteSettingsChanged` (called from `PointerManager.CalculateMirrors`).

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

## Open design questions (for the project owner, not to be guessed at)

1. **When does a mirror fork?** Today: explicit `symmetry.mirror.new`, plus one created
   lazily on the first symmetric stroke. Everything else edits the active mirror. The trap:
   draw an object, turn symmetry off, turn it on elsewhere, move the widget — and the first
   object is dragged along. Candidates discussed: fork when symmetry is turned off and back
   on (one session = one mirror); fork on a structural settings change; or a per-move
   modifier that moves the widget without dragging strokes. Owner leaned toward "immediate,
   but open to debate".
2. **Count-changing settings** — regeneration is a separate feature with its own question:
   what happens to copies the user has since edited by hand?

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
