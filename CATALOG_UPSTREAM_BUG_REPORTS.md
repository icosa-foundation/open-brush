# Non-SAF reference catalog bug report drafts

## Scope and evidence

1. Audited upstream baseline: `1d8c12df695d1ec230f2544c3ffdadb5b744b936`. These findings are not a claim about a later upstream revision. Source paths below refer to that baseline unless explicitly described as a SAF-only comparison.
2. These are drafts for upstream coordination; no issues have been submitted. Fixes are prepared locally on the SAF branch, except the model identity decision in U7.
3. "Isolated" means production methods or expressions executed with temporary filesystem fixtures and stubs for Unity/provider collaborators. It does not mean the full application was exercised. Native watcher tests do use actual OS file/directory rename notifications.
4. Full Unity widget restoration, native video/audio playback, Android provider behavior and rendered UI reproduction remain release validation. Reproduction instructions below are proposed application-level checks, not claims that every step has already been performed in the UI.
5. Progress and commit details are tracked in [CATALOG_PARITY_PLAN.md](CATALOG_PARITY_PLAN.md).

## U1 — Saved video lookup depends on the active browser folder (P1)

1. Reproduce: import videos from two sibling folders, save a sketch containing both, then reload while the video panel is at its root or an unrelated folder.
2. Expected: both references resolve by their saved paths. Actual source behavior: `VideoCatalog.GetVideoByPersistentPath` searches only the active `m_Videos` list; `VideoWidget.FromTiltVideo` substitutes a missing/dummy reference when lookup fails.
3. Sources: `Assets/Scripts/VideoCatalog.cs`, `Assets/Scripts/Widgets/VideoWidget.cs`.
4. Status: independent saved-path lookup implemented. Isolated lookup cases pass; playback/widget reload still needs integration reproduction.

## U2 — Video/sound updates bypass discovery filters and duplicate entries (P2)

1. Reproduce: browse a parent folder, modify a supported file in a nested folder and an unsupported file. Also create a supported direct-child file that produces both Created and Changed notifications.
2. Expected: only supported direct children appear, once each. Actual source behavior: recursive watcher Changed paths are concatenated directly into the scan list, outside the initial folder/extension filter and without deduplication. Changed followed by Deleted can also reintroduce a missing file.
3. Sources: `Assets/Scripts/VideoCatalog.cs`, `Assets/Scripts/SoundClipCatalog.cs`, `Assets/Scripts/FileWatcher.cs`.
4. Status: filtering, detected-file intersection and deduplication implemented. Isolated production local scans verify new/changed/deleted behavior. Native media decoding is not part of those checks.

## U3 — Saved-stroke folder navigation has no nested index (P2)

1. Reproduce: place valid saved strokes in the root and a nested folder; select the nested folder in the saved-stroke reference panel.
2. Expected: direct sketches in the selected folder appear. Actual source behavior: `FileSketchSet.ProcessDirectory` indexes only the library root; the reference catalog cannot retrieve the nested sketches. Its prefix filter also lacks a directory boundary.
3. Sources: `Assets/Scripts/Save/FileSketchSet.cs`, `Assets/Scripts/SavedStrokesCatalog.cs`, `Assets/Scripts/GUI/ReferencePanel.cs`.
4. Status: saved-stroke-only recursive indexing, direct-child panel filtering and index refresh after nested changes implemented. Ordinary User Sketchbook remains root-only. `.tilt` directories are treated as sketch containers, not navigation folders. Directory links are not recursively followed.
5. Evidence: isolated tree/container and nested move/deletion refresh cases pass. Valid-sketch UI import is still pending.

## U4 — Navigation abandons obsolete file watchers (P2)

