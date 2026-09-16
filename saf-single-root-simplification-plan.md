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

Remove the resume refresh. Combined with decision 3, nothing rebuilds a
projection while consumers hold paths into it, which removes the reason the
generational swap exists: `ProjectionPointer`, the per-generation directories,
generation-name validation, `CleanupOldGenerations` and
`DeleteOwnedGeneration`.

The section below goes further and removes the projection itself for scripts
and plugins, which makes most of this moot.

**Do not extend this decision to desktop.** `PlatformConfigPC.asset:22` sets
`UseFileSystemWatcher: 1` and it works: dropping a model into the folder makes
it appear. The catalogs are shared code, so removing the Android paths will
make the desktop watcher paths look vestigial. They are not. Scope the change
to the SAF backend.

## Unify The File Types

Sketches, media, plugins, scripts, fonts and generated output are currently
handled by four different subsystems. That split is not principled and should
be collapsed.

`IUserStorageBackend` is already file-type agnostic: `List`, `EnumerateTree`,
`OpenRead`, `BeginWrite`, `Rename`, `Materialize(documentId, scope)`. Nothing in
it knows what a sketch or a plugin is, and `MaterializationScope` is already
`{ File, DependencyTree }` — a model with its textures and a plugin folder with
its `require`d modules are the same shape.

Only one thing actually varies between callers: **whether the consumer can take
a stream, or genuinely requires a filesystem path.** That is a property of the
consuming library, not of the file type. The device probe established that a
detached descriptor exposes no usable `/proc/self/fd` path, so a consumer that
needs a path needs a real local file.

### Scripts and plugins do not need paths

This is the finding that collapses the design. `OpenBrushScriptLoader`
(`Assets/Scripts/API/Lua/OpenBrushScriptLoader.cs`, installed at
`LuaManager.cs:295`) has exactly two filesystem touchpoints:

```csharp
public override bool ScriptFileExists(string name)
    => File.Exists(path);

public override object LoadFile(string file, Table globalContext)
    => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
```

MoonSharp's loader contract is **stream-based**. `LoadFile` returns a `Stream`;
the path is only how this implementation happens to obtain one. And
`IUserStorageBackend.OpenRead` returns a `Stream`.

So scripts and plugins can be read directly from SAF with no materialization at
all, exactly as sketches are:

- `LoadFile` becomes `backend.OpenRead(documentId, requireSeekable: false, ct)`.
- `ScriptFileExists` becomes a lookup in the directory listing the catalog
  already holds.

The correct abstraction seam already exists and is simply being fed a path.
`ApiManager.cs:202` (`File.Exists(startupScriptPath)`) is the same shape and
converts the same way.

### Fonts are the only genuine path consumer

`SvgTextUtils.cs:30` takes `UserRuntimeContent.Instance.GetRuntimePath(
StorageArea.Fonts)`, and Unity has no clean runtime path from `byte[]` to
`Font`. Fonts genuinely need files on disk.

They are also few and small — single-digit megabytes. Materialize the font tree
once at startup into a small fixed app-private directory and never evict it.
That needs no eviction policy, no pinning mechanism and no manifest: "this
directory is not part of the media budget" is the whole rule.

### The resulting model

| Content | Mechanism |
| --- | --- |
| Sketches | `OpenRead` — streamed |
| Scripts, plugins | `OpenRead` — streamed, through the existing `OpenBrushScriptLoader` seam |
| Media | `Materialize` under a budget, evictable |
| Fonts | materialized once at startup, small fixed area, never evicted |
| Captures, exports | `BeginWrite`, then publish |

Two mechanisms, no pin flag, no per-type policy.

An earlier draft of this plan proposed a third concept — "path lifetime",
transient for media versus session-long for plugins and fonts — and a
corresponding pin flag on the materialization cache. That was an artefact of
assuming plugins needed files. They do not, and with plugins streamed the only
session-long path consumer is fonts, where an unevictable fixed directory is
simpler than any pinning mechanism.

### Consequences for `UserRuntimeContent`

