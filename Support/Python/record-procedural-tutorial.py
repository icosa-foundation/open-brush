#!/usr/bin/env python3
"""Run and optionally record an external Open Brush Virtual Input script.

Use --script with a JSON plan or a Python file exporting build_plan(args).
The --scenario choices load external example scripts through the same API.
Requires Python's standard library and an Open Brush Virtual Input session.
"""

import argparse
import importlib.util
import json
import math
from pathlib import Path
import shutil
import subprocess
import sys
import time
import uuid

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


def encode_video(result, destination, fps):
    frames = Path(result["Output"])
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(f".{destination.stem}-{uuid.uuid4().hex}.mp4")
    try:
        subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-n",
                        "-framerate", str(fps), "-i", str(frames / "frame_%06d.png"),
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p",
                        "-movflags", "+faststart", str(temporary)], check=True)
        count = subprocess.check_output(["ffprobe", "-v", "error", "-select_streams", "v:0",
                                         "-show_entries", "stream=nb_frames", "-of",
                                         "default=noprint_wrappers=1:nokey=1", str(temporary)], text=True)
        if int(count.strip()) != result["Frames"] or result["Frames"] <= 0:
            raise RuntimeError("Encoded video frame count does not match the recording.")
        temporary.replace(destination)
    finally:
        temporary.unlink(missing_ok=True)


def clean_capture_frames(client, capture_name, result):
    try:
        current = result or client.status()
        if not current.get("Output") or Path(current["Output"]).name != f"{capture_name}_frames":
            return
        deadline = time.monotonic() + 10
        while current.get("Recording") and time.monotonic() < deadline:
            time.sleep(0.2)
            current = client.status()
        output = current.get("Output")
        if output and not current.get("Recording"):
            frames = Path(output)
            if frames.name == f"{capture_name}_frames" and frames.is_dir():
                shutil.rmtree(frames)
        elif current.get("Recording"):
            print(f"Capture is still stopping; frames remain at {current.get('Output')}", file=sys.stderr)
    except (OSError, RuntimeError) as error:
        print(f"Could not clean this capture's frames: {error}", file=sys.stderr)


def main():
    examples = {path.stem: path for path in (Path(__file__).parent / "automation-scripts").glob("*.py")}
    examples["reshape"] = examples["reshape-grab"]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:40074")
    parser.add_argument("--script", type=Path, help="External JSON plan or Python build_plan(args) file")
    parser.add_argument("--scenario", choices=sorted(examples), default="draw", help="External example to run when --script is omitted")
    parser.add_argument("--name", help="Stable video filename without extension (default: script filename)")
    parser.add_argument("--output-dir", type=Path, help="Video directory (default: Open Brush Videos/Tutorials)")
    parser.add_argument("--frames-only", action="store_true", help="Keep PNG frames instead of encoding an MP4")
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
    name = args.name or path.stem
    if name in (".", "..") or any(char in name for char in '/\\:*?"<>|'):
        parser.error("--name must be a filename without path separators or reserved characters")
    capture_name = f"automation-{uuid.uuid4().hex}"
    plan = with_output(load_plan(path, args), args.width, args.height)
    if args.speed is not None:
        plan["speed"] = args.speed
    if args.rehearse:
        plan.pop("capture", None)
    else:
        plan = with_capture(plan, capture_name, args.fps)
    if args.write_plan:
        args.write_plan.write_text(json.dumps(plan, indent=2, allow_nan=False), encoding="utf-8")
        print(f"Wrote {args.write_plan}")
        return
    if not args.rehearse and not args.frames_only:
        for command in ("ffmpeg", "ffprobe"):
            if shutil.which(command) is None:
                raise RuntimeError(f"{command} is required to encode videos; install it or use --frames-only.")
    client = AutomationClient(args.url)
    if args.head:
        initial = client.status()
        if initial.get("Recording") or initial.get("Phase") != "Stopped":
            raise RuntimeError("Stop the active automation before moving the body reference.")
        client.command("automation.head", ",".join(f"{value:g}" for value in args.head))
    result = None
    try:
        result = client.run(plan, timeout=args.timeout)
        if "capture" in plan:
            frames = Path(result["Output"])
            if args.frames_only:
                print(f"Saved {result['Frames']} frames to {frames}")
            else:
                destination = (args.output_dir or frames.parent / "Tutorials") / f"{name}.mp4"
                encode_video(result, destination, plan["capture"]["fps"])
                print(f"Saved {destination}")
    finally:
        if "capture" in plan and not args.frames_only:
            # Only remove this invocation's unique frame directory, after capture has stopped.
            clean_capture_frames(client, capture_name, result)


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, ValueError, subprocess.CalledProcessError) as error:
        print(f"Automation failed: {error}", file=sys.stderr)
        sys.exit(1)
