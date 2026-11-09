# Authoring external automation scripts

Author sequences in Python or JSON and run them through the shared automation
executor. Plans define preparation, controller input, movement, timing and
camera framing.

Virtual Input supplies a virtual head and controllers without VR hardware.
It is separate from monoscopic mode. Invisible procedural arms constrain
controller reaches and grip orientation. Interactions use normal application UI
and tool input.

## Run the examples

1. Open the Main scene and **Open Brush > Procedural Automation**. Enable
   **Virtual Input next Play**, then enter Play mode. Alternatively launch a
   player with `--virtualInput`.
2. Keep the Game/player view visible. The default output is 4:3 at 1440 × 1080
   and 30 fps; the preview is letterboxed to the selected aspect ratio.
3. Run from the project root with Python 3 (`python3` on macOS):

   ```sh
   python Support/Python/record-procedural-tutorial.py --scenario draw
   ```

Choose a scenario from the library:

| # | Scenario | Demonstration |
| --- | --- | --- |
| 1 | [draw](../Python/automation-scripts/draw.py) | Select a brush and draw a curve. |
| 2 | [reshape-grab](../Python/automation-scripts/reshape-grab.py) | Grab and bend the middle of a prepared stroke. |
| 3 | [reshape-push](../Python/automation-scripts/reshape-push.py) | Push a straight stroke into an arch. |
| 4 | [reshape-flatten](../Python/automation-scripts/reshape-flatten.py) | Flatten a rippled stroke, then turn the scene to inspect its plane. |
| 5 | [reshape-smooth](../Python/automation-scripts/reshape-smooth.py) | Smooth a jittery arch. |
| 6 | [tint](../Python/automation-scripts/tint.py) | Apply a contrasting tint to the middle of a stroke. |
| 7 | [open-labs](../Python/automation-scripts/open-labs.py) | Open More panels and choose Labs. |
| 8 | [membrane](../Python/automation-scripts/membrane.py) | Draw matte and outlined membrane surfaces. |
| 9 | [snapping](../Python/automation-scripts/snapping.py) | Join straight edges with endpoint snapping. |
| 10 | [snapping-angle](../Python/automation-scripts/snapping-angle.py) | Snap an unfinished sloping line to horizontal before releasing the trigger. |
| 11 | [symmetry](../Python/automation-scripts/symmetry.py) | Draw with Snowflake, Plait or CandyCane. |

Pass a symmetry preset with `--param`, for example:

```sh
python Support/Python/record-procedural-tutorial.py --scenario symmetry --param preset=Plait --name symmetry-plait
```

Use `--rehearse` to run without recording, `--fps` to set capture rate and
`--url` to select the API address (default `http://127.0.0.1:40074`). The application
must be running or in Play mode. When several instances are open, use the URL
for the intended instance.

Use `--speed` to change step timings for any script:

```sh
python Support/Python/record-procedural-tutorial.py --scenario reshape-smooth --speed 1.5
```

Speed ranges from `0.25` to `2`: `1` is normal, `1.5` is 50% faster and `2`
is twice as fast. A step's `seconds` value is divided by this multiplier: a
1-second move takes about 0.67 seconds at `1.5`. Open Brush's built-in animations,
hover/click delays and tool behavior retain their normal timing. Capture FPS
stays independent. The CLI uses the plan's `speed` when
`--speed` is omitted, defaulting to `1`.

Draw and reshape examples accept `--brush` with a brush durable name. Grab and
Tint accept `--existing-stroke` to use the latest visible stroke; otherwise
they prepare a starter before capture. For Tint, use a contrasting stroke of
comparable size so its colour-coverage assertions remain useful.

Each plan starts from the default advanced panel layout with optional panels
closed. Your saved editing layout is preserved. Shared sections open panels
through the normal UI, including popup and panel animations.

The runner saves one canonical MP4 per script in Open Brush's
`Videos/Tutorials/<script-name>.mp4`. `--name` changes the filename and
`--output-dir` changes the directory. Keep the same name when repeating a take:
the previous MP4 is replaced only after the new video is encoded and its frame
count verified. Name preset variants explicitly, such as `symmetry-plait`.
FFmpeg and ffprobe must be on PATH. Temporary PNG frames are removed after
encoding, including when encoding fails. Use `--frames-only` to retain frames
and `recording.json` for a manual workflow instead.

Preparation can add a starter stroke on every submission; Undo or restore the
starting sketch before repeating a take. Capture uses a fixed simulation step
and can take longer than the resulting video's duration.

## Run a plan from the Editor window

