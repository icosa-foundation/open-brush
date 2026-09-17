# SAF Plan of Record

This is the plan. `saf-design-review.md` and
`saf-single-root-simplification-plan.md` hold the reasoning and the evidence
behind it; the SAF probes repository (`open-brush-saf-probes`) holds the device measurements. Read
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

### Verified by test run

The EditMode suite was run in full for the first time late in the work, and it
found four regressions that compilation had not:

1. **Publication records collided.** De-namespacing the publication directory
   turned a foreign record into a hard failure, because
   `GetPendingTopLevelNames` throws on a `RootId` mismatch rather than skipping
   it. Five export tests failed. Reverted. This is the same reasoning that was
   *correct* for the Drive ledger, where a stale record is merely misleading -
   the shapes matched, the premises did not.
2. **Quill listed nothing.** Gating Quill import also stopped the catalog
   listing. The gate was unnecessary: `QuillFileInfo` only reads a name, size
   and timestamp, all of which the storage listing already carries.
3. **Sound clips had no usable path.** `AbsolutePath` was being given a
   library-relative path; the local backend's document id serves instead.
4. **Seeding never re-ran after a folder change.** The per-root preference key
   names had been providing that; `SafRootChangeGuard` now clears the seeding
   records alongside the directories it discards.

Nine further test failures were tests of behaviour deliberately removed, and
were deleted with it.

Final state: 1123 EditMode tests, 443 failing, of which 416 are `Autodesk.Fbx`
(a missing native library, environmental) and the remaining 27 are pre-existing
failures in code this work did not touch - including two in test files whose
`.meta` GUIDs were repaired here, so they are running for the first time.
No SAF regression remains.

**The lesson worth keeping:** twenty-five commits went in on "compiles clean".
Compilation caught none of the four bugs above. `unity command run_tests
--mode EditMode` was available throughout.

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
- **Root identity collapsed to one startup comparison**, in
  `SafRootChangeGuard`. 99 references remain across `Assets` (counting
  `RootIdentity` and the whole word `RootId`), down from 255 at the start.
- **Two repo bugs fixed**: four `.meta` files with 33-character GUIDs, whose
  test files had never compiled; and the legacy migration, dead for a branch
  that has never run.
- **The file descriptor no longer crosses JNI.** The gate below failed, so
  `OpenBrushStorageBridge` keeps a table of open `FileChannel`s keyed by an int
  handle and `SafDocumentStream` issues positioned reads and writes against it.
  Buffered 64 KiB ahead and 256 KiB behind, because each crossing is dear enough
  that a zip central directory read a few bytes at a time would dominate.
  `FileStream.Flush(flushToDisk: true)` has no equivalent on a stream whose
  descriptor is unreachable, so the payload fsync is now an `ISyncableStream`
  over the channel's `force(true)`.
- **Free space is checked on the SAF save path.** See step 7.

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

- **A device run of any of this.** The gate below has been answered, but the
  branch as a whole has still never executed on hardware. `RunStorageStreamProbe`
  runs at startup on debug builds and exercises the channel path end to end -
  create, write a Tilt archive, seek, validate, reopen by document URI, read
  every entry back, delete - so the first build says whether the replacement
  holds.
- ~~**Root identity's last references.**~~ **Closed: there is nothing left to
  remove.** The earlier description of them was wrong on both counts. It named
  dead scan guards in `QuillFileCatalog`, which has none; and it did not mention
  `SafStagedOutputPublisher`, which holds 20 — more than any other file. Of 99
  references, 26 are test fixtures and 71 are production. Those divide into:

  - the interface member and its two implementations (3);
  - lock and cache keys, where the root is one component of a key rather than a
    comparison;
  - mid-operation staleness checks, which capture `RootIdentity` and re-read it
    after a long operation. These are live despite the root being fixed for an
    installation: `ReselectSharedFolder` can still fire after startup — it is the
    recovery path for a revoked grant — and it asks the user to restart without
    forcing it, so the root can move underneath work already in flight;
  - the publication records, which persist across runs. Removing their root
    comparison was tried, broke `GetPendingTopLevelNames`, and was reverted;
    `GetPublicationDirectory` now carries a comment saying why.

  `DriveSyncLedger`'s two are as previously described: only the storage-root
  third of its key is constant, and the account and Drive-root parts do real
  work.
