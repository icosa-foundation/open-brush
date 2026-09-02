# Google Play SAF Storage: FD-Backed Hybrid Plan

## Status

Implemented replacement for the whole-tree mirrored-cache design on Google
Play Android builds.

Static desktop, editor, Android-symbol, and Java compilation can validate the
code shape, but they do not satisfy the mandatory device gate. Before release,
run the built-in `SAF_FD` debug probe and the device matrix below on the target
local Android Documents provider. If detached descriptors are not reliable and
seekable under IL2CPP, retain the backend/catalog/transaction architecture and
replace direct archive reads with sparse per-document materialization.

## Decision Summary

Use the Android Storage Access Framework (SAF) tree as the only canonical,
user-visible store for Google Play builds.

- Sketches and saved strokes are enumerated directly from SAF.
- Known `.tilt` documents are read and written through seekable file
  descriptors when the provider supports them.
- Open Brush does not mirror SAF directories into app-private storage.
- Open Brush does not reconcile two directory trees.
- Autosaves and failure-recovery data remain app-private.
- Libraries that require ordinary paths use narrowly scoped staging or
  on-demand materialization.
- Generated outputs are produced locally and published to SAF once complete.
- Non-Google-Play platforms continue using the existing filesystem backend.

This is an FD-backed hybrid, not an attempt to make every third-party library
understand `content://` URIs.

## Problem Being Solved

Modern Google Play Android builds cannot normally use direct paths such as:

```text
/storage/emulated/0/Documents/Open Brush
```

The user can grant Open Brush read/write access to an `Open Brush` directory
through `ACTION_OPEN_DOCUMENT_TREE`, but Android represents that grant with
document URIs and `DocumentsContract` operations.

The current branch adapts the existing path-based application by copying whole
SAF directories into an app-private working cache and synchronizing changes in
both directions. That preserves most existing code but creates a general
synchronization problem:

- destructive reconciliation;
- canonical/cache conflicts;
- local-only path preservation;
- pending-transfer persistence;
- cache deletion notifications;
- retry ordering;
- startup provider failure handling;
- concurrent inbound and outbound transfers.

The FD-backed design replaces that open-ended synchronization problem with a
bounded storage-adapter problem.

## Goals

1. Preserve user-created files across application uninstall.
2. Keep the user-visible `Documents/Open Brush` hierarchy.
3. Make SAF the sole canonical source for shared user content.
4. Avoid whole-directory copying during startup.
5. Avoid deletion reconciliation entirely.
6. Read and write `.tilt` documents without an intermediate full-file copy when
   the provider supplies a seekable descriptor.
7. Never truncate the canonical sketch before a replacement is complete.
8. Preserve normal filesystem behavior on Windows, Android OpenXR, Quest, and
   other non-Google-Play configurations.
9. Keep platform differences behind a runtime storage backend instead of
   scattering Google Play conditionals through gameplay code.
10. Provide explicit, recoverable behavior when SAF is unavailable.

## Non-Goals

- Making all Unity or native plugins consume SAF streams.
- Treating arbitrary cloud providers as equivalent to local storage without
  capability checks.
- Providing an instantaneous filesystem watcher for external SAF changes.
- Maintaining a complete offline copy of every shared file.
- Supporting direct SAF access on non-Google-Play builds.
- Changing the shared folder layout without a separate migration decision.
- Replacing Open Brush's internal autosave system.

## Core Invariants

1. A shared document exists canonically only in SAF.
2. App-private materializations are disposable caches, not a second canonical
   directory.
3. App-private staged outputs are pending transactions, not synchronized
   mirrors.
4. Failure to query SAF never means that the SAF directory is empty.
5. A save is not reported as successful until the replacement transaction has
   completed.
6. The existing canonical document is never opened in truncating mode.
7. Local autosave recovery is not removed until the shared save commits.
8. SAF document IDs and URIs are opaque identities; display names are not
   identities.
9. Every detached file descriptor has exactly one clear owner responsible for
   closing it.
10. Provider capabilities are checked and failures are handled, not assumed
    away.

## Proposed Architecture

### Runtime Backend Boundary

Introduce an application-level storage backend selected at runtime:

```csharp
public interface IUserStorageBackend
{
    StorageBackendKind Kind { get; }
    bool IsReady { get; }

    Task<StorageDirectorySnapshot> ListAsync(
        StorageArea area,
        string relativeDirectory,
        CancellationToken token);

    Task<Stream> OpenReadAsync(
        StorageDocumentId documentId,
        bool requireSeekable,
        CancellationToken token);

    Task<IStorageWriteTransaction> BeginWriteAsync(
        StorageArea area,
        string relativePath,
        string mimeType,
        CancellationToken token);

    Task<StorageMutationResult> RenameAsync(
        StorageDocumentId documentId,
        string newDisplayName,
        CancellationToken token);

    Task<StorageMutationResult> DeleteAsync(
        StorageDocumentId documentId,
        CancellationToken token);

    Task<string> MaterializeAsync(
        StorageDocumentId documentId,
        MaterializationScope scope,
        CancellationToken token);
}
```

