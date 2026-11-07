#!/usr/bin/env python3
"""Record a panel click and stroke using Open Brush's hardware-free Virtual Input mode.

Enter Play mode with Open Brush > Procedural Automation > Virtual Input enabled,
or launch a player with --virtualInput. Keep the Game/player view rendering.
Requires only Python's standard library; output is a PNG sequence and recording.json.
Each rehearsal frames its declared subjects once and holds the camera still.
"""

import argparse
import datetime
import json
import shlex
import sys
import time
import urllib.error
import urllib.parse
import urllib.request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:40074")
    parser.add_argument("--name", default=f"tutorial-{datetime.datetime.now():%Y%m%d-%H%M%S}")
    parser.add_argument("--fps", type=int, default=30)
    parser.add_argument("--brush", default="", help="Optional visible brush durable name")
    parser.add_argument("--head", type=float, nargs=6, metavar=("X", "Y", "Z", "RX", "RY", "RZ"),
                        help="Virtual body/head reference in world metres, then rotation in degrees")
    parser.add_argument("--timeout", type=float, default=600)
    args = parser.parse_args()

    def command(name, value=""):
        query = urllib.parse.urlencode({name: value})
        with urllib.request.urlopen(f"{args.url.rstrip('/')}/api/v1?{query}", timeout=10) as response:
            return response.read().decode("utf-8")

    def status():
        return json.loads(command("query.automation"))

    initial = status()
    if initial.get("Mode") != "VirtualInput":
        raise RuntimeError("Open Brush must be running in Virtual Input mode (--virtualInput).")
    if initial.get("Recording") or initial.get("Phase") != "Stopped":
        raise RuntimeError("Another rehearsal or recording is active; stop it before starting this script.")
    if not 1 <= args.fps <= 120:
        raise RuntimeError("FPS must be between 1 and 120.")
    if args.head:
        command("automation.head", ",".join(f"{value:g}" for value in args.head))
    command("automation.record", f"{args.name},{args.fps},{args.brush}")

    saw_capture = False
    last_phase = None
    deadline = time.monotonic() + args.timeout
    try:
        while time.monotonic() < deadline:
            current = status()
            if current.get("Error"):
                raise RuntimeError(current["Error"])
            phase = current.get("Phase")
            if phase != last_phase:
                print(f"{phase}: {current.get('Frames', 0)} frames", flush=True)
                last_phase = phase
            if current.get("Recording"):
                saw_capture = True
            output = current.get("Output", "")
            if output != initial.get("Output") and current.get("Frames", 0) > 0:
                saw_capture = True
            if saw_capture and not current.get("Recording") and phase == "Stopped":
                print(f"Saved {current['Frames']} frames to {output}")
                pattern = f"{output}/frame_%06d.png"
                movie = f"{output}.mp4"
                print(f"Encode with: ffmpeg -framerate {args.fps} -i {shlex.quote(pattern)} "
                      f"-c:v libx264 -pix_fmt yuv420p -vf 'pad=ceil(iw/2)*2:ceil(ih/2)*2' {shlex.quote(movie)}")
                return
            time.sleep(0.5)
        raise TimeoutError("Timed out waiting for the rehearsal recording.")
    except (KeyboardInterrupt, RuntimeError, TimeoutError):
        command("automation.stop")
        raise


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, ValueError) as error:
        print(f"Recording failed: {error}", file=sys.stderr)
        sys.exit(1)