1. Export a JSON plan from any Python example, for example:

   ```sh
   python Support/Python/record-procedural-tutorial.py --scenario reshape --write-plan reshape.json
   ```

2. Open **Open Brush > Procedural Automation**, enable **Virtual Input next Play**
   and enter Play mode. Keep the Game view visible.
3. Choose **Load JSON plan** and select the exported file. Use **Reload** after
   editing or regenerating it.
4. Set **Script speed** for the take; it overrides the plan's speed and stays
   selected when loading another plan. **Rehearse** runs without capture.
   Select **Video format**: 4:3, 16:9 or custom pixel dimensions. The window's
   format overrides the plan for both rehearsal and recording.
   **Record** uses the window's recording name and FPS. **Stop** stops execution
   and capture; Escape stops active execution. The status and output path appear
   in the window.

The window and HTTP `automation.script` endpoint use the same executor and
plan settings. Set controller dimensions, timing, paths and framing in the
external plan.

## Create a new script

### Author a sequence

1. Copy an example Python file to your own location. Export a
   `build_plan(args)` function returning a dictionary. `args` contains the CLI
   options, including `brush`, `existing_stroke` and the `parameters` dictionary
   populated by repeatable `--param key=value` arguments. A JSON file containing the
   same dictionary is also supported.
2. Edit the preparation, subjects and ordered steps to describe the shot.
3. Run the file directly through the generic runner:

```sh
python Support/Python/record-procedural-tutorial.py --script path/to/my_sequence.py --rehearse
python Support/Python/record-procedural-tutorial.py --script path/to/my_sequence.json --name my-take --fps 30
```

To inspect or save an example's generated plan without contacting Open Brush:

```sh
python Support/Python/record-procedural-tutorial.py --scenario reshape --write-plan reshape.json
```

Python files run as ordinary local Python code, so they can calculate curves,
loop to assemble steps or share helpers. JSON plans are purely declarative.

### Choose a teaching shape

1. `reshape-push` starts with a straight stroke. Moving the repelling sphere upward
   from below creates a local arch while leaving the distant endpoints fixed.
2. `reshape-flatten` places three ripples on an oblique stroke, aligned with a
   comfortable forward grip. A sweep projects the ripples onto that plane.
3. `reshape-smooth` adds fine wobble to a broad arch. A sweep follows the clean
   arch so smoothing removes the small fluctuations without erasing the main curve.

Each shape and gesture is ordinary external Python data. The shared
`reshape_demo` builder handles preparation, native panel/tool/mode selection,
before/after pauses and result assertions. These examples prepare their own
strokes; Grab supports working on the latest stroke with `--existing-stroke`.

### Reuse sections from the demo library

[automation_sections.py](../Python/automation_sections.py) contains shared
sections for building plans. They return plain data and make no API
calls, so they also work with `--write-plan`.

| Helper | Returns |
| --- | --- |
| `sample_cubic(control_points, samples=97)` | Sampled points for a cubic starter stroke. |
| `prepare_stroke(...)` | One preparation item; accepts `stroke_id`, `points`, `brush`, `size`, `color` and `use_latest`. |
| `reshape_demo(mode, points, path, ...)` | A complete shot with a prepared teaching shape, default drawing state, Labs/tool/mode selection, a pause on the original shape, one native reshape gesture, movement/count checks and a final reveal. Radius, orientation and minimum movement are script parameters. |
| `prepare_brush(...)` | Starting brush and size, plus optional `palette_brush` to establish a palette page before capture. Recorded brush selection still uses a native button click. |
| `prepare_script(preset, category=...)` | Select the predecessor of a named preset before capture, ready for a visible Next click. |
| `select_script(preset, category=...)` | Present the Scripts panel, click Next and assert the named preset. Open Scripts first. |
| `select_control(selector, ...)` | Present a control's panel and click it; optional expected `tool`/`subtool`, or `present=False` when already shown. |
| `select_tool(tool, subtool=None)` | Select a tool and optionally move directly to its subtool. |
| `open_panel(panel, via_more=True, park=False)` | Open a closed panel through More Options. Use `via_more=False` for its own wand-pane button. `park=True` grips and moves the opened panel aside. |
| `park_panel(panel, position=...)` | Grip and move an opened floating panel to a body-relative position before using its controls. |
| `apply_path(points, ...)` | Approach, lower the wand, pause, hold trigger along a path, release and pause. Supports contact attachment, anchor, curve, rotations, timing and padding. |
| `reveal_result(seconds=1.0)` | Withdraw the applying hand and hold a clear view of the result. |
| `rotate_scene(pivot=..., span=0.28, yaw_degrees=90, seconds=3.0)` | Hold both grips, keep the wand steady and sweep the brush hand to turn the scene. Release, withdraw both hands and hold the result. Include the rotated artwork in the plan's framing subjects. |

