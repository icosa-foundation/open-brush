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
    gesture = [[0.084, -0.238, 0.43], [0.172, -0.270, 0.43],
               [0.228, -0.126, 0.43], [0.124, -0.102, 0.43]]
    return {
        "version": 1,
        "name": f"Draw with the {preset} symmetry preset",
        "prepare": [{"op": "drawing"}, prepare_brush(size=0.035),
                    prepare_script(preset), {"op": "color", "colorRgb": [0.12, 0.7, 0.95]}],
        "subjects": [{"name": "Symmetry result", "min": [-0.092, -0.414, 0.326],
                      "max": [0.276, -0.054, 0.534]}],
        "steps": [
            *open_panel("Scripts", via_more=False,
                        park={"position": (0, 0.12, 0.38), "rotation": (25, 0, 0)}),
            *select_script(preset),
            *select_control({"command": "ScriptedSymmetryCommand"}, present=False),
            {"op": "assert", "symmetry": "ScriptedSymmetryMode"},
            *apply_path(gesture, label=f"Draw with {preset}", seconds=3.8, curve="bezier",
                        attachment="tip", rotation=(25, -15, 0), padding=0.05, lower_wand=False),
            {"op": "assert", "strokeCountDelta": {"Snowflake": 12, "Plait": 5, "CandyCane": 15}[preset]},
            *reveal_result(seconds=3.0),
        ],
    }
