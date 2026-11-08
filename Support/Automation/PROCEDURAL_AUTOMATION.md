# Authoring external automation scripts

The 8 November library additions are `membrane`, `snapping`, `snapping-angle`
and `symmetry`. These were recorded in the Windows Unity Editor using Virtual
Input with no physical controllers, at 1920x888 and 30 fps. Native UI activations,
stroke counts and 1 mm endpoint checks passed; representative UI, drawing-contact
and final-result frames were reviewed. Snowflake, Plait and CandyCane created
12, 5 and 15 strokes respectively. The context-button press/hold/release edges
were also checked. These additions have not been run on macOS.

`snapping-angle` draws one sloping straight edge while holding the trigger,
pauses on the unfinished line, presses `Button04` without moving the controller,
then pauses on the horizontal snapped preview before releasing the trigger.
It checks one final stroke and the horizontal endpoints to a 1 mm tolerance.

New sequences can be authored entirely in Python or JSON. They use the
`automation.script` HTTP endpoint and a shared C# executor. Changing paths,
timings, button selections, input events or the order of operations needs no
C# changes or Unity recompilation. New engine capabilities still need engine code.

Virtual Input simulates head and controller input without VR hardware. It is
separate from monoscopic mode. Invisible procedural arms constrain controller
reaches; the recorded interaction uses normal UI and tool input.

## Run the examples

1. Open the Main scene and **Open Brush > Procedural Automation**. Enable
   **Virtual Input next Play**, then enter Play mode. Alternatively launch a
   player with `--virtualInput`.
2. Keep the Game/player view rendering at the intended recording resolution and
   aspect ratio. Let startup messages disappear before recording.
3. Run from the project root, using Python 3 (`python3` on macOS if appropriate):

```sh
python Support/Python/record-procedural-tutorial.py --scenario draw --name draw-example
python Support/Python/record-procedural-tutorial.py --scenario reshape --name grab-example
python Support/Python/record-procedural-tutorial.py --scenario reshape --existing-stroke --name grab-existing
python Support/Python/record-procedural-tutorial.py --scenario tint --name tint-example
python Support/Python/record-procedural-tutorial.py --scenario open-labs --name open-labs-example
python Support/Python/record-procedural-tutorial.py --scenario membrane --name membrane-example
python Support/Python/record-procedural-tutorial.py --scenario snapping --name snapping-endpoints
python Support/Python/record-procedural-tutorial.py --scenario snapping-angle --name snapping-angle
python Support/Python/record-procedural-tutorial.py --scenario symmetry --param preset=Snowflake --name symmetry-snowflake
python Support/Python/record-procedural-tutorial.py --scenario symmetry --param preset=Plait --name symmetry-plait
python Support/Python/record-procedural-tutorial.py --scenario symmetry --param preset=CandyCane --name symmetry-candycane
```

These load [draw.py](Support/Python/automation-scripts/draw.py),
[reshape-grab.py](Support/Python/automation-scripts/reshape-grab.py) and
[tint.py](Support/Python/automation-scripts/tint.py) and
[open-labs.py](Support/Python/automation-scripts/open-labs.py). Their
selections, paths, inputs, timing and assertions live in those external files.
The Reshape example prepares a starter stroke **before capture** or uses the
latest existing stroke. The video begins with the stroke already drawn, then
shows selecting Reshape, selecting Grab and bending its middle.

The Tint example starts with a blue stroke, selects Tint and applies coral to
the middle. It checks the colour at the centre, requires a local colour change,
and verifies that geometry and stroke count stay unchanged. It demonstrates
applying tint; the tool should be in apply mode. With `--existing-stroke`, choose
a contrasting stroke of comparable size so the coverage checks remain useful.

Every external plan starts from the default **advanced editing** panel layout,
with optional panels closed and the default wand-panel rotation. Saved panel
positions, attachments and floating panels are ignored in Virtual Input. Layout
changes made in this mode are not written back to the user's saved layout or
editing-mode preference. Preparation does not attach Labs to the wand.

