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

Last updated **2026-09-18**.

**In one line: the branch is code-complete for its scope; what remains is
testing and the bugs it finds.** See §4 for the distinction between that and the
future work deliberately left out of scope.

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

The former development-build startup stream probe was temporary bring-up
instrumentation and has been removed. Verify the write path through the actual
save workflow rather than creating and deleting a diagnostic document at every
development-build startup.

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

### Fixed on 2026-09-18, not yet re-tested

- SAF exports checked the staging volume rather than the shared destination.

### The shape of the remaining risk

The read path is proven end to end. The write path is not, and it is the part
where a mistake costs someone their sketch rather than an error message.

There is no known outstanding code work. That is not the same as the branch
being finished: it means the next thing to do is exercise it on a device, not
write more of it.

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

### Building locally in about five minutes

Much faster than CI once the caches are warm, and worth the setup. Close the
Editor first - batchmode needs the project to itself, and the link needs the
memory.

```bash
/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . -buildTarget Android \
  -logFile <log> -executeMethod BuildTiltBrush.CommandLine \
  -btb-target Android -btb-display OpenXR -btb-bopt Development \
  -btb-il2cpp -btb-scoped-storage -btb-stamp local -btb-out <path>/OpenBrush.apk
```

`-btb-bopt Development` is what compiles the startup probe in. The first build is
slow - 6144 shader variants and 5279 objects - but those caches persist, so later
builds are five to eight minutes.

Four things will waste a morning if you do not know them:

1. **The package is `foundation.icosa.openbrush`, not `...openbrushviewer`.**
   CI's artefacts are branch-suffixed (`...viewerPR1120`), a local build is not.
   Installing one and launching the other looks exactly like a build that
   produces an unlaunchable APK. Read the package from the APK rather than
   assuming: `aapt2 dump badging <apk> | grep package`.
2. **Bind logcat to the PID.** With several Open Brush variants installed,
   `adb logcat` shows all of them, and reading another app's output while
   believing it is yours produces confident, wrong conclusions.
   `PID=$(adb shell pidof <pkg>)` then `adb logcat -d | awk -v p=$PID '$3==p'`.
3. **Check the build actually succeeded, and not just for `error CS`.** A build
   can fail with `IOException: No space left on device` and still exit leaving
   the previous APK in place, so the next install silently tests stale code.
   Grep for `No space left|IOException|BuildFailedException|_btb_ Abort`.
   Verify a marker string reached the APK if it matters:
   `unzip -p <apk> assets/bin/Data/Managed/Metadata/global-metadata.dat | strings | grep <marker>`.
4. **Disk space is the real constraint.** A build needs several GB free on top
   of an 11 GB `Library/`, and `Library/Bee/artifacts` alone is 4 GB of cache
   worth keeping.

`adb install -r` does not stop a running app, so `adb shell am force-stop <pkg>`
before relaunching or you will read the old process's log.

### Satisfying pre-commit

`pre-commit` is a required PR check and runs `dotnet format whitespace`, failing
if the hook modifies anything. Before pushing:

```bash
dotnet format whitespace . --folder --include $(git diff --name-only \
    $(git merge-base origin/main HEAD) HEAD -- '*.cs' \
    | grep -vE '^(Assets/ThirdParty|Packages/|Assets/Photon/)' | tr '\n' ' ') \
    --verify-no-changes
```

That lists the offenders. **Then format them one file at a time** — the same
command without `--verify-no-changes` and with many files silently does nothing,
while a single file works. It also splits same-line braces and stacked `case`
labels, so `git diff -w` will still show those hunks; they are not behaviour
changes.

### Type-checking the Android path without a device

The Editor compiles only the active platform, so `UNITY_ANDROID` blocks are
invisible on desktop. To type-check them, copy `Assembly-CSharp.csproj`,
prepend `UNITY_ANDROID;OPEN_BRUSH_SCOPED_STORAGE;` to `<DefineConstants>`, and
`msbuild` it. This complements the Editor; it does not replace it, and it sees
neither Unity's analyzers nor `.meta`/GUID problems.

---

## 4. What is left to do

### Now: nothing. This branch is code-complete for its scope.

Every item that was outstanding has resolved, and the last one closed on
2026-09-18:

- ~~SAF exports checked the staging volume, not the destination.~~ Closed —
  `Export.ExportScene` now checks the shared folder before doing any work.
- ~~Root identity's remaining references.~~ Investigated; all 99 are
  load-bearing. Nothing to remove.
- ~~The transaction journal.~~ Removed, though not as originally written: two of
  its three types earn their place, and what was deletable was the schema around
  them.

**What remains is testing and the bugs it finds.** See §1 for where the risk is
— principally that no save has ever been performed on a device.

### Future work, deliberately out of scope

These are switched off on purpose. They are **not** unfinished work on this
branch, and nothing here blocks merging it. Each is a guarded early return that
names its reason, so reinstating one is local work once its loader can take a
stream or a URL.

| Gate | What it disables |
| --- | --- |
| `Model.cs:1141` | Model formats other than `.gltf`, `.glb`, `.gltf2`, `.obj` — so USD, FBX, PLY and Gaussian splats. Their importers open a path and shared storage has none to give. |
| `SvgTextUtils.cs:31` | Custom fonts in SVG text. Unity offers no runtime route from bytes to a `Font`. |
| `SketchControlsScript.cs:4155` | Bulk sketch export. |

That is the whole list. Earlier revisions of this document also claimed SVG
reference images, Quill and IMM import, and OBJ export were disabled. **They are
not** — that list was inherited from a superseded plan and never checked. OBJ is
explicitly in the allowed import set, and Quill's only scoped-storage code
*enables* SAF handling for its IMM source.

Reaching full parity (§5, decision 4) means reinstating the three above, and the
first row is the substantial one.

### Future work, gated on evidence: a shared direct ByteBuffer

**Only if a real performance problem appears.** An optimisation, not a
correctness gap, and nothing should be built assuming it is needed.

Writes cap at 28-31 MB/s because Unity marshals the array argument across JNI on
every call — about 0.7 s for a 20 MB sketch, 7 s for a 200 MB one. The fix
removes the crossing rather than making it cheaper: allocate native memory in
C#, wrap it once with `AndroidJNI.NewDirectByteBuffer`, hand Java the
`ByteBuffer` when the channel opens, and thereafter write with `Marshal.Copy`
plus an all-primitives call giving the byte count. `FileChannel.write` reads the
same memory. `SafDocumentStream`'s write-behind buffer can *be* that region, so
no copy is added.

Roughly 150 lines of hand-rolled JNI with manual global-reference lifetimes,
none of it verifiable without a device, and it costs 256 KiB of native memory
per open stream. Measure first.

### Recommended against

**Collapsing the publish surface.** An earlier plan called for merging "twenty
near-duplicate publish methods". There are five entry points on
`SafStagedOutputPublisher`, and most encode genuinely different behaviour —
frame-sequence bundling, directory publication, unique import naming, export
READMEs. Collapsing them would hide real differences behind flags. Recorded so
the idea is not revived without the correction attached.

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
- **Discussing a CI marker in a commit body fires it.** The push gate is
  `contains(message, '[CI BUILD]')` against the whole message, body included, so
  a commit that merely explains the marker triggers a build.
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
