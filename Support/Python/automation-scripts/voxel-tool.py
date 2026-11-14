"""Draft voxel tutorial: build a wall, carve a doorway, and paint a pillar.

Recording is deferred. Material-edit steps and remaining rehearsal work are
specified in Support/Automation/VOXEL_TOOL_VIDEO.md.
"""

from automation_sections import apply_path, open_panel, prepare_brush, secondary_button


COLOR_PANEL = "SketchControls/ColorPickerPanel(Clone)_Advanced/Mesh/CustomColorPalette/Palette1"


def build_plan(args):
    wall = []
    for row in range(16):
        ends = (-0.025, 0.185) if row % 2 == 0 else (0.185, -0.025)
        wall += [[x, -0.32 + row * 0.01, 0.44] for x in ends]
    doorway = []
    for column in range(9):
        ends = (-0.325, -0.235) if column % 2 == 0 else (-0.235, -0.325)
        doorway += [[0.045 + column * 0.01, y, 0.44] for y in ends]
    paint = []
    for column in range(7):
        ends = (-0.32, -0.17) if column % 2 == 0 else (-0.17, -0.32)
        paint += [[-0.025 + column * 0.01, y, 0.44] for y in ends]
    opening = open_panel("Labs")
    for step in opening:
        if step["op"] in ("click", "present", "placePanel", "rest"):
            step["seconds"] = 0.8
    contact = {"attachment": "tool", "rotation": [25, -15, 0]}
    return {
        "version": 1,
        "name": "Build, carve and paint with the voxel tool",
        "output": {"width": 1920, "height": 1080},
        "framing": {"enabled": False, "rotation": [20, 0, 0]},
        "panelPosition": [-0.24, -0.04, 0.40],
        "prepare": [{"op": "drawing"}, prepare_brush(size=0.009),
                    {"op": "color", "colorRgb": [0.12, 0.7, 0.95]}],
        "steps": [
            *opening,
            {"op": "present", "target": {"tool": "VoxelTool"}},
            {"op": "click", "target": {"tool": "VoxelTool"}, "tool": "VoxelTool", "seconds": 0.65},
            *apply_path(wall, label="Add voxels to build a wall", seconds=13.0,
                        approach_seconds=0.8, contact_pause=0.15, inspect_pause=0.4,
                        lower_wand=False, **contact),
            *secondary_button(doorway[0], attachment="tool", rotation=(25, -15, 0),
                              before_seconds=0.3, after_seconds=0.45),
            *apply_path(doorway, label="Erase a doorway through the wall", seconds=3.8,
                        approach_seconds=0.15, contact_pause=0.15, inspect_pause=0.4,
                        lower_wand=False, **contact),
            *secondary_button(doorway[-1], attachment="tool", rotation=(25, -15, 0),
                              before_seconds=0.3, after_seconds=0.45),
            {"op": "present", "target": {"path": COLOR_PANEL}, "seconds": 0.8},
            {"op": "pickColor", "colorRgb": [0.95, 0.3, 0.12], "seconds": 0.65,
             "label": "Choose a warm paint color"},
            *apply_path(paint, label="Paint the left pillar", seconds=4.0,
                        approach_seconds=0.8, contact_pause=0.15, inspect_pause=0.4,
                        lower_wand=False, **contact),
            {"op": "rest", "seconds": 0.8},
            {"op": "rest", "hand": "wand", "seconds": 0.8},
            {"op": "wait", "seconds": 2.0},
        ],
    }