The Open Labs demo points at the admin panel, presses the three-dot **More**
button, clicks **Labs** (the beaker icon) in **More Options**, and reveals the
floating panel. The API calls this button's command `MorePanels`.
Tint and Grab reuse that same recorded
opening section before selecting their tools. Opening a panel and interacting
with it use the normal UI, including popup and panel animations.

Use `--rehearse` to run without recording, `--fps` to set capture rate, `--brush`
for a brush durable name, and `--url` for a different API address. The default
address is `http://127.0.0.1:40074`. The application must be running or in Play
mode; ensure the URL belongs to the intended instance when several are open.
Use a new recording name for each take. Undo completed drawing or bending before
another take if the same starting canvas is needed. Preparation does not clear
the canvas; it can add a new starter on every submission.

Capture writes `<name>_frames` under Open Brush's Videos directory, with
`frame_000000.png` onwards and `recording.json`. The client reports the full path
and an FFmpeg command. PNG capture uses a fixed simulation step and can take
longer than the resulting video's duration.

```sh
ffmpeg -n -framerate 30 -i "/path/to/grab-example_frames/frame_%06d.png" -c:v libx264 -pix_fmt yuv420p -vf "pad=ceil(iw/2)*2:ceil(ih/2)*2" "grab-example.mp4"
```

## Create a new script

1. Copy either example Python file to your own location. Export a
   `build_plan(args)` function returning a dictionary. `args` contains the CLI
   options, including `brush`, `existing_stroke` and the `parameters` dictionary
   populated by repeatable `--param key=value` arguments. A JSON file containing the
   same dictionary is also supported.
2. Edit the preparation, subjects and ordered steps. Paths and dimensions are
   data; they do not require a new C# action class or a new scenario registration.
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

### Reuse sections from the demo library

[automation_sections.py](Support/Python/automation_sections.py) contains shared
sections used by all three examples. They return plain data and make no API
calls, so they also work with `--write-plan`.

| Helper | Returns |
| --- | --- |
| `sample_cubic(control_points, samples=97)` | Sampled points for a cubic starter stroke. |
| `prepare_stroke(...)` | One preparation item; accepts `stroke_id`, `points`, `brush`, `size`, `color` and `use_latest`. |
| `prepare_brush(...)` | Starting brush and size, plus optional `palette_brush` to establish a palette page before capture. Recorded brush selection still uses a native button click. |
| `prepare_script(preset, category=...)` | Select the predecessor of a named preset before capture, ready for a visible Next click. |
| `select_script(preset, category=...)` | Present the Scripts panel, click Next and assert the named preset. Open Scripts first. |
| `select_control(selector, ...)` | Present a control's panel and click it; optional expected `tool`/`subtool`, or `present=False` when already shown. |
| `select_tool(tool, subtool=None)` | Select a tool and optionally withdraw and select its subtool. |
| `open_panel(panel)` | From a closed state, present and focus the admin panel, press More, choose the panel in More Options, wait and check that it opened. |
| `apply_path(points, ...)` | Approach, lower the wand, pause, hold trigger along a path, release and pause. Supports contact attachment, anchor, curve, rotations, timing and padding. |
| `reveal_result(seconds=1.0)` | Withdraw the applying hand and hold a clear view of the result. |

Compose recorded sections with Python list unpacking, for example
`steps = [*select_tool("TintColorTool"), *apply_path(...), *reveal_result()]`.
Preparation items belong in `prepare`, not `steps`. Keep action-specific paths,
colours and result assertions in the example file.

New `.py` files under `Support/Python/automation-scripts` are automatically
available by filename stem through `--scenario`; the runner needs no registry
edit. `reshape` remains an alias for `reshape-grab`. Use `--script` for a file
elsewhere. Helpers can also be imported by adding `Support/Python` to the Python
module search path in your own authoring program.

## Plan format, version 1

The top-level fields are `version` (must be `1`), `name`, `prepare`, `subjects`,
`steps`, optional `capture`, optional `framing`, `panelPosition`, `pointingOffsetMeters`,
`upperArmMeters` and `forearmMeters`. Unknown field names are rejected.

