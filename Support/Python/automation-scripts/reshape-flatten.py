"""Flatten three broad ripples onto the tool's plane with one steady sweep."""

import math

from automation_sections import reshape_demo


def build_plan(args):
    angle = math.radians(55)
    points = []
    for i in range(97):
        t = i / 96
        offset = 0.045 * math.sin(6 * math.pi * t)
        points.append([-0.12 + 0.44 * t, -0.22 + math.sin(angle) * offset,
                       0.45 + math.cos(angle) * offset])
    return reshape_demo("Flatten", points, [[-0.12, -0.22, 0.45], [0.32, -0.22, 0.45]],
                        name="Flatten a rippled stroke onto a plane", radius=0.095,
                        seconds=4.5, rotation=(-55, 0, 0), minimum_movement=0.025,
                        brush=args.brush or "Icing")
