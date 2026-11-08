"""Composable preparation and recorded sections for Open Brush automation plans.

Helpers return ordinary dictionaries/lists. They do not contact Open Brush;
authors can concatenate, inspect and edit their output before submitting a shot.
"""

import copy


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
        steps += [{"op": "rest"}, *select_control({"subtool": subtool}, subtool=subtool, present=False)]
    return steps


def open_panel(panel):
    """Open an initially hidden panel through the native More panels popup."""
    return [
        {"op": "assert", "panel": panel, "panelOpen": False},
        {"op": "present", "target": {"command": "MorePanels"}},
        {"op": "focus", "target": {"command": "MorePanels"}},
        *select_control({"command": "MorePanels"}, present=False),
        *select_control({"opensPanel": panel}, present=False),
        {"op": "wait", "seconds": 0.6},
        {"op": "assert", "panel": panel, "panelOpen": True},
    ]


def apply_path(points, *, label="Apply", seconds=2.4, tool=None,
               attachment="tool", anchor=None, anchor_t=0.5,
               rotation=(-25, -35, 0), end_rotation=None, curve="polyline",
               padding=0.02, lower_wand=True, contact_pause=0.45, inspect_pause=0.6):
    """Approach a contact, apply the native tool along a path, release and pause."""
    if not points:
        raise ValueError("Tool application needs at least one point.")
    contact = {"attachment": attachment, "rotation": list(rotation)}
    if tool is not None:
        contact["tool"] = tool
    if anchor is not None:
        contact.update(anchor=anchor, anchorT=anchor_t)
    path = {"op": "move", "label": label, "seconds": seconds,
            "curve": curve, "points": copy.deepcopy(points), "paddingMeters": padding, **contact}
    if end_rotation is not None:
        path["endRotation"] = list(end_rotation)
    steps = [{"op": "move", "points": [list(points[0])], **contact}]
    if lower_wand:
        steps.append({"op": "rest", "hand": "wand"})
    return [*steps, {"op": "wait", "seconds": contact_pause},
            {"op": "input", "trigger": True}, path,
            {"op": "input", "trigger": False}, {"op": "wait", "seconds": inspect_pause}]


def reveal_result(seconds=1.0):
    """Withdraw the applying hand so the artwork can be seen, then hold the shot."""
    return [{"op": "rest"}, {"op": "wait", "seconds": seconds}]
