# SAF Plan of Record

This is the plan. `saf-design-review.md` and
`saf-single-root-simplification-plan.md` hold the reasoning and the evidence
behind it; `Support/SafFdProbe/README.md` holds the device measurements. Read
those for *why*. This document is *what*.

## Decision: reduce in place, do not rebuild

Rebuilding from scratch was considered and rejected. Roughly a third of the
branch is worth keeping, and it is the part that is hardest to get right again:

- the Java bridge (`OpenBrushStorageBridge.java`, 736 lines) — proven on device,
  one-cursor directory queries, struct-of-arrays JNI marshalling;
- the fd path — `detachFd` into `SafeFileHandle` into `FileStream`, measured at
  1.6 GB/s write and confirmed seekable on a real regular file;
- the commit sequence — write temp, validate, rename canonical to backup, rename
  temp into place, delete backup;
- the media catalog changes, which carry genuine bug fixes unrelated to SAF
  (case-insensitive extension matching, spaced OBJ texture filenames, network
  video pointers);
- the Drive sync port, which parity requires.

What is wrong is concentrated and identifiable, not diffuse. Subtract it.

But **subtract in one pass, not ten shippable steps.** Nothing on this branch
has ever been run, so there is no working state to preserve between steps and no
data in any format it introduced. Order the work by what unblocks the largest
deletions, not by what keeps the branch runnable.

## The four product decisions this rests on

1. Startup is gated on folder selection. Declining exits the application.
2. The root never changes for the life of an installation.
3. Shared files are not modified externally while the application runs; a
   user-initiated refresh is enough.
4. SAF builds must reach functional parity with non-SAF builds.

## Order of work

### 1. Gate startup; delete the degraded mode

Prompt for the folder at startup. Exit on decline. Then delete:

- both `RequireSharedFolderFor` overloads, the
  `m_PendingAction` / `m_PendingCanceledAction` / `m_RequestInProgress` state
  machine, and `OnOpenBrushFolderCanceled`'s resumption logic;
- the `kLegacyStartupPromptDismissedKey` preference and its migration;
- the save-locally-when-cancelled fallback;
- the `HasOpenBrushFolder` / `Backend.IsReady` checks, which become a startup
  invariant.

Nine call sites revert from continuation-passing to straight-line code:
`SketchControlsScript.cs:4313`, `CameraPathCaptureRig.cs:120`,
`MultiCamTool.cs:643, 1765, 2016`, `ApiMethods.Utils.cs:464, 516`,
`Export.cs:131`, `SaveLoadScript.cs:472`.

First because it is the largest structural win and removes an asynchronous
re-entry point from the save and export paths.

### 2. Point every consumer at a stream

The rule: **stream from storage, unless the library cannot take a stream.**

| Consumer | Change |
| --- | --- |
| Scripts, plugins | `OpenBrushScriptLoader.LoadFile` returns `backend.OpenRead(...)`; `ScriptFileExists` becomes a listing lookup. `ApiManager.cs:202` likewise. |
| Models | SAF `IDataLoader` for UnityGLTF (replacing `FileLoader` at `ImportGltfast.cs:118`); SAF `IUriLoader` for the toolkit importer. |
| Reference images | Stream into `ImageUtils.FromImageData`. Confirm first. |
| Audio, video | Loopback HTTP handler (below). |
| Fonts | The one exception. Copy once at startup to a fixed directory. |

**The loopback handler.** `UnityWebRequestMultimedia.GetAudioClip` and
`VideoPlayer.url` take only a URL, which is the entire reason media is copied
today. Both accept `http://`. Register a raw handler on the existing
`App.HttpServer` that streams `backend.OpenRead(documentId)` to the response,
and hand Unity `http://127.0.0.1:<port>/saf/<token>/<documentId>`.

Non-negotiable, because `HttpServer.cs:53` binds `http://+:{HTTP_PORT}/` — all
interfaces, and `IsTrustedLocalBrowserRequest` is opt-in per handler:

- an unguessable per-session token, compared in constant time;
- `IsTrustedLocalBrowserRequest` applied explicitly;
- identifiers resolved only within the selected root;
- registered only on Google Play builds;
- HTTP range support and `Content-Length`, or seeking re-reads from the start.

