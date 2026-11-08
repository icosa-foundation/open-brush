"""Select Tint and paint a contrasting colour onto the middle of an existing stroke."""

from automation_sections import apply_path, open_panel, prepare_stroke, reveal_result, select_tool


def build_plan(args):
    blue = [0.12, 0.32, 0.95]
    coral = [1.0, 0.25, 0.12]
    radius = 0.045
    return {
        "version": 1,
        "name": "Tint part of a stroke",
        "prepare": [
            prepare_stroke(brush=args.brush or "DuctTape", color=blue, use_latest=args.existing_stroke),
            {"op": "tool", "tool": "TintColorTool", "radiusMeters": radius},
            {"op": "color", "colorRgb": coral},
        ],
        "steps": [
            *open_panel("Labs"),
            *select_tool("TintColorTool"),
            *apply_path([[-0.035, 0.018, 0], [0.035, -0.018, 0]], label="Tint",
                        tool="TintColorTool", anchor="subject", padding=radius),
            {"op": "assert", "stroke": "subject", "strokeCountDelta": 0,
             "unchangedGeometry": True, "minimumChangedColorPoints": 5,
             "maximumChangedColorPoints": 48, "expectedColorRgb": coral},
            *reveal_result(),
        ],
    }
