# Splat settings

Add a top-level `Splats` object to your existing `Open Brush.cfg` JSON, alongside
sections such as `Flags`. Restart Open Brush after editing. These settings apply
to imported Gaussian splats, including imports restored when loading a sketch.
Omitted or `null` values preserve the existing asset/library/project defaults.

Example values to experiment with on slower hardware (not a measured preset):

```json
{
  "Splats": {
    "SHDegree": 1,
    "SplatDownscaleFactor": 0.1,
    "SortEveryNFrames": 2
  }
}
```

| Setting | Allowed values | Effect and default when omitted |
| --- | --- | --- |
| `SHDegree` | Integer, 0 to the asset's maximum | Lower values reduce directional colour detail. Defaults to the asset's full SH degree. This does not remove stored SH data. |
| `SplatDownscaleFactor` | 0–1 | Shrinks projected splats to reduce overdraw; higher values can create gaps. Defaults to 0. Does not reduce splat count or render resolution. |
| `SortEveryNFrames` | Integer, at least 1 | 1 sorts every frame; 2 skips one frame between sorts. Larger intervals can produce stale transparency ordering. Camera movement can force an earlier sort. Defaults to 1. |
| `DepthPrepassAlphaCutoff` | 0–1.1 | 0–1 enables approximate depth writing; values above 1 disable the extra pass. Disabling changes occlusion of later geometry. Defaults to the project setting (currently 0.01). |
| `CameraTranslationRefreshThreshold` | 0.05–1, Unity world units | Camera movement beyond this forces a sort. Defaults to the project setting (currently 0.2). |
| `CameraRotationRefreshThreshold` | 0.2–30, degrees | Camera rotation beyond this forces a sort. Defaults to the project setting (currently 10). |
| `EnableGlobalSort` | Boolean | Allows compatible captures to share a depth order. Disabling can cause incorrect overlap between captures; it is not necessarily faster. Defaults to the project setting (currently true). |

Numeric values outside these ranges are clamped. Non-finite floats are invalid.
Depth writing, camera thresholds, and global sorting are scene-wide settings;
they are applied when a splat is imported. Sorting less frequently does not skip
all GPU depth/merge work. Spark compression and asynchronous upload remain enabled
by the importer. Automatic LOD is unavailable in the pinned library version.