1. Reproduce: repeatedly navigate between image/background/video folders, then modify an old folder. Observe watcher handles/callbacks and catalog rescan activity.
2. Expected: each catalog owns only its current watcher(s), and retired callbacks are ignored. Actual source behavior: new watcher assignments replace references without disposing/unsubscribing old watchers. Some destruction paths disable events without disposing the watcher.
3. Sources: `Assets/Scripts/ReferenceImageCatalog.cs`, `Assets/Scripts/BackgroundImageCatalog.cs`, `Assets/Scripts/VideoCatalog.cs`. The audit also found incomplete model destruction cleanup and saved-stroke watcher replacement cleanup.
4. Status: owned watcher cleanup and stale-callback guards implemented. Image/background NUnit tests include native watcher disposal, but require execution inside Unity; whole-app handle measurements remain pending.

## U5 — Video extension matching is case-sensitive (P2)

1. Reproduce: compare a supported lowercase video extension with the same file renamed to uppercase; force a clean scan. Repeat for `.txt` versus `.TXT` network pointers.
2. Expected: matching depends on the file format, not extension casing. Actual source behavior: configured lowercase video extensions are compared case-sensitively, as is network-pointer recognition.
3. Sources: `Assets/Scripts/VideoCatalog.cs`, `Assets/Scripts/ReferenceVideo.cs`.
4. Status: discovery, change processing and network-pointer recognition use case-insensitive format matching. Isolated cases pass; network playback remains integration validation.

## U6 — Local media scans can remain stuck after folder errors (P2)

1. Reproduce: browse a video/sound folder, remove it or revoke access, then request a rescan. Restore access and try scanning again.
2. Expected: scanning state is released so retry remains possible. Actual source behavior: `Directory.GetFiles` can throw after the scanning flag is set, bypassing the normal reset. Video scheduling is gated by that flag.
3. Sources: `Assets/Scripts/VideoCatalog.cs`, `Assets/Scripts/SoundClipCatalog.cs`.
4. Status: finally-based cleanup and diagnostics implemented. Isolated production scans confirm missing-folder cleanup and correct ownership when a delayed scan is superseded.
5. Deferred separately: whether an error should retain or clear the last successful displayed list, and how that is presented to users.

## U7 — Blocks and Models references collide by relative path (P1/P2)

1. Reproduce: put different models at the same relative path under Models and Blocks/OfflineModels, then browse, import and save/reload both.
2. Expected: each reference preserves its source. Actual source behavior: catalog keys, model location equality and persistent grouping omit source identity. Catalog root bookkeeping can disagree with the first model retained under a shared key; legacy absolute-path resolution prefers Blocks.
3. Sources: `Assets/Scripts/ModelCatalog.cs`, `Assets/Scripts/Model.cs`, `Assets/Scripts/WidgetManager.cs`, metadata grouping/serialization code.
4. Status: source-confirmed; implementation deliberately deferred. New references need explicit source identity, and ambiguous old files need an agreed compatibility fallback. Do not resolve this by silently changing which root wins.

## U8 — Restoring an image mutates another folder's panel list (P2)

1. Reproduce: browse image folder A, then load a sketch referencing an image in B that is not already listed in A.
2. Expected: restore the reference without changing A's displayed contents. Actual source behavior: `RelativePathToImage` appends the fallback image to `m_Images`, which is also the active panel list.
3. Source: `Assets/Scripts/ReferenceImageCatalog.cs`.
4. Status: fallback no longer enters the active list; existing listed references are still reused. Isolated production lookup cases verify list isolation, reuse and containment. Panel refresh timing remains integration validation.

## U9 — Missing-model recovery drops state or repeats incorrect recovery (P1/P2)

1. Reproduce: load a sketch with a missing normalized model reference, restore its file, then rescan repeatedly. Also test raw transforms, multiple widgets and saved subtree/pin/group/layer/split settings; force an import failure before retrying.
2. Expected: recover once with original metadata, removing pending state only after success. Actual source behavior: normalized recovery removes from the raw dictionary; raw transforms are passed in the normalized argument position; metadata is omitted; pending entries are removed before asynchronous creation succeeds.
3. Sources: `Assets/Scripts/ModelCatalog.cs`, `Assets/Scripts/Widgets/ModelWidget.cs`.
4. Status: full pending metadata, success-only cleanup, duplicate/retry gating and asynchronous restoration implemented. Gate/lookup/import-sharing tests pass in isolation. Actual widget geometry and transforms still need Unity reproduction.

