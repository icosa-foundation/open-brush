# SAF Branch Design Review

**Superseded as a plan by `saf-plan-of-record.md`.** This document remains the
review and the reasoning; the plan of record is what to do.

## Status

Review of `feature/saf-google-play-fd-backed` as of commit `01805166b9`,
written in response to the concern that the design is more complex than the
problem requires.

Revised after review feedback. The governing requirement for this branch is
**functional parity between Google Play SAF builds and existing non-SAF
builds**. An earlier draft of this document proposed cuts that would have
broken that requirement; those are corrected below, and the corrections are
recorded rather than silently removed, because the reasoning that produced
them is the reasoning a future reviewer is most likely to repeat.

Updated 2026-09-16 with device probe results (the SAF probes repository (`open-brush-saf-probes`)), which
discharge the provider half of the release gate and independently validate two
parts of the design this review had questioned.

Conclusion: the constraint driving this branch is real, the fd-backed
approach is right and now measured, and under a parity requirement the great
majority of the code is justified. Roughly 800 lines are removable without any
behavioural change.

## The Question This Answers

> Why can't we just ask the user for permission to read/write the same
> `Documents/Open Brush` folder that the non-SAF build uses, and then read and
> write directly there?

Because Android does not offer that permission to an app like this one.

A SAF tree grant from `ACTION_OPEN_DOCUMENT_TREE` is a **ContentProvider**
permission, not a filesystem permission. It records a URI grant that
`DocumentsProvider` consults. It does not change what the kernel and the
FUSE/scoped-storage layer will let the app's uid `open()`. After the user
grants the folder:

- `Directory.GetFiles("/storage/emulated/0/Documents/Open Brush")` still
  returns empty or fails with `EACCES`;
- `File.OpenRead(".../Sketch.tilt")` still fails. `READ_EXTERNAL_STORAGE`
  covers MediaStore-indexed media only, and `.tilt`, `.lua`, `.glb` and `.obj`
  are not media.

The permission that *would* grant direct path access is
`MANAGE_EXTERNAL_STORAGE`. `Assets/Plugins/Android/AndroidManifest.xml:60`
still declares it, and `BuildTiltBrushPostProcess.cs` strips it for Google
Play builds, because Play policy restricts that permission to file managers,
backup tools and antivirus apps. A drawing application will not have the
declaration approved. Quest and Pico sideloads keep it and keep direct paths;
Play builds cannot.

The closest available approximation to direct access is what this branch
already does. `OpenBrushStorageBridge.java:609` calls `detachFd()` on the
`ParcelFileDescriptor` from `openFileDescriptor`, and the C# side wraps the
raw descriptor in a `SafeFileHandle` and a `FileStream`
(`AndroidSafStorage.cs:560`). That yields ordinary seekable stream I/O on a
document whose identity is already known. It does not yield listing,
creation, rename or delete — those must go through `DocumentsContract`.

So the floor for this feature is: SAF for directory operations, file
descriptors for file contents.

## Why The Line Count Is Large

22,318 lines added, 852 deleted. Measured by area:

| Area | Added | Assessment |
| --- | --- | --- |
| `Assets/Scripts/Storage/` | 7,480 | Required; one layer is over-elaborated |
| `Assets/Editor/Tests/` | 3,881 | Appropriate |
| Plan documents | 3,114 | Appropriate |
| Media catalogs and media types | 2,787 | Required |
| `Assets/Scripts/Save/` | 1,287 | Required |
| `DriveSync` + `DriveSyncLedger` + `DriveAccess` | 1,249 | Required |
| `Assets/Plugins/Android/` (Java bridge) | 977 | Required |

Tests and design documents account for 6,995 lines, leaving about 15,300
lines of production code.

The reason that number is large is not over-engineering. It is that
`MANAGE_EXTERNAL_STORAGE` was load-bearing for far more of the application
than the sketch save path. Every subsystem that called `Directory.GetFiles`,
`File.Exists` or `File.Move` against the user folder — six media catalogs,
save/load, export, capture publication, Drive sync — has to be re-expressed
against an interface that SAF can satisfy. There is no small version of that
change that preserves parity.

