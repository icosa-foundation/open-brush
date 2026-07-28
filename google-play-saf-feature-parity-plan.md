# Google Play SAF Feature-Parity Cleanup Plan

## Status

Implemented follow-up to `google-play-saf-fd-backed-storage-plan.md`.

The code now includes first-class SAF trees for `Scripts`, `Plugins`, and
`Fonts`; root-scoped coherent runtime projections; migration of legacy private
content; transactional publication of built-in and copied plugin content; and
storage-backend-based Google Drive sync with root-scoped conflict state.

Amendment (2026-07-28): the automatic `ContentObserver` provider-observation
layer described in this plan was implemented and later removed. Mobile
platform configs ship with `UseFileSystemWatcher: 0`, so no existing Android
build (Quest, Pico) has live external-change detection; observation exceeded
the parity bar this plan exists to meet, and its background-triggered
refreshes were the main source of concurrent-refresh complexity. Runtime
content is refreshed at startup, after folder selection or root changes, and
on application resume — which covers external edits made over USB, the Files
app, or a browser, since all of those background the application first. The
observation-related sections and test matrices below are retained for
history but are superseded by this amendment.

Desktop, editor, Google Play Android-symbol, non-Google-Play Android-symbol,
and Java static compilation pass. Unity and a target Android device are
unavailable, so the automated Unity tests and device acceptance matrix in this
document remain release gates and have not been claimed as run.

The FD-backed implementation keeps shared sketches and media canonical in the
user-selected SAF tree, but it deliberately left three user-managed directories
app-private and disabled Google Drive folder sync. Those choices are functional
regressions, not requirements imposed by Android's Storage Access Framework.

This plan closes those gaps while retaining the safety properties of the
FD-backed architecture:

- `Scripts`, `Plugins`, and `Fonts` are user-visible, uninstall-durable content
  in the selected `Open Brush` tree;
- their existing path-based runtime consumers continue to receive ordinary
  filesystem paths through bounded app-private projections;
- external changes are reflected automatically, without requiring routine
  manual refresh;
- Google Drive folder sync works through `IUserStorageBackend` on SAF and local
  filesystems;
- autosaves remain deliberately app-private recovery data;
- non-Google-Play behavior remains unchanged.

## Problem Statement

### User-managed runtime content was excluded

The current SAF implementation declares `Scripts`, `Plugins`, and `Fonts`
app-private. On Google Play builds this prevents users from managing those files
through Android's Documents UI, USB file access, backup tools, or other apps.
The files also do not inherit the uninstall durability provided by the selected
SAF tree.

The existing consumers require paths:

- `ApiManager` enumerates HTML files below `Scripts`, opens
  `startup.sketchscript`, and uses `FileSystemWatcher`;
- `LuaManager` recursively enumerates `.lua` files below `Plugins`, preserves
  `Plugins/LuaModules`, and uses a file watcher;
- Lua module loading and `App:ReadFile` use relative filesystem paths below
  `Plugins`;
- `SvgTextUtils` and font APIs open font files below `Fonts` by path.

These requirements do not justify making the content private. Models, videos,
SVG files, and generated output already use app-private materialization or
staging when a consumer requires a path. The same technique can provide
transparent runtime paths for these three bounded trees.

### Google Drive sync was disabled

`DriveSync` currently constructs `DirectoryInfo` and `FileInfo` objects from
local roots. SAF-canonical documents do not appear in those app-private paths,
so the FD-backed implementation disabled Drive sync rather than silently
uploading an incomplete view.

The existing Drive behavior covers:

| Sync option | Current direction | Recursion/filtering |
| --- | --- | --- |
| Sketches | Device to Drive | Top-level `.tilt` content |
| Media Library | Device to Drive | Models recursive; other media roots top-level |
| Snapshots | Device to Drive | Top-level |
| Videos | Device to Drive | Top-level, platform-dependent filters |
| Exports | Device to Drive | Recursive |
| Scripts | Device and Drive | Recursive `.html` |
| Plugins | Device and Drive | Recursive `.lua` |

That behavior should be preserved. Restoring Drive sync does not require
materializing every upload source: SAF documents can be enumerated through the
backend and uploaded directly from streams.

## Goals

1. Make `Scripts`, `Plugins`, and `Fonts` first-class SAF storage areas.
2. Preserve their visible paths under the selected `Open Brush` tree.
3. Preserve the existing runtime APIs and path expectations.
4. Make external additions, updates, renames, and deletions appear
   automatically during normal application use.
