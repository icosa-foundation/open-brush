# SAF Single-Root Simplification Plan

## Status

Proposal, arising from a design review of `feature/saf-google-play-fd-backed`
(see `saf-design-review.md`). Not started.

Four product decisions, taken 2026-09-16, remove constraints the current
implementation was built to satisfy. This plan records what each decision
makes redundant, what it does not touch, and what must be kept as a
safeguard.

Sequencing note: run the outstanding IL2CPP device gate first (see
`google-play-saf-fd-backed-storage-plan.md`). If detached descriptors turn out
to be unusable under Unity's runtime, the write path changes and parts of this
plan need rebasing onto whatever replaces it.

## Decisions

1. **Startup is gated on folder selection.** On a Google Play build the user
   chooses the shared `Open Brush` folder before the application finishes
   starting.
2. **Declining exits the application.** There is no degraded mode and no
   "continue without shared storage" path.
3. **The root never changes for the lifetime of an installation.** There is no
   supported hot-swap between roots.
4. **Shared files are not modified externally while the application runs.**
   External edits are picked up on next launch.

Decisions 1 and 2 are the load-bearing ones: together they make
`UserStorage.Backend.IsReady` a startup invariant rather than a condition
every feature must test and recover from.

## What Each Decision Removes

### Decisions 1 and 2: gated startup, exit on decline

Today folder selection is lazy and dismissible. `AndroidStorageManager`
(`RequireSharedFolderFor`, line 114) prompts on first use of each feature,
with the message "You can cancel and continue without shared storage", and
resumes the interrupted operation through an `onReady` callback.

This forces nine features into continuation-passing style, because saving,
exporting or capturing may pause mid-operation to show a folder picker and
resume later:

- `SketchControlsScript.cs:4313` — saving models
- `CameraPathCaptureRig.cs:120` — saving videos
- `Tools/MultiCamTool.cs:643, 1765, 2016` — GIF capture and snapshots
- `API/ApiMethods.Utils.cs:464, 516` — API snapshot and video publication
- `Export/Export.cs:131` — export
- `Save/SaveLoadScript.cs:472` — saving sketches and saved strokes

Removing the degraded mode deletes:

- both `RequireSharedFolderFor` overloads;
- the `m_PendingAction` / `m_PendingCanceledAction` / `m_RequestInProgress`
  state machine, and `OnOpenBrushFolderCanceled`'s resumption logic;
- the `kLegacyStartupPromptDismissedKey` preference and its migration
  (`AndroidStorageManager.cs:28, 55`);
- the save-locally-when-cancelled fallback, and the "continue without shared
  storage" messaging;
- most of the 20 `HasOpenBrushFolder` / `Backend.IsReady` readiness checks.

The nine call sites above revert to straight-line code. This is the largest
structural win in the plan: it removes an asynchronous re-entry point from the
save, export and capture paths, which is a correctness hazard as much as a
line-count cost.

**Retain:** `saveToLocalCacheOnly` in `SaveLoadScript.SaveOverwriteOrNewIfNeeded`
is autosave, not a SAF fallback. Autosaves remain deliberately app-private
recovery data. Do not remove it with the degraded-mode paths.

### Decision 3: the root never changes

Root identity is the largest cross-cutting concern on the branch — 255
references across 24 files, including every media catalog, the transaction
machinery, the projection, Drive sync, `CatalogScanGuard` and `SafSketchSet`.

Four mechanisms exist only to survive a root swap:

1. **Root-scoped persistent namespaces.**
   `OpenBrushStorage.GetSafRootScopedPreferenceKey`, `RootNamespace` in
   `DriveSyncLedger` and the projection manifest, and recovery journal paths
   keyed by a SHA-256 of the root identity. One namespace suffices.
2. **Cross-root guards.** `EnsureSelectedRoot` / `IsSelectedRootCurrent` in the
   transaction path, so an in-flight write cannot land in the wrong tree.
   Commits "Prevent SAF work from crossing roots" and "Reject sketch mutations
   from stale SAF roots" are entirely this.
3. **Catalog resets on root change.** The `rootChanged` branch in
   `AndroidStorageManager.RefreshSharedCatalogs`, plus "Clear SAF catalogs when
   roots change", "Reset IMM navigation when the selected SAF root changes" and
   "Preserve local models across SAF root changes".
4. **Scan-guard root checks.** `CatalogScanGuard` rejecting results whose
   source was replaced mid-scan.

`AndroidStorageManager.ReselectSharedFolder` (line 151) and its scripts action
should be retained as a *recovery* entry point for a revoked grant, but made
restart-required rather than supporting a hot swap.

### Decision 4: no external modification while running

On Android this is already true in every shipping build:
`PlatformConfigMobile.asset:22` sets `UseFileSystemWatcher: 0`, and the
branch already removed its `ContentObserver` layer. The only mid-session
external-change path is `AndroidStorageManager.OnApplicationPause(false)`
(line 470), which on resume refreshes runtime content, refreshes shared
catalogs and kicks Drive sync.

