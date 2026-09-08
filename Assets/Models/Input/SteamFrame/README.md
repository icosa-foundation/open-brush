# Steam Frame Controller Render Models

Source copied from a local SteamVR installation:

`C:/Program Files (x86)/Steam/steamapps/common/SteamVR/drivers/frame_controller/resources/rendermodels/`

Copied folders:

- `frame_controller_left`
- `frame_controller_right`

These are SteamVR driver render-model assets for the Steam Frame controllers. The Valve OpenXR Utilities Unity package does not include static controller model files; its render-model sample retrieves model data from the OpenXR runtime instead.

License note: no explicit license or notice file was present in the local `drivers/frame_controller` folder at copy time. Treat these assets as Valve-provided SteamVR runtime assets and verify redistribution terms before shipping them in a public build.

## Prefab Assembly Notes

The SteamVR JSON files are componentized. Important non-mesh anchors:

- `openxr_aim`: likely pointer/aim pose.
- `tip`: same local position/rotation as `openxr_aim` in the copied data.
- `openxr_grip`, `handgrip`, `grip`: likely controller grip/tracking pose.
- `base`: render-model basis correction point.

Mesh components:

- Left: `body`, `status`, `thumbstick`, `dpad`, `button_grip`, `trigger`, `button_view`, `button_system`, `bumper`.
- Right: `body`, `status`, `thumbstick`, `button_a`, `button_b`, `button_x`, `button_y`, `button_grip`, `trigger`, `button_menu`, `button_system`, `bumper`.

Motion data in the JSON maps the visible parts to SteamVR/OpenXR paths:

- `thumbstick`: joystick motion for `/input/thumbstick`.
- `button_grip`: rotation for `/input/grip`.
- `trigger`: rotation for `/input/trigger`.
- left `dpad`: dpad motion.
- left `button_view`: translation for `/input/view/click`.
- right face buttons: translations for `/input/a/click`, `/input/b/click`, `/input/x/click`, `/input/y/click`.
- right `button_menu`: translation for `/input/menu/click`.
- both `button_system`: translation for `/input/system/click`.
- both `bumper`: rotation for `/input/bumper/click`.

Open Brush does not need to preserve every SteamVR animation path in the first prefab pass, but the anchors and main trigger/grip/thumbstick parts should be retained so we can map them to existing `ControllerGeometry` fields and animation behavior.