5. Retain a resume-time refresh as a correctness fallback.
6. Keep SAF authoritative; local runtime projections are disposable.
7. Restore all existing Google Drive folder-sync options on SAF.
8. Preserve the existing upload-only versus two-way direction of every Drive
   option.
9. Transfer Drive data through backend streams and transactions rather than
   whole-tree materialization.
10. Prevent provider timestamps from creating repeated Drive transfer loops.
11. Preserve data when SAF, Drive, or the application fails during a transfer.
12. Preserve behavior on Windows, Android OpenXR, Quest, and other
   non-Google-Play targets.

## Non-Goals

- Moving autosaves into shared storage.
- Making app-private transaction journals user-visible.
- General bidirectional synchronization of every Open Brush directory.
- Claiming compatibility with every cloud-backed Documents provider.
- Replacing the Google Drive API or changing account/authentication behavior.
- Adding deletion propagation to Google Drive if the existing implementation
  does not propagate deletion.
- Giving scripts or plugins broader filesystem permissions.
- Making built-in resources dependent on the availability of SAF.

## Product Decisions

These decisions are part of this plan and should not be silently changed during
implementation:

1. `Scripts`, `Plugins`, and `Fonts` are canonical in SAF on the Google Play
   backend.
2. Their app-private runtime directories are projections, not independent user
   stores.
3. Superseded by the amendment above: refresh happens at startup, after
   folder selection or root changes, and on application resume, matching the
   no-watcher behavior of existing mobile builds.
4. Autosaves remain app-private and are not projected, published, or Drive
   synced as ordinary sketches.
5. Existing Google Drive sync directions and extension filters are preserved.
6. Drive downloads apply only to the areas that are currently two-way:
   `Scripts` and `Plugins`.
7. Drive sync continues not to interpret absence on one side as a deletion
   request.
8. If both SAF and Drive changed the same two-way file since the last confirmed
   sync, neither version is silently overwritten. Keep the SAF version at its
   canonical path and download the Drive version under a deterministic conflict
   name, then report the conflict.
9. If a selected SAF root changes, work associated with the old root stops and
   remains associated with that root.
10. A provider notification is a refresh hint, not proof of a particular
    mutation. Refresh always queries an authoritative snapshot.

## Core Invariants

1. A user-managed shared file has one canonical device copy: the SAF document.
2. A projection file can be deleted and rebuilt without losing user data.
3. Query failure never means that a directory is empty.
4. No projection cleanup occurs until a complete SAF snapshot has been
   obtained successfully.
5. Projection cleanup only removes files owned by that projection's manifest.
6. App-authored shared files are not reported as saved until their SAF
   transaction commits.
7. Google Drive uploads read the canonical backend document, not a potentially
   stale projection.
8. Google Drive downloads commit through the destination backend's safe write
   transaction.
9. A transfer captures root identity before it begins and cannot be retargeted
   by folder selection.
10. SAF document IDs remain opaque. Display paths are not substituted for
    identity.
11. Drive file IDs remain opaque and are retained in sync metadata.
12. Missing or unreliable timestamps do not authorize overwriting either side.
13. Every opened stream and detached descriptor has a single disposal owner.
14. Unknown projection manifests, Drive ledgers, and transaction-journal
    versions are retained and reported rather than discarded.
15. Non-Google-Play builds continue using their existing local paths and
    watchers.

## Shared Layout

Extend the Google Play SAF layout with:

```text
Documents/
  Open Brush/
    Sketches/
    Saved Strokes/
    Media Library/
    Snapshots/
    Videos/
    VrVideos/
    Exports/
    Scripts/
    Plugins/
      LuaModules/
    Fonts/
```

`Scripts`, `Plugins`, and `Fonts` must be included in folder creation,
selection validation, root-switch handling, provider observation, and storage
diagnostics.

Do not add these local-only locations to SAF:

```text
Autosave/
OpenBrushSafTransactions/
OpenBrushSafPublications/
OpenBrushSafRuntime/
```

## Storage Backend Extensions

### Add storage areas

Add these values to `StorageArea`:

```csharp
Scripts,
Plugins,
Fonts,
```

Map them as follows:

| Area | Local backend | SAF backend |
| --- | --- | --- |
| Scripts | `App.UserPath()/Scripts` | `Open Brush/Scripts` |
| Plugins | `App.UserPath()/Plugins` | `Open Brush/Plugins` |
| Fonts | `App.UserPath()/Fonts` | `Open Brush/Fonts` |

The local backend mapping preserves current behavior. The SAF backend mapping
is canonical shared content.

### Recursive enumeration

Drive sync and runtime projections require complete relative trees. Add a
backend-level recursive enumerator rather than teaching each caller to issue
provider queries:

```csharp
StorageTreeResult EnumerateTree(
    StorageArea area,
    string relativeDirectory,
    StorageTreeQuery query,
    CancellationToken cancellationToken);
```

`StorageTreeQuery` should declare:

- recursive or top-level enumeration;
- included extensions;
- excluded extensions;
- whether directories are returned;
- an optional maximum depth;
- an optional maximum item count.

Each entry should contain:

- opaque document identity;
- opaque parent identity;
- normalized relative display path;
- display name;
- MIME type;
- file/directory type;
- size when known;
- last-modified time when known;
- provider capability flags.

SAF should issue one child query per enumerated directory. It must not resolve
every child again by walking path segments. Local storage should produce the
same logical result from `DirectoryInfo`.

Return an explicit failure result. A partial enumeration is not a successful
empty or complete tree.

### Destination directories

The existing write transaction accepts an area and relative path. Ensure it can
create missing intermediate directories for nested Scripts, Plugins, Fonts,
Drive downloads, and conflict copies.

Directory creation must:

- validate every relative segment;
- reject rooted paths, `.`/`..`, empty segments, and reserved transaction names;
- resolve duplicate provider children as an error;
- capture root identity;
- serialize with other mutations in the same destination subtree.

### Direct metadata refresh

After a successful write transaction, expose the committed document metadata
or support querying it by returned document ID. Drive sync needs the resulting
provider fingerprint after a download; it must not assume that it can set or
preserve the provider's modification timestamp.

## Transparent Runtime Projections

### Ownership

Use an app-private root such as:

```text
OpenBrushSafRuntime/
  <root-identity-hash>/
    Scripts/
    Plugins/
    Fonts/
    manifests/
```

The hash prevents one selected SAF root from being confused with another.
Never use raw tree URIs, provider IDs, account names, or user paths in
filenames or logs.

Each area has a versioned manifest containing:

- selected root identity hash;
- storage area;
- format version;
- last successful generation;
- each projected relative path;
- opaque document ID;
- size and last-modified metadata when available;
- optional content hash for small files;
- projection-owned local path.

Write manifests atomically using temporary file plus replace. Unknown versions
remain untouched and cause a clean rebuild into a new generation.

### Refresh algorithm

For each area:

1. Capture the current backend and root identity.
2. Enumerate the complete SAF tree on a worker thread.
3. If enumeration fails or is partial, retain the existing projection and
   report the error.
4. Compare the successful snapshot with the current manifest by document ID,
   relative path, size, last-modified time, and content hash where appropriate.
5. Copy every new or changed document into a temporary file.
6. Flush and close the temporary file.
7. Replace the corresponding projection file.
8. Create required local directories.
9. Only after all required copies succeed, remove projection-owned files absent
   from the successful SAF snapshot.
10. Never delete unowned local files.
11. Write the new manifest atomically.
12. Notify the relevant loader once, after the coherent refresh completes.
13. Before every mutation and before manifest commit, verify that the selected
    root identity still matches the captured identity.

For these small configuration trees, materializing all matching files is an
acceptable bounded operation. It avoids forcing path and stream abstractions
through MoonSharp, HTML handlers, font libraries, and module resolution.

### Automatic change observation

Add an Android bridge around `ContentObserver`:

- register against the selected tree or relevant child-document URIs with
  descendant notifications where supported;
- deliver only a root-scoped "content may have changed" event to C#;
- debounce bursts;
- never perform provider queries inside the observer callback;
- schedule tree refreshes on the existing background-operation mechanism;
- unregister before root changes and application shutdown;
- ignore callbacks carrying an obsolete root generation.

Documents providers can support notifications by setting a cursor notification
URI and calling `notifyChange`, but client code must not assume every provider
does so reliably. Therefore also refresh:

- after the shared root becomes ready;
- when the application resumes;
- after an Open Brush mutation;
- before a feature first consumes an area if it has not been refreshed in the
  current session.