The precise interface may change during implementation. The important
separation is:

- catalog identity and metadata;
- stream access;
- write transactions;
- explicit mutations;
- optional path materialization.

Implementations:

- `LocalUserStorageBackend`: current `System.IO` behavior.
- `SafUserStorageBackend`: Google Play Android behavior.

Only `SafUserStorageBackend` and its Java bridge should require Android-specific
compilation. Gameplay, sketchbook, save, and export code should depend on the
backend interface.

### Storage Areas

Use logical storage areas instead of deriving behavior from absolute paths:

```text
Sketches
SavedStrokes
MediaLibrary/Images
MediaLibrary/BackgroundImages
MediaLibrary/Models
MediaLibrary/Videos
Snapshots
Videos
VRVideos
Exports
```

Autosaves are deliberately absent because they remain local-only.

### Document Identity

`StorageDocumentId` must wrap the provider's opaque identity or document URI.
It must not be reconstructed from a display name.

Catalog records should contain:

```text
DocumentId
ParentDocumentId
DisplayName
MimeType
IsDirectory
Size (nullable)
LastModified (nullable)
ProviderFlags
RelativeDisplayPath
```

The display path is for UI and compatibility metadata only. Operations use
`DocumentId`.

## Phase 0: Mandatory FD Feasibility Spike

Do not begin the catalog rewrite until this spike has been run on a Google Play
Android IL2CPP build.

### Java Spike API

Add a temporary, isolated bridge capable of:

1. Resolving a selected test document.
2. Calling `ContentResolver.openFileDescriptor(uri, "rw")`.
3. Calling `ParcelFileDescriptor.detachFd()`.
4. Returning the integer descriptor to C#.
5. Creating a temporary document and opening it with `"rwt"`.
6. Returning structured errors rather than sentinel values.

Once `detachFd()` succeeds, Java no longer owns the descriptor.

### C# Spike

On the Unity side:

1. Construct `SafeFileHandle` from the detached descriptor.
2. Construct `FileStream` with explicit access and ownership.
3. Verify that disposing `FileStream` closes the descriptor exactly once.
4. Verify `CanSeek`.
5. Read the first 16 bytes of an existing `.tilt`.
6. Seek to the end and back.
7. Open the archive with SharpZipLib's stream constructor.
8. Read `thumbnail.png`, `metadata.json`, and `data.sketch`.
9. Write a complete `.tilt` to a temporary SAF document.
10. Close and reopen it, then validate every archive entry.

### Threading Spike

Test descriptor creation and consumption across the actual threads used by
Open Brush:

- descriptor opened on the Unity main thread and read on a worker;
- descriptor opened from a worker thread;
- concurrent independent descriptors for thumbnail and metadata;
- cancellation while a read is active;
- application pause/resume during an open stream.

If Android Java calls from worker threads are unreliable, marshal descriptor
creation to the Unity main thread and pass the resulting `FileStream` to the
worker.

### Failure Cases

Test:

- permission revoked;
- folder renamed externally;
- document deleted before opening;
- provider returns `null`;
- provider rejects `"rw"`;
- descriptor is non-seekable;
- device storage becomes full;
- application is killed while writing a temporary document.

### Provider Matrix

Mandatory:

- Android's local primary-storage Documents provider.

Useful but not required for the first release:

- removable storage;
- at least one cloud-backed document provider.

Cloud-provider failure may fall back to materialization. It must not silently
corrupt or misidentify content.

### Spike Decision Gate

Proceed with direct `.tilt` streams only if:

- the local Documents provider returns seekable descriptors;
- the detached descriptor works under Unity IL2CPP;
- SharpZipLib can read the archive correctly;
- the stream writer produces a byte-compatible `.tilt`;
- descriptor ownership is deterministic;
- interruption leaves the existing canonical document intact.

If any of the first five fail, use sparse per-document materialization instead.
Do not return to whole-tree mirroring.

## Stream and `.tilt` Refactor

### Reopenable Stream Sources

`TiltFile` currently assumes a path and opens the archive repeatedly. Introduce
an abstraction that can provide a new readable stream for each operation:

```csharp
public interface IReopenableReadStream
{
    Stream Open();
}
```

Implementations:

- path-backed stream source;
- SAF-document stream source;
- resource-backed stream source where useful.

Each request for a zip subfile must use a fresh descriptor and stream. Do not
share a single descriptor across independently disposed zip-entry readers.

### Zip Reader

Extend `ZipSubfileReader_SharpZipLib` to accept an owned seekable stream as well
as a path.

It must:

- keep the underlying archive and stream alive while an entry is read;
- dispose the zip archive and underlying stream exactly once;
- reject a non-seekable stream with a clear storage capability error.

### Header Validation

Refactor `TiltFile.IsHeaderValid()` so it can validate the Tilt/PKZIP header
from a stream without requiring `File.Exists`.

The method must preserve the stream position or operate on a newly opened
stream.

### Stream Writer

Refactor `TiltFile.AtomicWriter` into two layers:

