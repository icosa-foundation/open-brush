# SAF Branch Design Review

## Status

Review of `feature/saf-google-play-fd-backed` as of commit `01805166b9`,
written in response to the concern that the design is more complex than the
problem requires.

Conclusion: the core approach is correct and the constraint driving it is
real, but roughly a third of the production code on this branch is
self-assigned scope rather than anything Android forces. Four specific
reductions are proposed at the end. None of them require rewriting the
storage layer.

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
descriptors for file contents. That is real, irreducible work. It is not
22,000 lines of work.

## Where The Code Actually Goes

22,318 lines added, 852 deleted. Measured by area:

| Area | Added | Assessment |
| --- | --- | --- |
| `Assets/Scripts/Storage/` | 7,480 | Core is required; the layering is not |
| Media catalogs and media types | 2,787 | Mostly required — they assume `Directory.GetFiles` |
| `Assets/Editor/Tests/` | 3,881 | Appropriate |
| Plan documents (6 files) | 3,114 | Appropriate |
| `Assets/Scripts/Save/` | 1,287 | Required |
| `DriveSync` + `DriveSyncLedger` + `DriveAccess` | 1,249 | Self-assigned scope |
| `Assets/Plugins/Android/` (Java bridge) | 977 | Required |

Tests and design documents together account for 6,995 lines, which leaves
about 15,300 lines of production code. The tests and the written-down
reasoning are the parts of this branch that are unambiguously worth having;
they are not the problem.

## Three Structural Concerns

### 1. The design contradicts itself on mirroring

`google-play-saf-fd-backed-storage-plan.md` rejects the mirrored-cache
approach explicitly, and the rejection is the central argument of the
document. It lists the costs: destructive reconciliation, canonical/cache
conflicts, local-only path preservation, pending-transfer persistence, cache
deletion notifications, retry ordering, startup provider failure handling,
and concurrent inbound and outbound transfers. Core invariant 2 states that
app-private materializations are disposable caches and not a second canonical
directory.

`google-play-saf-feature-parity-plan.md` then reintroduces a mirror for
`Scripts`, `Plugins` and `Fonts`. `SafUserRuntimeContent`
(`UserRuntimeContent.cs:265`) carries a `ProjectionManifest`,
`ProjectionEntry` and `MigrationRecord` — a projection with persistent
bookkeeping and write-back, which is a mirror with a different name.

That plan has already been amended once to remove the `ContentObserver` layer
it originally specified, on the grounds that observation "exceeded the parity
bar this plan exists to meet, and its background-triggered refreshes were the
main source of concurrent-refresh complexity." That amendment is evidence for
the general point: parity was pursued past the point where it paid for itself.

If a mirror is the wrong answer for sketches, it needs a stronger
justification than parity to be the right answer for three more trees.

### 2. The transaction machinery is oversized, and the branch already says so

`SafStorageTransaction.cs`, `SafTransactionRecovery.cs` and
`SafStagedOutputPublisher.cs` total 1,885 lines.

`saf-transaction-journal-removal-plan.md` is a plan, written on this branch,
to delete the journal portion of that. Its findings stand on their own:

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

Its own honest accounting puts the net saving at 400–500 lines, because the
load-bearing parts survive: the rename sequence (write temp, validate, rename
canonical to backup, rename temp to canonical, delete backup), payload
validation, presence-based restore, and `SafDestinationLocks`. The win is
removing a persistent state store, its retention policy, its failure handling
and its per-save fsync cost — not the line count.

The replacement it proposes (encode the target display name in the sidecar
names, so `MySketch.tilt.ob-bak` self-describes, and sweep at startup) is
simpler and strictly more robust, because it cannot desynchronize from the
directory it describes.

### 3. Four facades over one concept

The storage layer presents, in order: `OpenBrushStorage` (static, 797 lines),
`AndroidStorageManager` (MonoBehaviour, 599), `AndroidSafStorage` (static
JNI, 589), `SafUserStorageBackend` (`IUserStorageBackend`, 890), and
`UserStorageBackend` (interface plus `LocalUserStorageBackend`, 987).

A three-layer shape is defensible — JNI marshalling, a backend interface, and
call sites. The part that reads as abstraction leakage is `OpenBrushStorage`'s
publish surface, which has grown roughly twenty near-duplicate entry points:
`PublishGeneratedFileToSharedStorage`,
`PublishGeneratedFileToSharedStorageAsync`,
`PublishGeneratedFilesToSharedStorageAsync`,
`PublishMediaLibraryPathToSharedStorage`,
`PublishMediaLibraryPathToSharedStorageAsync`,
`PublishImportedMediaToSharedStorageAsync`,
`PublishVideoCaptureToSharedStorage`,
`PublishVideoCaptureToSharedStorageAsync`,
`PublishExportToSharedStorageAsync`,
`PublishGaussianCaptureToSharedStorageAsync`, and so on. These differ by
staging directory and target area, which are parameters, not methods.

## Proposed Reductions

In priority order. All four are removals or consolidations; none changes a
correctness invariant.

1. **Run the device probe first.** The entire read path is gated on an
   unvalidated assumption. `google-play-saf-fd-backed-storage-plan.md` states
   that if detached descriptors are not reliably seekable under IL2CPP, the
   architecture keeps its shape but direct archive reads must be replaced with
   sparse per-document materialization. `AndroidSafStorage.RunFileDescriptorProbe`
   (`AndroidSafStorage.cs:351`) exists for this. Running it on a target device
   is cheap and avoids simplifying code that is about to be reworked anyway.

2. **Execute the journal-removal plan.** Already written, already justified,
   deferred only until after the probe. 400–500 lines, one persistent store,
   and five fsyncs per save.

3. **Cut Google Drive sync from this branch.** `DriveSync` and its ledger are
   1,249 lines and the second-largest single change here. Drive sync is
   orthogonal to where files live locally; it was disabled for SAF by the
   fd-backed plan and re-enabled by the parity plan. Ship SAF storage, then
   decide separately whether Drive sync is still wanted on Play builds.

4. **Decide `Scripts`/`Plugins`/`Fonts` deliberately.** Either accept them as
   app-private — simpler, and honest about the tradeoff — or accept the
   projection and amend the fd-backed plan so the two documents agree.
   Shipping a mirror underneath a design document that argues mirrors are
   unacceptable is the worst of the three options. Removing the projection
   would drop 1,262 lines.

5. **Collapse the publish surface.** One `Publish(stagedPath, StorageArea,
   relativePath)` plus a small enum, with async as a wrapper rather than a
   parallel method family.

Items 3, 4 and 5 together remove roughly 2,800 lines; with item 2 the total is
around 3,300. The remaining ~12,000 lines of production code is a defensible
size for replacing a path-based storage model across sketches, six media
catalogs, save/load, and export on a platform that forbids paths.

## What Is Not Wrong Here

For the avoidance of doubt, since most of the above is criticism:

- The fd-backed approach is the right one. The alternative designs are a
  whole-tree mirror (rejected for good reasons) or per-file copies on every
  read (slower and no simpler).
- The commit sequence protecting the canonical document is correct and should
  not be weakened by any of the above.
- The catalog changes are largely unavoidable. Those classes call
  `Directory.GetFiles` and `File.Exists` directly, and something has to give.
- Writing the design down before implementing it, and writing 3,881 lines of
  tests, is why this review could be done from the repository at all.