Manual refresh remains an optional recovery control only.

### Runtime loader integration

Introduce one runtime-path service:

```csharp
public interface IUserRuntimeContent
{
    string GetRuntimePath(StorageArea area);
    Task<RuntimeProjectionResult> EnsureCurrentAsync(
        StorageArea area,
        CancellationToken cancellationToken);
    event Action<StorageArea> Refreshed;
}
```

Behavior:

- local backend returns the existing directory directly;
- SAF backend returns the root-scoped projection directory;
- loaders depend on this service rather than constructing
  `Path.Combine(App.UserPath(), ...)` themselves.

Integration points:

- `ApiManager` obtains the Scripts runtime path before populating handlers and
  running the startup script;
- `LuaManager` obtains the Plugins runtime path before copying modules,
  enumerating scripts, or configuring `ModulePaths`;
- `SvgTextUtils` resolves Fonts through the runtime content service;
- `OpenUserScriptsFolder` on Android opens or reselects the SAF tree rather than
  attempting to expose the app-private projection;
- Lua `App:ReadFile` continues validating relative paths against the projected
  Plugins root.

`ApiManager.Awake` currently registers API infrastructure and immediately
enumerates user scripts. Split those responsibilities: API endpoint and HTTP
server setup may remain in `Awake`, while user-script enumeration, watcher
setup, and `startup.sketchscript` execution wait for the runtime-content service
to report a coherent initial Scripts projection. Do the equivalent separation
for any `LuaManager` initialization that can run before shared storage is
ready. A delayed SAF grant must initialize user content exactly once without
recreating unrelated API or Lua state.

Existing local `FileSystemWatcher` behavior can remain for non-SAF backends. On
SAF, the projection refresh event should explicitly reload changed content so
correctness does not depend on whether local file watcher events fire during a
batch replacement.

### Built-in Lua modules and examples

Current initialization copies missing built-in Lua modules into
`Plugins/LuaModules`. Preserve that user-visible behavior:

1. seed missing built-in modules into the canonical SAF `Plugins/LuaModules`
   directory through normal write transactions;
2. never overwrite a user-modified module;
3. scope the seed-version marker to the selected SAF root;
4. refresh the Plugins projection after seeding;
5. when copying an example plugin into the user folder, write it to SAF first
   and refresh the projection only after commit.

HTML example scripts remain packaged resources unless the existing UI copies
one into the user Scripts directory.

### App-authored changes

Do not treat arbitrary projection changes as canonical. Route known writes
through the backend:

- copying example Lua scripts;
- copying or installing HTML scripts;
- installing fonts;
- any future plugin editor or downloader.

The operation sequence is:

1. write through a backend transaction;
2. commit in SAF;
3. refresh the projection;
4. report success.

If compatibility requires accepting a legacy component that writes only to a
path, give that operation explicit staging and publication. Do not introduce a
general projection-to-SAF watcher that could mistake projection cleanup,
built-in seeds, or partial writes for user intent.

### Limits and failures

Use conservative configurable bounds for configuration content:

- maximum recursive depth;
- maximum file count;
- maximum individual file size;
- maximum total projected bytes.

Do not silently ignore content beyond a limit. Retain the previous coherent
projection and show a diagnostic identifying the area and exceeded limit.
Avoid logging script contents, plugin contents, font names if unnecessary,
tree URIs, or document IDs.

Active scripts may have open streams or runtime state while their source
changes. Complete projection replacement first, then use existing loader
semantics to reload at a safe main-thread point. Do not terminate or restart an
active background script from an Android observer thread.

## Migration of Existing Private Content

The current branch may already have user-created files below private
`Scripts`, `Plugins`, or `Fonts`. Moving to SAF-canonical storage must not
discard them.

On the first successful refresh for an area and selected root:

1. Determine whether the local directory has a valid projection manifest.
2. Files owned by a valid manifest are disposable and are not migration input.
3. Files without projection ownership are legacy local user content.
4. Enumerate SAF successfully before making any migration decision.
5. If a legacy relative path is absent in SAF, publish it transactionally.
6. If both copies are byte-identical, record the SAF document as canonical.
7. If both exist and differ, preserve both:
   - keep the SAF file at the original path;
   - publish the local file under a deterministic
     `<name>.local-recovered-<timestamp><extension>` name;
   - report the conflict.
8. Do not delete the legacy local source until every required publication
   commits and a valid projection manifest exists.