1. `TiltArchiveWriter`, which writes a valid Open Brush `.tilt` archive to an
   arbitrary writable stream.
2. A destination transaction, which determines how the completed archive
   becomes canonical.

The stream writer must preserve:

- the Open Brush/Tilt zip header;
- thumbnail;
- high-resolution thumbnail where applicable;
- sketch data;
- metadata;
- compression behavior;
- correct close/finalize ordering.

Do not assume the existing `SketchSnapshot.WriteSnapshotToStream()` is
equivalent to `AtomicWriter`. Verify and consolidate the implementations.

### Directory-Format `.tilt` Files

SAF-backed saves should always use the zip `.tilt` representation.

Before implementation, determine whether Google Play users must be able to open
legacy directory-format `.tilt` sketches. If required, represent such a
directory as a document tree and open each named subdocument individually.
Do not materialize the entire Sketches directory merely to support this legacy
format.

## SAF Sketch Catalog

### Bulk Enumeration

Add a Java query that lists the direct children of a directory using one cursor
and requests all required columns at once.

Do not use `DocumentFile` per child. Do not issue a `FileInfo`-equivalent query
for every result.

Return structured results to C#, preferably as a compact JSON payload or a
well-defined parallel-array result with explicit error state.

Distinguish:

- successful empty directory;
- missing directory;
- unavailable permission;
- `null` cursor;
- provider exception;
- cancellation.

### `SafSketchSet`

Implement `SketchSet` for SAF-backed user sketches.

Responsibilities:

- enumerate `Sketches`;
- create `SafSceneFileInfo` entries;
- filter supported `.tilt` documents;
- sort using provider metadata;
- lazily request thumbnails and metadata;
- refresh on demand;
- retain the previous successful snapshot after query failure;
- publish one catalog-changed event after applying a successful snapshot.

Do not use `DirectoryInfo`, `DiskSceneFileInfo`, or `FileSystemWatcher`.

### `SafSceneFileInfo`

Implement `SceneFileInfo` using document identity and stream factories.

It should expose:

- human-readable name;
- availability;
- read-only/capability state;
- source and asset metadata after parsing;
- direct archive-entry streams;
- explicit rename and delete operations through the backend.

`FullPath` should not be overloaded with a fake filesystem path. Introduce a
separate stable identity/display property and migrate consumers that currently
misuse `FullPath`.

### Thumbnail and Metadata Loading

Open only the archive entries required for the visible sketchbook items.

Because a zip archive requires seeking to its central directory, each operation
should open a fresh seekable descriptor. Limit concurrent provider operations
and continue the existing per-frame icon request budget.

Cache decoded thumbnail textures in memory as today. An optional disposable
thumbnail-byte cache may be added only after measuring actual catalog behavior.

### Refresh Triggers

Refresh:

- after initial folder selection;
- on application resume;
- when the user explicitly refreshes;
- after a successful save, rename, or delete;
- after recovery changes a transaction state.

A `ContentObserver` may be investigated, but the implementation must not rely
on every provider delivering notifications.

## Save Transactions

### New Sketch

1. Ask the backend to begin a write transaction for the target display name.
2. Create a uniquely named temporary document in `Sketches`.
3. Open the temporary document through a seekable `"rwt"` descriptor.
4. Write and finalize the complete `.tilt`.
5. Close the stream.
6. Reopen and minimally validate the header/archive if the provider permits.
7. Rename the temporary document to the target name.
8. Refresh or insert the catalog record.
9. Mark the save successful.
10. Only then clear the relevant autosave recovery state.

### Overwrite

SAF has no general `File.Replace` operation. Implement a recoverable replacement
protocol:

1. Create and completely write a new temporary document.
2. Persist a small local transaction journal.
3. Rename the old destination to a reserved backup name.
4. Rename the temporary document to the canonical display name.
5. Delete the backup.
6. Remove the transaction journal.

The transaction is complete only after step 5 or after recovery has established
that the new canonical document is valid and the backup is no longer needed.

Provider rename can return a new document URI. Always update stored identities
from the returned result.

This protocol is failure-recoverable, but SAF does not promise that other
applications observe a single atomic namespace swap. Do not describe it as
strictly atomic in code or UI.

### Transaction Journal

Replacement and multi-document operations require a versioned app-private
journal. This is narrower than the general publication outbox used by a
mirrored-cache design: it records only an operation that has begun and is not
yet known to be committed or rolled back.

Each record should contain at least:

```json
{
  "version": 1,
  "transactionId": "stable-unique-id",
  "kind": "sketch-replacement",
  "rootId": "opaque-selected-root-identity",
  "area": "Sketches",
  "targetDisplayName": "My Sketch.tilt",
  "targetDocumentId": "opaque-or-null",
  "temporaryDocumentId": "opaque-or-null",
  "backupDocumentId": "opaque-or-null",
  "state": "TemporaryComplete",
  "createdUtc": "2026-07-27T12:00:00Z",
  "attemptCount": 0,
  "lastError": ""
}
```

Document IDs may change after provider rename operations, so update the record
after each mutation returns.

