# SAF Transaction Journal Removal Plan

## Status

Proposal. Not yet started, and deliberately deferred until after the device
validation gates in `google-play-saf-fd-backed-storage-plan.md` have been run.
If the `SAF_FD` device probe forces a rework of the write path, fold this
simplification into that rework instead of doing it separately.

## Scope

This plan covers the write-ahead transaction machinery in:

- `Assets/Scripts/Storage/SafStorageTransaction.cs` (728 lines):
  `SafTransactionRecord`, `SafTransactionJournal`, `SafTransactionState`,
  and the journal-persistence calls inside `SafFileWriteTransaction`.
- `Assets/Scripts/Storage/SafTransactionRecovery.cs` (355 lines): the
  journal-driven startup recovery pass.
- The journal-focused tests in `Assets/Editor/Tests/TestFile.cs`.

It does **not** cover, and must not weaken:

- The commit sequence itself (write temp → validate → rename canonical to
  backup → rename temp to canonical → delete backup). This is what enforces
  the "never truncate the canonical document" invariant, and it stays.
- Payload validation (`TiltFile.IsArchiveValid`) before and after install.
- `SafDestinationLocks` (in-process per-destination serialization). It is
  small, correct, and used by `SafUserStorageBackend` and
  `SafStagedOutputPublisher` independently of the journal. If it survives
  alone, move it to its own file.
- The root-change guards (`EnsureSelectedRoot` / `IsSelectedRootCurrent`).
- `SafStagedOutputPublisher`'s separate "publications" ledger. It shares only
  the `SafTransactionJournal.GetRecoveryRootDirectory` /
  `GetRootNamespaceId` path helpers, which should be extracted before
  deletion. Whether that ledger deserves the same treatment is a separate
  decision.

## Why Remove It

### 1. Recovery does not actually use the journal's state machine

`SafTransactionRecovery.RecoverRecord` makes every decision from observable
state: it lists the destination directory, locates the canonical, temporary
(`.ob-<guid>.tmp`), backup (`.ob-<guid>.bak`), and quarantine
(`.ob-<guid>.invalid`) documents by display name, validates candidate `.tilt`
archives, and restores the best valid document. The `SafTransactionState`
recorded at each transition is never consulted to choose a recovery action.

The journal therefore carries exactly one piece of load-bearing data: the
mapping from the GUID-based temporary/backup names back to the target display
name. Everything else in the record (state, timestamps, attempt counts, last
error) is diagnostics. A naming convention that encodes the target name in
the sidecar files (see below) makes the journal fully redundant.

### 2. It is a second persistent store with its own failure modes