- ~~**The journal removal**~~ (step 5) **done, though not as written.** The plan
  said to delete `SafTransactionRecord`, `SafTransactionJournal` and
  `SafTransactionState`. Two of the three earn their place: the record is live
  in-memory state that the transaction and recovery both read, and the state enum
  labels the commit sequence in the logs, which is where anyone debugging an
  interrupted save will start. What was actually deletable was the schema around
  them — six fields that only mattered across a process restart, and the
  branches that read them. `SafTransactionJournal` is renamed `SafPrivatePaths`,
  since what survives is two path helpers and not a journal.
- **The publish surface** (step 6) - and see the correction above: it is
  smaller and less duplicated than the plan claimed, so this may not be worth
  doing at all.

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

- ~~**fsync the payload**~~ **Done.** The transaction was calling plain `Flush()`
  on the payload while the journal was fsynced five or six times a save —
  durable bookkeeping around non-durable data. Measured cost: 740 ms per GiB, so
  about 150 ms for a 200 MB sketch, and net faster now the journal writes are
  gone. Since the replacement stream has no `FileStream.Flush(flushToDisk: true)`
  to call, this goes through `ISyncableStream` to the channel's `force(true)`.
- ~~**drop recovery to `testData: false`**~~ **Done.** With the payload fsynced,
  truncation is caught by the zip central directory and torn middles are
  prevented rather than detected.

**Still open:** deleting the journal types. Nothing is serialized to disk any
more, but `SafTransactionRecord`, `SafTransactionJournal` and
`SafTransactionState` survive as in-memory state plus two path helpers
(`GetRecoveryRootDirectory`, `GetStableId`). The name-encoded sidecars
(`MySketch.tilt.ob-bak`) and startup sweep are in place, so what remains is
removing the types. No compatibility shim is needed.

Keep `SafDestinationLocks`, payload validation, and the presence-based restore.

### 6. Collapse the publish surface

**Probably not worth doing — see the correction under "What was learned".** The
count was wrong: there are five public entry points on
`SafStagedOutputPublisher`, not twenty on `OpenBrushStorage`, and most encode
genuinely different behaviour — frame-sequence bundling, directory publication,
unique import naming, export READMEs. Collapsing them to one
`Publish(stagedPath, StorageArea, relativePath)` would hide real differences
behind flags. Left here so the decision is recorded rather than silently
dropped.

### 7. Fix the two real bugs

- ~~**No free-space accounting on the SAF path.**~~ **Done.** Re-enabling the
  existing check would not have worked: `FileUtils.HasFreeSpace` ignores its path
  argument on Android and stats `Application.persistentDataPath`, so it measures
  app-private storage whatever volume the folder is on — and under SAF that
  folder can be an SD card. It was also being handed `m_SaveDir`, which on this
  path is the local staging directory rather than the destination.

  The provider is asked instead. There is no path to stat, and
  `openFileDescriptor` refuses a directory document, so `getAvailableBytes`
  creates a throwaway document in the Open Brush folder, runs `fstatvfs` on its
  descriptor and deletes it. A provider not backed by a local filesystem cannot
  answer and returns -1; the caller allows the save rather than blocking on a
  number it could not obtain, which is what `FileUtils` already does on platforms
  where the query is unavailable.

  **Still open, found while fixing it:** SAF exports check staging, not the
  destination. `App.UserExportPath()` returns `LocalExportStagingPath` under SAF,
  so `SketchControlsScript.cs:4838` correctly checks the volume the export stages
  on, but the publish into the shared folder is unchecked. Non-SAF has no staging
  step, so its one check covers the destination. A parity gap.
- ~~**Silent truncation.**~~ **Withdrawn — this was wrong.** The review claimed a capped
  tree walk returned `Succeeded` with a partial list. It does not:
  `StorageTreeEnumerator` returns `StorageTreeResult.Failed` with an explicit
  message for both limits — "Storage tree exceeds the N item limit"
  (`UserStorageBackend.cs:910`) and "exceeds the N level depth limit at <path>"
  (`UserStorageBackend.cs:930`). Both fail loudly. The caps may still be worth
  raising, but there is no correctness bug here.

