"""Remove fine 3D wobble from a broad arch while retaining its overall shape."""

import math

from automation_sections import reshape_demo


def build_plan(args):
    points, path = [], []
    for i in range(65):
        t = i / 64
        x, y = -0.08 + 0.36 * t, -0.28 + 0.075 * math.sin(math.pi * t)
        points.append([x, y + 0.014 * math.sin(32 * math.pi * t),
                       0.45 + 0.012 * math.sin(26 * math.pi * t)])
        path.append([x, y, 0.45])
    return reshape_demo("Smooth", points, path, name="Smooth a jittery arch",
                        radius=0.085, seconds=4.8, minimum_movement=0.006,
                        fixed_endpoints=True, brush=args.brush or "Icing", size=0.007)