9. On failure, retain the local content and retry later.
10. Scope migration state to root identity and area.

Never copy an old root's disposable projection into a newly selected SAF root.
Only explicitly identified legacy, unowned local data is migration input.

## Google Drive Sync Architecture

### Separate logical trees from filesystem paths

Replace `SyncedFolder.AbsoluteLocalPath`, `DirectoryInfo`, and `FileInfo` as the
core model with a storage-neutral tree:

```csharp
sealed class SyncTree
{
    StorageArea Area;
    string RelativeDirectory;
    string DriveFolderId;
    SyncDirection Direction;
    bool Recursive;
    string[] IncludeExtensions;
    string[] ExcludeExtensions;
}

sealed class SyncEntry
{
    StorageDocumentId DocumentId;
    string RelativePath;
    string DisplayName;
    long? Size;
    DateTime? LastModified;
    string ContentSignature;
}
```

Both local and SAF backends provide `SyncEntry` values through
`EnumerateTree`. Do not add an SAF-only branch throughout `DriveSync`.

Construct the same logical trees and directions currently used by
`SetupSyncFoldersAsync`.

### Upload path

For an upload:

1. Capture backend instance, root identity, document ID, and metadata.
2. Open a fresh backend read stream.
3. Build Drive metadata from the logical name and MIME type.
4. For `.tilt`, generate thumbnail content hints from reopenable backend
   streams rather than `DiskSceneFileInfo`.
5. Upload or update the Drive file.
6. Close the stream.
7. Requery or otherwise verify the source identity has not changed.
8. Record the confirmed sync state.
9. Notify the Drive sketch catalog by logical document identity or refresh,
   rather than passing a nonexistent local canonical path.

Path-bound materialization is not required for ordinary uploads.

### Download path

Drive downloads currently apply to Scripts and Plugins. For a download:

1. Capture backend and root identity.
2. Download into app-private transaction staging, or directly into the
   backend transaction's temporary stream.
3. Close and validate the complete payload.
4. Commit through `IStorageWriteTransaction`.
5. Obtain the resulting canonical document ID and provider metadata.
6. Record confirmed sync state.
7. Refresh the affected Scripts or Plugins projection.
8. Reload the corresponding runtime catalog at a safe point.

Never delete or truncate the existing SAF document before the replacement is
complete.

### Drive sync ledger

The local implementation can set file modification times after download. SAF
providers generally control document timestamps, so timestamp comparison alone
can make the same downloaded file appear locally newer and upload it again.

Persist a small, versioned sync ledger under app-private storage. Key records by:

- Google account identity hash;
- Drive device-root identity;
- selected storage backend root identity hash;
- storage area;
- normalized relative path.

Each record contains:

- Drive file ID;
- Drive version/change token or checksum when supplied;
- Drive modified time and size;
- local opaque document ID;
- local size and last-modified metadata when supplied;
- a computed content hash when needed;
- last confirmed direction;
- last successful sync time;
- record format version.

The ledger is synchronization metadata, not a canonical file copy or a pending
payload queue.

After every successful upload or download, capture both resulting fingerprints.
On the next scan:

- neither side changed: no transfer;
- only backend changed: upload if the tree permits upload;
- only Drive changed: download if the tree permits download;
- both changed: apply the conflict policy;
- no ledger record: use existence and safe content comparison before deciding;
- missing metadata: compute a content hash rather than assuming newer.

Write ledger updates atomically. Unknown versions are retained and cause a safe
rescan/content comparison, not deletion or blind overwrite.

### Conflict handling

For the two-way Scripts and Plugins trees:

- if both versions changed and are identical, update the ledger only;
- if both changed and differ, keep the SAF version at the original path;
- download the Drive version to a unique sibling such as
  `<stem>.drive-conflict-<timestamp><extension>`;
- never overwrite an existing conflict name;
- refresh the runtime projection;
- report a concise user-visible conflict notification.

For upload-only areas, a Drive-side modification must not silently overwrite
the device source. Preserve existing behavior where possible, but use the
ledger to avoid repeatedly uploading unchanged content. If updating an existing
Drive item would overwrite an independently changed Drive revision, report a
conflict and require a later explicit policy rather than destroying it.

### Deletions

Preserve existing behavior: absence does not propagate deletion.

