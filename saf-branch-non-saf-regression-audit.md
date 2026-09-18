# Non-SAF regression audit — `feature/saf-google-play-fd-backed`

Audit report for handover. Written to be read cold by an agent that has not seen
this branch's history.

| | |
| --- | --- |
| Audited commit | `58e5754d9` (branch head) |
| Merge base | `c9328362b` (`origin/main`) |
| Branch size | 419 commits, 163 files, +19,304 / −884 |
| Method | Source audit. No Unity Editor run, no build, no device. |
| Prior artefact | `Logs/pr1120-catalog-parity-audit.md` (2026-09-13, at `d61fc4c13` — **stale**, superseded in part by this document) |

**What this document is.** An assessment of whether the Android Storage Access
Framework (SAF) work on this branch can regress the platforms that do *not* use
SAF: Windows, macOS, Linux, plain Android (Quest), iOS. It is not a SAF review —
the SAF path's own correctness is out of scope except where it forced a change to
shared code.

**What it is not.** It is not a test report. Nothing here was executed. Every
statement is either a direct code reading (marked `[read]`), a call-site or guard
trace (`[traced]`), or a finding from a delegated deep-dive that has been
spot-checked (`[spot]`) or not (`[reported]`). Section 8 is explicit about the
limits.

**Verification markers used throughout**

| Marker | Meaning |
| --- | --- |
| `[read]` | I read the cited lines myself |
| `[traced]` | I followed every call site / guard myself |
| `[spot]` | Reported by a delegated deep-dive, and I re-read the load-bearing lines |
| `[reported]` | Reported by a delegated deep-dive, not independently verified |

---

## 1. Verdict

**The branch is contained in the way that matters, and is not a full-regression-cycle
risk. It does need a bounded, specific pass before it can ship to existing targets,
and it contains one item that should be fixed rather than tested around.**

Concretely, what makes it safe:

1. Sketch save on every non-SAF platform still runs the original code path.
2. No feature is disabled outside SAF.
3. No on-disk format change is active in a shipped build.
4. Every new failure mode sits behind a runtime backend check, and no non-SAF code
   path became unreachable.

What makes it need work:

1. One platform-blind bug in the Google Drive transfer-cancel key (§4.A).
2. A rewrite of the Saved Strokes catalog's cost model that is live on PC (§4.B.1).
3. Two intentional UX changes that need a product decision (§4.B.2, §4.B.3).
4. One live on-disk metadata change for sketches containing missing models (§4.B.4).
5. One global Android player setting flipped for targets that do not use SAF (§4.C.4).

**Estimated scope of the required pass:** the six non-SAF CI targets, with PC
(Windows primarily) as the focus because that is where `UseFileSystemWatcher = 1`
and therefore where items 1 and 2 of §4.B actually bite.

---

## 2. The design, so you do not have to re-derive it

The branch introduces a storage abstraction and routes consumers through it.

| Element | Location |
| --- | --- |
| `IUserStorageBackend`, `StorageArea`, `StorageDocument`, `StorageDocumentId` | `Assets/Scripts/Storage/UserStorageBackend.cs` |
| Backend selection | `UserStorage.CreateBackend`, `UserStorageBackend.cs:356-365` |
| SAF implementation | `Assets/Scripts/Storage/SafUserStorageBackend.cs` |
| Local implementation | `LocalUserStorageBackend`, `UserStorageBackend.cs:369-782` |
| Shared tree walker | `StorageTreeEnumerator`, `UserStorageBackend.cs:815-995` |
| Scoped-storage predicate | `OpenBrushStorage.IsScopedStorageMode`, `OpenBrushStorage.cs:26-36` |

**Selection is runtime, not compile-time:**

```csharp
// UserStorageBackend.cs:356-365
private static IUserStorageBackend CreateBackend()
{
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
    if (OpenBrushStorage.IsScopedStorageMode)
    {
        return new SafUserStorageBackend();
    }
#endif
    return new LocalUserStorageBackend();
}
```

So the SAF backend is reachable only under
`UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE && RuntimePlatform.Android`. The define
is set only for `BuildTarget.Android` when `-btb-scoped-storage` is passed
(`Assets/Editor/BuildTiltBrush.cs:813`, `:1656`).

**Which CI flavours carry the define** (`.github/workflows/build.yml`):

| Flavour | SAF active? |
| --- | --- |
| Android AndroidXR | yes |
| Android Viewer OpenXR | yes |
| Android Viewer AndroidXR | yes |
| Android OpenXR | **no** |
| Android Meta Quest | **no** |
| Windows OpenXR | no |
| Linux | no |
| MacOS | no |
| iOS Zapbox | no |

Six of the nine CI targets are non-SAF and are the subject of this report.

**Three files are important context for the findings:**

- `PlatformConfigPC.asset` has `UseFileSystemWatcher: 1`;
  `PlatformConfigMobile.asset` has `0`. `EditTimeAssetReferences.cs:52-67` maps
  Windows, Linux and OSX to PC. **So OS-level file watching is a desktop-only
  behaviour**, which is why the `FileWatcher` findings are PC-scoped.
