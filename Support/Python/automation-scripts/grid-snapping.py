"""Enable a grid, then draw a stepped stroke along three spatial axes."""

from automation_sections import (apply_path, open_panel, prepare_brush, reveal_result,
                                 select_grid_spacing)


def build_plan(args):
    path = [[-0.097, -0.297, 0.447], [0.052, -0.298, 0.452],
            [0.048, -0.197, 0.449], [0.051, -0.198, 0.548],
            [0.151, -0.202, 0.549], [0.149, -0.299, 0.552]]
    return {
        "version": 1,
        "name": "Draw a stepped shape with grid snapping",
        "panelPosition": [-0.12, -0.10, 0.40],
        "prepare": [{"op": "drawing"}, prepare_brush(args.brush or "Icing", size=0.007),
                    {"op": "color", "colorRgb": [0.12, 0.7, 0.95]}],
        "steps": [
            *open_panel("SnapSettings", via_more=False),
            *select_grid_spacing(0.5),
            *apply_path(path, label="Draw along the grid in three dimensions",
                        attachment="tip", seconds=5.0, lower_wand=False),
            {"op": "assert", "strokeCountDelta": 1,
             "lastStrokeEndpoints": [[-0.10, -0.30, 0.45], [0.15, -0.30, 0.55]],
             "endpointToleranceMeters": 0.001},
            *reveal_result(seconds=2.5),
        ],
    }
