# Google Play SAF Storage Plan

## Goal

Add a Google Play Android storage mode that keeps Quest/Pico behavior unchanged while avoiding `MANAGE_EXTERNAL_STORAGE` for Play Store builds.

For Google Play builds, Open Brush should ask the user to create or select a single shared `Open Brush` folder using Android Storage Access Framework (SAF). If the user declines, the app should continue to run but warn that save/export/import features that need shared storage are reduced until a folder is chosen.

The selected `Open Brush` folder is the canonical user-visible storage root. `Application.persistentDataPath` may be used only as transient staging/cache, not as canonical storage.

## Current Implementation Status

Implemented in the current working tree:

- Google Play build profile: `-btb-google-play`, Android-only `OPEN_BRUSH_GOOGLE_PLAY`, Google Play AAB CI wiring, temporary `forceSDCardPermission = false`, and generated-manifest removal of all matching direct external/media storage permission entries, including `uses-permission-sdk-23` and Android partial-media variants.
- Android SAF bridge: folder picker activity, persisted tree URI, URI validity checks including persisted root name validation, safe relative-path validation, directory/file write helpers, safer overwrite/truncate behavior for existing files, directory copy, delete, list, and copy-to-local-cache helpers.
- First-run/shared-folder gate: Google Play builds prompt after the main scene becomes usable; startup refusal is remembered as a soft dismissal; feature commands can prompt again later. The current warning is an info card plus console message followed by a native Android `Choose Folder` / `Not Now` dialog, not a custom two-button VR popup.
- Exports: Play builds stage exports locally, publish them to `Open Brush/Exports`, write the export README through SAF, and keep the staging copy if SAF publishing fails.
- Sketches and saved strokes: Play builds use local filesystem paths as a working cache, require the shared folder for user-initiated saves, publish saved `.tilt` files to `Open Brush/Sketches` or `Open Brush/Saved Strokes`, sync those shared folders back into the local cache on startup/folder selection, and mirror sketchbook delete/rename to SAF.
- Snapshots: normal tool snapshots are staged locally and published to `Open Brush/Snapshots`.
- Videos: normal video captures and camera-path video captures require the shared folder and publish completed encoder output, or Android still-frame fallback folders plus sequence metadata, to `Open Brush/Videos`.
- Media Library: shared `Media Library` content is synced from SAF into the local cache for existing filesystem readers.
- Models saved through the built-in `SaveModel` command require the shared folder on Play builds and publish to `Open Brush/Media Library/Models`.
- Copy progress/threading: outbound SAF file and directory publishing uses background Java transfer jobs with Unity-side progress polling. Exports, sketches, sketch rename copies, snapshots, normal videos, Android still-frame fallback video folders, camera-path videos, and built-in model saves use this path.
- Retry/recovery UX: failed outbound transfers keep the local staging/cache copy, register an in-memory retry action, clear the stored folder selection so the next storage feature prompts again, and retry pending transfers after the user selects the `Open Brush` folder again.
- API/Lua media publishing: URL-based API imports into `Media Library`, HTTP/API base64 image saves, Lua `App:TakeSnapshot()`, and Lua `App:Take360Snapshot()` write to the local cache and publish the result to SAF on Google Play builds. If no shared folder is selected, these paths prompt for the `Open Brush` folder before publishing.

Still incomplete or unvalidated:

- Lua/HTTP API systems still run against the Play local cache root for script files and path-based reads. User-visible media outputs are now published to SAF, but script/plugin file management is intentionally still local-cache backed.
- The Android picker requires a selected folder named `Open Brush`; non-matching selections show native Android UI with `Choose Again` and `Not Now`.
- The first-run storage prompt is not yet a custom `Choose Folder` / `Not Now` VR popup; the refusal path is implemented with native Android UI before the document-tree picker opens.
- Unity batchmode project load/import succeeds with no compiler-error patterns in the latest log. The Android Java bridge compiles against temporary Android/Unity stubs; full Android build validation has not been run locally because no `android.jar` was found in the local Unity/Android SDK paths.

Validation performed in this working tree:

- `git diff --check` passes.
- Unity batchmode was run against the project. The latest log has no `error CS`, `error:`, `Exception`, `Build failed`, or `Scripts have compiler errors` matches.
- `OpenBrushStorageActivity.java` and `OpenBrushStorageBridge.java` compile with `javac` against temporary stubs for the Android and Unity classes they call.
- API/Lua file-output changes are limited to media/download/snapshot publish paths; script/plugin files still run against the Play local cache root.

## Remaining Non-Validation Items

1. Custom VR prompt: optional UX polish. Current behavior is an in-VR info card/console message plus native Android `Choose Folder` / `Not Now` UI.

## Current Code Shape

The current project is strongly path-based:

- `App.InitUserPath()` chooses the user root and `App.UserPath()` feeds most local storage.
- `App.UserSketchPath()`, `App.SavedStrokesPath()`, `App.AutosavePath()`, `App.UserExportPath()`, `App.SnapshotPath()`, `App.VideosPath()`, `App.MediaLibraryPath()` all return normal filesystem paths.
- `SaveLoadScript` initializes `m_SaveDir = App.UserSketchPath()` and writes through `DiskSceneFileInfo`.
- `SketchSnapshot.WriteSnapshotToFile()` writes `.tilt` files through `TiltFile.AtomicWriter`, which uses `Directory`, `FileStream`, `File.Move`, and `File.Delete`.
- `FileSketchSet` scans `App.UserSketchPath()` with `DirectoryInfo` and watches it with `FileWatcher`.
- `Export.ExportScene()` creates an export directory under `App.UserExportPath()`, writes many formats using normal paths, then reports `Located in ` + `App.UserExportPath()`.
- Lua/API helpers also assume filesystem paths under `App.UserPath()`.

Because SAF provides `content://` tree URIs rather than direct filesystem paths, replacing `App.UserPath()` with a SAF URI is not a small change. The implementation should introduce a Play-only storage layer and use local staging for path-only save/export code.

## First Cleanup

Before implementing this plan, revert the accidental global storage rewrite currently present in the working tree:

- Restore Android `App.InitUserPath()` behavior for non-Play builds: `m_UserPath = "/sdcard/"`, `m_OldUserPath = Application.persistentDataPath`.
- Restore loading-scene all-files-access behavior for Quest/Pico/non-Play Android builds.
- Restore `MANAGE_EXTERNAL_STORAGE` and `ForceSDCardPermission` for non-Play Android builds.
- Revert unrelated Unity editor churn in `Assets/Oculus/OculusProjectConfig.asset`, `Assets/XR/Settings/Open XR Package Settings.asset`, and unrelated `ProjectSettings.asset` scripting define changes.

Then reintroduce storage changes behind an explicit Google Play build flag only.

## Build Profile

Add a dedicated Google Play build flag, for example:

```csharp
OPEN_BRUSH_GOOGLE_PLAY
```

Implementation actions:

1. Extend `BuildTiltBrush.TiltBuildOptions` with `bool GooglePlay`.
2. Add a command-line option such as `-btb-google-play`.
3. Pass `OPEN_BRUSH_GOOGLE_PLAY` through `TempDefineSymbols` only when that option is set.
4. For Google Play Android builds, temporarily set:
   - `PlayerSettings.Android.forceSDCardPermission = false`
   - `unityplayer.SkipPermissionsDialog = true`
   - no `android.permission.MANAGE_EXTERNAL_STORAGE`
5. For Quest/Pico/normal Android builds, leave current behavior unchanged.

Manifest handling should be build-time, not a global checked-in Android behavior change. Use `BuildTiltBrushPostProcess.OnPostGenerateGradleAndroidProject()` to remove `MANAGE_EXTERNAL_STORAGE` and set `unityplayer.SkipPermissionsDialog=true` when `OPEN_BRUSH_GOOGLE_PLAY` is defined. Keep the checked-in manifest compatible with Quest/Pico.

## Android SAF Bridge

Add a small Android plugin rather than trying to handle activity results entirely from C#.

Suggested files:

- `Assets/Plugins/Android/OpenBrushStorageBridge.java`
- `Assets/Plugins/Android/OpenBrushStorageActivity.java`
- `Assets/Scripts/Storage/AndroidSafStorage.cs`
- `Assets/Scripts/Storage/OpenBrushStorage.cs`

Native responsibilities:

1. Launch `ACTION_OPEN_DOCUMENT_TREE`.
2. Request read/write/persistable/prefix URI flags:
   - `Intent.FLAG_GRANT_READ_URI_PERMISSION`
   - `Intent.FLAG_GRANT_WRITE_URI_PERMISSION`
   - `Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION`
   - `Intent.FLAG_GRANT_PREFIX_URI_PERMISSION`
3. On success, call `ContentResolver.takePersistableUriPermission()`.
4. Store the selected tree URI in Android `SharedPreferences`.
5. Send a Unity callback such as `AndroidStorageManager.OnOpenBrushFolderSelected(uriString)`.
6. On cancel, send `AndroidStorageManager.OnOpenBrushFolderCanceled()`.
7. Provide Java helpers for SAF operations that are awkward through raw C# JNI:
   - `hasOpenBrushFolder()`
   - `getOpenBrushFolderDisplayName()`
   - `clearOpenBrushFolder()`
   - `ensureDirectory(relativePath)`
   - `writeFileFromPath(relativePath, sourcePath, mimeType)`
   - `copyDirectoryFromPath(relativeDestinationPath, sourceDirectoryPath)`
   - `deleteTreeChild(relativePath)`
   - later: `listFiles(relativePath)` and `copyFileToPath(relativePath, destinationPath)`