The journal lives under `persistentDataPath/OpenBrushSafRecovery/<sha256 of
root id>/journals/*.json`, with versioned records, atomic
write-temp-then-replace persistence, malformed-record tolerance, and a
retention policy for records from obsolete roots. Several hardening commits
on this branch ("Preserve SAF recovery after journal errors", "Retain
obsolete SAF transfer records") exist only to service this store. Every
save must also handle the case where the *journal* write fails
(`SafFileWriteTransaction.Fail` has a dedicated catch for this). Removing
the store removes that entire class of bookkeeping bugs.

### 3. It costs real I/O on every save

A single successful sketch save persists the journal file five to six times
(`CreatingTemporary`, `WritingTemporary`, `TemporaryComplete`,
`OriginalBackedUp`, `ReplacementInstalled`, `Complete`/delete), each with
`Flush(flushToDisk: true)`. That is five synchronous fsyncs of overhead per
save on mobile flash, protecting a decision procedure that does not read the
data it is protecting.

### 4. It implies guarantees nothing uses

The record/journal/state-machine shape is a general write-ahead-log design,
suggesting multi-operation atomicity. Every transaction on the branch is a
single-file replacement. The generality is cognitive overhead for reviewers
and future contributors with no consumer.

### Honest accounting

The net line savings are moderate — roughly 400–500 lines once the sweep
replaces the journal-driven pass — because the genuinely necessary parts
(the rename dance, validation, presence-based restore logic, locks) survive.
The real win is eliminating a persistent state store, its retention policy,
its failure handling, and its per-save fsync cost, not the line count.

## Replacement Design: Name-Encoded Sidecars + Startup Sweep

Encode the transaction identity in the sidecar names instead of a journal:

| Current name            | New name              |
| ----------------------- | --------------------- |
| `.ob-<guid>.tmp`        | `<target>.ob-tmp`     |
| `.ob-<guid>.bak`        | `<target>.ob-bak`     |
| `.ob-<guid>.invalid`    | `<target>.ob-invalid` |

where `<target>` is the full target display name (e.g.
`MySketch.tilt.ob-bak`). Recovery then needs no external record: any
`*.ob-tmp` / `*.ob-bak` / `*.ob-invalid` document found in a SAF directory
self-describes which canonical document it belongs to, and the existing
presence + validation logic applies unchanged:

1. If the canonical document exists and validates: delete leftover sidecars.
2. Else if `<target>.ob-bak` validates: quarantine any invalid canonical,
   rename backup to canonical, re-validate, clean up.
3. Else if `<target>.ob-tmp` validates: same restore path.
4. Else: leave everything in place and log; never delete the only copy.

This is `SafTransactionRecovery.RecoverRecord` minus the journal load — most
of `CompleteWithCanonical` / `RestoreDocument` / `IsValidDocument` carries
over nearly verbatim.

Consequences that need explicit handling:

- **Same-destination collision replaces GUID uniqueness.** Two concurrent
  transactions on one destination are already impossible in-process
  (`SafDestinationLocks`). A leftover sidecar from a crashed session now
  collides by name with a new write; the transaction constructor must run
  the per-target recovery step for its destination before creating
  `<target>.ob-tmp` (this replaces `FindExistingTarget`'s duplicate checks
  for sidecar names, which must also learn to ignore `.ob-*` siblings when
  scanning for display-name conflicts).
- **Sweep timing.** Run the sweep where `RecoverAll` runs today
  (`AndroidStorageManager` startup, after the backend is ready), over the
  directories of each writable `StorageArea`. Per-directory lazy sweep on
  first listing is an acceptable alternative if startup listing cost
  matters; the constructor-time per-target recovery above already covers
  correctness for writes.
- **Failed listings.** A failed directory query means "unknown", not
  "clean" — the sweep skips that directory and retries next launch, which
  matches the current `MarkPending` behavior without needing attempt
  counters.
- **Diagnostics.** Attempt counts and last-error strings disappear with the
  journal. Keep the `SAF_TRANSACTION` / `SAF_RECOVERY` log lines; they are
  the diagnostics that actually get read.

## Removal Plan

Do this as its own PR after device validation, not inside the main branch
review.

1. **Precondition — device gates passed.** The FD-backed write path is
   confirmed working on the target provider under IL2CPP, including at least
   one adb-forced kill during each commit phase (while writing the temp,
   after backup rename, after install rename) showing the current recovery
   restores correctly. This proves the semantics the sweep must reproduce.
2. **Extract shared helpers.** Move `GetRecoveryRootDirectory` /
   `GetRootNamespaceId` out of `SafTransactionJournal` (they are used by
   `SafUserStorageBackend` and `SafStagedOutputPublisher`'s publications
   ledger) into a small `SafRecoveryPaths` helper. Move
   `SafDestinationLocks` to its own file.
3. **Rename the sidecar scheme.** Switch `SafFileWriteTransaction` to the
   `<target>.ob-tmp` / `.ob-bak` / `.ob-invalid` names. Add the
   constructor-time per-target recovery step and update
   `FindExistingTarget` to ignore `.ob-*` siblings.
4. **Rewrite recovery as a sweep.** Replace `RecoverAll`'s journal load with
   directory enumeration for sidecar suffixes; keep the restore logic.
   Delete the `rootIdOverride` / cross-root record handling — a sweep of the
   current tree is inherently root-scoped.
5. **Delete the journal.** Remove `SafTransactionRecord`,
   `SafTransactionJournal`, `SafTransactionState`, and every
   `Persist`/`Transition`/`Fail`-journal call from
   `SafFileWriteTransaction`. `Rollback`'s "namespace mutation requires
   startup recovery" branch simply leaves the sidecars for the sweep.
6. **Migration.** If this lands before the first public Google Play
   release: none needed; delete any `OpenBrushSafRecovery/*/journals`
   directory on startup. If users exist: keep the old journal-driven
   `RecoverAll` for one release as a pre-sweep pass (old GUID-named sidecars
   are unrecoverable without their journals), then delete both.
7. **Tests.** Replace the journal tests in `TestFile.cs`
   (`SafTransactionJournal_*`, journal-directory fixtures) with sweep tests
   against the fake backend: crash-after-temp, crash-after-backup-rename,
   crash-after-install, invalid canonical with valid backup, leftover
   sidecar colliding with a new write, failed listing leaves files
   untouched.
8. **Re-run the interruption matrix on device.** Same adb-kill scenarios as
   step 1, now against the sweep, plus an external-app check that leftover
   `<target>.ob-tmp` files are visibly associated with their sketch in the
   Files app (they now sort next to it, which is a minor UX improvement
   over anonymous `.ob-<guid>` litter).

## Rollback

Steps 3–5 are confined to `SafStorageTransaction.cs` and
`SafTransactionRecovery.cs`; no consumer outside `Storage/` sees anything but
`IStorageWriteTransaction`. If the sweep proves inadequate on device, revert
the PR — the interface boundary makes this a two-file revert.
