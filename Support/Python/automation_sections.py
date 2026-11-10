"""Composable preparation and recorded sections for Open Brush automation plans.

Helpers return ordinary dictionaries/lists. They do not contact Open Brush;
authors can concatenate, inspect and edit their output before submitting a shot.
"""

import copy
import math


DEFAULT_CURVE = ((-0.08, -0.24, 0.40), (0.02, -0.04, 0.44),
                 (0.24, -0.43, 0.46), (0.33, -0.20, 0.42))


def sample_cubic(control_points=DEFAULT_CURVE, samples=97):
    if len(control_points) != 4 or samples < 2:
        raise ValueError("A cubic needs four control points and at least two samples.")
    result = []
    for i in range(samples):
        t = i / (samples - 1)
        s = 1 - t
        result.append([s**3 * control_points[0][axis] + 3 * s**2 * t * control_points[1][axis]
                       + 3 * s * t**2 * control_points[2][axis] + t**3 * control_points[3][axis]
                       for axis in range(3)])
    return result


def prepare_stroke(stroke_id="subject", *, points=None, brush="DuctTape",
                   size=0.02, color=None, use_latest=False):
    result = {"op": "stroke", "id": stroke_id, "useLatest": use_latest,
              "brush": brush, "brushSizeMeters": size,
              "points": copy.deepcopy(points if points is not None else sample_cubic())}
    if color is not None:
        result["colorRgb"] = list(color)
    return result


def select_control(selector, *, tool=None, subtool=None, present=True):
    """Bring a control's panel forward and click it, optionally checking the selection."""
    steps = [{"op": "present", "target": dict(selector)}] if present else []
    click = {"op": "click", "target": dict(selector)}
    if tool is not None:
        click["tool"] = tool
    if subtool is not None:
        click["subtool"] = subtool
    return [*steps, click]


def prepare_brush(brush="DuctTape", *, size=0.012, palette_brush=None):
    """Set the starting brush/size and optional palette page before the shot."""
    result = {"op": "brush", "brush": brush, "brushSizeMeters": size}
    if palette_brush is not None:
        result["paletteBrush"] = palette_brush
    return result


def select_tool(tool, *, subtool=None):
    steps = select_control({"tool": tool}, tool=tool)
    if subtool is not None:
        steps += select_control({"subtool": subtool}, subtool=subtool, present=False)
    return steps


def park_panel(panel, position=(0.06, 0.10, 0.50), rotation=None):
    """Use a native grip to move an opened floating panel beside the working area."""
    step = {"op": "placePanel", "panel": panel, "points": [list(position)], "seconds": 1.2}
    if rotation is not None:
        step["rotation"] = list(rotation)
    return [step]


def open_panel(panel, *, via_more=True, park=False):
    """Open a closed panel through More Options, or its button on a wand pane."""
    selection = [
        {"op": "present", "target": {"command": "MorePanels"}},
        *select_control({"command": "MorePanels"}, present=False),
        *select_control({"opensPanel": panel}, present=False),
    ] if via_more else select_control({"opensPanel": panel})
    return [
        {"op": "assert", "panel": panel, "panelOpen": False},
        *selection,
        {"op": "rest", "hand": "wand"},
        {"op": "wait", "seconds": 0.6},
        {"op": "assert", "panel": panel, "panelOpen": True},
        *(park_panel(panel, **(park if isinstance(park, dict) else {})) if park else []),
    ]


def select_script(preset, *, category="SymmetryScript", present=True):
    """Reach the script controls and navigate to a named preset through native clicks."""
    nav = category.replace("Script", "ScriptsNav")
    path = f"SketchControls/ScriptsPanel(Clone)_Advanced/Mesh/{nav}/NextScriptButton"
    return [*([{"op": "present", "target": {"path": path}}] if present else []),
            {"op": "selectScript", "category": category, "scriptName": preset}]


def drag_slider(path, value, *, seconds=1.2):
    """Move a native slider to a normalized value using the controller's trigger."""
    return [{"op": "slider", "slider": path, "value": value, "seconds": seconds}]


def undo_gesture():
    """Undo one complete drawing gesture with the wand's undo button."""
    return [{"op": "input", "hand": "wand", "buttons": ["Button01"]},
            {"op": "input", "hand": "wand", "buttons": []},
            {"op": "wait", "seconds": 0.35}]


def prepare_script(preset, *, category="SymmetryScript"):
    """Start just before a preset so its selection is visible through the native Next button."""
    return {"op": "script", "category": category, "scriptName": preset, "selectPrevious": True}


def select_grid_spacing(value, *, current=0):
    """Cycle the native grid button from a known value, using its displayed units."""
    values = (0, 0.1, 0.25, 0.5, 1, 2, 3, 5)
    clicks = (values.index(value) - values.index(current)) % len(values)
    selector = {"path": "SketchControls/SnapSettingsPanel(Clone)_Advanced/Mesh/ChangeSnapGrid"}
    return [step for i in range(clicks)
            for step in select_control(selector, present=i == 0)]


