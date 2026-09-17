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

## Progress

Updated 2026-09-16, late. Every batch compiled in the editor; nothing has been
run.

### Done

- **Startup gated** on folder selection, exit on decline. The degraded mode,
  its continuation slots and the nine resumable save/export/capture paths are
  gone.
- **Nothing is copied out of shared storage.** Materialization is deleted
  outright - the interface members, both implementations, the cache, the
  512MB budget, eviction, and every `Func<string>` threading it through the
  media classes. Sketches, Lua modules and glTF read as streams; OBJ, audio,
  video and the visualiser's music read over a loopback HTTP handler;
  reference images decode from bytes.
- **The runtime-content projection is deleted** - 806 lines. Scripts and
  plugins enumerate and read through the backend; `LuaManager` keeps only a
  logical root string so `ModulePaths` and the loader agree on a prefix.
- **The write path is durable.** Payload fsynced before the rename sequence,
  recovery validation dropped to structural.
- **Root identity: 255 to 121 references**, with `SafRootChangeGuard` now
  providing the single startup comparison the collapse depends on.
- **Two repo bugs fixed**: four `.meta` files with 33-character GUIDs, whose
  test files had never compiled; and the legacy migration, dead for a branch
  that has never run.

### What was learned that the plan had wrong

1. **The path-consumer audit asked the wrong question.** It asked "can the
   importer take a stream?" but not "does anything *later* need the file?"
   OBJ passes the first test and fails the second: `ImportMaterialCollector`
   keeps the asset location and reads from it at *export* time. Any future
   conversion has to ask both.
2. **Unity's URL-taking loaders are the general answer, not an audio/video
   workaround.** `UnityWebRequest`, `VideoPlayer.url`, `WWW` and
   `UnityWebRequestMultimedia` all accept `http://`, so one loopback handler
   serves OBJ, textures, audio, video and music. Only genuinely path-bound
   third-party loaders - USD, splats, TMP fonts - resist it.
3. **Supporting a path-only loader never required a cache.** It requires a
   temporary file for the duration of one load. The 512MB budget, the
   eviction policy and the LRU question were all answering a question nobody
   asked.
4. **The materialization cache never served a hit.** `MaterializeFile` had no
   reuse branch at all, so every use re-copied. The eviction ordering that
   looked like a bug was moot.
5. **"Silent truncation" was wrong.** `StorageTreeEnumerator` fails loudly on
   both caps. Withdrawn.
6. **The publish surface was barely duplicated.** Of eight entry points, six
   encode genuinely different behaviour - frame-sequence bundling, directory
   publication, unique import naming, export READMEs - and collapsing them
   would hide real differences behind flags. One was dead; two shared a body.
   The "twenty near-duplicates" claim was wrong.
7. **Deleting root scoping needs its replacement landing in the same change.**
   Five commits of collapsing shipped before the startup comparison existed.
   That window should not have been open.

### Deliberately disabled, to reinstate later

USD, FBX, PLY and Gaussian splats; SVG reference images; Quill and IMM import;
OBJ export; bulk sketch export; custom fonts. Each is a guarded early return
naming its reason, so reversing one is local work once its loader can take a
stream or a URL.

### Not done

- **The IL2CPP device gate** (step 1) - still unrun, and still the thing that
  could invalidate the write path.
- **Root identity's last 121 references.** Three different kinds: dead scan
  guards in `QuillFileCatalog`; `DriveSyncLedger`, where only the storage-root
  third of its key is constant and the account and Drive-root parts do real
  work; and `SafDestinationLocks` / `SafPublicationRecord`, which carry the
  root as data rather than control flow.
- **The journal removal** (step 10).
- **The publish surface** (step 6).

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