Compose recorded sections with Python list unpacking, for example
`steps = [*select_tool("TintColorTool"), *apply_path(...), *reveal_result()]`.

Move directly between consecutive interaction targets. Use `rest` when withdrawing
the hand to reveal the result or ending a sequence.
`open_panel` lowers the wand immediately after opening a floating panel, before
interacting with its controls.
Preparation items belong in `prepare`, not `steps`. Keep action-specific paths,
colours and result assertions in the example file.

New `.py` files under `Support/Python/automation-scripts` are automatically
available by filename stem through `--scenario`; the runner needs no registry
edit. `reshape` remains an alias for `reshape-grab`. Use `--script` for a file
elsewhere. Helpers can also be imported by adding `Support/Python` to the Python
module search path in your own authoring program.

## Plan format, version 1

The top-level fields are `version` (must be `1`), `name`, optional `speed` (default
`1`, range `0.25` to `2`), `prepare`, `subjects`,
`steps`, optional `capture`, optional `framing`, `panelPosition`, `pointingOffsetMeters`,
`upperArmMeters` and `forearmMeters`. Unknown field names are rejected.

Coordinates use `[x, y, z]` arrays in metres. Without an anchor they are relative
to the virtual body/head reference and its yaw, independent of the filming
camera. Rotations use `[x, y, z]` Euler angles in degrees, relative to body yaw.
`automation.head` or the runner's `--head X Y Z RX RY RZ` changes the body
reference while idle. The filming camera is never the origin for later actions.

### Keep the target visible

The brush controller should point diagonally toward a UI target from the side,
so its opaque body stays out of the camera-to-target line.
`pointingOffsetMeters` places the tip relative to the target in panel axes:
right, up and forward. Its default `[0.12, 0.02, -0.12]` points from the right,
2 cm above the target and 12 cm in front of the surface. Z must be at most `-0.03`.
The ray still targets the real button; the model stays visible and opaque.

For tool application, the default contact rotation is `[25, -35, 0]`, which
keeps a forward grip to the side of the contact. `apply_path` uses that
convention, and Draw supplies a similar orientation with a gradual roll.
Authors can override rotations for brushes that need a particular orientation.
Review the contact and selected icon in the recording; fitting subjects in the
frame does not itself establish that they are unobstructed. Keep the camera
fixed while correcting hand positions and orientations.

The arm model uses the controller model's grip attachment to check wrist flexion,
sideways bend and forearm rotation. It can swivel the elbow around its preferred
outward/downward position. Preflight checks sampled reaches and paths, and runtime
checks each evaluated pose. A rejected pose reports the step and hand; adjust the
contact orientation or teaching shape to suit a comfortable grip. Rotations in a
plan describe the selected attachment, whose orientation can differ from the grip.
`panelPosition` is a preferred placement: presenting a wand pane can raise it by
up to 12 cm to keep the grip comfortable. Pane rotation uses the thumbstick.

An optional `capture` object has `name` and `fps`. The runner normally supplies
this from its CLI options; `--rehearse` removes it. The body, controller ownership
and capture clock are managed by the executor.

`output` sets the dimensions for framing, rehearsal and recording. Its default
is `{"width":1440,"height":1080}` (4:3). Use `--width 1920 --height 1080` for
16:9, or set those values in the plan's `output` object. Dimensions must be even
integers from 16 to 8192 pixels. The selected output size is independent of the
Game/player window size.

### Preparation

Preparation happens before recording and does not simulate visible user actions.

| `op` | Fields and behaviour |
| --- | --- |
| `stroke` | Unique `id`, `points`, optional `brush` (default `DuctTape`), `brushSizeMeters` (default `0.02`) and `colorRgb`. Creates a normal undoable stroke from sampled body-relative points. An explicit fixture colour does not change the active colour. |
| `stroke` with `useLatest: true` | Binds `id` to the latest visible existing stroke; does not create one. |
| `tool` | `tool` enum name, optional `radiusMeters` for stroke modification tools, optional initial `subtool` for Reshape. Sets initial options so subsequent visible selections are meaningful. |
| `color` | `colorRgb: [r,g,b]`, each channel from 0 to 1. Sets the active colour before recording. |
| `drawing` | Disable straight edge, endpoint snapping and symmetry, clear straight-edge endpoint history, and reset grid/angle snapping to off. Does not clear artwork. |
| `brush` | `brush` durable name, `brushSizeMeters`, optional `paletteBrush` durable name. Sets the starting brush/size and establishes the available palette's page before capture. |
| `script` | `category` (for example `SymmetryScript`), `scriptName`, optional `selectPrevious`. Initialise scripts and choose the named preset or its predecessor before capture. |

