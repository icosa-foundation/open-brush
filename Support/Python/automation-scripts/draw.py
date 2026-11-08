"""Select a brush and draw a curve through real controller input."""

from automation_sections import DEFAULT_CURVE, apply_path, reveal_result, select_control


def build_plan(args):
    brush = args.brush or "DuctTape"
    if args.existing_stroke:
        raise ValueError("The Draw example creates a new stroke; omit --existing-stroke.")
    return {
        "version": 1,
        "name": "Draw a stroke",
        "steps": [
            *select_control({"brush": brush}),
            *apply_path(DEFAULT_CURVE, label="Draw", attachment="tip", curve="bezier",
                        rotation=(12, -8, -12), end_rotation=(12, 8, 12)),
            {"op": "assert", "strokeCountDelta": 1},
            *reveal_result(),
        ],
    }
