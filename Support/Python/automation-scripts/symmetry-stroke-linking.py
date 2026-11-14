"""Change a linked mirror's settings, then move its widget to update existing strokes."""

from automation_sections import apply_path, prepare_brush, sample_cubic


POPUP = ("SketchControls/AdvancedToolsPanel(Clone)_Advanced/Mesh/"
         "PopupWindow_MultimirrorOptions(Clone)/")


def mirror_options():
    return [{"op": "present", "target": {"command": "MultiMirror"}, "seconds": 1.0},
            {"op": "click", "target": {"command": "MultiMirror"}, "longPress": True,
             "label": "Open symmetry options", "seconds": 0.7}]


def click_control(selector, seconds=0.65):
    return {"op": "click", "target": selector, "seconds": seconds}


def build_plan(args):
    center = [0.08, -0.23, 0.44]
    wing = sample_cubic(((0.115, -0.32, 0.44), (0.29, -0.05, 0.44),
                         (0.28, -0.38, 0.44), (0.115, -0.32, 0.44)))
    second = sample_cubic(((0.27, -0.29, 0.45), (0.34, -0.26, 0.45),
                           (0.34, -0.16, 0.45), (0.27, -0.13, 0.45)))
    contact = {"attachment": "tip", "rotation": [25, -15, 0]}
    return {
        "version": 1,
        "name": "Change a linked mirror and move its existing strokes",
        "output": {"width": 1920, "height": 1080},
        "framing": {"enabled": False, "rotation": [20, 0, 0]},
        "panelPosition": [-0.24, -0.04, 0.40],
        "prepare": [
            {"op": "drawing"},
            prepare_brush(args.brush or "Icing", size=0.009),
            {"op": "color", "colorRgb": [0.12, 0.7, 0.95]},
            {"op": "tool", "tool": "RepaintTool", "radiusMeters": 0.025},
            {"op": "symmetry", "symmetry": "SinglePlane", "points": [center],
             "rotation": [90, 0, 0]},
        ],
        "steps": [
            *mirror_options(),
            click_control({"command": "ToggleLinkedMirror"}),
            *apply_path(wing, label="Draw linked mirror copies", seconds=3.2,
                        rotation=(25, -15, 0), approach_seconds=0.8,
                        contact_pause=0.15, inspect_pause=0.3, lower_wand=False),
            {"op": "assert", "visibleStrokeCountDelta": 2},
            click_control({"path": POPUP + "Button Show Point Symmetry"}),
            {"op": "wait", "label": "Existing strokes follow the point symmetry setting", "seconds": 0.6},
            {"op": "assert", "symmetry": "MultiMirror", "visibleStrokeCountDelta": 6},
            click_control({"path": POPUP + "Point Symmetry Controls/Slider PointSymmetryOrder/Cap/Increment"}),
            {"op": "assert", "visibleStrokeCountDelta": 7},
            click_control({"path": POPUP + "Point Symmetry Controls/Slider PointSymmetryOrder/Cap/Increment"}, seconds=0.15),
            {"op": "wait", "label": "Increase point symmetry order from six to eight", "seconds": 0.75},
            {"op": "assert", "visibleStrokeCountDelta": 8},
            click_control({"path": POPUP + "Button_ClosePopup"}, seconds=0.55),
            {"op": "move", "points": [center], "seconds": 0.8, **contact},
            {"op": "wait", "seconds": 0.4},
            {"op": "input", "buttons": ["Grip"]},
            {"op": "wait", "seconds": 0.15},
            {"op": "move", "label": "Move and turn the linked mirror widget",
             "points": [center, [0.14, -0.18, 0.44]], "seconds": 2.4,
             "endRotation": [25, -15, 25], **contact},
            {"op": "input", "buttons": []},
            *apply_path(second, label="Draw another stroke with the mirror left in place",
                        seconds=2.4, rotation=(25, -15, 0), approach_seconds=0.8,
                        contact_pause=0.15, inspect_pause=0.3, lower_wand=False),
            {"op": "assert", "visibleStrokeCountDelta": 16},
            {"op": "present", "target": {"path": "SketchControls/ColorPickerPanel(Clone)_Advanced/Mesh/CustomColorPalette/Palette1"},
             "seconds": 0.8},
            {"op": "pickColor", "colorRgb": [0.95, 0.3, 0.12], "seconds": 0.65,
             "label": "Choose a contrasting warm color"},
            {"op": "present", "target": {"path": "SketchControls/AdvancedToolsPanel(Clone)_Advanced/Mesh/Button_Repaint"},
             "seconds": 0.8},
            {"op": "click", "target": {"path": "SketchControls/AdvancedToolsPanel(Clone)_Advanced/Mesh/Button_Repaint"},
             "seconds": 0.65, "tool": "RepaintTool"},
            *apply_path(second[46:51], label="Recolor one stroke and its linked copies",
                        tool="RepaintTool", attachment="tool", seconds=0.5,
                        approach_seconds=0.8, contact_pause=0.15, inspect_pause=0.4,
                        lower_wand=False),
            {"op": "assert", "visibleStrokeCountDelta": 16},
            {"op": "rest", "seconds": 0.8},
            {"op": "rest", "hand": "wand", "seconds": 0.8},
            {"op": "wait", "seconds": 2.0},
        ],
    }