- `Main.unity:26072` has `m_CaptureHiResSaveIcon: 0`, which downgrades one
  would-be format change to inert.
- `Assets/Plugins/Android/proguard-user.txt` is four additive `-keep` lines.

---

## 3. Why the containment is real — evidence, so you can stop looking here

Each of these was checked deliberately because it is a cheap way for this kind of
change to break the other six targets.

1. **`[traced]` The desktop sketch save path is untouched.** `SaveLoadScript.SaveLow`
   computes `directSafSave = !saveToLocalCacheOnly && UserStorage.Backend.Kind ==
   StorageAccessFramework` (`SaveLoadScript.cs:466-468`). On non-SAF that is false,
   so the code takes the original branch: `FileUtils.CheckDiskSpaceWithError(m_SaveDir)`
   (`:484`) and `snapshot.WriteSnapshotToFile(fileInfo.FullPath)` on a worker
   (`:511+`). `m_SaveDir` is still `App.UserSketchPath()` (`:254-256`).
   `DiskSceneFileInfo`'s delete / rename / read still call `System.IO` directly
   (`DiskSceneFileInfo.cs:192,239,272,315`).

2. **`[read]` The legacy branch is preserved verbatim, not reimplemented.**
   `SketchCatalog.CreateUserSketchSet` and `CreateSavedStrokesSketchSet` still
   return a plain `FileSketchSet` on desktop (`SketchCatalog.cs:81-97`). The
   pervasive pattern in the shared catalogs is an early return:
   `if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework) { ...old code...; return; }`
   — see `ReferenceImageCatalog.cs:77,91,569`, `VideoCatalog.cs:53,82`,
   `SoundClipCatalog.cs:54,147,473,528`, `QuillFileCatalog.cs:47,79`,
   `BackgroundImageCatalog.cs:34`, `ModelCatalog.cs:109`.

3. **`[traced]` `DriveSync` keeps the local decision rule.** `UploadItemAsync` still
   uploads with `requireSeekable: true` through `backend.OpenRead`, which on the
   local backend is `new FileStream(path, FileMode.Open, FileAccess.Read,
   FileShare.Read)` (`UserStorageBackend.cs:458-459`) — byte-for-byte the same share
   mode as the pre-branch `DriveSync.cs:996`. The content-hash ledger is used only
   on SAF; the local backend keeps the timestamp comparison
   (`DriveSync.cs:1041-1056`). `DownloadItemAsync` early-returns to
   `DownloadLocalItemAsync` when `Kind == Local` (`DriveSync.cs:1619-1623`).

4. **`[read]` The new SAF classes are ungated but stub-safe.** `SafSketchSet`,
   `SafUserStorageBackend`, `SafMediaHttpServer`, `SafStagedOutputPublisher`,
   `SafRootChangeGuard`, `RuntimeContentSeeder`, `DriveSyncLedger` and
   `ModelRestoreGate` contain **zero** preprocessor directives; they gate themselves
   at runtime. `AndroidSafStorage` provides an `#else` stub for every member
   (`AndroidSafStorage.cs:35-383`). A non-Android build therefore cannot fail to
   compile because of this branch.

5. **`[read]` `LoadingScene.cs` guards were narrowed correctly.** `#if UNITY_ANDROID`
   became `#if UNITY_ANDROID && !OPEN_BRUSH_SCOPED_STORAGE` at `:20,43,73,126`. On
   non-Android both are false, so nothing was deleted from desktop behaviour.

6. **`[read]` No scene or prefab changed.** Only `ProjectSettings.asset` moved.
   `AndroidStorageManager` bootstraps through a guarded
   `[RuntimeInitializeOnLoadMethod]` (`AndroidStorageManager.cs:36-53`) that returns
   immediately when `IsScopedStorageMode` is false, so no desktop scene gained a
   component.

7. **`[read]` The "disabled feature" claims in the handover are true.** Verified
   independently rather than trusted:
   - `Model.cs:1141-1152` — USD/FBX/PLY/gaussian import restriction is gated on
     `UserStorage.Backend.Kind == StorageAccessFramework`.
   - `SvgTextUtils.cs:31-36` — custom-font bail-out is gated the same way.
   - `SketchControlsScript.cs:4149-4158` — bulk-export skip is keyed on
     `rInfo is SafSceneFileInfo`.
   All three are inert on non-SAF platforms. Full parity is preserved.

8. **`[traced]` No non-SAF path became unreachable.** Every `EnumerateTree` call site
   (`LuaManager.cs:539`, `SoundClipCatalog.cs:407,476`, `SafSketchSet.cs:557`,
   `SafTransactionRecovery.cs:98`) is SAF-gated or SAF-only. The shared
   `StorageTreeEnumerator` failure modes — duplicate-name rejection, 10k item cap,
   depth cap, `OrdinalIgnoreCase` sort (`UserStorageBackend.cs:874-891,906-912,940-941`)
   — are therefore not reachable from desktop code. `LocalUserStorageBackend.List`
   preserves the old "missing directory → `NotFound`" behaviour
   (`UserStorageBackend.cs:395-399`) and its callers convert that to an empty
   listing.

