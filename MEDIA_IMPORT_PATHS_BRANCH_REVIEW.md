# Media Import Paths Branch Review

## Scope

This document reviews branch `claude/fix-media-import-paths-01B5UxoYgBZZBjgnXX6sTfjw` after merging `main` at `4786d55ad`. The merge conflict was resolved in commit `75d4a1770`.

The branch's own delta from `main` changes seven files:

- `Assets/Scripts/App.cs`
- `Assets/Scripts/API/ApiMethods.EditableModels.cs`
- `Assets/Scripts/API/ApiMethods.Utils.cs`
- `Assets/Scripts/API/ApiMethods.cs`
- `Assets/Scripts/Model.cs`
- `Assets/Scripts/ReferenceVideo.cs`
- `Assets/Scripts/SceneSettings.cs`

## Merge conflict resolution

`main` added a Blocks offline-model lookup to `Model.Location.AbsolutePath`, while this branch replaced the old model-library lookup with multi-root and absolute-path resolution.

The resolved order is:

1. If the supplied model path is absolute, use it directly.
2. For a relative path, check the Blocks offline-model library first.
3. Check the normal model library.
4. Check configured model-specific roots.
5. Check configured general media roots.
6. If no file exists, return the path under the normal model library as the backwards-compatible fallback.

This keeps the behavior introduced by both sides of the merge.

## What the branch does

### Absolute media paths

The API import endpoints for images, videos, models, and custom skyboxes can now receive an absolute filesystem path. Absolute paths are returned by `App.ResolveMediaPath()` without being combined with the normal Media Library directory.

This affects:

- `image.import`
- `video.import`
- `model.import`
- `skybox.import`
- `image.base64Decode`

Downloaded media can also be written to an absolute destination directory, although the ordinary image, video, and skybox URL call sites continue to pass fixed relative directory names.

### Additional media roots

`App.cs` introduces in-memory registration methods:

- `AddMediaRoot()`
- `AddModelRoot()`
- `AddImageRoot()`
- `AddVideoRoot()`
- `AddBackgroundImageRoot()`

It also adds getters that construct ordered root lists and a resolver that returns the first existing file found under those roots.

The intended priority is the normal Media Library, followed by media-type-specific roots, followed by general media roots. Models additionally check the Blocks offline-model library first as a result of the merge resolution.

### External videos

`ReferenceVideo` now retains the supplied absolute path and gives that path directly to Unity's `VideoPlayer`. This allows a video outside `Media Library/Videos` to play during the session in which it is imported.

### API documentation

The endpoint descriptions now advertise absolute paths and configured search roots.

## Limitations and defects

### 1. Additional roots are never registered

No current call site invokes any of the new `Add*Root()` methods. The lists therefore contain no additional roots during normal application startup.

As shipped, the branch provides absolute-path support, but its advertised multi-root behavior remains dormant unless another C# component explicitly registers roots at runtime.

The registrations are also static, in-memory state. They are neither user-configurable nor persisted between runs.

### 2. Type-specific root semantics are misleading

Methods such as `AddModelRoot(path)` sound as though `path` is the model directory itself. The corresponding getter actually appends `Models` to every registered value. The same mismatch exists for images, videos, and background images.

For example:

```text
AddModelRoot("D:/AssetLibrary/Models")
```

currently searches:

```text
D:/AssetLibrary/Models/Models
```

`AddMediaRoot()` appending the media-type subdirectory is reasonable because it represents a complete Media Library root. It is surprising for the type-specific methods.

### 3. External video paths do not survive sketch save/load reliably

For a video outside the default video library, `ReferenceVideo.PersistentPath` is reduced to only the filename. Sketch metadata saves this value. When the sketch is loaded, `VideoWidget.FromTiltVideo()` only asks `VideoCatalog` for a matching persistent path.

Consequences:

- The original external directory is lost in the saved sketch.
- A later load can resolve the wrong video if the same filename exists elsewhere.
- If the filename is not in the catalog, the loader creates a dummy video even when the original absolute file still exists.
- Additional video roots are not incorporated into `VideoCatalog` scanning by this branch.

### 4. Input lookup and output selection use the same resolver

`App.ResolveMediaPath()` searches for an existing file and is appropriate for imports. It is also used by `image.base64Decode`, which is an output operation.

If a relative output filename already exists under an additional image root, `image.base64Decode` selects and overwrites that file. This conflicts with its endpoint description, which says relative paths are saved to the default image root.

Input resolution and output-path construction need separate APIs.

### 5. Dead path-resolution helpers

`ApiMethods.Utils.cs` contains `_ResolveMediaPath()` and `_ResolveMediaPathWithMultipleRoots()`, but neither method has a call site. The live implementation is `App.ResolveMediaPath()`.

Keeping three similar resolvers makes future changes more likely to diverge.

### 6. Path comparison is not robust

`ReferenceVideo` uses a plain `StartsWith(videoLibraryPath)` check to decide whether a file is inside the default video library. This can misclassify sibling paths such as:

