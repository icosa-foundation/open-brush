"""Build up darkening over a bright surface with two overlapping brush strokes."""

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
    path = [[-0.07 + 0.29 * i / 64, -0.205 + 0.004 * math.sin(math.pi * i / 64), 0.44]
            for i in range(65)]
    return {
        "version": 1,
        "name": "Build up the Darken brush over bright artwork",
        "prepare": [{"op": "drawing"}, *background,
                    prepare_brush("DuctTape", size=0.065, palette_brush="Dark"),
                    {"op": "color", "colorRgb": [0.12, 0.12, 0.12]}],
        "steps": [
            *select_control({"brush": "Dark"}),
            {"op": "assert", "brush": "Dark"},
            *apply_path(path, label="Darken the surface", attachment="tip", rotation=(25, -20, 0),
                        seconds=3.0, padding=0.055, inspect_pause=0.9),
            {"op": "assert", "strokeCountDelta": 1},
            *apply_path(list(reversed(path)), label="Build up the darkening", attachment="tip",
                        rotation=(25, -20, 0), seconds=3.0, padding=0.055, lower_wand=False,
                        approach_seconds=0.15),
            {"op": "assert", "strokeCountDelta": 2},
            *[{"op": "assert", "stroke": f"band-{i}", "unchangedGeometry": True,
               "maximumChangedColorPoints": 0} for i in range(3)],
            *reveal_result(seconds=3.0),
        ],
    }
