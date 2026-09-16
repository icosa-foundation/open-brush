# SAF Single-Root Simplification Plan

**Superseded as a plan by `saf-plan-of-record.md`.** This document remains the
supporting analysis — the product decisions, the measurements and the findings,
including corrections made along the way. The plan of record is the short
version that should be worked from.

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

### Audit of every path consumer

Each importer was checked for a stream seam rather than assumed to need a file.

| Consumer | Seam | Verdict |
| --- | --- | --- |
| MoonSharp / Lua | `ScriptLoaderBase`; `OpenBrushScriptLoader` already custom, `LoadFile` returns a `Stream` | **Can stream** |
| TiltBrushToolkit glTF (Icosa/Poly) | `IUriLoader` → `IBufferReader`, a random-access read interface; `TiltBrushUriLoader` already custom | **Can stream** |
| UnityGLTF (user models) | `IDataLoader` / `IDataLoader2`; currently the stock `FileLoader` (`ImportGltfast.cs:118`) | **Can stream** |
| Reference images | `ImageCache.LoadImageCache(FilePath, ...)` behind `EnsureMaterialized()`; `ImageUtils.FromImageData(byte[])` already exists | **Probably can stream** — verify |
| Fonts | `SvgTextUtils.cs:30` takes a directory path; Unity has no runtime `byte[]` → `Font` | **Needs files** |
| Audio | `UnityWebRequestMultimedia.GetAudioClip(url, …)` (`SoundClip.cs:332`) | **Needs a file or URL** |
| Video | `m_VideoPlayer.url = …` (`ReferenceVideo.cs:328`) | **Needs a file or URL** |

The pattern is consistent: every importer was designed with a loader
abstraction, and in each case this branch bypassed it by materializing a path.
Two of the three already have a *custom* implementation in this repository —
the seam was built, then fed a file path.

There is an unfortunate inversion in what remains. The content that genuinely
cannot stream is the largest: audio and video. Models, scripts and images —
which can stream — are what currently dominate the materialization cache, while
the multi-gigabyte media that has to be copied is exactly what a 512 MB budget
cannot hold.

So the work has two halves:

1. Convert models, scripts and images to streams. This removes most cache
   pressure and, for models, avoids copying a large `.glb` before every import.
2. Size the remaining cache for what is left — audio and video — where copying
   is currently unavoidable.

**Threading caveat.** `ImportGltfast.cs` sets `gltf.IsMultithreaded = true` and
relies on `FileLoader` implementing `IDataLoader2` so the glTF JSON can be read
off the main thread. A SAF-backed loader reaches the provider through JNI, and
JNI calls from a non-Unity thread require `AndroidJNI.AttachCurrentThread`.
Any SAF `IDataLoader` must handle that explicitly or the multithreaded path will
fail at runtime rather than at compile time.

**Remaining conversions within the glTF loaders.**
`TiltBrushUriLoader.LoadAsImage` uses `File.ReadAllBytes(Path.Combine(...))` and
`GltfFileInfo(string path)` reads the JSON header with `File.ReadAllText` or
`GlbParser.GetJsonChunkAsString(path)`. Both need stream overloads before the
Icosa path is fully stream-backed. `OpenBrushAudioImportContext.GltfDirectory`
is also a directory path.

**Audio and video are not symmetric, despite both being `MediaPlayer` content.**
Android's `MediaPlayer.setDataSource(context, uri)` accepts `content://` URIs
for both, but what Open Brush needs back from each differs:

- **Video** only needs pixels. `ReferenceVideo.cs:64` uses `VideoPlayer.texture`
  to draw onto a widget. A native `MediaPlayer` rendering into a
  `SurfaceTexture` and handing Unity an external texture is the standard Android
  video-plugin pattern and preserves everything Open Brush actually uses. So if
  `MediaPlayer` accepts the URI, large video need never be copied.
- **Audio** needs samples inside Unity's mixer. `VisualizerScript.cs:129-130`
  calls `m_AudioSource.GetOutputData` and `GetSpectrumData(..., BlackmanHarris)`
  to drive the visualiser, and `SoundClip` needs `clip.length` and seekable
  `.time` for scrubbing. A `MediaPlayer` playing to the system mixer gives Unity
  neither, so it would cost the visualiser and spatialised audio.