### 8. Optional: a shared direct ByteBuffer for writes

**Only if a real performance problem shows up.** This is an optimisation, not a
correctness gap, and nothing should be built on the assumption that it is needed.

Writes currently cap at 28—31 MB/s, flat from 256 KiB chunks upwards. That is
not storage: the same document written from Java alone reached 1.6 GB/s. The cost
is Unity marshalling the `byte[]` argument across JNI on every call, and it is
paid per write regardless of how the bytes are batched. At that rate a 20 MB
sketch spends about 0.7 s in marshalling and a 200 MB one about 7 s.

The fix removes the crossing rather than making it cheaper. C# allocates native
memory, wraps it once with `AndroidJNI.NewDirectByteBuffer`, and hands Java the
resulting `java.nio.ByteBuffer` when the channel opens. Thereafter a write is a
`Marshal.Copy` into that memory — a plain memcpy, no JNI — followed by an
all-primitives call giving Java the byte count. `FileChannel.write` reads
straight out of the same memory. One copy in total, the same as today, but no
marshalling. `SafDocumentStream`'s write-behind buffer can be that native
region, so no copy is added. Reads can use it in reverse, which would also stop
allocating a Java `byte[]` per 64 KiB refill.

Two reasons it is not done, both worth weighing before starting:

- It is roughly 150 lines of hand-rolled JNI with manual global-reference
  lifetimes, and **none of it can be verified without a device**.
- It would sit on a channel path that has itself never run on hardware. If
  something is wrong on the phone, that is two new layers to debug at once
  instead of one, and there is no measurement to say whether the second layer
  helped.

So: get a build onto a phone, confirm `SAF_STREAM`, find out whether saves are
actually too slow, and only then decide. A per-channel buffer also costs 256 KiB
of native memory per open stream, which matters if glTF loading opens many at
once; a small pool is the answer if so.

## Gate — answered, and it failed

The provider half passed in the SAF probes repository
(`open-brush-saf-probes`). The IL2CPP half did not. The plan judged the residual
risk low on the grounds that the descriptor is a regular file. That reasoning was
wrong: the descriptor was never the problem.

Every variant segfaults in `libil2cpp.so`, with the fault address consistently at
**fd + 4** (fd 401 faults at 0x195, 403 at 0x197, 407 at 0x19b, 396 at 0x190):

| variant | result |
| --- | --- |
| `SafeFileHandle` alone | constructs, then crashes on `Dispose()` |
| `new FileStream(handle, access)` | crashes at construction — what the branch did |
| buffered `FileStream` overload | crashes at construction |
| `ownsHandle: false` | crashes at construction |

Not stripping — a `link.xml` made no difference. Not a bad overload. And
`RandomAccess`, which would have sidestepped `FileStream` entirely, is not in
Unity's profile.

The contingency the plan named — "if it fails the write path changes" — is what
happened. The descriptor now stays in Java behind a `FileChannel` and C# issues
positioned reads and writes against it; see the entry under Done.
`RunFileDescriptorProbe` is accordingly `RunStorageStreamProbe`, exercising the
channel path rather than a descriptor.

What the same probe measured, on a Nothing Phone (3a):

- reads 327 MB/s
- writes flat at 28—31 MB/s from 256 KiB chunks up to 4 MiB
- fsync 30 ms for 32 MiB

The write figure is not storage. The same document written from Java alone
reached 1.6 GB/s; the ceiling is marshalling the `byte[]` argument across JNI.
See the optional step below.

## Expected outcome

Roughly 15,300 lines of production code today. The deletions above total an
estimated 5,000–7,000, against perhaps 500 lines of new code — the loopback
handler, the font copy, the startup gate and the root check.

These are estimates from reference counts, not from doing the work.

The tests already on the branch (3,881 lines) verify the result either way, and
are the reason this reduction is safe to attempt in one pass.
