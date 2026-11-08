"""Flatten an oblique rippled stroke while keeping a forward-facing grip."""

import math

from automation_sections import reshape_demo


def build_plan(args):
    pitch, yaw = math.radians(25), math.radians(-12)
    normal = [math.sin(yaw) * math.cos(pitch), -math.sin(pitch),
              math.cos(yaw) * math.cos(pitch)]
    # Sweep along the plane; independent ripples across and out of it make a 3D stroke.
    length = math.hypot(normal[0], normal[1])
    along = [-normal[1] / length, normal[0] / length, 0]
    across = [-normal[2] * along[1], normal[2] * along[0],
              normal[0] * along[1] - normal[1] * along[0]]
    center = [0.10, -0.23, 0.44]
    points = []
    for i in range(97):
        t = i / 96
        offset = 0.055 * math.sin(6 * math.pi * t)
        sideways = 0.030 * math.sin(4 * math.pi * t)
        points.append([center[a] + along[a] * 0.38 * (t - 0.5)
                       + normal[a] * offset + across[a] * sideways
                       for a in range(3)])
    path = [[center[a] + along[a] * x for a in range(3)] for x in (-0.19, 0.19)]
    return reshape_demo("Flatten", points, path,
                        name="Flatten a rippled stroke onto an oblique plane", radius=0.095,
                        seconds=5.2, rotation=(25, -12, 0), minimum_movement=0.025,
                        brush=args.brush or "Icing")
