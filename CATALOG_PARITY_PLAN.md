# Reference catalog parity plan

## Scope and working rules

1. Track parity between local/non-SAF and SAF discovery, navigation, displayed identities, saved-reference resolution, and updates. Do not reproduce known local bugs merely to claim parity.
2. Keep independent fixes in separate commits. Record implementation and validation separately; isolated checks are not Unity/device certification.
3. Do not push or file upstream issues without explicit permission. After an authorized push, resolve only review threads actually addressed.
4. Preserve unrelated working-tree changes. Reuse an Editor if integration testing requires one; do not launch Unity for checks that narrower tools can establish.
5. Baseline audit compared branch `d61fc4c13` with upstream main `1d8c12df695d1ec230f2544c3ffdadb5b744b936`. Detailed machine-local evidence and report drafts are in `Logs/pr1120-catalog-parity-audit.md`; isolated checks are in `Logs/pr1120-catalog-audit-checks.ps1`. Logs are not durable tracked deliverables; summarize necessary results here.

## Shared contract

1. Ordinary folder pages display supported direct children. Recursive indexing may be used internally. Blocks' deliberately flat view and project containers are explicit exceptions.
2. Saved references resolve independently of the active browser folder, using validated logical paths and unambiguous source identities.
3. First scans, change notifications and rescans apply the same filtering policy. Stale async results cannot replace the current directory's state. Errors must clear scanning state.

## Prioritized actions

| ID | Priority | Action | Acceptance criteria | Status |
|---|---|---|---|---|
| 1 | P1 | Finish Blocks path conversion fix | Both WidgetManager helpers derive paths from the actual Blocks root, not UserPath; local, Google Play and Lepton cases work; sibling-prefix/traversal paths rejected | Implemented; isolated checks passed; Unity/device validation pending |
| 2 | P1 | Restore videos independently of browser state | Saved videos in two sibling folders reload from any active folder; local and SAF fallbacks validate paths and preserve missing-file behavior | Implemented; 26 isolated checks passed; Unity/device reload validation pending |
| 3 | P1 | Eliminate Blocks/Models identity collisions | Equal relative paths under different roots stay distinct and reload the intended file; compatibility policy for old references tested | Investigation complete; confirm ambiguous legacy-reference fallback before serialization changes |
| 4 | P1 | Verify/fix asynchronous SAF model restoration | Delayed cold-start indexing and restored missing files resolve pending widgets without requiring folder navigation | Pending |
| 5 | P2 | Share supported model-format policy | Local/SAF format sets match, including .spz/.sog and conditional USD/FBX formats | Implemented; all four isolated flag combinations passed; Unity validation pending |
| 6 | P2 | Repair saved-stroke folder navigation | Discovery follows selected folders in both modes; .tilt containers are not mistaken for ordinary folders; local defect documented upstream | Pending |
| 7 | P2 | Repair local video/sound change processing and watcher ownership | Changes retain folder/extension filters, new+changed paths deduplicate, prior watchers are disposed; independent fixes/reports remain separate | Pending |
| 8 | P2 | Standardize scan lifecycle | Root/directory/generation changes reject stale results; missing/unreadable folders clear scanning flags and permit recovery | Pending |
| 9 | P2 | Repair remaining restoration side effects | Resolved images do not enter another folder's panel list; normalized missing-model recovery clears the correct pending entry after success | Pending |
| 10 | P2 | Resolve Quill project-directory parity | Equivalent project containers work through SAF, or an intentional restriction is explicitly agreed and documented | Pending |
| 11 | P2 | Normalize video extension matching | Uppercase extensions behave like lowercase in discovery and updates | Discovery and network-pointer recognition implemented; isolated checks passed; update filtering remains action 7; Unity validation pending |
| 12 | Release gate | Run a shared parity matrix | Local and delayed fake-SAF tests pass, followed by real Android provider tests for navigation, identities, restoration and updates | Pending |

## Upstream report queue

1. U1 / action 2: video restoration depends on active catalog. Isolated lookup behavior reproduced; UI reproduction pending.
2. U2 / action 7: recursive watcher updates bypass folder/extension filtering and can duplicate new entries. Production selection expressions reproduced; native event/UI reproduction pending.
3. U3 / action 6: nested saved strokes are not discovered by the root-only sketch index. Production enumeration reproduced; valid-sketch UI reproduction pending.
4. U4 / action 7: image/background/video navigation leaves obsolete watchers active. Source-confirmed; handle/event measurement pending.
5. U5 / action 11: uppercase video extensions omitted. Production selection reproduced.
6. U6 / action 8: missing local video/sound folders can leave scanning stuck. Source-confirmed; UI recovery reproduction pending.
7. U7 / action 3: Blocks/Models same-relative-path collisions can select the wrong model. Source-confirmed; import/reload reproduction pending.
8. U8 / action 9: resolving an image outside the active folder mutates that folder's item list. Source-confirmed; UI reproduction pending.
9. U9 / action 9: normalized missing-model recovery removes from the wrong dictionary. Source-confirmed; repeated-recovery reproduction pending.

