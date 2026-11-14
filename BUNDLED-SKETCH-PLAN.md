# Bundled sketch packaging and loading

## Outcome

Keep offline examples available in the existing Local sketchbook view without
moving source assets during builds or copying every example into user storage
at startup. Do not add sketchbook tabs or a general collection framework.

The current baseline is integration/passthrough-ar-handtracking-improvements,
including ac3b11c73 (UI tweaks). The existing Aura build completed successfully
on 2026-10-23 at 14:45; preserve that APK while preparing this change.

## Constraints

1. Include the examples only in Android Viewer builds.
2. Keep binary sketches separate from the showcase JSON resources.
3. Keep source files at stable paths. Generated catalog data belongs in build
   intermediates, not Assets or the project root.
4. Viewer tabs are Featured Sketches / All Sketches / Liked Sketches. Featured is
   exclusively the fixed packaged set, not a merge with local files. Normal
   editing builds retain their Your Sketches tab and file-backed source.
5. Packaged examples are read-only. Viewer has no file-management or hide controls.
6. Do not migrate, delete or modify existing local/seeded files. Viewer simply stops
   using those files for Featured. Seeding/deletion-history bookkeeping is no
   longer part of the Viewer data source.
7. Read catalog metadata and thumbnails without loading all sketch binaries.
   Load a binary only when the selected sketch needs it. Prefer existing stream
   abstractions; avoid persistent copies and unnecessary binary caches.
8. Preserve unrelated work. Keep this plan local and untracked. Commit focused
   implementation changes, but do not push or install without authorization.

## Ordered milestones

### 1. Deliver stable packaging while preserving current behavior

Implement a minimal filename catalog and supply it and the existing 22 sketch
files as additional StreamingAssets through Unity's build context. Adapt the
existing seeder to read these packaged assets. Retain its tracking key, sketch
identifiers, overwrite protection and deletion history. Remove the temporary
folder move. Keep the catalog minimal; metadata/thumbnails are deferred to step 2.

Completion: the build hook is active, the seeder consumes its output, and the old
folder-move code is gone. A local AndroidXR Viewer build contains the catalog and
all 22 matching payloads, with unchanged source hashes and no new checkout debris.
This is an independently deliverable stage; the read-only redesign is separate.

### 2. Deliver read-only Featured access and list integration together

Expand the catalog to include metadata and thumbnails. Add read-only packaged
access using the existing sketch stream interface. Reuse the file sketch list's
metadata/icon loading with a fixed set and no filesystem scan/watcher. Read the
chosen sketch from packaged assets only when needed, with explicit stream
ownership and no automatic copy into UserSketchPath. Supply Viewer-specific
localized labels in VR and the non-headset picker.

Completion: browsing reads no sketch binary; selecting an example uses the normal
sketch loader and stream API. No new tab, user-file merging, seeding, migration,
directory watching or persistent binary cache. Existing files are untouched.
All Viewer build targets receive the packaged set; Android uses precompressed
inputs, other Viewer players use ordinary StreamingAssets. Regular builds remain
unchanged. The runtime and list cutover must ship together.

### 3. Verify each deliverable

Compile affected runtime and Editor code. Check all 22 source containers and the
generated catalog; exercise stream ownership, read-only behavior, local precedence
with focused checks. Run a fresh local AndroidXR Viewer build using
Support/windows/build-local.ps1, inspect its packaged files, and compare checkout
state before and after cleanup. Record unrelated pre-existing modifications.

Completion: package contents and build cleanup are verified. Clearly distinguish
those checks from device validation. Aura testing should cover offline listing,
loading multiple examples, a user sketch with a colliding name, restart behavior,
and lack of newly seeded files. Coordinate installation with the user. Existing
copied examples remain untouched and need not appear in Featured.

## Implementation notes

Unity's BuildPlayerContext.AddAdditionalPathToStreamingAssets accepts external
files/directories directly; it does not require moving them into the project's
StreamingAssets directory. Add individual sketch files so Unity .meta files are
not accidentally included.

Android packs raw StreamingAssets without compression. To retain the previous
package size, generate gzip inputs under Library/BundledSketchBuild/Payloads.
Android packaging imports those inputs as DEFLATE ZIP entries under the original
.tilt names, dropping .gz. The runtime reader uses those original names and
AssetManager supplies decoded bytes; it must not append .gz or decompress again.
This behavior was verified in both the actual APK and AAB, including all payload
hashes. No runtime managed decompressor is required.