Use `DocumentFile.fromTreeUri()` internally. Persist the URI grant and verify the grant still exists on app start; if the document was moved or deleted, clear the stored URI and prompt again.

## First-Run UX

Do not block the loading scene. The prompt should happen after the main scene is usable.

Implementation actions:

1. Add an `AndroidStorageManager` MonoBehaviour in `Main.unity` or attach it to an existing app-level singleton.
2. On startup, if `UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY` and no valid SAF root exists, show a VR popup:
   - Title: `Choose Open Brush folder`
   - Body: `Choose or create a folder named Open Brush for sketches and exports. Without it, Open Brush can run, but saving to shared storage, exports, imports, and file browsing will be limited.`
   - Primary action: `Choose Folder`
   - Secondary action: `Not Now`
3. `Choose Folder` calls the Java SAF bridge.
4. `Not Now` records only a soft dismissal in PlayerPrefs so startup does not nag every session. Do not permanently disable future prompts.
5. Any save/export/import command that needs the folder should call a shared gate:

```csharp
OpenBrushStorage.RequireSharedFolderFor("export", onReady: StartExport);
```

If the folder is missing, show a feature-specific prompt and retry the original action after a successful folder selection.

## Folder Policy

The user-selected tree is treated as the `Open Brush` root.

Required behavior:

- Require that the selected folder display name is exactly `Open Brush`.
- If the selected folder is not named `Open Brush`, warn and offer:
  - Choose again.
  - Not now.
- Do not silently write exports into arbitrary storage root.
- If provider supports directory creation and the user selected a parent folder, a later enhancement may offer `Create Open Brush folder here`, but the first implementation should keep the rule simple: user selects the `Open Brush` folder.

Suggested subdirectories:

- `Sketches`
- `Sketches/Autosave`
- `Saved Strokes`
- `Exports`
- `Snapshots`
- `Videos`
- `VRVideos`
- `Media Library`
- `Fonts`
- `Scripts`
- `Plugins`

For the first deliverable, prioritize `Exports`. Sketch save/load requires more refactoring and should be implemented as the second milestone.

## Export Implementation

Current export code writes all formats to normal filesystem paths. Keep that code path and publish the result to SAF.

Implementation actions:

1. Add a Play-only staging path:

```csharp
OpenBrushStorage.LocalExportStagingPath
// e.g. Path.Combine(Application.temporaryCachePath, "OpenBrushExports")
```

2. In `App.UserExportPath()`:
   - Non-Play builds: return existing configured export path.
   - Play builds: return the staging export path.
3. In `Export.ExportScene()`:
   - Before creating output, call `OpenBrushStorage.RequireSharedFolderFor("export", ...)` for Play builds.
   - Export into staging exactly as today.
   - After export succeeds, call `AndroidSafStorage.CopyDirectoryFromPath("Exports/<export-folder-name>", localParentExportFolder)`.
   - Delete the staging export folder after a successful copy.
   - If copy fails, keep staging and show a clear error.
   - Change the success message from `Located in App.UserExportPath()` to a storage-aware display string, e.g. `Located in Open Brush/Exports`.
4. Write `README.txt` through SAF into the selected root or `Exports`, not just into staging.
5. Follow up on `ApiMethods.OpenExportFolder()` and Lua `App:ShowExportFolder()`:
   - Desktop/non-Play: existing behavior.
   - Play Android: show the folder status or launch the Android document UI if useful; do not try to open a filesystem path.

This milestone satisfies the requirement that all exports end up under one user-visible `Open Brush` folder without requiring all-files access.

## Sketch Save/Load Implementation

Saving sketches directly to SAF requires deeper changes because `SaveLoadScript`, `FileSketchSet`, `DiskSceneFileInfo`, and `TiltFile.AtomicWriter` are path-based.

Recommended staged design:

1. Keep local staging/cache for the active `.tilt` write.
2. Save using existing `SketchSnapshot.WriteSnapshotToFile()` into a local temporary `.tilt` path.
3. Publish the resulting `.tilt` file to `Sketches/<name>.tilt` through SAF.
4. Keep a local sketch cache for browsing thumbnails and loading:
   - On startup or when the SAF root changes, sync `Sketches/*.tilt` from SAF into a local cache directory.
   - Point `FileSketchSet` at the local cache for thumbnail scanning and normal `SceneFileInfo` reads.
   - Track the SAF relative path in a new scene info type or sidecar mapping so save/rename/delete publish back to SAF.
5. Add a `SafSceneFileInfo` or `PublishedDiskSceneFileInfo` type only after the cache workflow is proven. It should implement `SceneFileInfo` and expose streams via local cached files where needed.
6. Rename/delete:
   - Apply to SAF first.
   - Then update/remove the local cache.
   - Notify `SketchCatalog` using the local cache path until `FileSketchSet` becomes storage-provider-aware.