```text
C:/OpenBrush/Media Library/Videos-Backup/example.mp4
```

It also does not normalize separators, `..` segments, case, or trailing directory separators before comparison.

### 7. API access policy is implicit

Accepting absolute paths allows API callers to request arbitrary files readable by the Open Brush process. That may be intentional for local automation, but it should be an explicit policy because remote API calls can be enabled in user configuration.

The behavior should either be documented as trusted-local functionality or guarded by an opt-in setting and allowed-root policy.

## Proposed fixes

### 1. Add a persisted media-roots configuration

Add a `MediaRoots` section to `UserConfig` with arrays for general and type-specific roots. Register or read these values once during application initialization.

Suggested shape:

```json
{
  "MediaRoots": {
    "General": ["D:/SharedMedia"],
    "Models": ["E:/Models"],
    "Images": ["E:/Images"],
    "Videos": ["E:/Videos"],
    "BackgroundImages": ["E:/Panoramas"]
  }
}
```

Prefer deriving the ordered root list from configuration instead of mutable static lists. If runtime registration is still needed, keep it as a separate overlay with explicit lifetime semantics.

### 2. Make root meanings unambiguous

Use these rules:

- A general media root is a parent containing `Models`, `Images`, `Videos`, and `BackgroundImages`.
- A type-specific root is the final directory containing that type of media and must not have another subdirectory appended.

Rename methods if necessary, for example `AddMediaLibraryRoot()` and `AddModelDirectory()`. Normalize paths when registering them and remove duplicates using the platform-appropriate path comparer.

### 3. Separate input and output resolution

Replace the current general-purpose resolver with two operations:

```csharp
ResolveExistingMediaPath(IReadOnlyList<string> roots, string path)
ResolveMediaOutputPath(string defaultRoot, string path)
```

The input resolver should search existing files. The output resolver should use an absolute path as supplied or combine a relative path with the explicitly selected output root; it should never search other roots.

Update `image.base64Decode` to use the output resolver. Remove the two unused helpers from `ApiMethods.Utils.cs`.

### 4. Preserve resolvable video identity in sketch metadata

For backwards compatibility, continue accepting old filename-only video metadata. For newly imported videos:

- Save a normalized relative path when the video is under the default video library.
- Save the absolute path, or a versioned root identifier plus relative path, when it comes from outside the default library.
- On load, resolve the saved path directly before falling back to catalog lookup.
- Scan configured video roots in `VideoCatalog`, or allow `VideoWidget.FromTiltVideo()` to construct and initialize a `ReferenceVideo` for an existing resolved path.

A root identifier plus relative path is more portable than an absolute path when a configured shared library may be mounted at different locations on different machines. Absolute paths are still useful as a backwards-compatible fallback for local automation.

### 5. Centralize normalized containment checks

Add a path utility that:

1. Calls `Path.GetFullPath()` on both root and candidate.
2. Normalizes directory separators and trailing separators.
3. Uses `StringComparison.OrdinalIgnoreCase` on Windows and the appropriate case-sensitive comparison on platforms that require it.
4. Compares path segments rather than using an unqualified string prefix.

Use it wherever code decides whether a file is inside a media root.

### 6. Define the absolute-path security policy

Choose and document one of these policies:

- Absolute paths are allowed whenever the API is enabled, and the API is treated as trusted-local access.
- Absolute paths require an explicit `AllowAbsoluteMediaPaths` setting.
- Imports are restricted to configured roots unless a separate unrestricted-local mode is enabled.

The API endpoint descriptions should reflect the selected policy.

## Suggested implementation order

1. Split input and output resolution and fix `image.base64Decode`.
2. Remove the unused resolver helpers.
3. Correct and rename the type-specific root APIs.
4. Add persisted configuration and startup loading.
5. Integrate configured video roots and preserve external video identity in sketch metadata.
6. Add normalized containment checks and document or enforce the access policy.

## Suggested tests

Add editor tests for path resolution without requiring a player build:

- Absolute input paths are returned unchanged.
- The Blocks model directory has the intended priority for relative model paths.
- The default Media Library wins over configured roots when both contain the same relative file.
- Type-specific roots win over general media roots.
- A missing relative input falls back to the default root.
- A type-specific directory is not given a duplicated media-type suffix.
- Relative output paths always target the selected default output directory.
- Output resolution does not overwrite a matching file in an additional root.
- Containment checks reject sibling directories with a shared string prefix.
- Video metadata round-trips default-library, configured-root, and absolute external paths.
- Old filename-only video metadata still loads using the legacy catalog behavior.

An API-level smoke test should then import one image, video, model, and skybox from both an absolute path and each configured-root category.

## Verification status

The merge has no remaining unmerged paths, conflict markers, or unstaged changes. The repository-wide staged whitespace warnings observed during the merge came from files merged from `main`, not from the conflict resolution.

Unity had not refreshed or compiled the merged tree at the time of review. The latest Unity editor log was dated 12 July 2026, while the merge was completed on 13 July 2026, so that log cannot be treated as verification of the merged code.
