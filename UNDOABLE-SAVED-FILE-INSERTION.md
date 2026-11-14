# Future widget-capable saved-file insertion

## Purpose and current decision

The immediate agreed fix is to exclude widgets and their metadata when saving selected strokes. See SAVED-STROKES-WIDGET-METADATA.md in this directory. General additive loading must continue to support widgets.

The user confirmed there are no existing saved-stroke files containing widgets that need compatibility handling. This document describes a proposed broader future fix, not authorization to implement it now. If saved strokes later allow widgets, save-side filtering alone would no longer protect insertion Undo.

## Problem

PR #1175 review point 2: https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4411026109

LoadSavedStrokesCommand loads through SaveLoadScript.Load with bAdditive enabled. It owns only the returned strokes: placement, grouping, selection, visibility on Undo/Redo, and destruction on disposal. Widget-capable additive loading can create additional objects that the insertion command does not own. If its input includes widgets, Undo leaves those objects behind, and later insertions can create additional copies.

Do not conflate this command with general additive loading. The inspected general path, SketchControlsScript.MergeBrushStrokes, loads directly without recording an undoable import command; that behavior already existed on main. Adding widget support there does not break an existing import-Undo transaction. Making general imports undoable would be a separate behavior change.

## Proposed broader design

1. Return an explicit import result from the loader, describing the strokes and widgets created by that import. Track object ownership directly rather than identifying new objects by comparing global scene collections.
2. Give the saved-file insertion command ownership of that result, including pending asynchronous widget creation. Capture only imported objects; preserve pre-existing scene objects.
3. Apply insertion placement and selection to both strokes and widgets while preserving their relative layout, authored layers/groups as required by insertion semantics, and symmetry links. Audit the existing stroke-centroid placement and grouping rules before extending them to mixed content.
4. Undo hides or deactivates all imported objects and restores the previous selection. No pending work may subsequently introduce visible imported objects after Undo.
5. Redo restores the same imported objects without re-reading the file or creating duplicate widgets. If asynchronous work must resume, keep it under the same import ownership and visibility state.
6. Dispose resources according to command/history ownership. Retained objects needed for Redo must remain usable; discarded insertion objects and pending work must not leak. Follow existing widget command lifecycle patterns after inspecting them.
7. Keep the loader separate from undo-history recording. Other additive-loading callers may consume the import result without changing their current behavior. They could opt into a reusable undoable import command later through a separately agreed change.

## Asynchronous creation is the main complication

Strokes are returned synchronously, but some widgets are created through media-loading coroutines and asynchronous asset loads. An insertion must remain responsible for those pending operations as well as the widgets already instantiated.

The exact mechanism needs investigation before implementation. Options include completing the import before exposing the finished insertion to normal interaction, or maintaining explicit pending/visible/undone/disposed states while loading continues. The choice must account for current loading overlays, user interaction, coroutine ownership, failures and shared asset loading. Do not assume that canceling an insertion permits canceling shared catalog requests.

In either approach, Undo during loading must not allow late widgets to appear; Redo must avoid duplicates; and disposing a command must not destroy objects still owned by the live sketch or another command.

## Scope and complexity

This is moderately complex and substantially broader than filtering widget metadata out of saved-stroke snapshots. It crosses loader results, asynchronous creation, insertion placement/selection, and command lifecycle handling. No implementation estimate or tested design is claimed.

Keep the immediate save-side fix focused. Defer widget-capable insertion until that behavior is wanted, then inspect existing widget creation/deletion commands and loading ownership before finalizing this design.

## Focused acceptance scenarios for future implementation

1. Insert mixed strokes/widgets, then Undo and Redo: all imported content follows the command, relative layout is preserved, and the previous selection is restored appropriately.
2. Undo before asynchronous widgets finish: no imported widget appears afterward. Redo produces one copy of each item.
3. Exercise widget-only and stroke-only inputs, partial asset-load failures, repeated Undo/Redo, and command disposal while an import is undone or still loading.
4. Confirm general additive loading retains widget support and existing behavior unless separately changed.

These are proposed checks, not executed validation. Findings so far are based on source inspection; no Unity runtime validation was performed.

This is a local future-work document. Leave it untracked unless explicitly asked to version it.