The executor resets to the default advanced panel layout and starts from the paint tool. For
example, setting Reshape's initial subtool to Push during preparation makes the
recorded click on Grab visible. Preparation is not a scene transaction: changes
already made before a later preflight error remain undoable on the canvas.

### Recorded steps

Each step has `op`, optional `label`, and optional `seconds` (default `1.2`).
`hand` is `brush` by default or `wand` where applicable.

| `op` | Fields and behaviour |
| --- | --- |
| `present` | `target` button selector. For wand panels, raises the wand and uses native thumbstick snaps to bring the panel forward. For an already opened floating panel, waits for its opening animation without twisting the wand. |
| `focus` | `target` button selector. Points at its parent panel and waits for native focus. |
| `click` | `target`. Reaches with the brush hand, waits for real hover, presses and releases the trigger, and requires a native button activation event. `seconds` controls the approach; hover/press/release use shared timing. Optional `tool` and `subtool` verify the resulting selection. |
| `placePanel` | `panel`, one destination in `points`, `rotation`, optional `endRotation`. Approaches, grips, moves and releases an opened floating panel. `seconds` controls each reach and drag. |
| `move` | `points`, optional `curve` (`polyline` or `bezier`), `rotation`, `endRotation`, `attachment`, `anchor`, `anchorT`, `tool`, `paddingMeters`. One point is a joint-interpolated reach. Multiple points follow the supplied contact path over `seconds`, with minimum-jerk timing. Bezier needs exactly four control points. |
| `input` | Optional `trigger` boolean, `thumbstick: [x,y]` (axes in `[-1,1]`) and `buttons` array of native `VrInput` enum names. `buttons` replaces the held non-trigger buttons for this hand; `[]` releases them. For example `Button04` is the context/full-pad press used by straight-edge angle snapping. State persists until changed. Each input step consumes a frame so native consumers receive the edges. |
| `wait` | Holds current poses and inputs for `seconds`. |
| `rest` | Returns the chosen hand to its comfortable rest over `seconds`. |
| `assert` | Optional `tool`, `subtool`, `brush`, `strokeCountDelta`, `straightEdge`, `endpointSnap`, `symmetry` (native mode name), or `category` with `scriptName`. `panel` with `panelOpen` verifies visibility. `lastStrokeEndpoints: [[x,y,z],[x,y,z]]` checks the latest stroke against body-relative metre positions using `endpointToleranceMeters` (default 0.003). For a named prepared `stroke`, can require `minimumMovementMeters`, `fixedEndpoints`, `unchangedGeometry`, `minimumChangedColorPoints`, `maximumChangedColorPoints` and `expectedColorRgb`. Failure stops the run. |

Button selectors contain exactly one of:

1. `{"brush": "DuctTape"}`: brush durable name.
2. `{"tool": "ReshapeTool"}`: native `BaseTool.ToolType` enum name.
3. `{"subtool": "Grab"}`: native sculpt subtool name.
4. `{"path": "SceneRoot/.../Button"}`: exact GameObject hierarchy path for any
   other button or to disambiguate repeated controls.
5. `{"command": "MorePanels"}`: a native option button's global-command name.
6. `{"opensPanel": "Labs"}`: a panel-opening button, including one that will
   appear in a popup created by an earlier click.

Selectors must match exactly one button in the predicted UI state. Hidden
tray buttons and popup controls are predicted before recording and resolved
against the live UI when reached. While a popup is open, click selectors resolve
within that popup. Present the
appropriate panel before clicking it; later clicks can stay on that same panel.
Different panels can be presented and clicked later in the same script.
Open a floating panel before presenting or clicking its controls. The executor
predicts the native floating-panel placement and includes it in fixed framing;
Virtual Input uses the stable body reference for that placement so the filming
camera adjustment cannot move the action.

`attachment` is `tip` by default, `tool` for a native tool contact, or `root` for
the controller itself. A tool contact can specify `tool: "ReshapeTool"` to
include that tool's native forward offset. Wand motion usually uses `root`.

For an existing-stroke interaction, use `anchor: "subject"` where `subject` is
the preparation ID. `anchorT` selects a point along the original stroke's
control-point sequence (default `0.5`); it is not an arc-length fraction. Motion
points then describe metre offsets from that fixed starting point. They do not
follow the point as it deforms.