`UserRuntimeContent` (1,262 lines) does not become a caller of shared
materialization; it largely **disappears**, replaced by a stream-backed script
loader and a small font copy at startup. Specifically:

- `ProjectionPointer`, `ProjectionManifest`, `ProjectionEntry`, the generation
  directories and their cleanup all go. An earlier draft argued for retaining
  the manifest as diff input; with nothing to diff, it goes too.
- The read loop with running SHA-256 (`UserRuntimeContent.cs:1040`) goes,
  removing the duplication with `SafUserStorageBackend.cs:448`. The two were
  independent implementations of open, copy, fsync and stamp mtime; only the
  backend's survives, and it already does atomic replace correctly.
- `MigrationRecord` is retained. It is one-shot legacy migration of app-private
  Scripts, Plugins and Fonts into the SAF tree and is unaffected by how those
  files are read afterwards.

### Manual refresh becomes trivial

A user-initiated refresh — "I added a plugin, show it" — costs almost nothing
under this model, because there is nothing to copy:

- **Scripts and plugins**: re-read the SAF directory listing. Newly added files
  are visible to `ScriptFileExists` and `LoadFile` immediately, since both go
  straight to storage. `LuaManager` already has the consumer-side entry point,
  `OnScriptsDirectoryChanged` calling `LoadScriptFromPath` (lines 309-310, 662),
  which needs redirecting to a document identity rather than a path.
- **Media and sketches**: a catalog rescan. `RequestRefresh()` already exists on
  those catalogs and on `SafSketchSet`. Media materializes on demand, so a newly
  added model needs nothing until it is used.
- **Fonts**: re-run the startup copy.

Deletion stays restart-only. `LuaManager.cs:311` shows it is unsupported at
runtime on desktop too — the watcher hookup is commented
`// m_FileWatcher.FileDeleted += OnScriptsDirectoryChanged; TODO` — so this is
parity with current behaviour rather than a new limitation.

Keep the refresh user-initiated. Wiring it to resume or a timer reintroduces
concurrent-refresh handling in the catalogs, which decisions 3 and 4 exist to
remove.

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

## Durability: Fix The Write, Not The Recovery

This section supersedes nothing above; it is an independent correctness fix
found while reviewing behaviour with multi-gigabyte sketches.

### The inversion

SAF has no atomic replace — the device probe confirmed `renameDocument` onto an
existing display name produces `Sketch (1).tilt` rather than overwriting. So a
save is a five-step sequence: write `.ob-tmp`, validate it, rename the canonical
to `.ob-bak`, rename the temporary into place, delete the backup. Process death
between steps 3 and 4 leaves no canonical document, and startup recovery
(`SafTransactionRecovery`) reinstates one from whichever leftover validates.

Recovery therefore has to distinguish a complete document from a truncated one.
It currently does that with `IsValidDocument` (`SafTransactionRecovery.cs:244`),
which passes `testData: true` to `TiltFile.IsArchiveValid` — decompressing every
zip entry to `Stream.Null`. The commit path validating the same bytes
(`SafStorageTransaction.ValidatePayload`) passes `testData: false`, reading only
the central directory. The two paths disagree about what "valid" means for the
same artifact.

Tracing why turns up the reason:

```csharp
// SafStorageTransaction.cs:114 - the journal (app-private JSON bookkeeping)
stream.Flush(flushToDisk: true);     // real fsync, 5-6 times per save

// SafStorageTransaction.cs:682 - the payload (the user's sketch)
m_Stream.Flush();                    // no fsync
```

The bookkeeping is made durable; the data is not. A zip's central directory is
written last, so an interrupted write normally leaves no readable directory and
the cheap check catches it. But with no fsync, a power loss or kernel panic can
lose dirty pages from the middle while the tail lands, producing a structurally
valid archive with garbage inside — which only decompression detects. The deep
check exists to find corruption that the missing fsync permits.

Note the two crash classes are not equally likely. Process death — the OOM
killer, a crash, the user swiping the app away, Android reclaiming a backgrounded
app — is common, and the page cache survives it, so the file is intact and
`Flush()` suffices. Power loss and kernel panic are rare, and only those produce
torn middles.