9. **`[traced]` Recovery and root-guard machinery never runs on desktop.**
   `SafTransactionRecovery.RecoverAll` refuses any backend that is not SAF
   (`SafTransactionRecovery.cs:40-46`), and its only non-test entry point is
   `AndroidStorageManager.cs:111-113`, itself behind `IsScopedStorageMode`.
   `SafRootChangeGuard.cs:42` early-returns the same way.

10. **`[read]` No existing test was weakened.** Every changed file under
    `Assets/Editor/Tests/` shows `-0` deletions. The branch only added tests
    (~3,900 lines), and a meaningful subset drives `LocalUserStorageBackend`
    directly: `TestFile.cs` (11 sites), `TestSoundClipStorage`,
    `SavedStrokeCatalogParityTests`, `TestImageCatalogLifecycle`,
    `TestMediaCatalogScanLifecycle`, `TestReferenceMediaStorage`,
    `TestImageCatalogQueries`, `TestReferenceImageRestoration`. **The Editor run on
    Windows is a genuine safety net for the indirection layer** — it is not vacuous.

11. **`[read]` `SharedUserConfig` preserves the desktop read.** `ReadText` skips the
    SAF branch entirely when the backend is not SAF and falls through to
    `File.Exists(localPath) ? File.ReadAllText(localPath, Encoding.UTF8) : null`
    (`SharedUserConfig.cs:30-64`) — equivalent to the code it replaced.

12. **`[spot]` `ImageCache` identity is unchanged on the local path.** Every local
    `ReferenceImage` constructor leaves `m_CacheIdentity` null
    (`ReferenceImage.cs:153-157`), so the new `virtual:` exemption
    (`ImageCache.cs:170-182`) cannot trigger; local signatures are still
    `filePath + version + Length + CreationTime + LastWriteTimeUtc`.

---

## 4. Findings

Severity is about *likelihood and user impact on a non-SAF platform*, not about how
hard the fix is.

### 4.A — Fix before shipping

#### A1. `[read]` Google Drive transfer-cancel key no longer matches

**Severity: MEDIUM-HIGH. Platform-blind (all desktop and Quest Drive users).**

`SaveLoadScript.cs:518-521` now calls:

```csharp
string transferId = fileInfo.StorageId ?? fileInfo.FullPath;
Task cancelTask = string.IsNullOrEmpty(transferId)
    ? Task.CompletedTask
    : App.DriveSync.CancelTransferAsync(transferId);
```

Pre-branch this was `App.DriveSync.CancelTransferAsync(fileInfo.FullPath)`.

`CancelTransferAsync` matches the incoming key against the transfer's document id
via `MatchesTransferDocument` (`DriveSync.cs:1838-1845,1847-1856`):

```csharp
return item.DocumentId.IsValid && !string.IsNullOrEmpty(storageId) &&
    string.Equals(item.DocumentId.Value, storageId,
        backendKind == StorageBackendKind.StorageAccessFramework
            ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
```

The only assignment of `SyncItem.DocumentId` in the codebase is `DriveSync.cs:926`,
from `localFile.DocumentId` — a `StorageDocument` identity, which on the local
backend is **the file path**.

But for a sketch coming from the Google Drive set,
`GoogleDriveFileInfo.StorageId => m_File?.Id` (`GoogleDriveSketchSet.cs:58`) — a
**Drive file id**.

So for a Drive-sourced sketch the cancel key changed from a path that matched to a
Drive id that cannot match. Saving such a sketch no longer cancels its pending
transfer, where it previously did; the worst case is a Drive download landing on
top of the sketch just saved.

**Why the tests do not catch it.** `TestFile.cs:1571-1573` exercises
`MatchesTransferDocument` with a synthetic opaque id
(`new StorageDocumentId("opaque/document:ABC")`), never with a `GoogleDriveFileInfo`.

**Suggested resolution.** Decide the intended identity for the cancel key and make
it consistent with how `SyncItem.DocumentId` is populated. The narrow fix is to
pass `fileInfo.FullPath` when the backend is local, or to have
`GoogleDriveFileInfo.StorageId` return the path. Either way add a regression test
that uses a Drive-set file info rather than a synthetic id.

### 4.B — Must-test on non-SAF targets

#### B1. `[read]` The Saved Strokes catalog was rewritten into a recursive index with full rebuilds

**Severity: MEDIUM. PC only in practice (requires `UseFileSystemWatcher = 1`), but
the recursion applies everywhere.**

This is the largest non-SAF behaviour change on the branch, and it is *intentional*
— a new navigable feature — not an accident. Both delegated deep-dives converged on
it independently.

1. `FileSketchSet.cs:411-413` — SavedStrokes now constructs the **one-argument**
   `FileWatcher(m_SketchesPath)`, which is recursive and unfiltered
   (`FileWatcher.cs:29-37` sets `IncludeSubdirectories = true`), where it previously
   used `new FileWatcher(path, "*.tilt")`.
