#!/usr/bin/env python3
"""Run and optionally record an external Open Brush Virtual Input script.

Use --script with a JSON plan or a Python file exporting build_plan(args).
The --scenario choices load external example scripts through the same API.
Requires Python's standard library and an Open Brush Virtual Input session.
"""

import argparse
import datetime
import importlib.util
import json
import math
from pathlib import Path
import shlex
import sys

from automation_client import AutomationClient, with_capture, with_output


def load_plan(path, args):
    if path.suffix.lower() == ".json":
        return json.loads(path.read_text(encoding="utf-8"))
    if path.suffix.lower() != ".py":
        raise ValueError("--script must name a .json plan or a .py script.")
    spec = importlib.util.spec_from_file_location("open_brush_authored_script", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.build_plan(args)


def main():
    examples = {path.stem: path for path in (Path(__file__).parent / "automation-scripts").glob("*.py")}
    examples["reshape"] = examples["reshape-grab"]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:40074")
    parser.add_argument("--script", type=Path, help="External JSON plan or Python build_plan(args) file")
    parser.add_argument("--scenario", choices=sorted(examples), default="draw", help="External example to run when --script is omitted")
    parser.add_argument("--name", default=f"tutorial-{datetime.datetime.now():%Y%m%d-%H%M%S}")
    parser.add_argument("--fps", type=int, default=30)
    parser.add_argument("--width", type=int, help="Output width in pixels (default: plan output or 1440)")
    parser.add_argument("--height", type=int, help="Output height in pixels (default: plan output or 1080)")
    parser.add_argument("--speed", type=float, help="Script timing multiplier, 0.25 to 2 (default: plan speed or 1)")
    parser.add_argument("--brush", default="")
    parser.add_argument("--param", action="append", default=[], metavar="KEY=VALUE",
                        help="Named parameter passed to an external script (repeatable)")
    parser.add_argument("--existing-stroke", action="store_true", help="Use the latest stroke in examples that support it")
    parser.add_argument("--head", type=float, nargs=6, metavar=("X", "Y", "Z", "RX", "RY", "RZ"))
    parser.add_argument("--timeout", type=float, default=600)
    parser.add_argument("--rehearse", action="store_true", help="Run without saving frames")
    parser.add_argument("--write-plan", type=Path, help="Write generated JSON and exit without contacting Open Brush")
    args = parser.parse_args()
    if args.speed is not None and (not math.isfinite(args.speed) or not 0.25 <= args.speed <= 2):
        parser.error("--speed must be a finite number between 0.25 and 2")
    args.parameters = {}
    for parameter in args.param:
        key, separator, value = parameter.partition("=")
        if not separator or not key or key in args.parameters:
            parser.error("--param needs a unique, nonempty KEY followed by =VALUE")
        args.parameters[key] = value
    path = args.script or examples[args.scenario]
    plan = with_output(load_plan(path, args), args.width, args.height)
    if args.speed is not None:
        plan["speed"] = args.speed
    if args.rehearse:
        plan.pop("capture", None)
    else:
        plan = with_capture(plan, args.name, args.fps)
    if args.write_plan:
        args.write_plan.write_text(json.dumps(plan, indent=2, allow_nan=False), encoding="utf-8")
        print(f"Wrote {args.write_plan}")
        return
    client = AutomationClient(args.url)
    if args.head:
        initial = client.status()
        if initial.get("Recording") or initial.get("Phase") != "Stopped":
            raise RuntimeError("Stop the active automation before moving the body reference.")
        client.command("automation.head", ",".join(f"{value:g}" for value in args.head))
    result = client.run(plan, timeout=args.timeout)
    if "capture" in plan:
        output = result["Output"]
        print(f"Saved {result['Frames']} frames to {output}")
        fps = plan["capture"]["fps"]
        print(f"Encode with: ffmpeg -n -framerate {fps} -i {shlex.quote(f'{output}/frame_%06d.png')} "
              f"-c:v libx264 -pix_fmt yuv420p -vf 'pad=ceil(iw/2)*2:ceil(ih/2)*2' {shlex.quote(f'{output}.mp4')}")


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, ValueError) as error:
        print(f"Automation failed: {error}", file=sys.stderr)
        sys.exit(1)