## Correction: Google Drive Sync Is Not Optional Scope

An earlier draft recommended cutting `DriveSync` from this branch, on the
reasoning that Drive sync is "orthogonal to where files live locally." That
reasoning was wrong on three counts.

**It is a shipping Android feature.** `DriveSync` on `main` carries no
platform gating. It is surfaced through `DrivePopUpWindow`,
`ProfilePopUpWindow`, `SketchbookPanel`, `ScriptsPanel` and
`SyncScriptsToDriveButton`, on every platform including Quest. Dropping it
from Play builds would be exactly the SAF/non-SAF functional gap this branch
exists to close.

**It is not orthogonal to storage — it is built directly on the filesystem.**
`DriveSync` on `main` uses `DirectoryInfo`/`FileInfo` enumeration
(`folder.Local.GetFiles()`), `Directory.CreateDirectory`, `File.Move`,
`File.Exists`, `File.Delete` and `File.SetLastWriteTime` against the user
folder. Every one of those fails on a SAF tree. Porting it to
`IUserStorageBackend` is mandatory for it to function at all under Play
storage, not discretionary hardening.

**The ledger that looked like gold-plating is a forced substitute.** `main`
has no conflict handling in `DriveSync` at all, which made the new
`DriveSyncLedger` (311 lines) look like scope creep. It is not.
`main`'s algorithm depends on `File.SetLastWriteTime` to stamp a downloaded
file with Drive's timestamp so the next comparison sees the two sides as
equal. SAF providers own document timestamps and expose no way to set them.
Without a substitute, every downloaded file appears locally newer than Drive
forever and re-uploads in a loop. The parity plan states this directly:
"SAF providers generally control document timestamps, so timestamp
comparison alone can make the same downloaded file appear locally newer and
upload it again." Recording Drive file ID and version per relative path is
the minimum replacement for a capability SAF does not provide.

The general lesson, which applies to the rest of this branch: when a SAF
implementation looks like it has more machinery than its local counterpart,
check first whether it is compensating for a filesystem capability that SAF
lacks. Several places here are.

## Device Probe Results

the SAF probes repository (`open-brush-saf-probes`) is a standalone ~16 KB Android app that exercises the
provider-side assumptions of this design without building Open Brush. It runs
in seconds rather than the twenty minutes a Unity Android build takes, so it is
practical to re-run per device and per OS release.

Run 2026-09-16 on a Nothing Phone (3a), Android 16 (API 36), against
`com.android.externalstorage`:

```text
INFO 2  flags=0x146 write=true rename=true delete=true
PASS 3  detachFd -> 132
INFO 3a fstat mode=0100660 regular=true size=0
PASS 3b lseek(SEEK_END) = 0
PASS 4  wrote 3145728 bytes through detached fd
PASS 5  random-access read at 1572864 got=64 match=true
FAIL 6  /proc/self/fd path unusable: EACCES (Permission denied)
PASS 7  reopen 'r' size=3145728 read=3145728 identical=true
PASS 8  rwt accepted, size after truncate = 0
PASS 9  renameDocument -> primary:Documents/obfdprobe.tilt.ob-bak
INFO 10 rename onto existing name -> obfdprobe (1).tilt
```

**The core assumption holds.** A detached descriptor is a seekable regular
file that accepts a multi-megabyte write, supports random-access read-back at
mid-file, and round-trips byte-identical when reopened by URI. Rename, delete
and `rwt` truncate are all supported. The fd-backed read path is sound on the
local provider.

**Check 6 failed, and that answer matters.** `/proc/self/fd/N` is not openable
as a path — it resolves to the underlying FUSE path the sandbox cannot reach.
This was the one remaining shortcut by which path-only libraries might have
consumed SAF documents directly. It does not exist. On-demand materialization
for models, SVG, fonts and Lua is therefore required, not convenient, and the
`Scripts`/`Plugins`/`Fonts` projection questioned below is the only practical
way to serve those consumers.

