# Main-layer visibility and selection follow-up

Local handoff only. Do not commit this document.

## Decision

The maintainer allows Main to be user-hidden. Hiding Main is rare, so defer a consistent visibility fix until after the symmetry PR is merged. The maintainer will perform the merge. Do not merge PR #1175 or begin the broader fix without their instruction.

Revert snapshot-only commit 7593caa1afb6afdfdff4817e1f0f1d7b160ae355 on feature/symmetry-stroke-linking. Keep the linked-mirror Undo performance and slider-grouping changes. The parent worktree is named open-brush-main but was on fix/export-brush-names when this note was written; do not switch it or disturb its local changes automatically.

## Confirmed behavior

- SelectionManager.RemoveFromSelection(bool) calls MainCanvas.gameObject.SetActive(!m_RemoveFromSelection). Entering subtraction mode hides Main; exiting forces it visible. It does not remember whether the user had already hidden Main.
- SelectionTool.EnableTool(false) calls EndSelection(), then unconditionally activates Main. This happens even when the tool was in ordinary selection mode. EnableTool(true) also calls RemoveFromSelection(false), which activates Main.
- SelectionManager.ClearActiveSelection() also activates Main when clearing a selection. SelectCommand's cleared-selection handling calls RemoveFromSelection(false).
- Main is user-hideable in the Layers UI and through API/Lua. LayerUI_Manager hides the main layer's squash/options controls, but not its visibility toggle. SceneScript.HideLayer and ToggleLayerVisibility do not exclude Main. Layer index 0 is Main.
- SceneScript.LayerCanvasesSerialized() saves activeSelf as layer visibility. There is currently no distinction between deliberate layer hiding and temporary tool hiding.
- These selection behaviors exist on remote main 04e6f4833302c3080fa2fd3a49dd48cd9ebbb68d, checked during this investigation. The old selection code originates in the 2021 codebase and assumes a main-canvas/selection-canvas split; other layers are not hidden in subtraction mode.

## PR-specific snapshot issue

PR https://github.com/icosa-foundation/open-brush/pull/1175 adds temporary deselection to normal snapshot capture to bake linked geometry and then undo that temporary operation. Main's normal snapshot path did not clear selection. This introduces another way saving can reveal Main and serialize the changed visibility.

Review: https://github.com/icosa-foundation/open-brush/pull/1175#discussion_r4565044452

Commit 7593caa1a preserved Main's active state and selection removal mode around TimeslicedConstructor. It restored visibility immediately after deselection, before metadata capture, then restored tool mode/visibility after undoing deselection. Compilation and an isolated iterator harness passed, but no live camera transition had been tested.

## User's actual test and why that patch was insufficient

The user selected a stroke on another layer, hid Main, then opened Save. Main became visible as soon as the save camera appeared and stayed visible. This occurs BEFORE snapshot capture: switching away from the selection tool calls EnableTool(false), which reveals Main. The snapshot-only patch consequently preserves an already-changed state. The earlier test instruction promising selection would remain selected across opening the camera was incorrect; the tool transition explicitly ends selection.

The old GlobalCommands.Save path also explicitly calls ClearActiveSelection before saving. SaveGallery enables SaveIconTool. Audit save entry points and camera cancellation/return as well as snapshot capture; do not assume snapshot-only coverage is end-to-end coverage.

## Proposed follow-up direction (not implemented)

Make layer visibility user-owned. Consider removing automatic main-canvas hiding/showing from selection mode changes, selection clearing, tool enable/disable, and snapshot capture. Subtraction already targets SelectionCanvas, so hiding Main is not needed to restrict which strokes are deselected.

If selection isolation is retained as a deliberate feature, it needs separate transient display state, must respect all layers, and must not overwrite saved visibility. Do not infer the user's original intent from activeSelf after the old tool code has already overwritten it.

Review the desired subtraction-mode presentation with the maintainer before changing it. Keep the main-layer hiding capability. Do not add a growing set of save-specific exceptions without addressing visibility ownership.

## Focused validation for the eventual fix

1. Select on another layer, hide Main, open the save camera: Main stays hidden through capture, cancellation and tool return; saved/reopened visibility is correct.
2. Enter/exit subtraction mode and switch tools with Main deliberately visible and deliberately hidden: user visibility is preserved, and only selected objects can be removed from selection.
3. Snapshot capture with a selection preserves the intended visibility and selection state on success, failure and iterator disposal, without altering Undo history.

When Main is visible in ordinary selection mode, the reported visibility bug has no effect: the old code merely activates an already-active canvas. Temporary subtraction-mode hiding remains a distinct old behavior.