Minimum states:

```text
CreatingTemporary
WritingTemporary
TemporaryComplete
OriginalBackedUp
ReplacementInstalled
BackupCleanupPending
Complete
RollbackRequired
```

Journal rules:

- Write records through a local temporary file and atomically replace the
  previous record.
- Persist a state transition before starting the next destructive provider
  operation.
- Flush the completed temporary document before recording
  `TemporaryComplete`.
- Remove a record only after commit or a verified rollback.
- Treat malformed and unknown-version records as recovery errors; retain them
  until explicitly diagnosed or discarded.
- Never serialize callbacks, delegates, Java objects, or live descriptors.
- Namespace records by the selected root identity.
- Keep enough identity and display information to recover when a provider
  changes a document URI during rename.

The journal directory is itself the durable list of incomplete transactions.
Do not duplicate it in PlayerPrefs.

### App-Private Layout

Keep unrelated state in disjoint roots:

```text
Application.persistentDataPath/
  OpenBrushLocalOnly/
    Autosave/
    Scripts/
    Plugins/
    Fonts/
  OpenBrushSafRecovery/
    <root-id>/
      journals/
      payloads/
  OpenBrushSafMaterialized/
    <root-id>/
      Images/
      Models/
      Videos/

Application.temporaryCachePath/
  OpenBrushSafStaging/
    Exports/
    Captures/
    GeneratedMedia/
```

The exact existing paths may be retained when changing them would cause an
unnecessary migration, but every path must have one declared ownership class:

- local-only;
- recovery-owned;
- disposable materialization;
- temporary generation staging.

No cleanup operation may cross ownership classes.

### Destination Serialization and Supersession

Allow only one active mutation for a logical destination at a time.

- Serialize save, overwrite, rename, delete, and recovery work affecting the
  same document or destination name.
- Do not allow cleanup of an old transaction to race a new save.
- Repeated saves to the same destination must either wait in order or
  deliberately supersede an earlier transaction before its namespace mutation
  begins.
- A newer transaction may replace an older staged payload only while the older
  transaction is still safely cancelable.
- Rename locks both the old identity and proposed destination.
- Directory transactions lock their destination subtree.
- Recovery acquires the same locks as live operations.

Use a normalized logical key consisting of selected root, storage area, and
destination display path. The key is for coordination only; provider operations
still use opaque document IDs.

### Replacement Recovery

On startup or folder reconnection:

- load any local transaction journal;
- query the target directory;
- identify canonical, temporary, and backup documents by transaction ID;
- prefer a validated canonical replacement;
- otherwise restore the backup;
- never delete the only validated copy;
- clean abandoned reserved documents only after their state is understood.

Reserved document names must be unambiguous and excluded from the sketchbook.

### Save Failure

If SAF is unavailable:

- do not report the save as successful;
- keep the current in-memory sketch;
- retain autosave recovery;
- show a concise retry/reselect-folder message.

Optionally write one explicit app-private recovery snapshot. If implemented,
the recovery directory itself is the durable queue and must be visible in a
recovery UI. Do not recreate a general mirrored transfer database.

### User-Visible Save and Recovery State

Expose operation state to existing UI and diagnostics:

```text
Saving
Committed
Shared folder required
Retry available
Recovery snapshot retained
Replacement recovery required
Failed
Discarded by user
```

Ordinary sketches do not need a persistent "unsynced" state after a successful
direct SAF transaction. Recovery snapshots and staged outputs do.

- Never display `Saved` before the SAF transaction commits.
- State clearly when the current work remains recoverable only through
  autosave.
- If a recovery snapshot exists, provide an explicit retry and discard path.
- Discard must be an intentional user operation and remove both record and
  owned payload.
- Provider cleanup failure after a valid replacement should be reported as
  cleanup pending, not as loss of the committed sketch.

## Rename and Delete

### Rename

- Check `FLAG_SUPPORTS_RENAME`.
- Call `DocumentsContract.renameDocument`.
- retain the returned document identity;
- refresh the catalog;
- report failure without modifying an unrelated local cache.

If the provider lacks rename support, either use a validated copy/delete
transaction or disable rename for that document with an explanatory UI.

### Delete

- Check delete/remove capability flags.
- Ask for confirmation through the existing UI.
- Delete the SAF document directly.
- Remove it from the catalog only after provider success.

There is no local canonical copy to reconcile.

## Saved Strokes

Use the same document and stream implementation as sketches, backed by the
top-level `Saved Strokes` directory.

Refactor `SavedStrokesCatalog` so it consumes the `SketchSet` entries rather
than filtering them by filesystem path prefix.

Saving selected strokes uses the same temporary-document write transaction as a
normal sketch.

## Autosaves

Autosaves remain under app-private storage and continue using ordinary
filesystem APIs.

- Never enumerate autosaves through SAF.
- Never include autosaves in shared-storage cleanup.
- Do not clear the latest valid autosave until a requested shared save commits.
- Continue offering recovery after a failed or interrupted shared save.

## Media Library Strategy