**Threading.** `ImportGltfast.cs` sets `IsMultithreaded = true` and depends on
`IDataLoader2` reading glTF JSON off the main thread. A SAF loader reaches the
provider through JNI, so it must call `AndroidJNI.AttachCurrentThread` or fail
at runtime rather than at compile time.

### 3. Delete what streaming makes unnecessary

- `UserRuntimeContent` (1,262 lines) — the projection, `ProjectionPointer`,
  `ProjectionManifest`, `ProjectionEntry`, the generation directories and their
  cleanup, and the duplicate SHA-256 copy loop. Replaced by the startup font
  copy.
- `MigrationRecord` and `MigrationItem` — their only possible source is an
  earlier state of this unreleased branch.
- The materialization cache in `SafUserStorageBackend`: the 512 MB cap,
  `EvictMaterializationCache`, and the per-call
  `GetFiles("*", SearchOption.AllDirectories)` walk. Do not add the cache-hit
  branch; there will be nothing left to cache.

### 4. Collapse root identity to one startup check

Replace 255 references across 24 files with: persist the root URI, compare at
startup, and if it differs, discard local caches and rebuild. Roughly twenty
lines.

That removes root-scoped preference keys and namespaces, `EnsureSelectedRoot` /
`IsSelectedRootCurrent`, the `rootChanged` catalog resets, and
`CatalogScanGuard`'s root checks.

`ReselectSharedFolder` stays as a recovery path for a revoked grant, but
requires a restart. A lost root is a terminal error, not a hot swap — report it,
offer re-grant, restart. A failed query must never be read as an empty
directory.

### 5. Rewrite the write transaction small

Keep the commit sequence exactly. Change two things:

- **fsync the payload** before the rename sequence. `SafStorageTransaction.cs:682`
  currently calls plain `Flush()` while the journal at line 114 gets
  `Flush(flushToDisk: true)` five or six times a save — durable bookkeeping
  around non-durable data. Measured cost: 740 ms per GiB, so about 150 ms for a
  200 MB sketch, and net faster once the journal goes.
- **drop recovery to `testData: false`**, matching the commit path. With the
  payload fsynced, truncation is caught by the zip central directory and torn
  middles are prevented rather than detected.

Then delete the journal: `SafTransactionRecord`, `SafTransactionJournal`,
`SafTransactionState` and the journal-driven recovery pass. Replace it with
name-encoded sidecars (`MySketch.tilt.ob-bak`) and a startup sweep. No
compatibility shim is needed for the rename.

Keep `SafDestinationLocks`, payload validation, and the presence-based restore.

### 6. Collapse the publish surface

`OpenBrushStorage`'s twenty near-duplicate publish methods become one
`Publish(stagedPath, StorageArea, relativePath)`, with async as a wrapper.

### 7. Fix the two real bugs

- **No free-space accounting on the SAF path.** `SaveLoadScript.cs:490` skips
  `CheckDiskSpaceWithError` for direct SAF saves — correctly, since `m_SaveDir`
  is the wrong directory — but nothing replaced it. The commit sequence needs
  twice the sketch size transiently in shared storage.
- ~~**Silent truncation.**~~ **Withdrawn — this was wrong.** The review claimed a capped
  tree walk returned `Succeeded` with a partial list. It does not:
  `StorageTreeEnumerator` returns `StorageTreeResult.Failed` with an explicit
  message for both limits — "Storage tree exceeds the N item limit"
  (`UserStorageBackend.cs:910`) and "exceeds the N level depth limit at <path>"
  (`UserStorageBackend.cs:930`). Both fail loudly. The caps may still be worth
  raising, but there is no correctness bug here.

## Gate

Before any of this, run `AndroidSafStorage.RunFileDescriptorProbe` from a real
Google Play build. The provider half of the device gate has passed
(`Support/SafFdProbe`); the IL2CPP half — `SafeFileHandle` over a detached
descriptor under Unity's runtime — has not. Residual risk is low, because the
descriptor is a regular file, but if it fails the write path changes and steps 2
and 5 need rebasing.

## Expected outcome

Roughly 15,300 lines of production code today. The deletions above total an
estimated 5,000–7,000, against perhaps 500 lines of new code — the loopback
handler, the font copy, the startup gate and the root check.

These are estimates from reference counts, not from doing the work.

The tests already on the branch (3,881 lines) verify the result either way, and
are the reason this reduction is safe to attempt in one pass.
