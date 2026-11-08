"""Select a brush and draw a curve through real controller input."""


def build_plan(args):
    brush = args.brush or "DuctTape"
    curve = [[-0.08, -0.24, 0.40], [0.02, -0.04, 0.44],
             [0.24, -0.43, 0.46], [0.33, -0.20, 0.42]]
    return {
        "version": 1,
        "name": "Draw a stroke",
        "steps": [
            {"op": "present", "target": {"brush": brush}},
            {"op": "click", "target": {"brush": brush}},
            {"op": "move", "points": [curve[0]], "rotation": [12, -8, -12]},
            {"op": "input", "trigger": True},
            {"op": "move", "label": "Draw", "seconds": 2.4, "curve": "bezier",
             "points": curve, "rotation": [12, -8, -12], "endRotation": [12, 8, 12]},
            {"op": "input", "trigger": False},
            {"op": "wait", "seconds": 0.6},
            {"op": "assert", "strokeCountDelta": 1},
            {"op": "rest"},
            {"op": "rest", "hand": "wand"},
        ],
    }
