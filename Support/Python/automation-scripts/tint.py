"""Apply a soft tint, increase its strength, then clear it to restore the stroke."""

from automation_sections import (apply_path, drag_slider, open_panel, prepare_stroke,
                                 reveal_result, sample_cubic, secondary_button, select_tool)


def build_plan(args):
    blue = [0.12, 0.32, 0.95]
    coral = [1.0, 0.25, 0.12]
    radius = 0.045
    curve = sample_cubic()
    centre = curve[len(curve) // 2]
    relative = [[point[a] - centre[a] for a in range(3)] for point in curve]
    soft_path = relative[18:31]
    full_path = relative[66:79]
    clear_path = relative[10:87]
    clear_rotation = (10, -20, 0)
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
            *drag_slider(slider, 0.3),
            *apply_path(soft_path, label="Apply a 0.3-strength tint", seconds=1.2,
                        tool="TintColorTool", anchor="subject", padding=radius,
                        lower_wand=False, inspect_pause=0.8),
            {"op": "assert", "stroke": "subject", "strokeCountDelta": 0,
             "unchangedGeometry": True, "minimumChangedColorPoints": 5},
            *drag_slider(slider, 1.0),
            *apply_path(full_path, label="Tint a separate section at full strength", seconds=2.0,
                        tool="TintColorTool", anchor="subject", padding=radius, lower_wand=False),
            {"op": "assert", "stroke": "subject", "strokeCountDelta": 0,
             "unchangedGeometry": True, "minimumChangedColorPoints": 5,
             "maximumChangedColorPoints": 80, "colorPointT": 0.75, "expectedColorRgb": coral},
            *secondary_button(clear_path[0], attachment="tool", rotation=clear_rotation,
                              tool="TintColorTool", anchor="subject"),
            *apply_path(clear_path, label="Clear both tinted sections",
                        tool="TintColorTool", anchor="subject", padding=radius, lower_wand=False,
                        rotation=clear_rotation, approach_seconds=0.35),
            {"op": "assert", "stroke": "subject", "strokeCountDelta": 0,
             "unchangedGeometry": True, "maximumChangedColorPoints": 0, "expectedColorRgb": blue},
            *reveal_result(seconds=2.5),
        ],
    }