- A file deleted from SAF is not automatically deleted from Drive.
- A file deleted from Drive is not automatically deleted from SAF.
- A two-way scan treats the remaining file as an unsynced addition, subject to
  ledger/conflict rules.

Any future deletion propagation requires tombstones and a separate product
decision.

### Cancellation, retries, and root changes

- Transfer queue items carry backend root identity and logical source identity.
- Revalidate root identity before opening, before commit, and before ledger
  update.
- Cancellation closes streams and leaves existing canonical files untouched.
- An interrupted Drive download retains only transaction-owned staging or a
  reconstructible backend journal.
- Retry resolves the document again through opaque identity or a successful
  parent enumeration; it does not trust a stale display path.
- Provider query failure pauses the affected scan and retains the previous
  ledger.
- Drive query failure does not alter SAF or projection state.
- Root switching cancels queued old-root work and never retargets it.

### UI behavior

Remove the blanket `StorageBackendSupportsDriveSync` rejection after backend
support is complete.

Retain the existing controls for:

- enabling Drive sync;
- selecting synced folder types;
- progress;
- cancellation;
- low-space warnings.

Add explicit messages for:

- shared folder unavailable;
- provider query failed;
- root changed during sync;
- Drive/SAF conflict preserved;
- download committed but runtime projection refresh pending;
- transfer retry required.

Do not claim a transfer completed until the backend transaction and ledger
update both reach a recoverable state.

## Threading and Lifecycle

- SAF enumeration, hashing, copying, and Drive I/O run off the Unity main
  thread.
- Unity UI and script/plugin reload callbacks return to the main thread.
- Use one refresh coordinator per root and area.
- Coalesce observer events while a refresh is running.
- Serialize projection refresh against app-authored publication to the same
  area.
- Serialize Drive downloads against other writes to the same logical
  destination.
- Drive uploads may run concurrently only when they read different immutable
  document identities.
- Application pause cancels or safely checkpoints background work.
- Application resume rechecks permission, root identity, transaction recovery,
  projections, and then Drive eligibility in that order.

Recommended resume order:

```text
Validate persisted SAF grant
  -> recover SAF transactions/publications
  -> refresh Scripts/Plugins/Fonts projections
  -> notify runtime loaders
  -> resume or start Drive scan
```

## Security and Path Handling

- Reject rooted paths and traversal before calling either backend.
- Normalize logical paths with `/` independent of platform separators.
- Never accept provider display names containing unexpected separators as
  trusted paths.
- Preserve Lua `App:ReadFile` confinement to the projected Plugins root.
- Do not expose the app-private projection through a user-facing "open folder"
  action.
- Do not execute a newly observed script until its complete file is committed
  and projection refresh succeeds.
- Apply existing script/plugin trust settings unchanged.
- Do not log script contents, Drive access tokens, account IDs, tree URIs, or
  opaque document IDs.

## Logging

Use stable, searchable prefixes:

```text
SAF_PROJECTION
SAF_OBSERVER
SAF_MIGRATION
DRIVE_BACKEND_SYNC
DRIVE_CONFLICT
```

Include safe fields:

- storage area;
- normalized relative path only when acceptable;
- root identity hash prefix;
- operation state;
- item counts and byte counts;
- structured result code;
- retryability.

Do not include raw provider URIs, raw document IDs, Google account identifiers,
tokens, or file contents.

## Implementation Phases

### Phase 1: Backend tree support

- add Scripts, Plugins, and Fonts storage areas;
- add recursive filtered enumeration;
- support nested transactional destinations;
- expose committed document metadata;
- add fake-backend coverage.

No loader behavior changes in this phase.

### Phase 2: Runtime projection service

- implement manifests and root-scoped directories;
- implement coherent refresh and bounded cleanup;
- implement legacy private-content migration;
- expose runtime paths for local and SAF backends;
- test query failure and root switching.

### Phase 3: Loader integration

- route `ApiManager` through the runtime Scripts path;
- route `LuaManager`, module lookup, and `App:ReadFile` through the runtime
  Plugins path;
- route font loading through the runtime Fonts path;
- move example/module writes to backend transactions;
- add explicit reload hooks.

### Phase 4: Automatic observation

- add Android `ContentObserver` bridge;
- debounce and root-scope callbacks;
- refresh on ready/resume/mutation;
- prove external changes appear without manual refresh;
- keep resume refresh as fallback.

### Phase 5: Drive storage-neutral model