The current ZIP reader accepts filesystem paths. Investigate a stream overload
before introducing an on-demand disk cache. Any cache fallback must be bounded,
outside the user sketchbook, and documented as a change from direct packaged access.

The previous estimate was approximately 200–400 lines of production code, replacing
some existing code. Reassess after the stream and list integration is understood;
do not distort the implementation merely to meet that estimate.

## Progress

1. Preparatory data code committed as e5bebd391. This alone was scaffolding, not
   an independent functional deliverable.
2. Connected packaging, Android asset reading, seeder adaptation and removal of
   TempIncludeBundledSketches committed as 16c7a9021. The stage uses filenames only.
3. Player runtime, Editor runtime and full Editor build code compile with cached
   Unity compiler settings. The production catalog builder passes checks for all
   22 names, deterministic output, unchanged source hashes, and rejection of
   unsupported versions, path traversal, duplicate names and empty catalogs.
4. Initial full build succeeded and passed payload/source checks, but exposed a
   16 MiB package-size regression because the raw payloads were stored uncompressed.
   Compression was added in 04c121bf2. Package validation then caught Android's
   original-name handling; db0d72c8a corrected the reader. These intermediate APKs
   were not installed or pushed.
5. Deliverable 1 COMPLETE: final local AndroidXR Viewer build from db0d72c8a
   succeeded on 2026-10-23 at 15:54:35. The APK and AAB each contain all 22 payloads
   under original names with matching decoded hashes. The APK uses DEFLATE for
   each payload. All 45 sketch input files/metas retain their original hashes.
6. No new dirty/untracked paths appeared after the build. Existing font/settings
   modifications and previously untracked files remain; no manual cleanup or
   restoration of those unrelated files was performed. This status comparison
   does not claim byte-for-byte checks of pre-existing dirty font/settings files.
7. Final APK is 415.80 MiB, only 0.3 KiB larger than the archived prior UI APK.
   Artifact: Builds/Android-Viewer-AndroidXR/OpenBrush.apk. Detailed check results:
   Logs/BundledSketchPackaging/package-verification.json.
8. Nothing pushed or installed. Fresh-install seeding has not been tested on Aura;
   that remains a device-validation boundary, not a claimed result.
9. Viewer simplification approved: Featured directly lists packaged examples.
   Label changes committed as 745c77bdf; read-only access, fixed-list integration
   and removal of startup seeding committed as 34d2acff9.
10. Focused production-code checks passed: all 22 metadata/thumbnail entries,
   browsing without a binary present, fixed-set initialization without a Unity App
   or user path, mutation rejection, both ZIP stream readers and stream ownership,
   deterministic catalog and unchanged source hashes. Viewer and non-Viewer runtime
   and Editor code compile with cached Unity compiler settings.
11. Read-only Featured build from 34d2acff9 succeeded on 2026-10-23 at 17:47:56.
   APK and AAB contain all 22 unchanged sketch payloads and matching v2 catalog
   metadata/thumbnails. Payloads remain DEFLATE-compressed. Android stores the
   2,227,346-byte JSON catalog uncompressed despite its gzip build input; this is
   a small index cost, not a payload-compression regression.
12. Final APK is 417.92 MiB versus 415.80 MiB for the preceding packaging build.
   No new example copies are seeded into Viewer storage. Existing copied files
   are intentionally left untouched and ignored by Featured.
13. All 45 source files/metas retain their hashes; checkout status has no new or
   removed paths compared with the pre-build baseline. Unrelated dirty files remain.
14. Pushed through 34d2acff9 at the user's explicit request. No installation,
   visual validation or on-device loading test has been performed. Other Viewer
   platforms use plain StreamingAssets; they were not locally player-built here.
15. Read-only Featured/list integration is complete in code and locally build-tested.
   Detailed results: Logs/ViewerFeatured/package-verification.json. Editor testing
   of the Viewer variant requires OPEN_BRUSH_VIEWER; ForceViewOnly alone remains
   the normal editing build's view-only mode.
16. Previous good APKs preserved under Builds/Android-Viewer-AndroidXR/Archive.

Reference: https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/build/buildplayercontext/addadditionalpathtostreamingassets
