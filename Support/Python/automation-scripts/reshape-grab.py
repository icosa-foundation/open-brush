"""Start with a stroke already drawn, select Reshape and Grab, then bend its middle."""


def build_plan(args):
    curve = [[-0.08, -0.24, 0.40], [0.02, -0.04, 0.44],
             [0.24, -0.43, 0.46], [0.33, -0.20, 0.42]]
    points = []
    for i in range(97):
        t = i / 96
        s = 1 - t
        points.append([s**3 * curve[0][axis] + 3 * s**2 * t * curve[1][axis]
                       + 3 * s * t**2 * curve[2][axis] + t**3 * curve[3][axis]
                       for axis in range(3)])
    contact = {"attachment": "tool", "tool": "ReshapeTool", "anchor": "subject", "rotation": [12, 0, 0]}
    return {
        "version": 1,
        "name": "Bend a stroke with Grab",
        "prepare": [
            {"op": "stroke", "id": "subject", "useLatest": args.existing_stroke,
             "brush": args.brush or "DuctTape", "brushSizeMeters": 0.02, "points": points},
            {"op": "tool", "tool": "ReshapeTool", "radiusMeters": 0.07, "subtool": "Push"},
        ],
        "subjects": [
            {"name": "Bend and tool influence", "anchor": "subject",
             "min": [-0.09, -0.09, -0.09], "max": [0.09, 0.21, 0.09]},
        ],
        "steps": [
            {"op": "present", "target": {"tool": "ReshapeTool"}},
            {"op": "click", "target": {"tool": "ReshapeTool"}, "tool": "ReshapeTool"},
            {"op": "rest"},
            {"op": "click", "target": {"subtool": "Grab"}, "subtool": "Grab"},
            {"op": "move", "points": [[0, 0, 0]], **contact},
            {"op": "rest", "hand": "wand"},
            {"op": "wait", "seconds": 0.45},
            {"op": "input", "trigger": True},
            {"op": "move", "label": "Grab", "seconds": 2.4,
             "points": [[0, 0, 0], [0, 0.12, 0]], "paddingMeters": 0.07, **contact},
            {"op": "input", "trigger": False},
            {"op": "wait", "seconds": 0.6},
            {"op": "assert", "stroke": "subject", "minimumMovementMeters": 0.10,
             "fixedEndpoints": True, "strokeCountDelta": 0},
            {"op": "rest"},
        ],
    }