Audio could in principle stream via `AudioClip.Create(..., stream: true,
PCMReaderCallback)`, but that callback wants PCM, so compressed formats would
need a decoder implemented in-process. That is real work for little benefit:
audio files are single-digit to low-hundreds of megabytes, whereas video is
where gigabytes live. **Materialize audio; pursue the native path only for
video.**

**Measured 2026-09-16.** Check 15a passed: Android's `MediaPlayer` played
`Media Library/Videos/animated-logo.mp4` directly from its `content://` URI
(duration 3435 ms) on a Nothing Phone (3a), Android 16. So the capability is
real — large video *can* be rendered from SAF without copying, via a native
`MediaPlayer` into a `SurfaceTexture` exposed to Unity as an external texture.

That is a capability result, not a recommendation. Against it: `ReferenceVideo`
uses `VideoPlayer.texture`, `isPlaying`, `Play`, `Pause`,
`GetDirectAudioMute` and `GetDirectAudioVolume`, so a native replacement has to
reimplement playback control, per-source audio and lifetime management — a
meaningful plugin, and one that cuts against the simplification this plan is
otherwise pursuing. For it: it is the only way to avoid copying multi-gigabyte
video into app-private storage.

Decide it on whether users actually import very large video. Open Brush's own
camera-path exports are a few hundred kilobytes and the seeded sample is 1.5 MB;
imported 4K footage is the case that would justify the work. Until that is
known, materialize video and revisit.

Check 15b skipped — no audio in the folder. It is informational either way,
since the audio conclusion rests on Unity's mixer requirements rather than on
`MediaPlayer`'s capabilities.

### Serve media over the existing loopback HTTP server

The copying of audio and video is not a SAF limitation. The device probe
measured a SAF descriptor reading at 1024 MiB in 258 ms; the bytes are
available and the descriptor is an ordinary seekable regular file.

The constraint is Unity's API surface. `UnityWebRequestMultimedia.GetAudioClip(
url, audioType)` (`SoundClip.cs:332`) and `VideoPlayer.url`
(`ReferenceVideo.cs:328`) both accept only a **URL string** — not a stream, not
a byte array, not a descriptor. So the current implementation reads every byte
out of SAF, writes every byte into an app-private file, and hands Unity a
`file://` URL to the copy, which Unity then reads again. The copy exists purely
to convert bytes we already hold into a path an API will accept.

Both APIs do, however, accept `http://`. Open Brush already runs an HTTP server
with the needed extension points:

- `HttpServer.AddRawHttpHandler(path, Func<HttpListenerContext,
  HttpListenerContext>)` (`HttpServer.cs:183`);
- `HttpServer.IsTrustedLocalBrowserRequest(request)` (`HttpServer.cs:210`);
- `ApiManager.cs:102` already registers a raw handler (`/cameraview`).

So register a handler that streams a SAF document straight from
`backend.OpenRead(documentId)` to the response, and hand Unity:

```text
http://127.0.0.1:<port>/saf/<token>/<documentId>
```

No copy, no second file, no eviction. One handler replaces both the audio
materialization and the native `SurfaceTexture` video plugin considered above;
that plugin proposal is superseded, being strictly more work for a subset of
the benefit.

**Security. This is not optional.** `HttpServer.cs:53` binds
`http://+:{HTTP_PORT}/` — all interfaces, not loopback — so the server is
reachable from the local network. `IsTrustedLocalBrowserRequest` is opt-in per
handler rather than automatic, and even it only establishes that a request is
loopback and local, which on Android any other installed application satisfies.
A handler that serves arbitrary documents by identifier would therefore be a
file-disclosure surface for every app on the device and, absent the trust check,
for the local network.

Requirements:

- an unguessable per-session token in the path, rejected by constant-time
  comparison;
- `IsTrustedLocalBrowserRequest` applied explicitly in the handler;
- document identifiers resolved only within the selected root, never accepted as
  arbitrary paths;
- the handler registered only on Google Play SAF builds.

**Also required for correctness:**

- HTTP range request support, so seeking and scrubbing do not re-read from the
  start. The descriptor is seekable, so this is a `lseek` plus a bounded copy.
- `Content-Length` from the document metadata the directory listing already
  returns.

**What this leaves.** Fonts become the only content needing a local file, since
Unity offers neither a `byte[]` → `Font` path nor a URL-based font loader. They
are a few megabytes, copied once at startup into a fixed directory.

