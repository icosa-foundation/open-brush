# Saved strokes must exclude widget metadata

## Problem

Saving selected strokes uses the general sketch metadata builder. That builder includes widget metadata such as models, images, guides, text, audio, video and camera paths. Saved strokes should contain strokes, not widgets. This save-side problem also exists on main; it is not specific to the symmetry-stroke-linking branch.

Relevant code: Assets/Scripts/Save/SaveLoadScript.cs (SaveSelected), Assets/Scripts/Save/SketchSnapshot.cs (TimeslicedConstructor and GetSketchMetadata), and Assets/Scripts/Save/MetadataUtils.cs.

## Agreed fix

1. Exclude all widget metadata when creating a selected-strokes snapshot (selectedOnly). Apply this consistently across widget types and associated widget-link metadata where present.
2. Preserve widget metadata in normal full-sketch saves.
3. Preserve widget support in general additive loading. Do not disable widget loading globally or add widget ownership to LoadSavedStrokesCommand as part of this fix.
4. The user confirmed there are no existing saved-stroke files containing widgets that require compatibility handling. Do not add migration or defensive loading behavior for that hypothetical case.

## Relationship to PR #1175, review point 2

Review: https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4411026109

The symmetry branch added widget loading to additive imports. LoadSavedStrokesCommand still tracks only strokes, so an input containing widgets would leave them behind on Undo. Main previously skipped widgets in additive loading, so that observable Undo failure is branch-specific, but the agreed correction is at the save boundary: saved-stroke files must not contain widgets. Under the confirmed absence of existing affected files, there is no separate agreed insertion-command fix.

The save-side correction needs to reach both main and the symmetry branch. Keep it in a focused commit so it can be applied independently. No implementation or commit has been made as part of writing this note.

## Suggested verification

1. Save selected strokes from a sketch containing widgets, including a mixed selection, and confirm the saved file has no widget metadata.
2. Confirm normal sketch saving still includes widgets.
3. Confirm general additive loading still imports widgets.

This is a local problem/fix note. Leave it untracked unless explicitly asked to version it.
