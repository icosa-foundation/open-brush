"""Build up darkening over a bright surface with three light-grey layers at different depths."""

import math

from automation_sections import apply_path, prepare_brush, prepare_stroke, reveal_result, select_control


def build_plan(args):
    colors = ((0.12, 0.85, 0.95), (1.0, 0.72, 0.12), (0.95, 0.35, 0.3))
    background = [prepare_stroke(
        f"band-{i}", brush="DuctTape", size=0.085, color=color,
        points=[[-0.10 + i * 0.11 + 0.13 * t,
                 -0.24 + 0.006 * math.sin(math.pi * t),
                 0.49 + 0.004 * math.sin(2 * math.pi * t)]
                for t in (j / 32 for j in range(33))])
        for i, color in enumerate(colors)]
    layers = [((-0.09, -0.20), (0.20, -0.20), 0.460),
              ((0.235, -0.178), (-0.025, -0.218), 0.435),
              ((-0.05, -0.215), (0.25, -0.185), 0.410)]
    strokes = []
    for i, (start, end, depth) in enumerate(layers):
        path = [[start[0] + (end[0] - start[0]) * t,
                 start[1] + (end[1] - start[1]) * t + 0.003 * math.sin(math.pi * t),
                 depth + 0.004 * math.sin(2 * math.pi * t)]
                for t in (j / 64 for j in range(65))]
        strokes += apply_path(path, label=f"Add light-grey darkening layer {i + 1}",
                              attachment="tip", rotation=(25, -20, 0), seconds=2.6,
                              padding=0.06, lower_wand=i == 0,
                              approach_seconds=1.2 if i == 0 else 0.45, inspect_pause=0.7)
        strokes += [{"op": "assert", "strokeCountDelta": i + 1}]
    return {
        "version": 1,
        "name": "Build up the Darken brush over bright artwork",
        "prepare": [{"op": "drawing"}, *background,
                    prepare_brush("DuctTape", size=0.125, palette_brush="Dark"),
                    {"op": "color", "colorRgb": [0.7, 0.7, 0.7]}],
        "steps": [
            *select_control({"brush": "Dark"}),
            {"op": "assert", "brush": "Dark"},
            *strokes,
            *[{"op": "assert", "stroke": f"band-{i}", "unchangedGeometry": True,
               "maximumChangedColorPoints": 0} for i in range(3)],
            *reveal_result(seconds=3.0),
        ],
    }
