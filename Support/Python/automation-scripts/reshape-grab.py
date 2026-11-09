"""Start with a stroke already drawn, select Reshape and Grab, then bend its middle."""

from automation_sections import apply_path, open_panel, prepare_stroke, reveal_result, select_tool


def build_plan(args):
    return {
        "version": 1,
        "name": "Bend a stroke with Grab",
        "prepare": [
            prepare_stroke(brush=args.brush or "DuctTape", use_latest=args.existing_stroke),
            {"op": "tool", "tool": "ReshapeTool", "radiusMeters": 0.07, "subtool": "Push"},
        ],
        "subjects": [
            {"name": "Bend and tool influence", "anchor": "subject",
             "min": [-0.09, -0.09, -0.09], "max": [0.09, 0.21, 0.09]},
        ],
        "steps": [
            *open_panel("Labs", park=True),
            *select_tool("ReshapeTool", subtool="Grab"),
            *apply_path([[0, 0, 0], [0, 0.12, 0]], label="Grab", tool="ReshapeTool",
                        anchor="subject", padding=0.07, lower_wand=False),
            {"op": "assert", "stroke": "subject", "minimumMovementMeters": 0.10,
             "fixedEndpoints": True, "strokeCountDelta": 0},
            *reveal_result(),
        ],
    }
