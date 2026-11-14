"""Resize a live Circle preview, then release the trigger to commit one stroke."""

from automation_sections import (apply_path, open_panel, prepare_brush, prepare_script,
                                 reveal_result, select_script, select_tool)


def build_plan(args):
    center = [0.10, -0.24, 0.44]
    gesture = [center, [0.16, -0.24, 0.44], [0.22, -0.24, 0.44],
               [0.17, -0.24, 0.44], [0.21, -0.24, 0.44]]
    return {
        "version": 1,
        "name": "Adjust a live Tool Script preview and release to commit",
        "prepare": [{"op": "drawing"}, prepare_brush(args.brush or "Icing", size=0.007),
                    prepare_script("Circle", category="ToolScript"),
                    {"op": "color", "colorRgb": [0.12, 0.7, 0.95]}],
        "subjects": [{"name": "Circle preview and committed stroke",
                      "min": [-0.035, -0.375, 0.375], "max": [0.235, -0.105, 0.505]}],
        "steps": [
            *open_panel("Scripts", via_more=False, park=True),
            *select_script("Circle", category="ToolScript"),
            *select_tool("ScriptedTool"),
            *apply_path(gesture, label="Grow and shrink the live preview", attachment="tip",
                        rotation=(15, -15, 0), seconds=5.4, lower_wand=False,
                        before_release=[{"op": "assert", "strokeCountDelta": 0},
                                        {"op": "wait", "seconds": 1.0}]),
            {"op": "assert", "tool": "ScriptedTool", "strokeCountDelta": 1},
            *reveal_result(seconds=2.5),
        ],
    }