Colour assertions compare against the stroke's effective colours at preparation
time. `expectedColorRgb` inspects the point at `colorPointT` (default `0.5`, a
control-point fraction). `colorTolerance` defaults to `0.05` per RGB channel;
the same tolerance determines how many points have visibly changed. This checks
native tool results without directly writing stroke colours during recording.

First approach a path's first pose using a single-point `move`, then press the
trigger and follow the multi-point path. Preflight rejects a discontinuity of
more than 5 mm or 2 degrees. Use a Bezier curve or sufficiently smooth sampled
points for continuous drawing; a polyline retains its authored corners.

Example step sequence, assuming the desired brush has already been selected:

```json
[
  {"op":"move", "points":[[0.0,-0.25,0.4]]},
  {"op":"input", "trigger":true},
  {"op":"move", "seconds":2.0, "points":[[0.0,-0.25,0.4],[0.25,-0.20,0.4]]},
  {"op":"input", "trigger":false},
  {"op":"wait", "seconds":0.6},
  {"op":"rest"}
]
```

`placePanel` takes `panel`, one destination in `points`, and `seconds` for each
approach and drag. It uses the brush hand's native grip input and verifies the
panel's final position. The layout planner can offset the destination to keep
it clear of the working area. `rotation` describes the pointer pose when grabbing.
The panel turns toward the performer at its destination; `endRotation` sets an
explicit final pointer orientation instead.

### Fixed camera framing

One plan represents one continuous fixed-camera shot. The executor frames it
once before capture, using the current FOV and the chosen output aspect ratio. It automatically
includes prepared strokes, contact paths with `paddingMeters`, controller
interaction poses, presented panels, panel rotation, popups, floating panels and
expanded target trays. Reaches from rest and returning hands to rest need not
remain in frame; they must not displace the framing of the menus and artwork.

Add explicit `subjects` for results that extend beyond those automatic bounds.
Each subject has `name`, `min`, `max` and optionally `anchor`/`anchorT`, with the
same body-relative metre coordinates as motion. The Grab example includes the
bend and influence sphere. Declare the complete before/after visual envelope.
Raw thumbstick steps cannot predict arbitrary later UI changes; declare those
subjects explicitly and use `present` for automatic panel navigation.

`framing` supports `enabled`, `margin` (default `0.08`),
`maximumPullbackMeters` (`0.15`) and `maximumAngleDegrees` (`40`). If the shot
cannot fit, reduce its spatial extent or submit separate shots. Do not change
the output format during a take or use repeated camera tracking.

The planner keeps floating panels, their popups and expanded trays clear of the artwork
and tool paths. It tries nearby wand presentation positions, preferring the
non-drawing side, before choosing the fixed camera view. `framing.panelGap`
sets the gap in normalized screen coordinates (default `0.025`). If no comfortable
layout fits, adjust `panelPosition`, the artwork positions or the declared bounds.

Automation recording hides floating message output, the controller console,
exception overlays and error cards. Errors continue to be logged; normal
visibility is restored when capture ends.

## Use the client from your own Python program

Make `Support/Python` importable, then use:

```python
from automation_client import AutomationClient, with_capture

client = AutomationClient()
result = client.run(with_capture(plan, "my-take", fps=30))
print(result["Output"])
```

`run` uploads the whole plan once, then monitors completion. Motion timing runs
on Open Brush's simulation clock rather than Python sleeps or network latency.
Several `run` calls can form separate shots. New files need no edit to the CLI's
example choices. The CLI's `--script` option is the extension point.

For another language, POST form data to `/api/v1` with
`automation.script=<run-id>,<URL-safe-base64-encoded-UTF8-JSON>`. Keep base64
padding. Poll `query.automation`; wait for the matching `RunId`, check `Error`,
and finish when `Phase` is `Stopped` and `Recording` is false. `automation.stop`
or Escape cancels and releases inputs before returning controller ownership.
Only one automation and one capture can own the controllers/clock at a time.

## Troubleshooting

1. Rehearse first, then inspect a short recording. Check ray/reticle feedback,
   contact alignment and the before/after result. Use `assert` steps for the
   expected tool and stroke changes.
2. Read `Error` in `query.automation`. Step and execution errors are logged with
   `[AutomationScript]`; capture logs use `[ProceduralCapture]`.
3. For an unreachable pose, move the target closer or adjust the plan's arm
   lengths. If framing fails, reduce the shot's extent or split it into separate
   shots. Keep the selected output format stable throughout a take.
4. If a run stops during preparation or after modifying geometry, Undo those
   changes or restore the starting sketch before retrying.