2. `FileSketchSet.cs:423,428,433` — for SavedStrokes every watcher event now sets
   `m_RefreshRequested` instead of enqueuing one incremental add or delete.
3. `FileSketchSet.cs:578-589` — `ProcessDirectory` recurses into subdirectories for
   SavedStrokes on all platforms, unbounded depth, reparse points skipped.
4. `FileSketchSet.cs:489-506` — I verified directly that **pre-branch
   `RequestRefresh()` was an empty method body** (`{ }`), and that **both call sites
   do not exist at the merge base** (`SavedStrokesCatalog.cs:69` and `:259` are new).
   The new implementation calls `UnloadIcon()` on every sketch, clears
   `m_RequestedLoads` and `m_Sketches`, rescans the tree and fires `OnChanged()`.

Net effect on desktop: the set went from a cheap top-level listing with incremental
updates to a recursive library-wide index torn down and rebuilt on every folder
navigation, every saved-stroke storage change, and every filesystem event anywhere
in that tree.

**Also note the two views can now disagree** (`[reported]`): the reference-panel
folder page stayed direct-children-only via `IsDirectChildPath`
(`SavedStrokesCatalog.cs:312-322`), while the set became recursive.

**What to test.** See §5 items 2 and 4.

#### B2. `[read]` Saved strokes in subfolders no longer appear at the library root

**Severity: MEDIUM. Platform-blind.**

Pre-branch `SavedStrokesCatalog.cs:194` filtered with
`sketchFileInfo.FullPath.StartsWith(m_CurrentSavedStrokesDirectory)`. The new
`IsInCurrentDirectory` uses `IsDirectChildPath` on the non-SAF path
(`SavedStrokesCatalog.cs:312-322`).