**Check 10 deduplicated instead of replacing.** `renameDocument` onto an
existing display name produced `obfdprobe (1).tilt`. Rename can never be used
to atomically replace a document, so the commit sequence must free the target
name first. That is exactly what it does — rename canonical to `.ob-bak`,
then rename temporary into place. The ordering is load-bearing, not defensive
habit, and the branch's "Reject ambiguous SAF renames" and "Target SAF
overwrites by document identity" commits are addressing a real provider
behaviour.

The IL2CPP half of the gate is still outstanding: `SafeFileHandle` over a
detached descriptor under Unity's runtime needs a real Google Play build and
`AndroidSafStorage.RunFileDescriptorProbe`. A regular-file descriptor is the
case `FileStream` handles natively, so residual risk is low but not zero.
No cloud-backed provider has been probed; those are expected to fail check 3
and fall back to materialization.

## Remaining Concerns

### 1. Two design documents disagree about mirroring

`google-play-saf-fd-backed-storage-plan.md` rejects the mirrored-cache
approach as its central argument, listing the costs: destructive
reconciliation, canonical/cache conflicts, local-only path preservation,
pending-transfer persistence, cache deletion notifications, retry ordering,
startup provider failure handling, and concurrent transfers. Core invariant 2
states that app-private materializations are disposable caches and not a
second canonical directory.

`google-play-saf-feature-parity-plan.md` then introduces a projection for
`Scripts`, `Plugins` and `Fonts` — `SafUserRuntimeContent`
(`UserRuntimeContent.cs:265`) with a `ProjectionManifest`, `ProjectionEntry`
and `MigrationRecord`.

**Correction (2026-09-16).** This review originally called the projection "a
mirror with a different name". That was wrong, and the error is the same one
made about Drive sync: unfamiliar machinery read as redundancy without checking
which way the data flows. `UserRuntimeContent` has exactly one write entry
point, `PublishIfMissingAsync` (line 118), which seeds bundled content if
absent. There is no write-back of user edits, no reconciliation and no conflict
resolution. What looked like sync is a generational snapshot — build a new
directory from SAF, flip `ProjectionPointer`, garbage-collect the old — which
is a one-directional cache with an atomic swap, plus a one-shot
`MigrationRecord` for legacy app-private content. A mirror implies two
authorities; this has one.

Under a parity requirement this projection is **correct and necessary**.
Those trees are user-managed content that Quest users populate over USB, and
their consumers (`ApiManager`, `LuaManager`, Lua module loading,
`SvgTextUtils`) require real filesystem paths. Making them app-private would
be a parity gap, and rewriting every consumer to take streams is a far larger
change than projecting them.

So this is a documentation defect, not a code defect. The fd-backed plan's
blanket anti-mirror language should be amended to state what the branch
actually implements: whole-tree bidirectional mirroring of the *canonical
sketch store* is rejected; bounded, manifest-tracked projection of
*path-consumer trees* is the accepted pattern. As written, the two documents
send a reviewer to opposite conclusions, which is how the earlier draft of
this review went wrong.

The amendment already appended to the parity plan — removing the
`ContentObserver` layer because observation "exceeded the parity bar this
plan exists to meet" — is a good precedent for this kind of correction.

**Resolved 2026-09-16.** `google-play-saf-fd-backed-storage-plan.md` has been
amended: the blanket "does not mirror" line now distinguishes whole-tree
mirroring of the canonical sketch store (still rejected) from bounded
projection of path-consumer trees (accepted), core invariant 2 names the
exception, and the device probe result establishing that no `/proc/self/fd`
path exists is cited as the reason projection is unavoidable.

### 2. The transaction journal is redundant, and the branch already says so

`SafStorageTransaction.cs`, `SafTransactionRecovery.cs` and
`SafStagedOutputPublisher.cs` total 1,885 lines.
`saf-transaction-journal-removal-plan.md`, written on this branch, argues for
deleting the journal portion. Its findings stand:

- `SafTransactionRecovery.RecoverRecord` makes every decision from observable
  directory state. The recorded `SafTransactionState` "is never consulted to
  choose a recovery action."
- The journal is a second persistent store with its own failure modes, and
  several hardening commits on this branch exist only to service it.
