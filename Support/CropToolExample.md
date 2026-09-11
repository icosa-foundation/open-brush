# Crop ToolScript example

1. Use a disposable sketch or save a copy first. Cropping changes existing strokes and does not currently support Undo.
2. After Unity imports the changes, choose the `Crop` example in the ToolScript picker (`ToolScript.Crop.lua`).
3. Choose Sphere, Box, Capsule, Ellipsoid or Plane in the plugin's Shape parameter.
4. Turn off grid and angle snapping when comparing the preview with the crop. The built-in preview applies snapping; this example uses the actual trigger positions and release orientation.
5. Press the trigger at the crop centre, drag, and release to apply. The whole sketch is cropped once on release. A click without a drag does nothing.

The drag distance is `r`:

| Shape | Dimensions and orientation |
| --- | --- |
| Sphere | Radius `r`. |
| Box | Width, height and depth `2r`, oriented with the controller at release. |
| Capsule | Radius `r`, full height `4r` including its rounded ends; its Y axis follows the release orientation. |
| Ellipsoid | Full dimensions `(2r, 4r, 2r)`, oriented with the controller at release. |
| Plane | Passes through the press point; keeps the side toward the release point. Enable “Keep other side of plane” to reverse it. |

Sphere, box and capsule have built-in previews. Ellipsoid and plane do not show a preview in this example. The plane cuts an infinite half-space, not a thin slice.

Cropping clips stroke control paths and rebuilds the brushes. Brush width or generated geometry can extend beyond the boundary.

The Lua calls demonstrated are `CropSphere`, `CropBox`, `CropCapsule`, `CropEllipsoid` and `CropPlane` on `Sketch.strokes`. They also work on other stroke lists, such as `Sketch.layers[0].strokes`. Box and ellipsoid take full size vectors; capsule takes radius and full height; rotation arguments are optional Euler angles in degrees. Each list is updated with the retained strokes, including any new segments created by splitting.
