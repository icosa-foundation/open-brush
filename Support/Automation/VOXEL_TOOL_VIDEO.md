# Voxel tool video

Recording is deferred. This is the agreed sequence for the next tutorial video.

1. Open Labs and select the Voxel tool.
2. Keep the default one-voxel brush size.
3. Draw a filled voxel wall.
4. Press the controller's context button to switch to Erase.
5. Carve a doorway through the wall.
6. Press the context button to switch to Paint.
7. Choose a contrasting orange color.
8. Paint the left pillar.
9. Open the voxel palette's Material tab in brush mode.
10. Lower opacity and paint a transparent window into the remaining wall.
11. Restore full opacity, increase emission, and paint a strip above the doorway.
12. Withdraw the controllers and hold the final view.

Use fluid, natural interactions: reach directly between targets, keep relevant
settings open across related actions, and withdraw only for the final view.
Do not include Undo or Redo. Keep the default one-voxel footprint throughout.

Material settings must apply to the painting brush so the window and emissive
strip receive their own material settings. Editing a shared palette entry would
change every voxel that already uses it.

## Draft status

The [Python scenario](../Python/automation-scripts/voxel-tool.py) currently covers
the wall, doorway and pillar color. Its paths use the default one-voxel footprint.
The material-edit actions still need automation support and implementation.
The Labs-opening interaction also needs correction before recording; the draft
has not yet completed a successful rehearsal.
