"""Compare an unconstrained straight edge with the context-button angle constraint."""

from automation_sections import apply_path, prepare_brush, reveal_result, select_control


def build_plan(args):
    free = [[-0.08, -0.32, 0.43], [0.27, -0.27, 0.43]]
    constrained = [[-0.08, -0.13, 0.43], [0.27, -0.08, 0.43]]
    snapped = [constrained[0], [0.27, -0.13, 0.43]]
    return {
        "version": 1,
        "name": "Hold the context button for straight-edge angle snapping",
        "prepare": [{"op": "drawing"}, prepare_brush("Icing", size=0.012),
                    {"op": "color", "colorRgb": [0.12, 0.7, 0.95]}],
        "steps": [
            *select_control({"command": "StraightEdge"}),
            {"op": "assert", "straightEdge": True},
            *apply_path(free, label="Draw without the angle constraint", attachment="tip"),
            {"op": "assert", "lastStrokeEndpoints": free, "endpointToleranceMeters": 0.001},
            *reveal_result(),
            {"op": "input", "buttons": ["Button04"]},
            *apply_path(constrained, label="Hold context to constrain the angle", attachment="tip"),
            {"op": "input", "buttons": []},
            {"op": "assert", "lastStrokeEndpoints": snapped, "endpointToleranceMeters": 0.001},
            {"op": "assert", "strokeCountDelta": 2},
            *reveal_result(seconds=2.5),
        ],
    }