The media library must not be whole-tree mirrored. Use the least invasive
strategy appropriate to each content type.

### Images

For PNG and JPEG:

- enumerate with SAF metadata;
- open through a stream;
- decode from bytes;
- key disposable caches using document ID plus size/last-modified metadata.

For SVG, HDR, or other path-bound importers:

- materialize only the selected document;
- pass the temporary path to the existing importer;
- invalidate the materialization when provider metadata changes.

### Models

Models can have sidecar buffers and textures and several importers accept only
paths.

- enumerate model roots through SAF;
- materialize the selected model and required sibling/dependency documents into
  a document-ID-based cache directory;
- preserve the relative dependency layout;
- pass the materialized root to existing importers;
- treat the cache as disposable;
- never interpret cache absence as a shared deletion.

Measure whether dependency discovery can be performed from the model manifest.
If not, materialize the selected model directory, not the entire Media Library.

### Reference Videos

Attempt direct `content://` playback only in an isolated device experiment.
Do not rely on undocumented player behavior.

The supported baseline is:

- enumerate videos through SAF;
- materialize a selected video before preparing `VideoPlayer`;
- reuse it while document metadata is unchanged;
- evict it under cache pressure.

### Media Created by Open Brush

When Open Brush creates an image, model, or video:

1. generate it in app-private staging if the producer requires a path;
2. publish the completed file or directory to SAF;
3. retain the staging data only until the transaction succeeds or recovery is
   explicitly abandoned;
4. refresh the relevant catalog.

## API, Lua, Scripts, Plugins, and Fonts

API and Lua callers must use the storage backend for user-visible output.

- API/Lua code must not call Android SAF directly.
- Media created through HTTP or scripting APIs follows the same stream-write or
  staged-output transaction as equivalent UI-created media.
- Async publication failure must be observable through existing API results,
  logs, or user messaging; it must not be silently dropped.
- A directory payload created by an API remains staged until its transaction
  commits or the user explicitly discards recovery data.

Implemented ownership policy:

- Scripts are canonical in the selected SAF tree and are projected into a
  root-scoped app-private runtime generation for the existing HTML loader.
- Plugins and `Plugins/LuaModules` are canonical in the selected SAF tree and
  are projected into a root-scoped app-private runtime generation for Lua.
- Fonts are canonical in the selected SAF tree and are projected into a
  root-scoped app-private runtime generation for path-based font APIs.
- Autosaves remain app-private.

Projection generations are disposable app-private data. Canonical runtime
content and autosaves must not be placed below a materialization, staging, or
cleanup root traversed by unrelated SAF operations.

## Generated Outputs

The following remain path-staged because existing Unity/native producers need
real files or directory trees:

- exports;
- FBX/USD/glTF output;
- snapshots requiring path-only APIs;
- video capture;
- still-frame sequences;
- generated model packages.

Publication must use the same safe temporary-document principles as sketch
saves. Multi-file directories require a directory transaction with explicit
completion state.

Generated outputs do not need inbound synchronization.

### Multi-File Publication Transactions

Directory publication must preserve a complete recoverable staging payload
until every required child has committed.

- Record whether the staged payload is transaction-owned or retained as a
  disposable materialization.
- Never delete staging merely because one child succeeded.
- Retrying may skip a child only after verifying that its committed provider
  document matches the transaction.
- Decide explicitly whether each output kind merges into an existing directory
  or replaces a uniquely named directory.
- Prefer unique destination directories for exports and captures so incomplete
  output cannot be confused with an older complete result.
- Do not delete unrelated provider children as an implicit synchronization
  step.
- Retain attempt count and last structured error in the journal.
- Provide progress and cancellation without treating cancellation as commit.

Frame-sequence rules:

1. Capture all frames locally.
2. Close every frame file.
3. Complete the sequence manifest/metadata.
4. Begin publication only after capture completion.
5. Keep one directory transaction until all frames and metadata commit.
6. Do not expose metadata that claims a complete sequence before all required
   frames exist.

Model/export rules:

- Preserve relative dependency layout.
- Treat sidecar buffers and textures as part of the same transaction.
- Do not report success until the complete dependency set is present.
- On retry, preserve the source staging directory until verification succeeds.

## Google Drive Sync

`DriveSync` now enumerates `IUserStorageBackend` rather than local cache paths.
Uploads open canonical backend documents as streams. Downloads to SAF use safe
write transactions. Existing folder directions, recursion, extension filters,
low-space behavior, and non-Google-Play local behavior are retained.

SAF sync state is namespaced by Google account, Drive device root, and selected
SAF root. The ledger records confirmed content fingerprints so provider
timestamps cannot create repeat loops. Simultaneous changes preserve the SAF
canonical file and create a deterministic Drive conflict copy for two-way
Scripts and Plugins. Upload-only conflicts retain both sides and remain
deferred rather than overwriting a changed Drive revision. Absence on either
side is not propagated as deletion.

## Folder Selection and Readiness

Retain:

- the SAF folder-picker activity;
- persisted read/write URI permission;
- validation that the selected tree represents the intended Open Brush folder;
- re-selection when a grant is lost.

