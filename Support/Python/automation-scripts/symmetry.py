"""Compare three symmetry presets with a looping gesture that varies in depth.

The default video shows Snowflake, Plait and CandyCane in one fixed shot.
Use --param preset=NAME to reuse an individual section.
"""

import math

from automation_sections import (apply_path, open_panel, prepare_brush, prepare_script,
                                 reveal_result, select_control, select_script, undo_gesture)


def looping_gesture():
    return [[0.10 + 0.095 * math.sin(2 * math.pi * t),
             -0.24 + 0.055 * math.sin(4 * math.pi * t),
             0.48 + 0.025 * math.sin(3 * math.pi * t)]
            for t in (i / 128 for i in range(129))]


def demonstrate_preset(preset, *, enable=False, final=False):
    steps = select_script(preset)
    if enable:
        steps += select_control({"command": "ScriptedSymmetryCommand"}, present=False)
    steps += [
        {"op": "assert", "symmetry": "ScriptedSymmetryMode"},
        *apply_path(looping_gesture(), label=f"Draw a 3D figure eight with {preset}",
                    seconds=5.2, attachment="tip", rotation=(25, -15, 0),
                    padding=0.04, lower_wand=False, inspect_pause=0.2),
        {"op": "assert", "visibleStrokeCountDelta": {"Snowflake": 12, "Plait": 5, "CandyCane": 15}[preset]},
    ]
    if final:
        steps += reveal_result(seconds=2.5)
    else:
        steps += [
            {"op": "move", "points": [[0.24, -0.24, 0.48]], "attachment": "tip", "seconds": 0.6},
            {"op": "wait", "seconds": 1.4},
            *undo_gesture(), {"op": "assert", "visibleStrokeCountDelta": 0},
        ]
    return steps


def build_plan(args):
    single = args.parameters.get("preset")
    presets = [single] if single else ["Snowflake", "Plait", "CandyCane"]
    if any(p not in ("Snowflake", "Plait", "CandyCane") for p in presets):
        raise ValueError("preset must be Snowflake, Plait or CandyCane")
    steps = open_panel("Scripts", via_more=False, park=True)
    for i, preset in enumerate(presets):
        steps += demonstrate_preset(preset, enable=i == 0, final=i == len(presets) - 1)
    return {
        "version": 1,
        "name": "Compare symmetry presets with a 3D figure eight",
        "prepare": [{"op": "drawing"}, prepare_brush(size=0.035),
                    prepare_script(presets[0]), {"op": "color", "colorRgb": [0.12, 0.7, 0.95]}],
        "subjects": [{"name": "Symmetry comparison results",
                      "min": [-0.05, -0.39, 0.38], "max": [0.25, -0.09, 0.58]}],
        "steps": steps,
    }
