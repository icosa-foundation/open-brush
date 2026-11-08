"""Snap an unfinished sloping straight edge to horizontal before releasing the trigger."""

from automation_sections import prepare_brush, reveal_result, select_control


def build_plan(args):
    path = [[-0.08, -0.22, 0.43], [0.27, -0.11, 0.43]]
    snapped = [path[0], [path[1][0], path[0][1], path[1][2]]]
    contact = {"attachment": "tip", "rotation": [-25, -35, 0]}
    return {
        "version": 1,
        "name": "Snap an unfinished straight edge to horizontal",
        "prepare": [{"op": "drawing"}, prepare_brush("Icing", size=0.012),
                    {"op": "color", "colorRgb": [0.12, 0.7, 0.95]}],
        "steps": [
            *select_control({"command": "StraightEdge"}),
            {"op": "assert", "straightEdge": True},
            {"op": "move", "points": [path[0]], **contact},
            {"op": "rest", "hand": "wand"},
            {"op": "wait", "seconds": 0.45},
            {"op": "input", "trigger": True},
            {"op": "move", "label": "Draw a sloping unfinished line", "points": path,
             "seconds": 2.4, **contact},
            {"op": "wait", "label": "Hold the sloping preview", "seconds": 1.5},
            {"op": "input", "buttons": ["Button04"]},
            {"op": "wait", "label": "Snap the preview to horizontal", "seconds": 1.8},
            {"op": "input", "trigger": False},
            {"op": "wait", "seconds": 0.6},
            {"op": "input", "buttons": []},
            {"op": "assert", "lastStrokeEndpoints": snapped, "endpointToleranceMeters": 0.001},
            {"op": "assert", "strokeCountDelta": 1},
            *reveal_result(seconds=2.5),
        ],
    }