- replace filesystem-centric sync records with logical storage entries;
- implement local backend adapter without changing behavior;
- generate `.tilt` hints from reopenable backend streams;
- keep existing direction and filtering rules.

### Phase 6: SAF Drive uploads

- enumerate SAF areas directly;
- upload from backend read streams;
- refresh Drive sketch catalog without local canonical paths;
- validate cancellation and root changes.

### Phase 7: SAF Drive downloads and ledger

- implement versioned sync ledger;
- commit Scripts/Plugins downloads through backend transactions;
- implement content comparison and conflict preservation;
- refresh projections after download;
- remove the blanket SAF Drive disablement.

### Phase 8: Cleanup and regression validation

- remove obsolete unsupported-backend UI;
- update the FD-backed plan's implementation decisions;
- document user-visible storage ownership;
- run platform build and behavior matrices;
- run target-device observer, projection, and Drive tests.

## Suggested Commit Sequence

Keep each independently reviewable:

1. `Add user runtime storage areas`
2. `Enumerate storage backend trees recursively`
3. `Project SAF runtime content locally`
4. `Migrate private runtime content to SAF`
5. `Load user scripts from runtime storage`
6. `Load Lua plugins from runtime storage`
7. `Load user fonts from runtime storage`
8. `Publish bundled plugin modules through SAF`
9. `Observe SAF tree changes automatically`
10. `Describe Drive sync sources logically`
11. `Upload Drive content from storage streams`
12. `Persist backend Drive sync state`
13. `Download Drive scripts through transactions`
14. `Preserve Drive sync conflicts`
15. `Enable Drive sync with SAF storage`
16. `Cover SAF feature parity regressions`
17. `Document SAF feature parity behavior`

Do not combine all loader, observer, and Drive work into one commit.

## Testing Strategy

### Projection unit tests

- successful empty snapshot produces an empty owned projection;
- query failure retains every existing projection file and manifest;
- partial recursive query is a failure and performs no cleanup;
- new SAF file appears locally;
- changed SAF file replaces its projection atomically;
- renamed SAF file removes the old owned path only after the new copy succeeds;
- deleted SAF file removes only the manifest-owned projection;
- unrelated local file is never deleted;
- duplicate provider child names fail safely;
- rooted and traversing paths are rejected;
- root change during enumeration performs no commit;
- root change during copying performs no manifest update;
- stale observer generation is ignored;
- repeated notification bursts coalesce;
- unknown manifest version is retained;
- size/count/depth limit retains the prior projection;
- built-in Lua modules never overwrite user versions;
- app-authored example copy is visible only after SAF commit;
- legacy local-only files migrate;
- identical migration conflicts deduplicate;
- differing migration conflicts preserve both copies;
- migration failure retains the legacy local source.

### Loader tests

- HTML scripts populate from the projected Scripts directory;
- `startup.sketchscript` runs after the initial projection is ready;
- HTML add/change/delete refreshes registered handlers coherently;
- Lua scripts recursively load from projected Plugins;
- `LuaModules` relative imports work;
- `App:ReadFile` cannot traverse outside the projection;
- Lua changes reload only after coherent projection commit;
- font APIs resolve files from the projected Fonts directory;
- missing/unavailable SAF reports a useful error rather than reading stale data
  as current;
- local backend paths and watchers remain unchanged.

### Android observer tests

- registration uses the selected root generation;
- external file creation triggers a debounced refresh;
- external modification triggers a refresh;
- external rename triggers a refresh;
- external deletion triggers a refresh;
- self-originated writes do not cause an unbounded refresh loop;
- observer unregistration occurs during root change and shutdown;
- a provider that emits no notification is corrected on resume;
- callbacks do not perform blocking queries on the Android main thread.

### Drive sync unit tests

- every existing folder option produces the same direction and filters;
- SAF Sketches upload without materialization;
- SAF media upload without whole-tree materialization;
- recursive model and export paths retain relative hierarchy;
- `.tilt` thumbnail hints are generated from backend streams;
- Scripts and Plugins download through safe transactions;
- a failed download leaves the previous SAF file intact;
- cancellation leaves the previous SAF file intact;
- provider failure pauses scan without treating the tree as empty;
- Drive failure does not change SAF or the projection;
- root switch cancels old-root queue items;
- ledger prevents download-then-reupload loops;
- ledger prevents repeated unchanged uploads;
- missing timestamps fall back to content comparison;
- unknown ledger version does not authorize overwrite;
- both-sides-changed conflict preserves both files;
- absence on either side does not propagate deletion;
- projection refresh occurs after a Drive download commits;
- no sync success is reported before recoverable ledger state exists.