Change startup behavior:

- do not copy directories;
- initialize the backend;
- query the required catalog roots asynchronously;
- allow local-only application startup if the user cancels;
- disable only features that require the unavailable shared backend.

Folder selection should not block unrelated local initialization.

### Cancellation and Feature-Triggered Selection

If the user cancels or chooses `Not Now`:

- continue local-only initialization;
- do not repeatedly show the startup prompt during the same session;
- retain autosave and any explicit recovery snapshot;
- do not claim that a requested shared save completed;
- prompt again when the user invokes a feature that requires shared storage;
- allow the user to return to folder selection through settings.

If product requirements permit saving a recovery snapshot after cancellation,
label it as local recovery rather than a shared save.

### Switching Shared Roots

Treat a different selected tree as a different storage identity.

- Derive a stable, opaque root identity from the persisted tree grant.
- Namespace transaction journals and materialization caches by root identity.
- Stop accepting new work against the old root before switching.
- Recover, finish, explicitly retain, or explicitly discard incomplete old-root
  transactions; never silently retarget them to the new root.
- Clear in-memory catalog entries and query the new root.
- Do not interpret absent new-root documents as deletions from the old root.
- Materializations from the old root may be garbage-collected only when no
  recovery transaction references them.
- If the old permission remains valid, provide diagnostics sufficient to
  reconnect and recover it.

Selecting a folder with the wrong name or structure must fail validation
without replacing the currently valid root.

## Migration from the Current Branch

### Keep

- Google Play build flag and manifest integration;
- folder-picker activity;
- persisted tree permission handling;
- safe relative-path validation where display paths are still accepted;
- structured provider error handling;
- temporary SAF document creation;
- completed-write-before-replacement principle;
- app-private export staging;
- platform-scoped build configuration.

### Replace

- `FileSketchSet` Google Play conditionals with `SafSketchSet`;
- path-derived shared identities with opaque document IDs;
- path-based sketch writing with backend write transactions;
- copy-before-load with seekable descriptor streams;
- `FileSystemWatcher` expectations with catalog refresh.

### Remove

- startup whole-directory downloads;
- `copyDirectoryToPath` reconciliation;
- deletion of unmatched local children;
- preserved-path machinery;
- pending-local-path protection;
- PlayerPrefs-backed transfer records for ordinary sketch saves;
- cache deletion notifications;
- shared-to-local sketch reconciliation;
- concurrent full Media Library synchronization;
- gameplay-level `#if OPEN_BRUSH_GOOGLE_PLAY` branches superseded by backend
  dispatch.

### Development-State Migration

The current branch stores pending transfer information in PlayerPrefs and may
leave app-private cache payloads from development builds.

Before removing that implementation:

1. Determine whether any released build can contain this state.
2. If released users can have it, migrate complete, valid pending sketch
   payloads into explicit recovery snapshots or transactions.
3. If only development builds can have it, document that the pre-release state
   is unsupported and provide an explicit cleanup path.
4. Never reinterpret an old absolute cache path as a SAF document identity.
5. Do not delete old pending state until migration, successful recovery, or
   explicit cleanup is complete.
6. Retain unknown record versions and report them rather than silently deleting
   possible user work.

## Implementation Phases

### Phase 0: Device Spike

- implement detached-FD prototype;
- verify Unity IL2CPP ownership and seek behavior;
- read and write representative `.tilt` files;
- record timings and failures;
- make the direct-stream/materialization decision.

### Phase 1: Storage Backend

- define storage identities, metadata, results, and backend interface;
- implement local backend without changing existing behavior;
- select backend at runtime;
- add edit-mode tests for backend consumers.

### Phase 2: SAF Catalog and Read Path

- implement bulk Java enumeration;
- implement `SafSketchSet`;
- implement stream-backed `SafSceneFileInfo`;
- refactor Tilt zip readers for owned streams;
- load thumbnails, metadata, and full sketches;
- add refresh behavior.

### Phase 3: SAF Write Transactions

- refactor the Tilt writer to target streams;
- implement temporary-document creation;
- implement new-save commit;
- implement overwrite journal and recovery;
- preserve autosave until commit;
- implement rename and delete.

### Phase 4: Saved Strokes

- share the sketch implementation;
- remove path-prefix assumptions;
- test save/load/rename/delete.

### Phase 5: Generated Outputs

- retain local path staging;
- route final publication through the backend;
- implement transaction recovery for multi-file output where required;
- remove duplicated gameplay conditionals.

### Phase 6: Media Library

- implement SAF catalogs;
- direct-decode supported images;
- materialize path-bound images;
- materialize selected model dependency trees;
- materialize selected videos;
- add bounded cache eviction.

### Phase 7: Drive Sync Decision

- refactor Drive sync to the backend or explicitly disable it for this backend;
- test that no files are silently omitted.

### Phase 8: Remove Mirror Infrastructure

- delete inbound directory synchronization;
- delete reconciliation and preservation logic;
- delete obsolete transfer persistence;
- remove superseded Java copy jobs;
- reduce Android compile guards to the bridge/selection boundary.

