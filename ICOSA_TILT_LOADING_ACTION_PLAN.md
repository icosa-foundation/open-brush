# Icosa Tilt Loading Failure-Case Action Plan

## Scope

Review target: loading `.tilt` sketches from the Icosa Gallery API through both the VR sketchbook path and the non-VR thumbnail grid.

Primary code paths inspected:

- `Assets/Scripts/Sharing/IcosaSketchSet.cs`
- `Assets/Scripts/GUI/DownloadPopupWindow.cs`
- `Assets/Scripts/GUI/SketchbookPanel.cs`
- `Assets/Scripts/InitNoHeadsetMode.cs`
- `Assets/Scripts/Sharing/VrAssetService.cs`

## Findings

### 1. Invalid or missing Icosa API fields can crash metadata parsing

`IcosaSceneFileInfo` assumes required fields exist:

- `json["assetId"].ToString()`
- `json["displayName"].ToString()`
- `json["formats"].FirstOrDefault(...)`
- `x["formatType"].ToString()`
- `Int32.Parse(triCount ?? "1")`

Unexpected API payloads can throw before the sketch set can filter the asset out or show an error. This affects both VR and non-VR because both consume `IcosaSceneFileInfo`.

Action:

- Make `IcosaSceneFileInfo` parsing tolerant of missing or malformed fields.
- Treat assets without a valid TILT format URL as invalid and filter them before UI exposure.
- Replace `Int32.Parse` with `Int32.TryParse`, defaulting to a conservative value.
- Add editor tests for missing `formats`, missing `assetId`, missing `displayName`, nonnumeric `triangleCount`, no TILT format, and TILT format without `root.url`.

### 2. Invalid sketch entries are considered valid by the UI

`IcosaSceneFileInfo.IsValid` checks `m_TiltFileUrl != null`, but `Valid` always returns `true`, `Exists` always returns `true`, and `IsHeaderValid()` returns `true` before download. As a result, entries can reach the UI even when the API does not provide a usable `.tilt` URL.

Action:

- Align `Valid`, `Exists`, and/or sketch-set filtering with `IsValid`.
- In `PopulateSketchesCoroutine`, skip invalid `IcosaSceneFileInfo` records before assigning cache paths.
- Ensure VR sketchbook and non-VR grid do not show clickable entries that cannot be downloaded.

### 3. Download implementations are duplicated and behavior differs

Icosa `.tilt` downloads exist in at least three places:

- `IcosaSketchSet.DownloadTiltsCoroutine`
- `DownloadPopupWindow.DownloadTiltCoroutine`
- `VrAssetService.LoadTiltFile`

They differ in validation, retry behavior, progress handling, temp-file cleanup, and error reporting. This increases the chance that VR, non-VR, and API-triggered loads fail differently.

Action:

- Extract a shared Icosa tilt download helper that takes `IcosaSceneFileInfo`, target path, cancellation/progress hooks, and returns a typed success/failure result.
- Use it from `IcosaSketchSet`, `DownloadPopupWindow`, and `VrAssetService.LoadTiltFile`.
- Keep UI-specific progress display outside the helper.
- Add tests around temp-file replacement, invalid downloaded tilt header, failed replace, missing URL, and canceled download cleanup.

### 4. Non-VR can get stuck in loading UI after failed download

`InitNoHeadsetMode.LoadSketchEntry` calls `BeginSketchLoadingState()` before starting an Icosa download. If `DownloadAndLoadSketchEntry` later finds that the sketch is invalid or still unavailable, it logs and exits without restoring the grid or clearing `m_LoadInProgress`.

Examples:

- `sketchSet.IsSketchIndexValid(sketchIndex)` becomes false.
- `entry.SceneFileInfo == null`.
- Download fails and `entry.SceneFileInfo.Available` remains false.

Action:

- Add a non-VR failure path that restores the grid/tabs/lower controls and clears `m_LoadInProgress`.
- Show a readable status message such as "Could not download sketch." rather than leaving "Downloading sketch..." or a hidden grid.
- Add a timeout or explicit failure result from the shared downloader so the non-VR UI can deterministically recover.

### 5. VR download popup retries without a final failure state

`DownloadPopupWindow.RetryDownloadTiltCoroutine` retries three times, but if all attempts fail, the popup remains active with no terminal state beyond console/log messages. `BaseUpdate` only resolves the delayed load when `m_SceneFileInfo.Available` becomes true.

