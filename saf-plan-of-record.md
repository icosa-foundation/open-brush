# SAF on Android — Plan of Record and Handover

Branch `feature/saf-google-play-fd-backed`, PR
[#1120](https://github.com/icosa-foundation/open-brush/pull/1120).

Google Play forbids `MANAGE_EXTERNAL_STORAGE` for an app like this, so Play
builds cannot reach `/sdcard/Open Brush` the way sideloaded builds do. This
branch replaces direct filesystem access with the Storage Access Framework: the
user grants one folder at startup, and every user-visible file is read and
written through the provider.

This is the single live document. Six earlier planning documents were removed
from the pull request rather than shipped to `main` — two of them described
deleted subsystems as implemented, and one was 1,365 lines built on a design the
device disproved. They remain in this branch's history.

---

## 1. Status

Last updated **2026-09-17**, after the first device run.

### Proven on hardware

On a Nothing Phone (3a), release build, sideloaded:

- **The stream works.** `App.Awake` read the user config out of the shared
  folder through `SafDocumentStream` → `SafJniDocumentChannel` → a Java
  `FileChannel`, under IL2CPP, and got the bytes back. **No SIGSEGV.** This is
  the point of the whole design; see §2.
- **Folder selection works.** `OpenBrushStorageActivity` launches the system
  picker and returns.
- **The grant persists.** After a restart the picker did not reappear, so the
  persistable URI permission survives and the startup gate passes without
  re-prompting.

### Not yet verified on hardware

Everything else. In particular **no save has ever been performed on a device.**
The write path — temp document, fsync, rename sequence — has only ever run in
EditMode against fakes.

`RunStorageStreamProbe` exercises the whole channel path at startup (write a
Tilt archive, seek, validate, reopen by document URI, read every entry back,
delete) but it is gated on `Debug.isDebugBuild`, so it needs a **Development**
build. See §3.

### Fixed after that run, not yet re-tested

- `AndroidSafStorage.GetActivity` rebuilt an `AndroidJavaClass` for
  `UnityPlayer` on every call, disposed it, and leaked the activity it returned.
  The first lookup succeeded and every later one failed with *"Field
  currentActivity or type signature not found"*, which silently took out
  seeding, all five catalog queries, the default-destination checks and
  transaction recovery. The activity is now resolved once and kept.
- The channel crossed JNI with `byte[]`. Java's `byte` is signed, so Unity
  converted element by element and logged a deprecation warning on **every read
  and write**. It now uses `sbyte[]`, with `Buffer.BlockCopy` at the boundary.

### The shape of the remaining risk

The read path is proven end to end. The write path is not, and it is the part
where a mistake costs someone their sketch rather than an error message.

---

## 2. The finding that shapes everything

The original design detached the file descriptor with `ParcelFileDescriptor
.detachFd()` and handed it to C# as a `SafeFileHandle` inside a `FileStream`.
**That segfaults IL2CPP.** Every variant, reproducibly, fault address always at
**fd + 4**:

| variant | result |
| --- | --- |
| `SafeFileHandle` alone | constructs, then crashes on `Dispose()` |
| `new FileStream(handle, access)` | crashes at construction — what the branch did |
| buffered `FileStream` overload | crashes at construction |
| `ownsHandle: false` | crashes at construction |

Not managed stripping — a `link.xml` made no difference. `RandomAccess`, which
would have sidestepped `FileStream`, is not in Unity's profile.

**So the descriptor never crosses JNI.** `OpenBrushStorageBridge` keeps a table
of open `FileChannel`s keyed by an `int` handle; `SafDocumentStream` issues
positioned reads and writes against it. `FileStream.Flush(flushToDisk: true)`
has no equivalent on a stream whose descriptor is unreachable, so the payload
fsync goes through `ISyncableStream` to the channel's `force(true)`.

Each JNI crossing is dear enough that unbuffered access would dominate — a zip
central directory is read a few bytes at a time — hence a 64 KiB read-ahead and
a 256 KiB write-behind, at most one holding data at a time.

Measured on the same device:

- reads **327 MB/s**
- writes flat at **28–31 MB/s** from 256 KiB chunks up to 4 MiB
- fsync **30 ms** for 32 MiB

The write figure is not storage. The same document written from Java alone
reached 1.6 GB/s; the ceiling is JNI argument marshalling. See §4.

Device measurements live in the `open-brush-saf-probes` repository.

---

## 3. How to build and test this

**This section exists because getting a testable build is the single most
time-consuming part of working on this branch.** Read it before you start.

### Which build has SAF active

Scoped storage is selected by `-btb-scoped-storage`, which sets the
`OPEN_BRUSH_SCOPED_STORAGE` define. It is **not** a distribution flag — nothing
about it concerns uploading to Play, and Play uploads take only `.aab` files.

Three matrix entries in `.github/workflows/build.yml` carry it:

| flavour | artefact | notes |
| --- | --- | --- |
| Android AndroidXR | `.aab` | needs bundletool to install |
| **Android Viewer OpenXR** | **`.apk`** | **the one to sideload** |
| Android Viewer AndroidXR | `.aab` | needs bundletool |

Plain *Android OpenXR* — the normal Quest build — does **not** have it, so SAF
is inactive there. Testing with it will mislead you.

### Triggering a build

`build.yml`'s `configuration` job gates everything:

- a **push** builds only if the head commit message contains **`[CI BUILD]`**;
- a **pull_request** event builds unconditionally — but only if the PR is
  mergeable (see §6);
- **Development** flavours, which are the only ones where the startup probe
  runs, need **`[CI BUILD DEV]`** — `[CI BUILD]` alone gives release only.

So: **`[CI BUILD DEV]` is almost always what you want.** A build takes ~45
minutes.

### Installing and reading the log

```bash
adb install -r "<path>/com.Icosa.OpenBrush-<branch>.apk"
adb shell monkey -p foundation.icosa.openbrushviewerfeaturesafgoogleplayfdbacked \
    -c android.intent.category.LAUNCHER 1
```

The package name is branch-suffixed, so it installs **alongside** any existing
Open Brush rather than replacing it. No data is at risk.

Three traps that will waste your time:

1. **Enlarge the log buffer first: `adb logcat -G 16M`.** logcat keeps separate
   ring buffers per source. This device is chatty enough that Unity's `main`
   buffer rolls within a minute or two while `system` entries survive, so the
   app's own output vanishes while window-manager noise remains.
2. **Never put `2>/dev/null` on an adb command.** It hides *"device not
   found"*, and a disconnected phone then looks identical to a grep that
   matched nothing.
3. **Filter to the app.** `adb logcat -d | grep ' Unity'` — the SAF tags are
   `SAF_STORAGE`, `SAF_CATALOG`, `SAF_TRANSACTION`, `SAF_RECOVERY`,
   `SAF_SOUND`, `SAF_STREAM`.

The line that confirms the design, on a Development build:

```
SAF_STREAM Channel-backed Tilt archive passed (N bytes)
```

### Running the EditMode tests

With the Editor open:

```bash
unity command run_tests --mode EditMode --filter "TiltBrush.TestSafDocumentStream"
```

Do **not** pass `--filter_type class`; it silently matches nothing and reports
`0/0 passed`, which reads like success.

The SAF suites and their runtimes: `TestSafDocumentStream` 17 (~13 s),
`TestSafExportNaming` 10, `TestSafRecoverySweep` 5, `TestGaussianCapturePublication`
4, `TestSafQuillCatalog` 3, `TestSafSketchMutationGuard` 2.

A full EditMode run leaves ~443 failures, of which 416 are `Autodesk.Fbx`
missing a native library and the rest pre-existing in untouched code. It also
**deletes `Assets/Resources/PerformanceTestRun*.json`** from the working tree;
those are now gitignored, so ignore them.

### Type-checking the Android path without a device

The Editor compiles only the active platform, so `UNITY_ANDROID` blocks are
invisible on desktop. To type-check them, copy `Assembly-CSharp.csproj`,
prepend `UNITY_ANDROID;OPEN_BRUSH_SCOPED_STORAGE;` to `<DefineConstants>`, and
`msbuild` it. This complements the Editor; it does not replace it, and it sees
neither Unity's analyzers nor `.meta`/GUID problems.

---

## 4. What is left to do

### Disabled, awaiting reinstatement

USD, FBX, PLY and Gaussian splats; SVG reference images; Quill and IMM import;
OBJ export; bulk sketch export; custom fonts.

Each is a guarded early return naming its reason, so reversing one is local work
once its loader can take a stream or a URL. **This is the main outstanding debt**
— the branch is not at feature parity until they are back, and parity is a
stated requirement (§5).

### Deferred by decision

**Which Android flavours build with scoped storage.** The flag is new on this
branch (`main` has no occurrences) and applied asymmetrically: *Android OpenXR*
and *Android Viewer OpenXR* are both sideloaded APKs, but only the viewer gets
scoped storage. Kept deliberately — it is the only sideloadable artefact with
SAF active, which makes it the easiest test target. **Revisit once it is decided
where the APK viewer build is published**, since that determines whether it
should follow Play storage rules at all.

### Optional: a shared direct ByteBuffer for writes

**Only if a real performance problem appears.** An optimisation, not a
correctness gap.

Writes cap at 28–31 MB/s because Unity marshals the array argument across JNI on
every call — about 0.7 s for a 20 MB sketch, 7 s for a 200 MB one. The fix
removes the crossing rather than making it cheaper: allocate native memory in
C#, wrap it once with `AndroidJNI.NewDirectByteBuffer`, hand Java the
`ByteBuffer` when the channel opens, and thereafter write with `Marshal.Copy`
plus an all-primitives call giving the byte count. `FileChannel.write` reads the
same memory. `SafDocumentStream`'s write-behind buffer can *be* that region, so
no copy is added.

Roughly 150 lines of hand-rolled JNI with manual global-reference lifetimes,
none of it verifiable without a device, and it costs 256 KiB of native memory
per open stream. Measure before starting.

### Probably not worth doing

**Collapsing the publish surface.** An earlier plan called for merging "twenty
near-duplicate publish methods". There are five entry points on
`SafStagedOutputPublisher` and most encode genuinely different behaviour —
frame-sequence bundling, directory publication, unique import naming, export
READMEs. Collapsing them would hide real differences behind flags.

### Known gap

**SAF exports check staging, not the destination.** `App.UserExportPath()`
returns `LocalExportStagingPath` under SAF, so `SketchControlsScript.cs:4838`
checks the volume the export stages on while the publish into the shared folder
is unchecked. Low severity: the failure path keeps the staged copy and reports
an error, so a full volume costs a worse message rather than data.

---

## 5. Product decisions this rests on

1. Startup is gated on folder selection. Declining exits the application.
2. The root never changes for the life of an installation.
3. Shared files are not modified externally while the application runs; a
   user-initiated refresh is enough.
4. **SAF builds must reach functional parity with non-SAF builds.** Removing a
   feature is not an acceptable way to simplify this branch.

---

## 6. Traps that have already cost time

- **"It compiles" proves very little.** Twenty-five commits went in on "compiles
  clean"; the first full EditMode run then found four real regressions —
  publication records colliding, Quill listing nothing, sound clips with no
  usable path, seeding never re-running after a folder change. Run the tests.
- **Same shape, different premise.** Removing root scoping was *correct* for the
  Drive ledger, where a stale record is merely misleading, and *wrong* for
  publication records, where `GetPendingTopLevelNames` throws on a mismatch. The
  reasoning transferred; the premise did not. `GetPublicationDirectory` carries a
  comment saying why.
- **Deleting root scoping needs its replacement in the same change.** Five
  commits shipped before `SafRootChangeGuard` existed. That window should not
  have been open.
- **A conflicted PR silently kills CI.** GitHub cannot compute a merge ref for a
  conflicted PR, so `pull_request` workflows never fire — while
  `pull_request_target` keeps working, which makes it look like CI is fine. If
  builds go quiet, check `gh pr view <n> --json mergeable` first.
- **Worker threads need `AndroidJNI.AttachCurrentThread`.** Reads are issued from
  image decoding, glTF loads and Lua resolution. `AndroidSafStorage
  .AttachToJvmIfNeeded` handles it, cached per thread.
- **The loopback media server is load-bearing for security.** `HttpServer.cs:53`
  binds all interfaces and admits remote callers when `EnableApiRemoteCalls` is
  set, so `SafMediaHttpServer` uses a per-session token compared in constant
  time, applies `IsTrustedLocalBrowserRequest` explicitly, and resolves
  identifiers only within the selected root. Do not loosen any of that.

---

## 7. How the code is arranged

| File | Role |
| --- | --- |
| `Assets/Plugins/Android/OpenBrushStorageBridge.java` | All provider access. Channel table, directory queries, document mutation, free space. |
| `Assets/Plugins/Android/OpenBrushStorageActivity.java` | Hosts the folder picker; reports back via `UnitySendMessage`. |
| `Assets/Scripts/Storage/AndroidSafStorage.cs` | Thin C# face of the bridge. |
| `Assets/Scripts/Storage/SafDocumentStream.cs` | The seekable stream and its buffering; `ISafDocumentChannel` is the seam the tests use. |
| `Assets/Scripts/Storage/SafUserStorageBackend.cs` | `IUserStorageBackend` over SAF. |
| `Assets/Scripts/Storage/SafStorageTransaction.cs` | The write commit sequence and `SafPrivatePaths`. |
| `Assets/Scripts/Storage/SafTransactionRecovery.cs` | Startup sweep for interrupted saves, driven by `.ob-tmp` / `.ob-bak` / `.ob-invalid` sidecars. |
| `Assets/Scripts/Storage/SafStagedOutputPublisher.cs` | Publishing staged output into shared storage. |
| `Assets/Scripts/Storage/SafMediaHttpServer.cs` | Loopback HTTP for URL-only Unity loaders. |
| `Assets/Scripts/Storage/SafRootChangeGuard.cs` | The single root comparison, at startup. |
| `Assets/Scripts/Storage/AndroidStorageManager.cs` | Startup gate, folder selection, recovery kickoff. |

**Recovery works from sidecars in shared storage, not from a journal.** An
interrupted save leaves `MySketch.tilt.ob-tmp` or `.ob-bak` beside its target;
the startup sweep reconstructs what it needs from those names. Nothing is
serialized to app-private storage any more.

---

## 8. History

The branch began as a much larger design and was reduced in place rather than
rebuilt. Deleted outright: the materialization cache and its 512 MB budget
(which never served a hit), the 806-line runtime-content projection, the degraded
mode and its nine resumable save/export paths, the on-disk transaction journal,
and the legacy-content migration. Root identity went from 255 references to 99,
of which 26 are test fixtures and the rest are keys, staleness checks and
persisted records that all do real work.

Things earlier plans asserted that turned out to be false, recorded so they are
not re-derived: the path-consumer audit asked only whether an importer could
take a stream, not whether anything later needed the file (OBJ's
`ImportMaterialCollector` reads the asset location at *export* time); Unity's
URL-taking loaders — `UnityWebRequest`, `VideoPlayer.url`, `WWW`,
`UnityWebRequestMultimedia` — all accept `http://`, which is what makes one
loopback handler serve OBJ, textures, audio and video; supporting a path-only
loader never required a cache, only a temporary file for the duration of one
load; and `StorageTreeEnumerator` does **not** silently truncate — it fails
loudly on both caps.
