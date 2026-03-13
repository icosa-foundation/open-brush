# Quill/IMM Chapter Import Support Plan

## Objective
Add chapter-aware importing for files loaded through the Quill library flow, with clear behavior for both:
- `.imm` files (native chapter support exists in `ImmStrokeReader`)
- Quill project folders (`Quill.json` + `Quill.qbin`) where chapter representation is not currently explicit in Open Brush code

## Current State (Codebase Findings)
- Import entry point is `Quill.Load(...)` in `Assets/Scripts/Quill.cs`.
- Source detection is path-based:
  - Directory => `SQ.QuillSequenceReader.Read(path)` (Quill project)
  - File => `ImmStrokeReader.SharpQuillCompat.ReadImmAsSequence(path)` (IMM adapter)
- There is no chapter parameter in:
  - `Quill.Load(...)`
  - `ApiMethods.LoadQuill(...)` (`Assets/Scripts/API/ApiMethods.SharpQuill.cs`)
  - UI command state (`Quill.PendingLoadPath` is only a string)
- IMM package already exposes chapter APIs:
  - `StrokeReader_GetChapterCount`
  - `StrokeReader_GetCurrentChapter`
  - `StrokeReader_SetChapter`
  - Wrapper: `StrokeReaderDocument.Load(..., chapterIndex = -1)` and `SetChapter(...)`
- `SharpQuillCompat.ReadImmAsSequence(...)` currently does not accept/select chapter index.

## Scope Decisions
1. Phase 1: ship chapter selection for IMM imports.
2. Phase 2: define and implement Quill chapter semantics after validating real Quill files that contain chapters.

Reason: IMM chapter control is concrete and available now; Quill chapter representation is ambiguous in current parser/importer usage.

## Phase 1: IMM Chapter Selection End-to-End

### 1) Import options plumbing
- Replace single-path pending load state with a small settings object:
  - Path
  - Source type (optional/derived)
  - Chapter mode (`Default`, `SpecificIndex`, optionally `AllChapters`)
  - Chapter index (if specific)
- Update command handoff in:
  - `Assets/Scripts/GUI/QuillFileButton.cs`
  - `Assets/Scripts/SketchControlsScript.cs` (`LoadQuillConfirmUnsaved`, `LoadQuillFile`, coroutine path)

### 2) Quill API surface
- Extend signatures with optional chapter index:
  - `Quill.Load(..., int? chapterIndex = null, ...)` in `Assets/Scripts/Quill.cs`
  - `ApiMethods.LoadQuill(..., int chapterIndex = -1, ...)` in `Assets/Scripts/API/ApiMethods.SharpQuill.cs`
- Preserve backward compatibility:
  - Omitted chapter index keeps current behavior.

### 3) IMM adapter changes
- Extend `ReadImmAsSequence` to accept optional chapter index:
  - `Library/PackageCache/com.immersive-foundation.imm-stroke-reader@.../Runtime/SharpQuillCompat.cs`
- In adapter load path:
  - Query chapter count.
  - Clamp/validate requested chapter.
  - Call `StrokeReader_SetChapter` before layer/drawing extraction.
- Add explicit logging for invalid index fallback behavior.

### 4) UI behavior (IMM source)
- In Quill Library IMM tab:
  - Add chapter picker control (at minimum index-based).
  - Show chapter count (lazy load metadata when selecting a file).
- Keep Quill-project UI unchanged initially.
- Files:
  - `Assets/Scripts/GUI/QuillLibraryPanel.cs`
  - `Assets/Scripts/QuillFileInfo.cs`
  - `Assets/Scripts/QuillFileCatalog.cs`
  - prefab updates likely needed in `Assets/Prefabs/Panels/QuillLibraryPanel.prefab`

### 5) Metadata strategy for chapter count
- Avoid parsing every IMM during directory scan.
- Recommended:
  - Keep catalog scan cheap.
  - Resolve chapter count on demand when file is focused/selected.
  - Cache `{path, lastWriteTimeUtc, chapterCount}` to avoid repeated native loads.

### 6) Tests and validation for IMM
- Add/edit tests to cover:
  - Default import unchanged when no chapter specified.
  - Specified valid chapter index routes through adapter and changes imported output.
  - Invalid chapter index fallback behavior.
- Manual QA checklist:
  - Merge import with chapter N.
  - Load (clear scene) with chapter N.
  - Undo/redo integrity via `LoadQuillCommand`.

## Phase 2: Quill Chapter Support (Discovery then Implementation)

### 1) Discovery spike (required)
- Collect representative Quill project samples containing chapters.
- Inspect `Quill.json` structures and map chapter concept to parser output:
  - Layer hierarchy
  - animation timeline/keyframes
  - any chapter-like markers/metadata
- Confirm whether chapter is:
  - explicit metadata, or
  - emergent via animated groups/visibility over time.

### 2) Define chapter semantics for Open Brush
- Decide one behavior and document it:
  - Import chapter as “state at chapter time/index”
  - Import chapter as dedicated top-level layer group
  - Import all chapters as separate layer groups
- Ensure behavior is consistent between Quill and IMM where possible.

### 3) Implement Quill path
- Add chapter-aware evaluation in `Assets/Scripts/Quill.cs`:
  - likely requires animation-key evaluation (visibility/opacity/offset/transform) at selected chapter time/index
  - current importer mostly ignores animation (`loadAnimations=false` path), so this is non-trivial.
- If needed, extend SharpQuill parser usage (or package) for easier chapter extraction.

### 4) UI/API convergence
- Reuse same chapter picker for Quill projects once semantics are finalized.
- Keep same API shape (`chapterIndex`) across IMM and Quill to avoid fragmented integrations.

## Risks and Unknowns
- Quill chapter semantics are not currently explicit in the Open Brush importer path.
- IMM chapter index likely numeric only (no chapter names exposed by current managed API).
- Loading IMM repeatedly for metadata could cause UI hitching without async/caching.
- Prefab/UI changes increase merge risk; isolate to minimal controls in first pass.

## Recommended Delivery Order
1. IMM-only chapter plumbing + adapter + API + minimal UI picker.
2. IMM QA with known multi-chapter files.
3. Quill chapter discovery spike with sample assets and documented semantics.
4. Quill chapter implementation using same UI/API contract.

## Definition of Done
- User can select a chapter when importing IMM from Quill Library.
- Selected chapter is actually reflected in imported layers/strokes.
- Existing import flow remains unchanged when chapter is not selected.
- Quill chapter support has a documented, tested behavior (after discovery phase), not an inferred heuristic.
