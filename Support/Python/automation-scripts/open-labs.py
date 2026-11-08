"""From the default layout, open More panels, choose Labs and reveal the floating panel."""

from automation_sections import open_panel, reveal_result


def build_plan(args):
    return {
        "version": 1,
        "name": "Open the Labs panel",
        "steps": [
            *open_panel("Labs"),
            *reveal_result(seconds=2.0),
        ],
    }