7. Autosave:
   - Keep autosave local while the app is running.
   - If the shared folder exists, optionally publish autosaves to `Sketches/Autosave`.
   - If the user refused the folder, autosave may be local-only and should be described as not uninstall-safe.

Do not make `Application.persistentDataPath` the canonical sketch library.

## Import/Media/Scripts Follow-Up

After export and sketch save/load are working:

1. Media Library:
   - Use SAF import or copy chosen files into local working cache.
   - Store canonical imported media under `Open Brush/Media Library`.
2. Fonts/Scripts/Plugins:
   - Existing Lua and font code reads filesystem paths under `App.UserPath()`.
   - For Play builds, keep these paths pointed at the local cache so the app and HTTP/Lua subsystems still start.
   - Separately decide whether to sync these folders from SAF into the local cache on startup or disable external script/plugin loading until a storage-provider abstraction exists.
3. Screenshots/videos:
   - Publish generated files to `Snapshots`, `Videos`, and `VRVideos` under SAF.
4. `App.UserPath()` consumers:
   - Audit each use and classify as canonical shared data, local cache, or unsupported on Play.
   - Avoid returning the SAF URI from `App.UserPath()` because callers expect filesystem semantics.

## Permission and LoadingScene Changes

For `OPEN_BRUSH_GOOGLE_PLAY` only:

- Do not declare `android.permission.MANAGE_EXTERNAL_STORAGE`.
- Do not declare direct external/media storage permissions such as `READ_EXTERNAL_STORAGE`, `WRITE_EXTERNAL_STORAGE`, or Android 13 `READ_MEDIA_*` permissions.
- Do not force SD-card permission.
- Do not run `LoadingScene` all-files-access permission gate.
- Runtime permissions such as microphone should remain feature-triggered.

For all other Android builds:

- Preserve existing `/sdcard/Open Brush` behavior.
- Preserve the startup all-files access flow if it is required by current Quest/Pico behavior.

## Error Handling

Handle these cases explicitly:

- User cancels folder picker: continue, mark shared storage unavailable, show reduced-functionality message only when relevant.
- User selected non-`Open Brush` folder: warn, let the user choose again, or let them decline for now.
- Persisted URI grant disappeared: clear saved URI, prompt again.
- Folder was deleted/moved outside the app: clear saved URI and show a recovery prompt.
- Provider refuses directory/file creation: show error and keep local staging copy.
- Export generated locally but SAF copy failed: keep staging and offer retry after choosing folder.
- Very large export/sketch copy: show progress, avoid blocking the main thread.

## Verification Matrix

Build/profile tests:

- Quest/Oculus Android build still requests/uses all-files access and stores under `/sdcard/Open Brush`.
- Pico Android build behavior is unchanged.
- Google Play Android build manifest does not contain `MANAGE_EXTERNAL_STORAGE`.
- Google Play Android build does not set `ForceSDCardPermission`.
- Google Play Android build does not show Unity startup permission dialog for storage.

Runtime tests on a Play-style Android build:

- Fresh install, user chooses `Open Brush`: URI persists across app restart.
- Fresh install, user taps `Not Now`: app loads; export/save commands show a targeted prompt.
- User cancels Android picker: app returns cleanly to VR UI.
- Export creates files under selected `Open Brush/Exports`, not under app-private persistent storage.
- Uninstall/reinstall leaves the selected `Open Brush` folder and exported files intact.
- If stored URI is invalidated, app detects it and asks the user to choose again.

Regression tests:

- Desktop export still writes to `App.UserExportPath()`.
- Existing `.tilt` save/load still works on desktop and non-Play Android.
- `Export.ExportScene()` still handles each enabled format.
- `SketchCatalog` and `FileSketchSet` behavior remains unchanged outside `OPEN_BRUSH_GOOGLE_PLAY`.

## Suggested Milestones

1. Revert accidental global storage changes and add the `OPEN_BRUSH_GOOGLE_PLAY` build profile.
2. Add Android SAF bridge and first-run prompt with no save/export behavior changes.
3. Gate Play export on folder selection and publish staged exports to `Open Brush/Exports`.
4. Update export success messages for Play.
5. Add Play sketch save/load via local cache plus SAF publishing.
6. Add import/media/scripts handling.
7. Audit Lua/API folder-opening behavior and other remaining path-based shared-storage assumptions for Play builds.

## Non-Goals For First Patch

- Do not rewrite all of `App.UserPath()` around SAF.
- Do not move Quest/Pico users away from `/sdcard/Open Brush`.
- Do not use `Application.persistentDataPath` as canonical storage.
- Do not remove all-files access globally.
- Do not implement sketch migration for Play builds if Play builds can assume no legacy `/sdcard/Open Brush` files.