## U10 — New Sketch does not invalidate pending model restoration (P1/P2)

1. Reproduce: start a delayed model restore or load a sketch containing a missing model, then clear the scene before restoration completes. Restore the file or allow the import to finish.
2. Expected: the cleared scene stays clear and does not retain old missing references. Actual source behavior: missing-model clearing occurs in the load path, not the general widget/scene clear path; delayed work has no scene-generation guard.
3. Sources: `Assets/Scripts/WidgetManager.cs`, `Assets/Scripts/SketchControlsScript.cs`, `Assets/Scripts/Save/SaveLoadScript.cs`, model restoration methods.
4. Status: scene resets now invalidate pending model work. Generation tests pass in isolation; interactive clear-during-import reproduction remains pending.

## U11 — Native renames are not forwarded to catalogs (P2)

1. Reproduce: rename a watched media file or an ordinary folder containing media without modifying its contents.
2. Expected: remove the old path and discover the new one. Actual source behavior: `FileWatcher` forwards Created/Changed/Deleted but not Renamed; LastWrite-only filters in several catalogs further omit name-change notification coverage.
3. Sources: `Assets/Scripts/FileWatcher.cs` and catalog watcher initialization.
4. Status: Renamed forwards old-path deletion and new-path creation; relevant catalog filters include names. Actual OS file and directory rename tests pass.

## U12 — Batched and concurrent change notifications can be lost (P2)

1. Reproduce: modify two images/models before the next catalog update, or modify one then create/delete another. For video/sound, issue changes while the main thread consumes a previous batch.
2. Expected: every affected entry is invalidated. Actual source behavior: image/model catalogs retain only one changed filename and overwrite/reset it on subsequent events. Video/sound lock a pending set while also replacing that set, allowing producers/consumers to lock different objects. Model scans additionally clear dirty state at the end, erasing new notifications received during the scan.
3. Sources: `Assets/Scripts/ReferenceImageCatalog.cs`, `Assets/Scripts/ModelCatalog.cs`, `Assets/Scripts/VideoCatalog.cs`, `Assets/Scripts/SoundClipCatalog.cs`.
4. Status: stable synchronized batching and corrected dirty-state timing implemented. Multiple-change and concurrent producer/drain tests pass; native application stress reproduction remains pending.

## U13 — Catalog thumbnail lifetime is coupled incorrectly to video playback (P2)

1. Reproduce locally: open videos and repeatedly navigate/remove their catalog entries; measure whether retired thumbnail textures are released while placed widgets continue playing.
2. Expected: catalog thumbnails are released independently of widget playback. Local source behavior: removed/navigated entries are abandoned without thumbnail cleanup.
3. Source: `Assets/Scripts/VideoCatalog.cs`, `Assets/Scripts/ReferenceVideo.cs`.
4. Separate SAF comparison: the branch's SAF retirement path called `Dispose`, which also stopped active playback controllers. This part is a SAF regression, not an upstream non-SAF finding.
5. Status: thumbnail release is now separate from playback disposal in both paths. A Unity ownership regression has been added and compiled; native playback and texture measurements remain pending.

## Additional integration checks before filing

1. Include Unity/platform versions and confirm each scenario against the intended upstream revision; attach logs/screenshots only after reproducing the UI consequence.
2. Prioritize U1, U7, U9 and U10 because they can change or lose restored scene content.
3. U2, U4, U6, U11 and U12 have overlapping watcher/scan symptoms; keep the underlying fixes and issue scopes distinct.
4. Do not label stubbed media initialization or production-method checks as Android provider or full Unity tests.
