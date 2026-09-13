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
| 2 | P1 | Restore videos independently of browser state | Saved videos in two sibling folders reload from any active folder; local and SAF fallbacks validate paths and preserve missing-file behavior | Pending |
| 3 | P1 | Eliminate Blocks/Models identity collisions | Equal relative paths under different roots stay distinct and reload the intended file; compatibility policy for old references tested | Pending |
| 4 | P1 | Verify/fix asynchronous SAF model restoration | Delayed cold-start indexing and restored missing files resolve pending widgets without requiring folder navigation | Pending |
| 5 | P2 | Share supported model-format policy | Local/SAF format sets match, including .spz/.sog and conditional USD/FBX formats | Pending |
| 6 | P2 | Repair saved-stroke folder navigation | Discovery follows selected folders in both modes; .tilt containers are not mistaken for ordinary folders; local defect documented upstream | Pending |
| 7 | P2 | Repair local video/sound change processing and watcher ownership | Changes retain folder/extension filters, new+changed paths deduplicate, prior watchers are disposed; independent fixes/reports remain separate | Pending |
| 8 | P2 | Standardize scan lifecycle | Root/directory/generation changes reject stale results; missing/unreadable folders clear scanning flags and permit recovery | Pending |
| 9 | P2 | Repair remaining restoration side effects | Resolved images do not enter another folder's panel list; normalized missing-model recovery clears the correct pending entry after success | Pending |
| 10 | P2 | Resolve Quill project-directory parity | Equivalent project containers work through SAF, or an intentional restriction is explicitly agreed and documented | Pending |
| 11 | P2 | Normalize video extension matching | Uppercase extensions behave like lowercase in discovery and updates | Pending |
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