Action:

- Track a terminal failed state after retries are exhausted.
- Enable closing/backing out cleanly and show failure text in the popup.
- Resolve or cancel the delayed load command explicitly on final failure.

### 6. `UnityWebRequest.Get` is called with unchecked URLs

Both icon and tilt downloads call `UnityWebRequest.Get(sceneFileInfo.IconUrl)` or `UnityWebRequest.Get(sceneFileInfo.TiltFileUrl)` without first checking for null/empty/malformed URLs.

Action:

- Validate URLs before creating `UnityWebRequest`.
- Mark the file as failed/skipped without throwing if a URL is not usable.
- Log with a unique prefix per work item when implementing, so current test logs are easy to isolate.

### 7. Icon download failure removes otherwise loadable sketches

`PopulateSketchesCoroutine` removes sketches whose icon did not download:

- `sketches.RemoveAll(x => !x.IcosaSceneFileInfo.IconDownloaded)`

This can hide loadable sketches because a thumbnail CDN issue blocks the entire entry.

Action:

- Keep loadable sketches even when thumbnail download fails.
- Use the existing unknown/loading thumbnail fallback.
- Only filter out sketches with invalid or unavailable TILT download data.

### 8. `VrAssetService.LoadTiltFile` can delete the temp file too early

`VrAssetService.LoadTiltFile` downloads to a temp path, issues `LoadNamedFile`, then immediately deletes the temp file. If the load command consumes the path asynchronously, the file may be gone before it is read.

Action:

- Replace this path with the shared downloader and cache path flow, or delay deletion until load completion.
- Check web request status and tilt header before issuing `LoadNamedFile`.
- Report fetch/download failures instead of silently yielding out.

### 9. Query parameters are not URL-escaped

`ListAssets` appends search, license, curated, category, and order-by values directly into a query string. Search text containing spaces, `&`, `?`, `=`, or non-ASCII characters can corrupt the Icosa request.

Action:

- URL-escape query parameter values with `UnityWebRequest.EscapeURL` or a structured URI builder.
- Add tests for search text with spaces and reserved characters.

### 10. Refresh coroutine can leave stale refreshing state on some early exits

`Refresh()` sets `IsActivelyRefreshingSketches = true`, then yields `PopulateSketchesCoroutine()`, then resets it. If future changes introduce exceptions, or if coroutine stopping paths are hit, the UI can show contacting/loading state indefinitely.

Action:

- Wrap refresh state transitions in a helper/finally-style pattern available to Unity coroutines.
- Ensure forced refresh, logout, and API unavailable paths all clear active refreshing state.

## Implementation Order

1. Add `IcosaSceneFileInfo` parsing and validity tests.
2. Filter invalid tilt entries before VR/non-VR UI exposure.
3. Extract a shared Icosa tilt download helper and migrate `IcosaSketchSet` first.
4. Migrate `DownloadPopupWindow` and add final failure UI/cancel behavior.
5. Fix non-VR `DownloadAndLoadSketchEntry` to recover the grid on failure.
6. Migrate or harden `VrAssetService.LoadTiltFile`.
7. Stop filtering sketches solely because thumbnail download failed.
8. URL-escape Icosa query parameters.
9. Add focused editor/playmode tests for malformed API payloads and failed downloads.
10. Manually verify both modes with current log timestamps and a unique log prefix.

## Verification Plan

- Editor tests:
  - malformed Icosa asset JSON does not throw
  - no TILT format means no selectable sketch
  - invalid `triangleCount` defaults safely
  - query escaping preserves search text
  - failed icon download does not hide a valid sketch

- Playmode/manual checks:
  - VR curated sketch download succeeds
  - VR download failure exits popup state cleanly
  - non-VR curated sketch download succeeds
  - non-VR download failure restores the grid
  - liked-sketch signed-out, signed-in, and no-connection states show correct messages

- Unity logs:
  - Use a unique prefix for any added diagnostic logs.
  - Compare relevant error timestamps against the current clock.
  - Search the full Unity editor log or search backward from the end without arbitrary tail limits.

## Notes

- `Assets/Scenes/Main.unity` was already modified before this plan was written and was not touched.
- This plan is limited to Icosa `.tilt` sketch loading, not Icosa model import.