## Validation matrix

1. Root/A/B/deeper folders, same basenames across folders and roots, and supported/unsupported extensions with varied case.
2. Saved references outside the open folder, references spanning sibling folders, cold startup and recovery of missing files.
3. Navigation during delayed listing/thumbnail work, external changes/deletions/renames, duplicate notifications and obsolete watchers.
4. Missing folders, provider denial, selected-root replacement, and cancellation/retry.
5. Blocks flat discovery, Quill project containers, saved-stroke containers and empty libraries.
6. Compare displayed paths, root-qualified identities, navigation state and restored widgets, not merely counts.

## Progress log

1. 2026-09-13: Created plan. Prior review fixes `1b1d048e9` (reject directory overwrite targets) and `d61fc4c13` (direct-child video/sound listings) are committed locally, not pushed. The latter needs action 2 before the workflow can be considered covered.
2. 2026-09-13: Began action 1. Earlier `3261395be` fixed the Blocks root but left UserPath-based substring assumptions in `WidgetManager.GetPathRootedAtBlocks` and `GetModelSubpath`; audit reproduced an exception with a private working-cache path.
3. 2026-09-13: Implemented action 1: both Blocks conversions share a root-based, normalized, boundary-checked helper; legacy `Blocks/OfflineModels/...` output remains unchanged. Added NUnit regressions. Passed 23 isolated production-method checks across shared Android, Lepton and desktop roots, root equality, sibling-prefix/traversal rejection, and ordinary Models lookup. The App.UserPath stub throws if accessed, verifying the dependency is removed. Unity/device tests have not run. Commit message: `Derive Blocks model paths from the actual Blocks root`.
4. 2026-09-13: Implemented action 5 with a cheaper-model subagent; root reviewed the diff. Local and SAF discovery now use the same case-insensitive model format helper, including .spz/.sog. Added NUnit coverage. Independently compiled/executed the production helper with neither, USD-only, FBX-only and both symbols: respectively 9, 12, 10 and 13 formats, with expected conditional membership. Commit message: `Share model extension policy between local and SAF catalogs`.
5. 2026-09-13: Implemented action 11 discovery matching with a cheaper-model subagent; root reviewed the diff. Local and SAF video scans use one case-insensitive extension helper. Added NUnit regressions and passed five isolated production-helper cases (lower/upper/mixed case and unsupported formats). Changed-file filtering is deliberately still tracked under action 7. Commit message: `Match video extensions case-insensitively in local and SAF scans`.
6. Actions 1, 5 and 11 have not been Unity/device tested; release gate 12 remains open. The reviewed checks can be rerun from machine-local `Logs/pr1120-plan-checks.ps1`. No fixes have been pushed during this plan execution.
7. 2026-09-13: Implemented action 2. Video saved-path lookup falls back independently of the active list, normalizes logical paths, rejects outside/unsupported paths, and lazily resolves local files or root-guarded SAF media sources. Provider failures/missing documents never fall back to stale local cache files. Explicit logical paths now bypass redundant global-path derivation in the ReferenceVideo constructor. Added local sibling-folder and fake-SAF NUnit regressions. Passed 26 isolated production-code checks including denial, stale cache, deferred materialization and root changes; no actual playback/widget reload tested. Machine-local runner: `Logs/pr1120-video-restore-checks.ps1`. Commit message: `Resolve saved videos independently of the active catalog folder`.
8. Next priorities: action 3 model identity compatibility (decision below) and action 4 async model recovery. Release gate 12 remains open.
9. 2026-09-13: Completed an action 11 follow-up: ReferenceVideo now recognizes .TXT network pointers case-insensitively, matching discovery. Added three NUnit cases. The isolated runner passes 29 checks: the preceding 26 restoration checks plus lowercase/uppercase network pointers and a normal video. Network playback itself remains untested. Commit message: `Recognize network video pointer extensions case-insensitively`.
10. 2026-09-13: Cheaper-model read-only investigation completed for action 3. Both root scans collapse to the same relative key; Model.Location equality and MetadataUtils grouping also omit source. A catalog-only key change would not fix serialization/restoration. Proposed direction: additive optional source discriminator in TiltModels75 and Model.Location, source-qualified catalog/missing-reference keys, source-aware lookup and preservation through SAF materialization. Existing FilePath values remain unchanged. Old files cannot reveal the intended root when both contain the same relative path. Current Model.Location.AbsolutePath prefers Blocks; confirm retaining that legacy fallback (with an ambiguity warning) before implementation. No model serialization code has changed.