### Integration tests

- select a new empty `Open Brush` tree and seed Plugins/LuaModules;
- add HTML, Lua, module, data, and font files externally;
- verify each becomes usable without pressing refresh;
- edit each externally while Open Brush is running;
- remove each externally and verify runtime state updates safely;
- copy an example plugin and confirm it appears in Documents;
- switch roots while a projection refresh is active;
- switch roots while a Drive upload is active;
- switch roots while a Drive download is active;
- lose and restore the persisted SAF grant;
- kill the process during projection copy;
- kill the process during Drive download commit;
- restart and recover without losing either canonical version.

### Non-Google-Play regression matrix

Verify unchanged behavior on:

- Windows OpenXR;
- Android OpenXR;
- Quest;
- Editor;
- other existing local-filesystem targets.

For each applicable target:

- Scripts use their existing path;
- Plugins and LuaModules use their existing path;
- Fonts use their existing path;
- local file watchers behave as before;
- Drive sync uses the local backend and preserves current results;
- no Android SAF observer or projection code is initialized.

### Build validation

- compile desktop;
- compile Android with `OPEN_BRUSH_GOOGLE_PLAY`;
- compile Android without `OPEN_BRUSH_GOOGLE_PLAY`;
- compile Editor tests;
- compile the Android Java bridge;
- build and run on the target Google Play Android XR device.

## Device Acceptance Matrix

On the target local Android Documents provider:

1. Select or create `Documents/Open Brush`.
2. Confirm Scripts, Plugins, Plugins/LuaModules, and Fonts are visible.
3. Add files from Android Documents while Open Brush is running.
4. Confirm automatic availability without manual refresh.
5. Modify, rename, and delete files externally.
6. Confirm coherent runtime updates.
7. Suspend and resume after an observer notification is intentionally missed.
8. Confirm resume refresh repairs state.
9. Enable each Google Drive sync option.
10. Upload sketches, media, snapshots, videos, and exports.
11. Upload and download Scripts and Plugins.
12. Confirm no repeated transfer loop after download.
13. Create simultaneous Drive and SAF edits and verify conflict preservation.
14. Interrupt uploads and downloads.
15. Switch SAF roots during active work.
16. Confirm no old-root work appears in the new root.
17. Uninstall and reinstall, reselect the tree, and confirm shared runtime
    content survives.

Also test at least one provider with incomplete metadata or notification
support to verify resume fallback and content-signature behavior.

## Acceptance Criteria

The feature-parity cleanup is complete when:

- Scripts are canonical and user-visible in SAF;
- Plugins and LuaModules are canonical and user-visible in SAF;
- Fonts are canonical and user-visible in SAF;
- all three work through their existing runtime APIs;
- external changes appear automatically during normal use;
- missed notifications are repaired on resume;
- routine use does not require manual refresh;
- query failure never clears a runtime projection;
- root switching cannot retarget projection or Drive work;
- legacy private content is migrated without overwrite or loss;
- app-authored runtime content is published transactionally;
- autosaves remain app-private and recoverable;
- all existing Google Drive sync options are enabled on SAF;
- existing Drive direction and filter behavior is preserved;
- Drive uploads read canonical backend streams;
- Drive downloads commit through safe backend transactions;
- Drive timestamps cannot cause repeated transfer loops;
- simultaneous Drive and SAF edits preserve both versions;
- Drive or provider failure does not delete canonical data;
- no whole Sketches or Media Library mirror is reintroduced;
- non-Google-Play behavior remains unchanged;
- static builds, automated tests, and the device matrix pass.

## Relationship to the FD-Backed Plan

Keep `google-play-saf-fd-backed-storage-plan.md` as the architectural record for
canonical SAF sketches, media, safe transactions, staged outputs, and
materialization.

This document supersedes only these decisions in its
`Implementation Decisions` section:

- Scripts, Plugins, and Fonts remaining app-private;
- legacy Google Drive folder sync being disabled on the SAF backend;
- manual refresh being an expected path for externally modified runtime
  content.

The earlier document now references this feature-parity follow-up and records
the implemented behavior. Do not delete either plan: together they explain the
base architecture and the parity cleanup.
