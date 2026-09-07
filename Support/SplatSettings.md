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

## Import-time pruning

To remove splats that are both large and faint, set both thresholds. For example:

```json
"Splats": {
  "PruneOpacityBelow": 0.05,
  "PruneScaleFractionAbove": 0.02
}
```

This removes a splat only when its opacity is **below 5%** AND its largest linear
Gaussian scale is **greater than 2% of the original capture bounds' longest dimension**.
The scale is a Gaussian axis scale, not a full diameter or projected pixel size.
These are experimental starting values, not a measured mobile preset. Large faint
splats can collectively contribute visible surfaces, so compare the result visually.

Pruning is disabled when either setting is omitted or null. Opacity must be finite
and between 0 and 1; scale fraction must be finite and positive. Invalid pruning
thresholds cause an import error rather than silently changing their meaning.
An opacity threshold of 0 removes nothing. Zero-sized capture bounds also remove
nothing. If filtering removes every splat, the import reports an error.

Restart Open Brush after changing the config. Original files and model bounds stay
unchanged. Rejected splats are excluded from packed CPU data and GPU uploads,
including their SH coefficients. Parallel decoder workers grow output buffers as
needed, then combine survivors in source order. Temporary buffers and the final
arrays coexist during that combination.

The library stores original bounds in `GsplatBounds` under Unity's persistent data
directory. The cache checks source path, coordinate convention, file length and UTC
modification time. It does not hash file contents. A missing, stale or corrupt entry
triggers a bounds scan; later imports reuse it even after changing thresholds.
Ordinary unfiltered imports also populate the cache. Deleting the cache is safe.
An unwritable cache falls back to scanning on subsequent imports.

Standard PLY, SPZ and SOG retain their direct Spark decoding paths. The first filtered
load on a cache miss performs a bounds-only pass without packed output, then a filtered
pass. Formats already requiring canonical decoded arrays still require those arrays;
this change does not remove their existing decompression/decoding allocations.