- A single successful save persists the journal five to six times, each with
  `Flush(flushToDisk: true)` — five synchronous fsyncs per save on mobile
  flash, protecting a decision procedure that does not read the data.
- The write-ahead-log shape implies multi-operation atomicity that nothing
  uses. Every transaction on the branch is a single-file replacement.

Net saving is 400–500 lines by its own accounting, because the load-bearing
parts survive: the rename sequence (write temp, validate, rename canonical to
backup, rename temp to canonical, delete backup), payload validation,
presence-based restore, and `SafDestinationLocks`. The real win is removing a
persistent store, its retention policy, its failure handling and its per-save
fsync cost.

Its replacement — encode the target display name in sidecar names, so
`MySketch.tilt.ob-bak` self-describes, and sweep at startup — is simpler and
strictly more robust, because it cannot desynchronize from the directory it
describes.

This is a pure simplification with no parity implications.

### 3. The publish surface is duplicated per call site

`OpenBrushStorage` (797 lines) exposes roughly twenty near-duplicate publish
entry points: `PublishGeneratedFileToSharedStorage`,
`PublishGeneratedFileToSharedStorageAsync`,
`PublishGeneratedFilesToSharedStorageAsync`,
`PublishMediaLibraryPathToSharedStorage`,
`PublishMediaLibraryPathToSharedStorageAsync`,
`PublishImportedMediaToSharedStorageAsync`,
`PublishVideoCaptureToSharedStorage`,
`PublishVideoCaptureToSharedStorageAsync`,
`PublishExportToSharedStorageAsync`,
`PublishGaussianCaptureToSharedStorageAsync`, and others. These differ by
staging directory and target area, which are parameters rather than methods.

A single `Publish(stagedPath, StorageArea, relativePath)` with async as a
wrapper would remove roughly 300 lines. The broader five-type stack
(`OpenBrushStorage`, `AndroidStorageManager`, `AndroidSafStorage`,
`SafUserStorageBackend`, `UserStorageBackend`) is defensible — JNI
marshalling, backend interface, call sites — and is not proposed for change.

## Recommended Actions

1. ~~**Run the device probe before any other work.**~~ Done for the provider
   half; see above and the SAF probes repository (`open-brush-saf-probes`). **Still outstanding:**
   run `AndroidSafStorage.RunFileDescriptorProbe` (`AndroidSafStorage.cs:351`)
   from a real Google Play build to close the IL2CPP half, and re-run
   the SAF probes repository (`open-brush-saf-probes`) against any further target provider.

2. ~~**Amend the fd-backed plan's mirroring language.**~~ Done.

3. **Execute the journal-removal plan.** 400–500 lines, one persistent store,
   five fsyncs per save. Already specified and justified on this branch. Safe
   to start now that the commit sequence it must preserve has been validated
   on device.

4. ~~**Collapse the publish surface.**~~ Done. It was eight methods, not twenty,
   and six were not duplicates; 42 lines came out.

5. **Execute `saf-single-root-simplification-plan.md`.** Added 2026-09-16 after
   four product decisions removed constraints the implementation was built to
   satisfy: startup is gated on folder selection, declining exits, the root
   never changes, and shared files are not modified externally while running.
   Roughly 1,500–2,500 lines across 24 files, and it supersedes the "mirror
   with a different name" criticism below — the generational projection was a
   correct answer to a requirement that no longer exists.

Total removable: roughly 800 lines, none of it behavioural. The remaining
~14,500 lines of production code is a defensible size for replacing a
path-based storage model across sketches, six media catalogs, save/load,
export, capture publication and Drive sync, on a platform that forbids paths,
while holding parity with builds that allow them.

## What Is Right Here

- The fd-backed approach is correct. The alternatives are a whole-tree mirror
  (rejected for good reasons) or per-file copies on every read (slower and no
  simpler).
- The commit sequence protecting the canonical document is correct and must
  not be weakened by recommendation 2.
- The catalog and Drive sync changes are forced by the platform, not chosen.
- Writing the design down before implementing, and writing 3,881 lines of
  tests, is why this review could be conducted from the repository at all —
  and why the errors in its first draft were correctable from the repository
  too.