## Suggested Commit Sequence

Keep commits independently reviewable:

1. `Add SAF file descriptor feasibility probe`
2. `Support stream-backed tilt archive reads`
3. `Separate user storage behind a runtime backend`
4. `Query SAF sketch metadata in one pass`
5. `Add SAF-backed sketch catalog`
6. `Load SAF sketches through seekable descriptors`
7. `Write tilt archives to storage transactions`
8. `Recover interrupted SAF sketch replacements`
9. `Serialize SAF destination mutations`
10. `Move SAF rename and delete into the backend`
11. `Use SAF storage for saved strokes`
12. `Publish generated outputs through the backend`
13. `Recover interrupted SAF directory publications`
14. `Materialize path-bound SAF media on demand`
15. `Adapt API output to the storage backend`
16. `Adapt Drive sync to the storage backend`
17. `Remove mirrored SAF cache synchronization`

If the feasibility spike fails, commit it separately with its findings, then
replace commits 6 and 7 with sparse-materialization equivalents.

## Testing Strategy

### Edit-Mode and Unit Tests

Test backend-independent logic with fake storage backends:

- successful empty listing versus failed listing;
- catalog snapshot replacement;
- failed refresh retains the prior catalog;
- opaque identity survives rename;
- duplicate display names are handled deterministically;
- transaction journal serialization and version handling;
- atomic journal replacement;
- transaction state transitions;
- replacement recovery for every interruption point;
- unknown and malformed journals are retained and reported;
- same-destination operations are serialized;
- repeated saves cannot publish out of order;
- rename locks both source and destination;
- reserved temp/backup names are hidden;
- autosave clearing occurs only after commit;
- recovery payload removal requires commit or explicit discard;
- root-scoped journals cannot be retargeted to another root;
- cache invalidation by document metadata;
- local-only roots cannot be traversed by cleanup;
- complete versus partial directory publication;
- frame-sequence metadata cannot commit before required frames;
- path traversal rejection for display-path APIs.

### Java Tests

Test:

- bulk child query;
- null cursor;
- provider exception;
- URI permission loss;
- document-ID preservation;
- descriptor detach ownership;
- temporary creation;
- rename returning a changed URI;
- delete capability;
- one successful empty query is distinct from a null cursor;
- safe cleanup of abandoned transaction documents.

### `.tilt` Compatibility Tests

For stream-written files:

- compare archive entry names with path-written files;
- verify Tilt header bytes;
- load metadata;
- load thumbnails;
- load sketch strokes;
- preserve AssetId and SourceId;
- overwrite repeatedly;
- open files in desktop Open Brush;
- open desktop-created files through SAF.

Include small, large, legacy, and malformed files.

### Device Tests

On the target Android XR device:

- select `Documents/Open Brush`;
- reject a folder with the wrong name or structure;
- cancel initial selection and continue local-only startup;
- invoke Save after choosing `Not Now`;
- trigger folder selection again from a shared-storage feature;
- switch to a different valid root with and without incomplete recovery state;
- launch with 0, 10, 100, and several hundred sketches;
- measure query and visible-thumbnail latency;
- open small and large sketches;
- save new;
- overwrite;
- repeatedly save the same sketch while an earlier operation is active;
- rename;
- delete;
- externally add, rename, and delete files;
- pause/resume;
- revoke and restore permission;
- fill storage during save;
- kill the process during each replacement phase;
- restart and verify recovery;
- explicitly retry and discard a retained recovery snapshot;
- load representative images, models, and videos;
- generate every supported output category;
- kill the process during a multi-file export publication;
- retry a partially populated model/export destination;
- publish a frame sequence and verify that its metadata is committed last;
- verify API/Lua-generated media follows the same transaction behavior.

Do not set a cache or optimization policy until these measurements establish
actual item counts, file sizes, and call frequency.

### Non-Google-Play Regression Matrix

Compile and smoke-test:

- Windows OpenXR;
- Android OpenXR;
- Quest;
- Pico;
- other existing Android variants;
- editor play mode.

Verify that the local backend preserves existing:

- paths;
- file watching;
- overwrite semantics;
- imports;
- exports;
- command-line/editor behavior;
- Google Drive sync.

### Build Validation

- compile Android with and without `OPEN_BRUSH_GOOGLE_PLAY`;
- compile Standalone;
- inspect Unity Editor logs after compilation;
- build the Google Play Android target;
- run the FD spike on device;
- check Java/IL2CPP stripping and ProGuard behavior.

## Logging

Use distinct prefixes:

```text
SAF_FD
SAF_CATALOG
SAF_STREAM
SAF_TRANSACTION
SAF_RECOVERY
SAF_MATERIALIZE
SAF_OUTPUT
```

Log:

- operation and transaction ID;
- opaque document identity in a safely abbreviated form;
- provider authority;
- requested mode;
- seekability;
- byte counts;
- duration;
- state transition;
- structured error.

Never log file contents, access tokens, or full sensitive URIs.