### The fix

1. Call `Flush(flushToDisk: true)` on the payload stream before the rename
   sequence begins. One fsync per save, proportional to a write already being
   performed.
2. Change `IsValidDocument` to `testData: false`, matching the commit path.
   Truncation is caught by the central directory; torn middles are now
   prevented rather than detected.

The cost accounting is favourable. One fsync is added per save while
`saf-transaction-journal-removal-plan.md` removes five or six, so saves get
faster. Recovery drops from decompressing up to three multi-gigabyte candidates
to reading three central directories — milliseconds rather than a stall behind
the startup gate.

This supersedes an earlier suggestion in `saf-design-review.md` to make recovery
validation size-aware. Preventing the corruption is better than thresholding the
detection of it.

### Unverified assumption

`flushToDisk: true` should reach the disk through a SAF descriptor, since the
probe established that the descriptor is a real regular file and `fsync(2)` on
it is meaningful. A provider could in principle ignore it. `Support/SafFdProbe`
checks 12-14 now measure write throughput, the cost of a single `fsync`, and
read-back throughput, so the assumption and its cost can both be confirmed
before this change is relied upon.

## Scale: Fewer But Larger Files

The realistic power-user shape is a modest number of very large files — a
multi-gigabyte sketch, a large model, long video captures — not tens of
thousands of small ones.

### What suits that shape already

Reading a large `.tilt` involves no copy at all. The probe confirmed the
descriptor is a seekable regular file, so `ZipSubfileReader` seeks directly into
the archive in shared storage and a 2 GB sketch costs zero bytes of app-private
storage to open. This is the strongest argument for the fd-backed approach over
the mirrored cache it replaced.

Also sound: no whole-file buffering anywhere in the storage path, no `int` casts
on sizes (all `long`, so no 2 GB overflow), `Stream.CopyTo`'s default 80 KiB
buffer, one-cursor directory queries with struct-of-arrays JNI marshalling
(eleven calls regardless of row count), and lazy thumbnail loading through
`SafSketchSet.RequestOnlyLoadedMetadata`.

### Gaps

1. **No free-space accounting on the SAF path.** `SaveLoadScript.cs:490` reads
   `if (!directSafSave && !FileUtils.CheckDiskSpaceWithError(m_SaveDir))`. The
   check is correctly skipped for direct SAF saves, because `m_SaveDir` is not
   where the sketch lands — but nothing replaced it. The commit sequence needs
   twice the sketch size transiently in shared storage, so a 2 GB sketch needs
   4 GB free. Materialization copies a whole document into app-private storage
   with no check either.

2. **The materialization cache cap is the wrong shape.**
   `EvictMaterializationCache` caps at a hardcoded 512 MB with no free-space
   awareness. A single 500 MB model nearly exhausts the budget and evicts
   everything else.

3. **The materialization cache never serves a hit.** `MaterializeFile`
   (`SafUserStorageBackend.cs:427`) has no cache-hit branch — no
   `if (File.Exists(destination) && upToDate) return destination`. It
   unconditionally re-copies the document from SAF and overwrites through
   `File.Replace`, every call. So the 512 MB budget is a staging directory with
   an eviction policy, not a cache: a 500 MB model is re-copied from shared
   storage on every import, and the eviction pass, the ordering and the tree
   walk are all managing something that never avoids work.

   Add a hit path keyed on the document metadata the directory listing already
   returns — `DocumentId`, `Size` and `LastModified` — reusing the cached copy
   when all three match. This applies to the media cache only; scripts and
   plugins are streamed and fonts are copied once at startup, so neither has a
   cache to hit.

   An earlier draft of this plan attributed the poor eviction ordering to
   `/data` being mounted `noatime` (confirmed on a Nothing Phone (3a):
   `f2fs rw,lazytime,...,noatime,...`). That reasoning was wrong: `noatime`
   suppresses only *implicit* atime updates on read, and
   `SafUserStorageBackend.cs:478` sets it explicitly via
   `File.SetLastAccessTimeUtc`, which works regardless. The ordering is
   materialization order because there is no reuse to record, not because the
   timestamp is unwritable. Once a hit path exists, stamping access time on a
   hit makes the ordering a real LRU.