Coordinates use `[x, y, z]` arrays in metres. Without an anchor they are relative
to the virtual body/head reference and its yaw, independent of the filming
camera. Rotations use `[x, y, z]` Euler angles in degrees, relative to body yaw.
`automation.head` or the runner's `--head X Y Z RX RY RZ` changes the body
reference while idle. The filming camera is never the origin for later actions.

### Keep the target visible

The brush controller should point diagonally toward a UI target from beside and
below it, so its opaque body stays out of the camera-to-target line.
`pointingOffsetMeters` places the tip relative to the target in panel axes:
right, up and forward. Its default `[0.10, -0.06, -0.10]` points from the right
and below, 10 cm in front of the surface. Z must be at most `-0.03`.
The ray still targets the real button; the model stays visible and opaque.

For tool application, the default contact rotation is `[-25, -35, 0]`, which
keeps the body to the right and below the contact. `apply_path` uses that
convention, and Draw supplies a similar orientation with a gradual roll.
Authors can override rotations for brushes that need a particular orientation.
Review the contact and selected icon in the recording; fitting subjects in the
frame does not itself establish that they are unobstructed. Keep the camera
fixed while correcting hand positions and orientations.

An optional `capture` object has `name` and `fps`. The runner normally supplies
this from its CLI options; `--rehearse` removes it. The body, controller ownership
and capture clock are managed by the executor.

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
| `focus` | `target` button selector. Points at its parent panel and waits for native focus. Use this before clicking controls that expand on panel hover, such as the admin buttons. |
| `click` | `target`. Reaches with the brush hand, waits for real hover, presses and releases the trigger, and requires a native button activation event. `seconds` controls the approach; hover/press/release use shared timing. Optional `tool` and `subtool` verify the resulting selection. |
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
against the live UI when reached. Present the
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

### Fixed camera framing

One plan represents one continuous fixed-camera shot. The executor frames it
once before capture, using the current FOV and aspect ratio. It automatically
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
resolution/aspect during a take or use repeated camera tracking.

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

## Implementation and verification

The shared engine is [AutomationScript.cs](Assets/Scripts/Input/AutomationScript.cs)
and [ScriptedAutomation.cs](Assets/Scripts/Input/ScriptedAutomation.cs), exposed by
[ApiMethods.Automation.cs](Assets/Scripts/API/ApiMethods.Automation.cs). The Python
transport is [automation_client.py](Support/Python/automation_client.py).
The older Editor Draw/Reshape convenience controls and dedicated endpoints
assume wand-attached targets. Use the external library for these default-layout
popup/floating-panel sequences; the window still provides the Virtual Input toggle.

1. Rehearse without capture, then inspect a short MP4. Check panel rotation,
   ray/reticle feedback, contact alignment and the readable before/after result.
2. Put expected tool/stroke results in `assert` steps. Check Undo/Redo for native
   tool operations when authoring a new interaction.
3. Read `Error` in `query.automation`; the runtime also logs each step and errors
   with `[AutomationScript]`. Capture logs use `[ProceduralCapture]`.
4. Validate each target platform separately. Windows Editor validation does not
   establish macOS or standalone player support; macOS validation is pending.

The external Draw, Grab and Tint examples have completed in the Windows Editor
via HTTP, including native result assertions. Tint changed 31 of 99 points on
its standard starter, left geometry unchanged, and passed native Undo/Redo.
Draw and Grab were also rerun after conversion to the shared Python sections.
Open Labs was recorded from the default layout; Tint, Grab and Draw were then
run sequentially with a fresh default-layout reset before each. All 33 panel
attachments/offsets were audited against their authored defaults, optional panels
started closed, and the saved layout and editing-mode preferences remained unchanged.
After the visibility pose changes, sampled camera-to-target rays found no
controller-body bounds overlapping icon centres/inner corners in Open Labs,
Tint or Grab, or the contact points during Tint/Grab application. Selected
recorded frames were also inspected. These checks cover the current controller
model and examples, not every possible authored path or brush orientation.
A custom JSON plan switched between
multiple panels, and lifecycle checks covered an unreachable pose and cancelling
while a trigger was held. These HTTP checks used a temporary alternate listener
for this instance's existing API handlers because another Editor owned port 40074.