So nested saved strokes no longer appear at the root and now require navigating into
the folder. The comment at `:310-311` says this is intended ("The sketch set is a
library-wide index; the reference panel is a folder page"), and the old prefix test
had a boundary bug (`Foo` also matched `FooBar`), so this is a deliberate trade.
It is still a user-visible listing change and needs a product decision.

**Interaction with B1:** B1 makes the *set* recursive while B2 keeps the *page*
flat. Confirm those two are consistent with the intended UX, because they were
changed by the same branch.

#### B3. `[spot]` The Blocks folder is no longer created, watched, or offered

**Severity: MEDIUM. Platform-blind.**

`ModelCatalog.PrepareModelWatchDirectory` (`ModelCatalog.cs:129-138`) now only calls
`Directory.CreateDirectory` when the directory equals `App.ModelLibraryPath()`, and
the caller `continue`s when the path does not exist (`:159`). The comment says
Blocks "must already exist before we watch it" — defensible, since Blocks is an
externally managed source.

The knock-on: a user who has never had a Blocks folder loses the "Open Blocks"
navigation entry, because `ReferencePanel.cs:307-310` requires `Directory.Exists`.
Previously the app created the folder and offered the entry.

**What to test.** See §5 item 3.

#### B4. `[read]` Missing-model metadata written into saved `.tilt` files changed

**Severity: MEDIUM. Platform-blind, and this one is live.**

Pre-branch `MissingModels` hand-built sparse `TiltModels75` records
(`ModelCatalog.cs:63-83` at the merge base): `FilePath` always, `Transforms` from the
normalized dictionary or null, `RawTransforms` only on the first of the two union
sets — leaving `Subtrees`, `PinStates`, `GroupIds`, `LayerIds`, `SplitMeshPaths` and
`NotSplittableMeshPaths` at their defaults. The new version returns the full cached
record via `GetMissingModelData` (`ModelCatalog.cs:82-90`).

This reaches disk, which is what makes it live rather than theoretical:
`MetadataUtils.GetTiltModels` concatenates `ModelCatalog.m_Instance.MissingModels`
straight into the serialised model array
(`Assets/Scripts/Save/MetadataUtils.cs:152-154`), and `TiltModels75` is a UnityGLTF
serialisable type, so every populated field is written. Saved metadata for a sketch
containing a missing model therefore carries those extra fields on every platform.

Plausibly a restore fix, but it changes what lands on disk. Unlike the hi-res entry
change (§4.C.1) this is **not** inert behind a scene flag.

**Related, `[reported]`:** `ModelCatalog.cs:296-299` now passes `data.Transforms` as
`xfs` and `data.RawTransforms` as `rawXfs`, where the base put the raw array in the
`xfs` slot with `rawXfs` null. The new mapping matches the normal load path
(`ModelWidget.cs:915-926`), so it looks like a fix — but it changes restored widget
geometry on all platforms.

**Open question the next agent should close:** whether cached `GroupIds` are
pre- or post-remap at load time, i.e. whether ids replayed on re-save are stale.
`TiltFile.cs` was not read for that.

### 4.C — Low, latent, or informational

1. **`[spot]` `.tilt` hi-res entry name changed — inert in the shipped build.**
   `SketchSnapshot.WriteSnapshot` extracted the archive body so both backends share
   it, and hi-res bytes now go to `FN_HI_RES` (`hires.png`) instead of a second
   entry named `FN_THUMBNAIL`, as the pre-branch `WriteSnapshotToFile` did.
   **Downgraded to LOW because `Main.unity:26072` has `m_CaptureHiResSaveIcon: 0`**,
   and nothing in the repo reads `hires.png`. Only reachable if that flag is turned
   on, in which case archives differ from every previously saved sketch and from
   external readers of `thumbnail.png`.

2. **`[read]` `LocalWriteTransaction` is a new sequence with no sweeper.**
   `UserStorageBackend.cs:653-781` writes `target.obtmp-<guid>`, renames target to
   `target.obbackup-<guid>`, renames tmp over target, deletes the backup. A crash
   between the two `File.Move` calls leaves the user's file surviving only as
   `Foo.tilt.obbackup-<guid>`, invisible to the catalog. `SafTransactionRecovery`
   refuses non-SAF backends (`:40-46`) and only knows `.ob-tmp` / `.ob-bak` /
   `.ob-invalid` (`:79-82`), not the local names. **Latent, not live:** all twelve
   `BeginWrite` call sites are SAF/scoped-gated, and the `SeedSafDefaults` routines
   check `Kind == StorageAccessFramework` before starting.

3. **`[reported]` On-demand video resolution returns an uninitialised
   `ReferenceVideo`.** `VideoCatalog.cs:612-617` + `:648-649` fall back to
   `ResolveVideoByPersistentPath`; `VideoCatalog.cs:514` is the only `Initialize()`
   call site, so the result has `Aspect == 0` and a null thumbnail.
   `VideoWidget.SetVideo` clamps size from `Aspect` (`VideoWidget.cs:51-57,69`)
   where the base returned null and got a 16:9 dummy plus a "Could not find video"
   message (`VideoWidget.cs:177-182`). Affects sketches whose video exists but sits
   in a folder that is not currently displayed. **Static reading only.**

4. **`[read]` `useCustomProguardFile` flipped 0 → 1 for all Android flavours.**
   `ProjectSettings/ProjectSettings.asset:285`. This is a global Android player
   setting, so it now applies to **Android OpenXR** and **Android Meta Quest**,
   which do not define `OPEN_BRUSH_SCOPED_STORAGE` and do not need it. I read
   `Assets/Plugins/Android/proguard-user.txt`: four additive `-keep` lines for
   classes that do not exist in those builds, merged with Unity's defaults rather
   than replacing them, so I expect it to be harmless. But it is a release
   minification change on targets that did not ask for it, which is the classic
   shape of a works-in-dev/breaks-in-release bug. Verify the diff against `main`
   yourself — it is not inherited from `main` (main still has `0`).

5. **`[reported]` `VideoCatalog.ChangeDirectory` can defer a rescan indefinitely.**
   `VideoCatalog.cs:73-80` with `:307-320`: the rescan is deferred while
   `m_ScanningDirectory` is true, and only the scan wrapper's `finally` clears it.
   `ReferenceVideo`'s waits are unbounded (`ReferenceVideo.cs:324-331,364-372`). A
   video whose `Prepare` never completes would wedge all later directory scans.
   **Premise unverified — nobody has run it.** Worth one deliberate test.

6. **`[reported]` `StillFrameSequenceExporter.IsSaving` changed meaning.**
   The field was renamed and the public property is now
   `!m_IsCapturing && m_IsWritingMetadata`. Any UI or logic gating on `IsSaving`
   during capture sees a different answer than before, on every platform.

7. **`[read]` `WidgetManager` Blocks path comparison hard-codes `OrdinalIgnoreCase`.**
   `GetBlocksModelSubpath` compares with `StringComparison.OrdinalIgnoreCase`
   unconditionally, where the old code used `CanonicalizeForCompare`. On
   case-sensitive filesystems that misclassifies paths. Note the rest of the branch
   gets this right — `LocalUserStorageBackend.ResolveRelativePath`
   (`UserStorageBackend.cs:616-618`) and `ApiMethods.FileSystemPathComparison`
   (`ApiMethods.cs:1891-1894`) both branch on `Path.DirectorySeparatorChar` — so
   this spot is inconsistent with the branch's own convention. **Relevant to Linux
   and macOS.**

8. **`[read]` `ZipSubfileReader` now reads through a stream.**
   Both variants open via `File.OpenRead(zipPath)` and set ownership explicitly
   (`ZipSubfileReader.cs:34-36,85-86`) instead of `new ZipFile(path)` /
   `ZipFile.Read(path)`. The ownership hand-off looks correct. **Unverified:** I
   could not determine what share mode SharpZipLib's own path constructor used,
   because the package ships only `Unity.SharpZipLib.dll`
   (`Library/PackageCache/com.unity.sharp-zip-lib@.../Runtime/`) with no source.

9. **`[read]` `DefaultMediaSeeder` now evaluates `Directory.GetFileSystemEntries`
   unconditionally.** `DefaultMediaSeeder.cs:40-42` — previously short-circuited
   behind `!initialized`. A missing directory would now throw
   `DirectoryNotFoundException`. Unreachable today because the only production
   caller, `App.SeedDefaultMedia` (`App.cs:2200-2204`), calls `InitDirectoryAtPath`
   first. One refactor away from a bug.

10. **`[spot]` Watched scope narrowed in two catalogs.**
    `VideoCatalog.cs:85` and `SoundClipCatalog.cs:531` switched to the two-argument
    `FileWatcher(path, filter)`, and `FileWatcher.cs:39-46` does **not** set
    `IncludeSubdirectories`. So subfolder changes no longer raise events there.
    Consistent with the flat direct-child listing, but a real reduction in scope.

11. **`[spot]` Extension and URL matching became case-insensitive.**
    `VideoCatalog.cs:561-567` — `CLIP.MP4` now appears where the old
    `m_supportedVideoExtensions.Contains(...)` was case-sensitive.
    `ReferenceVideo.cs:209` — `CLIP.TXT` is now read as a URL list instead of being
    handed to `VideoPlayer`. Both are arguably fixes; both are behaviour changes.

12. **`[spot]` `Directory.GetFiles` failures are now swallowed.**
    `VideoCatalog.cs:343-352`, `SoundClipCatalog.cs:185-197` catch, warn and treat
    the scan as empty. This fixes the stuck-scan symptom of the known upstream
    defect U6, at the cost of a missing folder degrading silently. Compare against
    `Logs/pr1120-catalog-parity-audit.md` §5 U6.

13. **`[reported]` Retired catalog entries now destroy their thumbnails
    immediately** (`ReferenceVideo.cs:424-431`, `SoundClip.cs:468-477`), so an icon
    still holding that texture sees a destroyed `Texture2D` until the next
    `CatalogChanged`.

14. **`[read]` Source-breaking interface additions.** `SketchSet.NotifySketchDeleted`
    (`SketchSet.cs:76`) and `SceneFileInfo.StorageId` (`SceneFileInfo.cs:46`).
    In-repo all four implementers of each were updated; out-of-tree C# implementers
    would break. Lua plugins are unaffected.

15. **`[spot]` `IcosaAssetCatalog.cs:1270-1283`** — the deliberate `File.OpenRead`
    probe that provoked a typed exception was dropped; the sole caller wraps
    failures generically (`:1227-1230`), so only the message changed. INFO.

16. **`[reported]` `ModelCatalog.OnChanged` drops events** whose source watcher is
    not yet in `m_FileWatchers` (`ModelCatalog.cs:211-219` with `:160-168`), so an
    event arriving after `EnableRaisingEvents` but before the add is lost. No
    evidence of an actual lost event; the window is theoretical. INFO.

---

## 5. Retest plan

Ordered by value per unit of effort. Each item has an explicit pass criterion so a
later agent can report pass/fail rather than "looks fine".

1. **Drive transfer cancel (fixes §4.A1).** Save a sketch that came from the Google
   Drive set while a transfer for it is pending, on Windows.
   *Pass:* the transfer is cancelled. *Fail:* it completes and overwrites the save.
2. **Saved Strokes catalog on PC (§4.B1, §4.B2).** Create nested folders with saved
   strokes. Navigate between folders. Add/remove/rename files inside the tree while
   the panel is open.
   *Pass:* correct listing per the agreed UX, no visible hitch, no duplicate or
   ghost entries, no unbounded rescan storm. *Record:* how long a navigation takes
   with a large tree, since the rebuild is now unconditional.
3. **Blocks folder (§4.B3).** Start with no Blocks folder present.
   *Pass:* either the "Open Blocks" entry still appears, or its absence is an
   accepted product decision recorded somewhere.
4. **Sketch round-trip with a missing model (§4.B4).** Save a sketch referencing a
   model that is not on disk, restart, load it.
   *Pass:* the sketch loads with the model reported missing and no geometry change.
   Also re-save and diff the `.tilt` metadata against a pre-branch save.
5. **`.tilt` compatibility.** Save on the branch, load on `main`, and vice versa.
   *Pass:* both directions load. Do this with `m_CaptureHiResSaveIcon` off (the
   shipped default) and, for completeness, on.
6. **Quest / plain Android release build (§4.C4).** Build the non-SAF Android
   flavour in release/IL2CPP with the new custom proguard setting.
   *Pass:* app starts and catalogs populate. This is the only item here that needs a
   release build rather than a dev build.
7. **Linux and macOS (§4.C7).** Exercise the Blocks navigation path and a
   case-mismatched media path.
   *Pass:* Blocks models resolve; no case-related misclassification.
8. **Video edge case (§4.C5).** A sketch whose video sits in a folder that is not
   displayed, plus a deliberately stalled `Prepare`.
   *Pass:* other directory scans still complete.
9. **iOS.** Compile-only is probably sufficient; the sandbox stubs cover it and no
   non-SAF iOS-specific path changed.

**Cheap regression check that covers a lot:** run the full EditMode suite in the
Editor on Windows. Per `saf-plan-of-record.md` §3 expect ~443 failures of which 416
are `Autodesk.Fbx` missing a native library and the rest pre-existing. What matters
is that the SAF and local-backend suites stay green and no *new* failure appears
outside those two known groups. Note the suite deletes
`Assets/Resources/PerformanceTestRun*.json` from the working tree; those are
gitignored.

---

## 6. Checked and cleared — do not re-litigate

Recorded so the next agent does not spend hours re-deriving them.

1. `[spot]` `LocalUserStorageBackend`'s missing-directory → `NotFound` semantics are
   absorbed correctly; callers convert to an empty listing, matching the old
   `Directory.Exists` guard.
2. `[read]` `SketchSnapshot` hi-res / `TiltFile` header handling introduces no
   on-disk format change beyond §4.C1, and `TiltFile`'s local read path is unchanged.
   Corrupt/partial file tolerance is unchanged or slightly better: local
   `GetReadStream` still returns null on corruption (`TiltFile.cs:375-406`).
3. `[read]` `FileWatcher.Renamed` forwarding (`FileWatcher.cs:62-70`) is a
   **normalisation, not a new hazard.** `FileSketchSet.cs:415-419` is a pre-existing
   comment stating "Renamed event not implemented on OS X, so we rely on Deleted +
   Created" — the codebase was already compensating by hand. Residual, LOW:
   `FileSketchSet.RenameSketch` also calls `NotifyDelete` explicitly, so a rename can
   enqueue a second add/delete pair for User sketches. Refresh-flag consumers are
   idempotent; the add/delete enqueue path is worth a dedup check but is unlikely to
   be user-visible.
4. `[spot]` `CatalogChangeQueue` replacing lock + switcheroo is thread-safe and
   behaviour-preserving.
5. `[spot]` `SoundClip`'s saved-path resolution still requires `File.Exists`
   (`SoundClipCatalog.cs:489`); public constructors keep `PersistentPath` /
   `AbsolutePath` as before.
6. `[spot]` `ThreadedImageReader`'s new stream constructor is additive; the local
   path still uses the path constructor.
7. `[spot]` `QuillFileCatalog`'s local `*.imm` scan body is unchanged
   (`TopDirectoryOnly`, `LastWriteTimeUtc` ordering). `m_IsScanningDirectory` now
   clears in a `finally`, and `ForceCatalogScan` queues instead of dropping.
8. `[spot]` `ModelCatalog`'s extension matching was already `OrdinalIgnoreCase`
   (`GetSupportedExtensions`), so no change there.
9. `[read]` `App.cs` autosave restore is explicitly skipped under scoped storage
   only (`!OpenBrushStorage.IsScopedStorageMode`, `App.cs:769-771`); desktop is
   unchanged.
10. `[read]` `SafMediaHttpServer.Register()` is a no-op off SAF
    (`SafMediaHttpServer.cs:45`).
11. `[read]` `Export.ExportScene`'s new space check is SAF-gated and the desktop
    console message "Located in " + `App.UserExportPath()` is preserved verbatim in
    the `else` branch.
12. `[read]` `FileUtils.CheckSharedStorageSpaceWithError` is called only when
    `directSafSave` is true.
13. `[read]` `SceneSettings.LoadCustomSkybox`'s new
    `Path.GetRelativePath(BackgroundImagesLibraryPath(), path)` is a no-op for a
    bare filename, because `ApiMethods.GetSafeRelativePathInDirectory`
    (`ApiMethods.cs:1865-1889`) has already validated and combined it. **No skybox
    name format break.** (Worth stating explicitly: this looked alarming in the diff
    and is not a problem.)
14. `[read]` `MultiCamTool` uses a proper dual path — `if (error != null ||
    !OpenBrushStorage.IsScopedStorageMode) { FinishGifSave(path, error); return; }`.
15. `[read]` `ImportGltfast`'s SAF branch early-returns when the backend is not SAF
    (`ImportGltfast.cs:96-100`).
16. `[read]` `ApiManager` skipping user scripts unless local is deliberate
    (`ApiManager.cs:232`); the SAF path is handled separately.

---

## 7. The verification gap that matters most

This is the part worth escalating to a human, more than any individual bug.

1. The branch's own parity audit (`Logs/pr1120-catalog-parity-audit.md`, dated
   2026-09-13 at `d61fc4c13`) opens with **"SAF/non-SAF parity is not established.
   The non-SAF implementation also contains defects."** It lists nine pre-existing
   non-SAF defects (U1–U9) and states that no Unity Editor was launched and no
   integration testing was performed. It is five days and several commits stale
   relative to `58e5754d9`, so treat it as indicative, not current.