4. **Every materialization walks the whole cache.** `GetFiles("*",
   SearchOption.AllDirectories)` plus a `Sum` of lengths runs on each call.
   Currently this compounds with gap 3, since every use is a fresh copy.

5. **Tree enumeration truncates silently.** `StorageTreeQuery` defaults to
   `MaximumItemCount = 10000`, `UserRuntimeContent` uses 5000, and
   `StorageTreeResult` has no truncation flag — a capped walk returns
   `Succeeded` with a partial list, indistinguishable from a complete one. Lower
   priority for the large-file shape, but it violates core invariant 4 of the
   fd-backed plan ("failure to query SAF never means the directory is empty"),
   and enumerate-once-at-startup would make a truncated listing permanent for
   the session.

### Correction to the startup gate

The gating described above must gate on folder *selection*, not on enumeration.
Startup latency should not scale with library size. Once a root exists the
application can start, with catalogs populating in the background as they do
today. The simplification this plan is after — no degraded mode, no resumable
save paths — comes from "a root always exists after startup", not from
"everything is enumerated before startup".

## Interaction With The Journal-Removal Plan

`saf-transaction-journal-removal-plan.md` is complementary, not superseded.
It removes the write-ahead journal; this plan removes the root namespacing
*around* that journal. Doing this plan first makes the journal removal smaller,
since the recovery root directory and root namespace helpers it has to extract
would already be gone. Either order works; this order is cheaper.

## Estimated Scale

Roughly 1,500–2,500 lines from decisions 1–4, across 24 files. Unifying the
file types removes most of `UserRuntimeContent` (1,262 lines) outright rather
than relocating it, since streaming scripts and plugins needs no projection,
no manifest and no second copy routine; a small font materialization and the
retained one-shot `MigrationRecord` are what remain.

These are estimates from reference counts, not from doing the work: 255
root-identity references are not 255 deleted lines, since many are a single
argument in a signature that survives.

The line count is not the main benefit. The benefits are that
`Backend.IsReady` becomes a startup invariant instead of a condition checked in
twenty places, that nine save, export and capture paths stop being resumable
mid-operation, that root identity stops being something every future
contributor must reason about in every catalog, and that there is one rule for
how a shared document reaches its consumer — stream it, unless the library
cannot take a stream.

## Suggested Order

1. Run the outstanding IL2CPP device gate.
2. Gate startup on folder selection (not on enumeration); exit on decline.
   Remove `RequireSharedFolderFor` and unwind the nine call sites.
3. Add the missing cache-hit path in `MaterializeFile`; fsync the payload
   before the rename sequence and drop recovery to `testData: false`. Small,
   independent correctness fixes that should not queue behind structural work.
4. Redirect `OpenBrushScriptLoader` and `ApiManager`'s startup-script check to
   `OpenRead`. This is the highest-leverage structural change and is
   self-contained: it removes the reason the projection exists.
5. Replace the rest of `UserRuntimeContent` with a startup font materialization.
6. Remove the resume refresh; add the user-initiated refresh if wanted.
7. Collapse root namespacing to the startup URI comparison.
8. Then the journal removal.

Each step is independently shippable and independently revertable. Steps 2, 3
and 4 carry most of the value and do not depend on each other.

## Open Question

Dropping the automatic resume refresh means external edits made while the
application is backgrounded are not visible until a relaunch or a
user-initiated refresh. On Android every route to editing files — USB, the
Files app, a browser download — backgrounds the application first, so this is
the common workflow rather than an edge case.

Accepted, because with scripts and plugins streamed the refresh is a directory
re-listing rather than a tree rebuild, so the cost of offering it is close to
zero and it need not be automatic to be adequate.

Deletion remains restart-only, matching `LuaManager.cs:311`.
