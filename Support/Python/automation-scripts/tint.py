"""Apply a soft tint, increase its strength, then clear it to restore the stroke."""

from automation_sections import (apply_path, drag_slider, open_panel, prepare_stroke,
                                 reveal_result, select_tool)


def build_plan(args):
    blue = [0.12, 0.32, 0.95]
    coral = [1.0, 0.25, 0.12]
    radius = 0.045
    path = [[-0.035, 0.018, 0], [0.035, -0.018, 0]]
    slider = "SketchControls/LabsPanel(Clone)_Advanced/Mesh/Slider_TintAmount"
    return {
        "version": 1,
        "name": "Adjust Tint strength and restore the original colour",
        "prepare": [
            prepare_stroke(brush=args.brush or "DuctTape", color=blue, use_latest=args.existing_stroke),
            {"op": "tool", "tool": "TintColorTool", "radiusMeters": radius},
            {"op": "color", "colorRgb": coral},
        ],
        "steps": [
            *open_panel("Labs", park=True),
            *select_tool("TintColorTool"),
            *drag_slider(slider, 0.5),
            *apply_path(path, label="Apply a half-strength tint", seconds=1.2,
                        tool="TintColorTool", anchor="subject", padding=radius,
                        lower_wand=False, inspect_pause=0.8),
            {"op": "assert", "stroke": "subject", "strokeCountDelta": 0,
             "unchangedGeometry": True, "minimumChangedColorPoints": 5},
            *drag_slider(slider, 1.0),
            *apply_path(list(reversed(path)), label="Build up the full-strength tint", seconds=2.0,
                        tool="TintColorTool", anchor="subject", padding=radius, lower_wand=False),
            {"op": "assert", "stroke": "subject", "strokeCountDelta": 0,
             "unchangedGeometry": True, "minimumChangedColorPoints": 5,
             "maximumChangedColorPoints": 48, "expectedColorRgb": coral},
            {"op": "wait", "seconds": 0.5},
            {"op": "input", "buttons": ["Button04"]},
            {"op": "input", "buttons": []},
            {"op": "wait", "seconds": 0.5},
            *apply_path([[-0.055, 0.025, 0], [0.055, -0.025, 0]], label="Clear the tint",
                        tool="TintColorTool", anchor="subject", padding=radius, lower_wand=False,
                        approach_seconds=0.35),
            {"op": "assert", "stroke": "subject", "strokeCountDelta": 0,
             "unchangedGeometry": True, "maximumChangedColorPoints": 0, "expectedColorRgb": blue},
            *reveal_result(seconds=2.5),
        ],
    }
