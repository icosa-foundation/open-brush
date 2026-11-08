"""Choose a symmetry preset through the Scripts panel and draw one repeated gesture.

Use --param preset=Snowflake (default), Plait or CandyCane. Each is a separate
shot, so preset comparisons can be cut together without camera movement.
"""

from automation_sections import (apply_path, open_panel, prepare_brush, prepare_script,
                                 reveal_result, select_control, select_script)


def build_plan(args):
    preset = args.parameters.get("preset", "Snowflake")
    if preset not in ("Snowflake", "Plait", "CandyCane"):
        raise ValueError("preset must be Snowflake, Plait or CandyCane")
    gesture = [[0.08, -0.24, 0.43], [0.19, -0.28, 0.43],
               [0.26, -0.10, 0.43], [0.13, -0.07, 0.43]]
    return {
        "version": 1,
        "name": f"Draw with the {preset} symmetry preset",
        "prepare": [{"op": "drawing"}, prepare_brush(size=0.035),
                    prepare_script(preset), {"op": "color", "colorRgb": [0.12, 0.7, 0.95]}],
        "subjects": [{"name": "Symmetry result", "min": [-0.14, -0.46, 0.30],
                      "max": [0.32, -0.01, 0.56]}],
        "steps": [
            *open_panel("Scripts", via_more=False),
            *select_script(preset),
            *select_control({"command": "ScriptedSymmetryCommand"}, present=False),
            {"op": "assert", "symmetry": "ScriptedSymmetryMode"},
            *apply_path(gesture, label=f"Draw with {preset}", seconds=3.8, curve="bezier",
                        attachment="tip", rotation=(25, -15, 0), padding=0.05),
            {"op": "assert", "strokeCountDelta": {"Snowflake": 12, "Plait": 5, "CandyCane": 15}[preset]},
            *reveal_result(seconds=3.0),
        ],
    }