### The resulting model

| Content | Mechanism |
| --- | --- |
| Sketches | `OpenRead` — streamed |
| Scripts, plugins | `OpenRead` — streamed, through the existing `OpenBrushScriptLoader` seam |
| Models | `OpenRead` — streamed, through a SAF `IDataLoader` / `IUriLoader` |
| Reference images | `OpenRead` — streamed, pending confirmation |
| Audio, video | `OpenRead` — streamed over the loopback HTTP handler |
| Fonts | materialized once at startup, small fixed area, never evicted |
| Captures, exports | `BeginWrite`, then publish |

One mechanism — stream from storage — plus a single small font directory. No
materialization cache, no eviction policy, no pin flag, no per-type policy.

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
- `MigrationRecord` is deleted, not retained. See "Nothing On This Branch Has
  Ever Been Run" below: its only possible source is an earlier state of this
  unreleased branch.

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

**Measured 2026-09-16, and affordable.** `Support/SafFdProbe` check 13 timed a
full `fsync` of 1 GiB through a detached SAF descriptor at 740 ms on a Nothing
Phone (3a), Android 16. A realistic 200 MB sketch therefore costs roughly
150 ms — clearly affordable once per save, and set against the five or six
journal fsyncs `saf-transaction-journal-removal-plan.md` removes, saves get
faster overall. Write throughput was 1024 MiB in 624 ms.

Check 14's read figure (3954 MB/s) should not be quoted as a recovery cost
floor: the file had just been written and was served from page cache. Recovery
cost is dominated by decompression rather than I/O in any case, which is the
reason for removing the deep check rather than optimising it.

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

2. **The materialization cache should be deleted, not fixed.** Three findings
   below were written when models, audio and video were all materialized. With
   models and images streamed through their loader seams and media streamed over
   the loopback handler, nothing remains in the cache except fonts, which are
   copied once at startup into a fixed directory that is never evicted. All
   three then become deletions rather than repairs:

   - *The cap is the wrong shape.* `EvictMaterializationCache` caps at a
     hardcoded 512 MB with no free-space awareness, and a single 500 MB model
     nearly exhausts it.
   - *It never serves a hit.* `MaterializeFile`
     (`SafUserStorageBackend.cs:427`) has no cache-hit branch — no
     `if (File.Exists(destination) && upToDate) return destination`. It
     unconditionally re-copies from SAF and overwrites through `File.Replace`
     on every call, so the budget is a staging directory with an eviction
     policy rather than a cache.
   - *Every materialization walks the whole cache.* `GetFiles("*",
     SearchOption.AllDirectories)` plus a `Sum` of lengths runs on each call.

   If the streaming conversions are staged rather than done together, add the
   cache-hit branch as an interim fix — keyed on `DocumentId`, `Size` and
   `LastModified` from the directory listing — and delete the whole thing at the
   end. Do not invest in the eviction policy.

   An earlier draft attributed the poor eviction ordering to `/data` being
   mounted `noatime` (confirmed on a Nothing Phone (3a):
   `f2fs rw,lazytime,...,noatime,...`). That reasoning was wrong: `noatime`
   suppresses only *implicit* atime updates on read, and
   `SafUserStorageBackend.cs:478` sets it explicitly via
   `File.SetLastAccessTimeUtc`. The ordering was materialization order because
   there was no reuse to record.

3. **No free-space accounting on the SAF path** still applies, and matters more
   once copying stops hiding it. `SaveLoadScript.cs:490` reads
   `if (!directSafSave && !FileUtils.CheckDiskSpaceWithError(m_SaveDir))`: the
   check is correctly skipped for direct SAF saves, because `m_SaveDir` is not
   where the sketch lands, but nothing replaced it. The commit sequence needs
   twice the sketch size transiently in shared storage.

4. **Tree enumeration truncates silently.** `StorageTreeQuery` defaults to
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

## Nothing On This Branch Has Ever Been Run

Recorded 2026-09-16: the branch has never been run, and no build from it has
shipped. There is no installed base holding data in any format it introduced.

This removes a class of work that is otherwise invisible, because it looks like
ordinary robustness.

### Dead upgrade paths