## Performance Rules

- Perform directory queries off the rendering path.
- Use one cursor query per listed directory.
- Retain returned document IDs.
- Do not resolve every child by walking from the tree root.
- Limit concurrent thumbnail/archive opens.
- Do not add caches before measuring repeated access.
- Materialize only a selected file or dependency directory.
- Perform large copies and writes on worker threads with progress/cancellation.
- Never block the Unity main thread waiting for a provider operation.

## Principal Risks and Mitigations

### Unity Cannot Reliably Wrap Detached Descriptors

Mitigation: sparse per-document materialization. The catalog and transaction
architecture remains valid.

### Provider Does Not Supply Seekable Descriptors

Mitigation: detect the capability and materialize that document. Do not pretend
a pipe is seekable.

### SAF Replacement Is Not Strictly Atomic

Mitigation: completed temporary document, backup rename, local transaction
journal, and deterministic recovery.

### Folder Listing Is Slow

Mitigation: one projection query, asynchronous refresh, retained successful
snapshot, visible-item thumbnail scheduling, and measured optimization.

### External Changes Are Not Immediately Reported

Mitigation: recursive provider observation for Scripts, Plugins, and Fonts,
debounced coherent projection replacement, refresh after app-authored
mutations, and resume-time refresh as a correctness fallback. Manual refresh
is recovery UI, not the routine path.

### Media Importers Require Paths

Mitigation: narrow on-demand materialization preserving dependency layout.

### Google Drive Sync Misses Unmaterialized Content

Mitigation: backend enumeration and canonical document streams. Drive sync
does not depend on materialization or projection contents.

### Save Fails While Provider Is Unavailable

Mitigation: preserve autosave and optionally create an explicit recovery
snapshot. Do not claim success.

## Acceptance Criteria

The design is ready to replace the mirrored-cache branch when:

- the FD spike passes on the target local Documents provider, or the sparse
  fallback is implemented;
- the sketchbook enumerates SAF without copying the Sketches directory;
- `.tilt` load and save work through the selected access strategy;
- canonical sketches are never truncated during writes;
- interrupted replacements recover without losing the only valid copy;
- external additions, renames, and deletions appear after refresh;
- query failure does not clear the catalog;
- autosaves remain local and survive failed shared saves;
- transaction journals are atomic, versioned, and reconstructible;
- same-destination mutations cannot commit out of order;
- root switching cannot retarget old-root recovery work;
- recovery data is removed only after commit or explicit discard;
- saved strokes use the same safe model;
- path-only outputs use bounded staging;
- multi-file outputs retain staging until the entire transaction commits;
- frame-sequence metadata cannot describe an incomplete publication;
- path-only media uses bounded materialization;
- API/Lua-generated media uses the backend;
- Scripts, Plugins, and Fonts are canonical in SAF while autosaves remain
  app-private; all remain outside unrelated SAF cleanup roots;
- Google Drive sync behavior is explicit and correct;
- mirror reconciliation and preserved-path machinery are removed;
- non-Google-Play platform behavior remains unchanged;
- target builds compile and device tests pass.

## Implementation Decisions

The implementation makes these choices explicitly:

1. The first release target is Android's local primary-storage Documents
   provider. Other providers may work when they expose the required seekable
   descriptors and mutation capabilities, but cloud-provider support is not
   claimed.
2. Failed or canceled shared saves preserve the existing app-private autosave.
   On reconnection, a marked autosave is published through a normal SAF
   transaction as a uniquely named recovered sketch. There is no second
   general-purpose recovery snapshot queue.
3. Google Play SAF sketches use ZIP-format `.tilt` documents. Legacy
   directory-format `.tilt` sketches are not enumerated by the SAF catalog.
4. Google Drive folder sync is implemented through `IUserStorageBackend` on
   SAF. Other platforms retain the local backend and legacy sync decisions.
5. Scripts, Plugins, and Fonts use provider observation, post-mutation refresh,
   and application-resume refresh. Existing manual controls remain recovery
   actions rather than required workflow.
6. Materializations are document-ID- and root-scoped disposable caches. The
   current provisional pressure limit is 512 MiB per active root and must be
   revisited using the device measurements in this plan.
7. Documents lacking provider rename/delete/remove capabilities expose
   explanatory operation failures; overwrite is disabled without rename
   support. The implementation does not fall back to truncating writes.
8. Root switching is allowed. Active work detects the identity change and
   stops; old-root journals and payloads remain root-scoped and are never
   retargeted.
9. Existing save/error UI and structured logs expose recovery and cleanup
   states. No new persistent recovery badge is added.
10. Scripts, Plugins, and Fonts are canonical in the selected SAF tree and use
    bounded root-scoped app-private projections. Autosaves remain app-private.
    Canonical runtime content and autosaves stay outside unrelated
    publication/materialization cleanup roots.
11. The detailed runtime projection, migration, provider observation, and
    Drive conflict rules are recorded in
    `google-play-saf-feature-parity-plan.md`.

Any later change to these decisions is a product change rather than an
implementation detail.
