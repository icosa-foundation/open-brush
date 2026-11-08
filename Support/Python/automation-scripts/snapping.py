"""Enable endpoint snapping, then join three native straight-edge strokes into a triangle."""

from automation_sections import apply_path, open_panel, prepare_brush, reveal_result, select_control


def build_plan(args):
    a, b, c = [-0.08, -0.32, 0.43], [0.27, -0.32, 0.43], [0.095, -0.07, 0.43]
    near_b = [b[0] + 0.003, b[1] + 0.003, b[2]]
    near_c = [c[0] - 0.003, c[1] - 0.003, c[2]]
    near_a = [a[0] + 0.003, a[1] - 0.003, a[2]]
    toggle = "SketchControls/SnapSettingsPanel(Clone)_Advanced/Mesh/Stroke Snapping/Snap to Strokes"
    steps = [
        *open_panel("SnapSettings"),
        *select_control({"path": toggle}),
        {"op": "assert", "endpointSnap": True},
        {"op": "rest"},
        *select_control({"command": "StraightEdge"}),
        {"op": "assert", "straightEdge": True},
    ]
    for label, path, endpoints in (
        ("Draw the first edge", [a, b], [a, b]),
        ("Snap to the previous endpoint", [near_b, c], [b, c]),
        ("Close the triangle with snapped endpoints", [near_c, near_a], [c, a]),
    ):
        steps += [*apply_path(path, label=label, attachment="tip", seconds=2.4),
                  {"op": "assert", "lastStrokeEndpoints": endpoints, "endpointToleranceMeters": 0.001},
                  *reveal_result(seconds=0.8)]
    return {
        "version": 1,
        "name": "Connect straight edges with endpoint snapping",
        "prepare": [{"op": "drawing"}, prepare_brush("Icing", size=0.012),
                    {"op": "color", "colorRgb": [0.12, 0.7, 0.95]}],
        "steps": [*steps, {"op": "assert", "strokeCountDelta": 3}, *reveal_result(seconds=2.0)],
    }