**Runtime content migration.** `UserRuntimeContent`'s `MigrationRecord`,
`MigrationItem`, `CreateMigrationRecord`, `IsValidMigrationRecord`, the
hash-verified copy loop and the `CanonicalCopiesComplete` /
`LocalCleanupComplete` state exist to move app-private `Scripts`, `Plugins` and
`Fonts` into the SAF tree. `m_LegacyRoot` defaults to
`LocalUserStorageBackend.GetAreaRoot` (`UserRuntimeContent.cs:349`), which on a
Google Play build is app-private storage. The only way to have content there is
to have run an earlier state of *this* branch, when those trees were
deliberately app-private — an acknowledged regression that
`google-play-saf-feature-parity-plan.md` then fixed. Nobody has. Delete it, and
with it the SHA-256 verification that only ever guarded this transfer.

An earlier draft of this plan listed `MigrationRecord` under "what stays". That
was wrong.

**Legacy preference migration.** `AndroidStorageManager.cs:28, 55` reads and
deletes `GooglePlayStorage.StartupPromptDismissed`. No device has that key.

### Dead format tolerance

`google-play-saf-feature-parity-plan.md` invariant 14 requires that "unknown
projection manifests, Drive ledgers, and transaction-journal versions are
retained and reported rather than discarded". That is forward-compatibility with
formats nothing has ever written. `DriveSyncLedger.cs:27, 229-236` carries a
`kVersion`, a mismatch branch and a "retained" diagnostic; `UserRuntimeContent`
carries three more `Version` fields.

Reduce each to nothing, or to a bare assertion. Version negotiation can be added
when there is a shipped version to negotiate with.

This also frees the journal-removal plan's rename of sidecars from
`.ob-<guid>.tmp` to `<target>.ob-tmp`: no compatibility shim, no recovery pass
for the old naming.

### Freedom of sequence

The ordering below was built so each step ships independently and the branch
works between steps. That constraint does not apply. The steps can be collapsed
into a single pass, and the interim cache-hit branch described under Scale can
be skipped entirely — delete the materialization cache rather than repairing it
on the way past.

### What is still real

Being unreleased removes compatibility with *this branch's* formats. It does not
remove compatibility with everything:

- **The shared folder layout must stay compatible.** Released builds put
  `Media Library`, `Plugins`, `Scripts`, `Sketches`, `Snapshots` and `Videos`
  under `Open Brush`, and users have data there now — a Nothing Phone (3a)
  checked during this review had exactly that tree, populated. The SAF layout
  must continue to match it.
- **`.tilt` format handling stays**, including the ZIP package migration
  preserved from `main`. Those are real user sketches.
- **Crash recovery stays.** It protects a save interrupted at runtime, not an
  upgrade across versions.
- **The correctness fixes stay** — the payload fsync, and free-space accounting
  on the SAF path.

### Consider rebuilding rather than subtracting

With no data to preserve, no shipped format and no working state to protect
between steps, the usual argument for incremental refactoring is weak. The
findings in this document amount to a specification for a substantially smaller
implementation: one storage backend, one streaming rule with a single font
exception, one commit sequence, no root namespacing, no degraded mode, no
projection, no materialization cache, no migration.

Rebuilding the storage layer against that specification, keeping the parts
proven on device — the fd read/write path, the rename sequence, the Java
bridge — may well be less work than subtracting from roughly 15,000 lines while
keeping it consistent at each step. That is a judgement call rather than a
recommendation, and the tests already on the branch make either route
verifiable.

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
3. Fsync the payload before the rename sequence and drop recovery to
   `testData: false`. Measured at roughly 150 ms for a 200 MB sketch, and net
   faster once the journal fsyncs go.
4. Redirect `OpenBrushScriptLoader` and `ApiManager`'s startup-script check to
   `OpenRead`, removing the reason the projection exists.
5. Add the loopback media handler, with its token and range support, and point
   `SoundClip` and `ReferenceVideo` at it.
6. Convert models and reference images to their loader seams.
7. Delete the materialization cache; replace the rest of `UserRuntimeContent`
   with a startup font materialization.
8. Remove the resume refresh; add the user-initiated refresh if wanted.
9. Collapse root namespacing to the startup URI comparison.
10. Then the journal removal.

Steps 2, 3 and 5 are independent of each other and carry most of the value.
Step 7 is only safe once 4, 5 and 6 are done, since it removes the fallback
they replace.

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
