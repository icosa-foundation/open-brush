# Quill Sound Layer Parameters

Parameters available on `SharpQuill.LayerSound` and their mapping status in Open Brush.

## Mapped

| Parameter | Type | Maps To | Notes |
|---|---|---|---|
| Gain | float | `SoundClipController.Volume` | Applied in `CreateSoundWidgetFromQuillLayer` |
| ImportFilePath | string | Audio file load path | External file reference |
| Data.AudioBytes | byte[] | Extracted to disk, then loaded | Embedded audio from qbin |

## Not Yet Mapped

| Parameter | Type | Unity Equivalent | Notes |
|---|---|---|---|
| Loop | bool | `AudioSource.loop` | Currently hardcoded `true` in `PrepareAudioPlayer`. Comment in Quill.cs acknowledges this. |
| SoundType | enum (Flat) | `AudioSource.spatialBlend` | Flat = 0 (2D), spatial = 1 (3D). Only "Flat" exists in Quill currently. |
| Attenuation.Mode | enum (None) | `AudioSource.rolloffMode` | Only "None" exists in Quill currently. |
| Attenuation.Minimum | float | `AudioSource.minDistance` | Inner radius, full volume. |
| Attenuation.Maximum | float | `AudioSource.maxDistance` | Outer radius, silence. |
| Modifiers | List\<SoundModifier\> | — | Only "None" type exists, nothing actionable yet. |

## Implementation Priority

1. **Loop** — straightforward, directly maps to `AudioSource.loop`
2. **SoundType / spatialBlend** — Flat vs 3D. Flat is the only Quill value today but supporting spatial audio unlocks positional sound in VR scenes.
3. **Attenuation (min/max distance)** — only meaningful when spatial audio is enabled. Maps directly to `AudioSource.minDistance` / `maxDistance`.
4. **Modifiers** — skip until Quill defines modifier types beyond "None".

## Relevant Files

- `Assets/Scripts/Quill.cs` — `CreateSoundWidgetFromQuillLayer()` (line ~718)
- `Assets/Scripts/SoundClip.cs` — `PrepareAudioPlayer()`, `SoundClipController`
- `Assets/Scripts/Widgets/SoundClipWidget.cs` — widget lifecycle
- `Assets/Scripts/Save/SketchMetadata.cs` — `TiltSoundClip` (save/load, line ~723)