2. `saf-plan-of-record.md` §1 states the SAF write path **has never run on a
   device**. So SAF's risky half is unproven, *and* the non-SAF side's coverage
   rests on EditMode plus a stubbed out-of-Unity harness
   (`Logs/pr1120-parity-regressions.log`: "43 production/NUnit cases passed outside
   Unity").

3. `Logs/Editor.log` is dated 13/09 — five days stale. There is no recent evidence
   of a green full EditMode run on this commit, and the ~443 known failures make
   "did my change break something?" hard to read off the results.

4. **Net:** the branch is well engineered for confinement, but the confinement has
   not been demonstrated by execution on any platform. That gap is cheap to close
   relative to a device bring-up and is the single highest-value next action.

---

## 8. Coverage, confidence, and known gaps

**How this audit was produced.** I audited the storage layer, save/load path,
catalogs, sharing, export and App/scene bootstrapping directly, and traced every
`BeginWrite`, `Backend.Rename` / `Backend.Delete`, `EnumerateTree` and
`IsScopedStorageMode` call site to its guard. Three delegated deep-dives covered the
catalog/media layer, the save/load/sketch layer, and the sharing/API/misc layer; the
first two returned full reports, whose load-bearing claims I re-read and which are
marked `[spot]` or `[reported]`. The third was stopped without reporting.

**Read directly, high confidence:** `UserStorageBackend.cs`, `OpenBrushStorage.cs`,
`AndroidSafStorage.cs` (structure), `SafTransactionRecovery.cs`, `SharedUserConfig.cs`,
`SaveLoadScript.cs`, `SketchSnapshot.cs`, `FileSketchSet.cs`, `SketchCatalog.cs`,
`SceneFileInfo.cs` / `DiskSceneFileInfo.cs`, `TiltFile.cs` (zip settings and entry
names), `ModelCatalog.cs` (targeted), `DriveSync.cs` (targeted), `SceneSettings.cs`,
`WidgetManager.cs` (diff), `FileWatcher.cs`, `DefaultMediaSeeder.cs`,
`ZipSubfileReader.cs`, `App.cs` (targeted), `LoadingScene.cs`, `FileUtils.cs`,
`SharedUserConfig.cs`, `MultiCamTool.cs` (diff), `Export.cs` (diff),
`Save/MetadataUtils.cs` (the missing-model serialisation path).

**Read only at diff or grep level, lower confidence:** `Widgets/ModelWidget.cs`,
`API/ApiManager.cs` and `API/Lua/LuaManager.cs` internals, `Export/ExportUsd.cs`,
`Poly/IcosaAssetCatalog.cs`, `Sharing/DriveSyncLedger.cs`, `ReferenceImage.cs`,
`ReferenceVideo.cs`, `SoundClip.cs`, `QuillFileCatalog.cs`, `BackgroundImageCatalog.cs`,
`ThreadedImageReader.cs`, `ImageCache.cs`, `SafSketchSet.cs`, `SafUserStorageBackend.cs`.

**Explicitly not established:**

1. Anything requiring execution: no build, no Editor run, no device, no test run. All
   statements are static.
2. Whether a `ReferenceVideo.Prepare` can actually hang without raising
   `errorReceived` (§4.C5).
3. Whether an on-demand video is `Initialize()`d anywhere other than
   `VideoCatalog.cs:514` (§4.C3).
4. Whether cached `GroupIds` are pre- or post-remap at load, i.e. whether replayed
   ids are stale on re-save (§4.B4).
5. What share mode SharpZipLib's own path constructor used (§4.C8) — the package
   ships only a DLL.
6. The runtime cost of the Saved Strokes rebuild (§4.B1). Static analysis only.
7. Whether the changed saved-strokes visibility (§4.B2) matches user expectation.
   The code change is confirmed; the UX judgement is not mine to make.

---

## 9. Corrections made during this audit

Recorded because a reader may find earlier reasoning that these supersede.

1. **`FileWatcher.Renamed` was initially rated MEDIUM as a duplicate-notification
   hazard. Downgraded to LOW.** The `FileSketchSet.cs:415-419` comment predates the
   branch and already documents that OS X does not raise `Renamed`, so the handler
   is a normalisation. See §6.3.
2. **`StorageId ?? FullPath` was initially called behaviour-preserving on desktop.
   Wrong.** That conclusion came from checking `DiskSceneFileInfo` only; the Google
   Drive case is a real regression. See §4.A1.
3. **`SceneSettings`' skybox changes initially looked like a `.tilt` name-format
   break. They are not.** `GetSafeRelativePathInDirectory` makes the relative
   conversion a no-op for a bare filename. See §6.13.
4. **The `SketchSnapshot` hi-res entry change was initially MEDIUM. Downgraded to
   LOW** once `Main.unity:26072 m_CaptureHiResSaveIcon: 0` was verified. See §4.C1.
5. **`DefaultMediaSeeder`'s eager `Directory.GetFileSystemEntries` was initially
   MEDIUM as a potential `DirectoryNotFoundException`. Downgraded to LOW** after
   confirming `App.SeedDefaultMedia` calls `InitDirectoryAtPath` first. See §4.C9.

---

## 10. Document status

Written 2026-09-18 against `58e5754d9`. **Untracked by design** — repository policy
in `AGENTS.md` is that temporary design, planning and audit documents are not
committed unless explicitly requested. Do not commit or push this file without
asking.

The live handover document for the branch itself remains `saf-plan-of-record.md`;
this document supplements it with a non-SAF specific risk assessment and does not
supersede it.
