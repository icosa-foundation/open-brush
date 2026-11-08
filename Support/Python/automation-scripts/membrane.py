"""Select and draw a membrane, then compare the outlined variant beside it."""

import math

from automation_sections import apply_path, prepare_brush, reveal_result, select_control


def membrane_loop(center_x, samples=121):
    """A closed, gently twisted boundary, kept within a comfortable drawing reach."""
    return [[center_x + 0.105 * math.cos(t), -0.23 + 0.13 * math.sin(t),
             0.43 + 0.045 * math.sin(2 * t)]
            for t in (2 * math.pi * i / (samples - 1) for i in range(samples))]


def build_plan(args):
    return {
        "version": 1,
        "name": "Draw membrane surfaces and outlines",
        "prepare": [{"op": "drawing"}, prepare_brush(palette_brush="MatteMembrane"),
                    {"op": "color", "colorRgb": [0.1, 0.65, 0.9]}],
        "steps": [
            *select_control({"brush": "MatteMembrane"}),
            *apply_path(membrane_loop(-0.035), label="Draw a membrane", seconds=3.6,
                        attachment="tip", rotation=(-25, -40, -12)),
            {"op": "assert", "strokeCountDelta": 1},
            *reveal_result(seconds=1.4),
            *select_control({"brush": "OutlinedMembrane"}),
            *apply_path(membrane_loop(0.235), label="Draw an outlined membrane", seconds=3.6,
                        attachment="tip", rotation=(-25, -40, -12)),
            {"op": "assert", "strokeCountDelta": 2},
            *reveal_result(seconds=2.5),
        ],
    }