Removing the resume refresh, together with decision 3, means the projection is
built exactly once per process. That makes the generational projection in
`UserRuntimeContent` unnecessary:

- `ProjectionPointer`, `ProjectionManifest`, `ProjectionEntry`;
- generation path helpers, generation-name validation,
  `CleanupOldGenerations`, `DeleteOwnedGeneration`.

The generational swap exists because `LuaManager` and `ApiManager` hold live
paths into the projection across a rebuild. If there is never a rebuild, a
single directory suffices.

Most of the concurrent-refresh handling also goes, since refresh-during-scan
had two sources — resume and root change — and both are now gone.

**Do not extend this decision to desktop.** `PlatformConfigPC.asset:22` sets
`UseFileSystemWatcher: 1` and it works: dropping a model into the folder makes
it appear. The catalogs are shared code, so removing the Android paths will
make the desktop watcher paths look vestigial. They are not. Scope the change
to the SAF backend.

## What Stays, And Why

None of the following is external-change or multi-root machinery, and none of
it is affected by these decisions:

- **The fd read/write path.** Forced by the absence of filesystem access;
  validated on device (`Support/SafFdProbe`).
- **The commit sequence** — write temp, validate, rename canonical to backup,
  rename temp to canonical, delete backup. This is crash safety, not external
  change. The device probe showed `renameDocument` onto an existing name
  deduplicates rather than replaces, so the ordering is load-bearing.
- **On-demand materialization** for models, SVG, fonts and Lua. The probe
  confirmed a detached descriptor has no usable `/proc/self/fd` path, so
  path-consuming libraries must be given real files.
- **`DriveSyncLedger`.** It compensates for SAF providers owning document
  timestamps, replacing `File.SetLastWriteTime`. The changing party is a remote
  Drive, which no decision here constrains.
- **`MigrationRecord`** in `UserRuntimeContent`. One-shot legacy migration of
  app-private Scripts/Plugins/Fonts into the SAF tree, hash-verified. Dormant
  after first run; dead for new installs.
- **Autosave and crash recovery.** Deliberately app-private.
- **Provider failure handling.** See the safeguard below.

## Safeguards To Keep

### Compare the root URI at startup

Do not remove root identity entirely. Persist the selected root URI and compare
it on each launch. If it differs from the previous run, discard local caches,
materializations and the projection, and rebuild.

This is roughly twenty lines and replaces 255 references. Without it, a root
URI that changes for any reason — reinstall, provider change, the user
re-granting a different folder through the recovery path — would silently
write into a second namespace with no reconciliation. That is exactly the bug
root-scoping prevents, and it should not be reintroduced with nothing catching
it.

### Distinguish "root never changes" from "root is never lost"

The grant can still be revoked in Android Settings, app data can be cleared,
and the provider can fail. Decision 3 does not cover these.

Handle them as a terminal error state: report clearly, offer re-grant through
the retained `ReselectSharedFolder` path, and require a restart. Do not attempt
to recover in place. Retain "Preserve SAF roots during provider failures" and
"Retain SAF sketches after refresh failures" — a failed query must never be
read as an empty directory (core invariant 4 of the fd-backed plan).

## Interaction With The Journal-Removal Plan

`saf-transaction-journal-removal-plan.md` is complementary, not superseded.
It removes the write-ahead journal; this plan removes the root namespacing
*around* that journal. Doing this plan first makes the journal removal smaller,
since the recovery root directory and root namespace helpers it has to extract
would already be gone. Either order works; this order is cheaper.

## Estimated Scale

Roughly 1,500–2,500 lines, across 24 files. This is an estimate from reference
counts, not from doing the work: 255 root-identity references are not 255
deleted lines, since many are a single argument in a signature that survives.

The line count is not the main benefit. The benefits are that
`Backend.IsReady` becomes a startup invariant instead of a condition checked in
twenty places, that nine save/export/capture paths stop being resumable
mid-operation, and that root identity stops being something every future
contributor must reason about in every catalog.

## Suggested Order

1. Run the outstanding IL2CPP device gate.
2. Gate startup on folder selection; exit on decline. Remove
   `RequireSharedFolderFor` and unwind the nine call sites.
3. Remove the resume refresh; collapse the generational projection to a single
   directory.
4. Collapse root namespacing to the startup URI comparison.
5. Then the journal removal.

Each step is independently shippable and independently revertable. Step 2 is
the highest value and should not wait for the others.

## Open Question

Dropping the resume refresh means external edits made while the application is
backgrounded are not visible until relaunch. On Android every route to editing
files — USB, the Files app, a browser download — backgrounds the application
first, so this is the common workflow rather than an edge case.

Accepted on the basis that relaunching is cheap and now unavoidable anyway,
given that declining the folder prompt exits. If it proves annoying in
practice, add a manual "Refresh" action that performs a full teardown and
rebuild. That is far cheaper than restoring automatic refresh, because it can
invalidate every held path rather than having to swap safely underneath live
consumers.