def apply_path(points, *, label="Apply", seconds=2.4, tool=None,
               attachment="tool", anchor=None, anchor_t=0.5,
               rotation=None, end_rotation=None, rotation_constraint="full", curve="polyline",
               padding=0.02, lower_wand=True, contact_pause=0.45, inspect_pause=0.6,
               before_release=(), approach_seconds=1.2):
    """Approach a contact, apply the native tool along a path, release and pause."""
    if not points:
        raise ValueError("Tool application needs at least one point.")
    contact = {"attachment": attachment}
    if rotation is not None:
        contact["rotation"] = list(rotation)
    if rotation_constraint != "full":
        contact["rotationConstraint"] = rotation_constraint
    if tool is not None:
        contact["tool"] = tool
    if anchor is not None:
        contact.update(anchor=anchor, anchorT=anchor_t)
    path = {"op": "move", "label": label, "seconds": seconds,
            "curve": curve, "points": copy.deepcopy(points), "paddingMeters": padding, **contact}
    if end_rotation is not None:
        path["endRotation"] = list(end_rotation)
    steps = [{"op": "move", "points": [list(points[0])], "seconds": approach_seconds, **contact}]
    if lower_wand:
        steps.append({"op": "rest", "hand": "wand"})
    return [*steps, {"op": "wait", "seconds": contact_pause},
            {"op": "input", "trigger": True}, path, *copy.deepcopy(before_release),
            {"op": "input", "trigger": False}, {"op": "wait", "seconds": inspect_pause}]


def reveal_result(seconds=1.0):
    """Withdraw the applying hand so the artwork can be seen, then hold the shot."""
    return [{"op": "rest"}, {"op": "wait", "seconds": seconds}]


def rotate_scene(*, pivot=(0.10, -0.23, 0.44), span=0.24, yaw_degrees=90, seconds=3.0):
    """Turn with relaxed wrists, releasing and regripping before the hands cross the body."""
    turns = max(1, math.ceil(abs(yaw_degrees) / 26))
    angle = math.radians(yaw_degrees / turns)
    starts = [[-0.08, -0.28, 0.22], [-0.08 + span, -0.28, 0.22]]
    arcs = []
    for start in starts:
        x, y, z = [start[a] - pivot[a] for a in range(3)]
        arcs.append([[pivot[0] + x * math.cos(angle * i / 64) + z * math.sin(angle * i / 64),
                      pivot[1] + y,
                      pivot[2] - x * math.sin(angle * i / 64) + z * math.cos(angle * i / 64)]
                     for i in range(65)])
    contact = {"attachment": "grip", "paddingMeters": 0}

    def move_pair(points, duration, label):
        return {"op": "move", "label": label, "hand": "wand", "points": points[0],
                "seconds": duration, **contact,
                "parallel": {"op": "move", "hand": "brush", "points": points[1],
                             "seconds": duration, **contact}}

    steps = [move_pair([[start] for start in starts], 1.2, "Reach to grab the scene")]
    for turn in range(turns):
        if turn:
            steps.append(move_pair([[start] for start in starts], 0.45, "Regrip for the next turn"))
        steps += [
            {"op": "input", "hand": "wand", "buttons": ["Grip"]},
            {"op": "input", "buttons": ["Grip"]},
            {"op": "wait", "seconds": 0.15},
            move_pair(arcs, seconds / turns, "Turn the scene to inspect the result"),
            {"op": "input", "buttons": []},
            {"op": "input", "hand": "wand", "buttons": []},
        ]
    return [*steps,
        {"op": "rest", "hand": "wand"},
        *reveal_result(seconds=3.0),
    ]


def reshape_demo(mode, points, path, *, name, radius=0.08, seconds=3.0,
                 rotation=None, rotation_constraint="full", minimum_movement=0.02, fixed_endpoints=False,
                 brush="Icing", size=0.009, panel_placement=True, reveal=True):
    """Prepare a teaching shape, visibly select a reshape mode, then apply one gesture."""
    return {
        "version": 1,
        "name": name,
        "prepare": [
            {"op": "drawing"},
            prepare_stroke(points=points, brush=brush, size=size, color=(0.12, 0.7, 0.95)),
            {"op": "tool", "tool": "ReshapeTool", "radiusMeters": radius,
             "subtool": "Grab" if mode == "Push" else "Push"},
        ],
        "steps": [
            *open_panel("Labs", park=panel_placement),
            *select_tool("ReshapeTool", subtool=mode),
            *apply_path(path, label=mode, tool="ReshapeTool", rotation=rotation,
                        rotation_constraint=rotation_constraint,
                        seconds=seconds, padding=radius, lower_wand=False),
            {"op": "assert", "stroke": "subject", "minimumMovementMeters": minimum_movement,
             "fixedEndpoints": fixed_endpoints, "strokeCountDelta": 0},
            *(reveal_result(seconds=3.0) if reveal else []),
        ],
    }
