#!/usr/bin/env python3
"""Run bloom comparisons on one installed APK. Python standard library only."""
import argparse
import csv
import itertools
import json
import math
import random
import re
import shutil
import subprocess
import time
import urllib.parse
import urllib.request
from pathlib import Path

PROFILES = [
    "off-ldr", "encoded-off", "encoded-both", "encoded-alternate", "encoded-reproject", "native-ldr",
    "off-hdr32", "native-hdr32", "off-hdr64", "native-hdr64",
    "off-hdr32-opaque", "native-hdr32-opaque",
]


def make_plan(args):
    rng = random.Random(args.seed)
    cases = []
    for profile in args.profiles:
        settings = itertools.product(args.levels, args.downsample) if profile in ("encoded-both", "encoded-alternate", "encoded-reproject") else [(3, 2)]
        for levels, downsample in settings:
            for fixture in args.fixtures:
                scenes = args.scenes if fixture == "scene" and args.scenes else [None]
                for scene in scenes:
                    case = dict(profile=profile, levels=levels, downsample=downsample, fixture=fixture)
                    if scene:
                        case["scene"] = scene["name"]
                    cases.append(case)
    plan = []
    for repeat in range(args.repeats):
        order = cases.copy()
        rng.shuffle(order)
        for case in order:
            plan.append(dict(case, repeat=repeat + 1, token=f"run{len(plan)+1:04d}"))
    return plan


class Device:
    def __init__(self, args):
        self.args = args
        self.adb = [args.adb]
        if args.serial:
            self.adb += ["-s", args.serial]
        self.url = f"http://127.0.0.1:{args.port}/api/v1"
        self.launch_id = None

    def run(self, *arguments, binary=False, timeout=60):
        result = subprocess.run(self.adb + list(arguments), check=True, capture_output=True, timeout=timeout)
        return result.stdout if binary else result.stdout.decode("utf-8", errors="replace")

    def request(self, key, value=""):
        url = f"{self.url}?{key}={urllib.parse.quote(str(value), safe='')}"
        with urllib.request.urlopen(url, timeout=10) as response:
            return response.read().decode("utf-8")

    def status(self):
        return json.loads(self.request("query.bloom.benchmark.status"))

    def wait(self, predicate, timeout=120):
        deadline = time.monotonic() + timeout
        last = None
        while time.monotonic() < deadline:
            try:
                last = self.status()
                if predicate(last):
                    return last
            except (OSError, ValueError):
                pass
            time.sleep(1)
        details = {key: last.get(key) for key in ("state", "ready", "token", "error", "active")} if last else None
        raise TimeoutError(f"API did not reach expected state. Last status: {details}")

    def command(self, command, arguments, token, timeout=30):
        self.request(command, ",".join(str(x).lower() if isinstance(x, bool) else str(x) for x in arguments))
        status = self.wait(lambda s: s.get("token") == token, timeout)
        if status.get("error"):
            raise RuntimeError(status["error"])
        return status

    def restart(self, expected_profile=None):
        # Android can acknowledge a preferences save before its disk write settles.
        # Do not kill the process immediately after the configure acknowledgement.
        time.sleep(2)
        self.run("shell", "am", "force-stop", self.args.package)
        if self.args.activity:
            self.run("shell", "am", "start", "-n", self.args.activity)
        else:
            self.run("shell", "monkey", "-p", self.args.package,
                     "-c", "android.intent.category.LAUNCHER", "1")
        return self.wait(lambda s: s.get("state") == "idle" and s.get("ready") and
                         s.get("token") == "" and
                         (expected_profile is None or s.get("active", {}).get("Profile") == expected_profile),
                         self.args.launch_timeout)

    def load_scene(self, scene):
        self.request(scene["load"], scene["value"])
        # A command acknowledgement is not a completed download or sketch load.
        # Require an observed loading state, then several stable ready observations.
        deadline = time.monotonic() + self.args.scene_timeout
        saw_loading = False
        ready_since = None
        while time.monotonic() < deadline:
            status = self.status()
            if not status.get("ready"):
                saw_loading = True
                ready_since = None
            elif saw_loading:
                ready_since = ready_since or time.monotonic()
                if time.monotonic() - ready_since >= 3:
                    break
            time.sleep(0.25)
        else:
            raise TimeoutError(f"Scene {scene['name']} did not complete an observed load; refusing to benchmark an unverified scene.")
        for command in scene.get("view", []):
            self.request(command["key"], command["value"])
        time.sleep(self.args.scene_settle)
        return self.wait(lambda s: s.get("ready"), self.args.scene_timeout)


def write_json(path, data):
    path.write_text(json.dumps(data, indent=2, allow_nan=False), encoding="utf-8")


def capture_text(device, path, *arguments):
    try:
        path.write_text(device.run(*arguments), encoding="utf-8")
    except (subprocess.SubprocessError, OSError) as error:
        path.write_text(f"Unavailable: {error}", encoding="utf-8")


def validate_result(result, case):
    metadata = result["metadata"]
    active = metadata["active"]
    problems = []
    if active["Profile"] != case["profile"]:
        problems.append("profile mismatch")
    expected_hdr = "hdr" in case["profile"]
    if metadata.get("sessionHdr") != expected_hdr:
        problems.append("HDR mismatch")
    if case["profile"].endswith("-opaque") and not any(
            target.get("format") == "B10G11R11_UFloatPack32" for target in metadata.get("targets", [])):
        problems.append("opaque HDR32 format unavailable")
    if metadata.get("automaticQuality"):
        problems.append("automatic quality enabled")
    encoded = case["profile"].startswith("encoded")
    if bool(metadata.get("encoded")) != encoded:
        problems.append("encoded backend mismatch")
    if case["profile"] in ("encoded-alternate", "encoded-reproject") and metadata.get("reuse") != "alternating array slices":
        problems.append("alternate-eye layout unavailable")
    if not metadata.get("targets"):
        problems.append("camera target diagnostics unavailable")
    if result.get("gpuSampleCoverage", 0) < 0.9:
        problems.append("insufficient app GPU timing coverage")
    if metadata.get("sessionMsaa") != case["msaa"]:
        problems.append("MSAA request fell back")
    return problems



def stereo_halo_check(path, metadata):
    # Optional Pillow analysis happens after sampling and never modifies the image.
    # Synthetic fixtures have white cores and saturated halos on a black background.
    if metadata.get("fixture") == "scene":
        return {"available": False, "reason": "scene fixture cannot assume equal eye content"}
    if not any(target.get("slices") == 2 for target in metadata.get("targets", [])):
        return {"available": False, "reason": "not a two-eye target"}
    try:
        from PIL import Image, ImageChops
    except ImportError:
        return {"available": False, "reason": "install Pillow or inspect screenshots manually"}
    with Image.open(path) as image:
        counts = []
        for eye in range(2):
            crop = image.crop((eye * image.width // 2, 0,
                               (eye + 1) * image.width // 2, image.height)).convert("HSV")
            _, saturation, value = crop.split()
            mask = ImageChops.multiply(saturation.point(lambda x: 255 if x > 96 else 0),
                                       value.point(lambda x: 255 if x > 32 else 0))
            counts.append(sum(mask.histogram()[1:]))
    ratio = min(counts) / max(counts) if max(counts) else None
    return {"available": True, "coloredHaloPixels": counts, "eyeBalance": ratio,
            "missingEye": max(counts) > 50 and ratio < 0.15}


def summary_row(result, case, problems):
    samples = result.get("samples", [])
    drops = [x["droppedFrames"] for x in samples if x.get("droppedFrames") is not None]
    hz = [x["refreshHz"] for x in samples if x.get("refreshHz")]
    gpu = result.get("appGpu") or {}
    wall = result.get("wallFrame") or {}
    return dict(**case, gpu_mean_ms=gpu.get("mean"), gpu_median_ms=gpu.get("median"),
                gpu_p95_ms=gpu.get("p95"), wall_p95_ms=wall.get("p95"),
                gpu_coverage=result.get("gpuSampleCoverage"),
                dropped_frames=max(0, drops[-1] - drops[0]) if len(drops) > 1 else None,
                refresh_hz=sum(hz)/len(hz) if hz else None,
                targets=json.dumps(result["metadata"].get("targets"), separators=(",", ":")),
                issues="; ".join(problems))


def run(args, plan):
    if not re.fullmatch(r"[A-Za-z0-9_.]+", args.package):
        raise ValueError("Package must be an Android package name.")
    out = Path(args.output)
    out.mkdir(parents=True, exist_ok=False)
    write_json(out / "plan.json", dict(arguments=vars(args), cases=plan))
    device = Device(args)
    forwards = device.run("forward", "--list").splitlines()
    local = f"tcp:{args.port}"
    if any(local in line.split() for line in forwards):
        raise RuntimeError(f"ADB forward {local} already exists; choose a different --port.")
    device.run("forward", "--no-rebind", local, "tcp:40074")
    original = None
    rows = []
    try:
        if args.apk:
            device.run("install", "-r", args.apk, timeout=300)
        # Ensure there is an initial running application to receive the configure command.
        original = device.restart()
        write_json(out / "original-status.json", original)
        capture_text(device, out / "device.txt", "shell", "getprop")
        capture_text(device, out / "package.txt", "shell", "dumpsys", "package", args.package)
        for number, case in enumerate(plan, 1):
            token = case["token"]
            directory = out / token
            directory.mkdir()
            configure_token = f"configure{token}"
            device.command("bloom.benchmark.configure",
                           [case["profile"], args.msaa, args.eye_scale, configure_token], configure_token)
            status = device.restart(case["profile"])
            if case.get("scene"):
                scene = next(item for item in args.scenes if item["name"] == case["scene"])
                status = device.load_scene(scene)
                write_json(directory / "scene.json", scene)
            if status.get("qualityLevels", 0) <= args.quality:
                raise RuntimeError(f"Quality {args.quality} is not available.")
            tune_token = f"tune{token}"
            threshold = args.threshold
            if threshold is None:
                threshold = 1.05 if "hdr" in case["profile"] else 0.5 if case["profile"] == "native-ldr" else 0
            intensity = args.native_intensity
            if intensity is None:
                intensity = 0.1 if "hdr" in case["profile"] else 1
            device.command("bloom.benchmark.tune",
                           [case["levels"], case["downsample"], args.amount, threshold,
                            args.native_iterations, not args.native_half, args.native_hq,
                            args.native_scatter, intensity, tune_token], tune_token)
            capture_text(device, directory / "thermal-before.txt", "shell", "dumpsys", "thermalservice")
            trace = None
            try:
                if args.trace_command:
                    substitutions = dict(app=args.package, serial=args.serial or "",
                                         duration=str(int((args.warmup+args.duration+5)*1000)),
                                         output=str((directory / "trace.pftrace").resolve()))
                    command = [item.format(**substitutions) for item in json.loads(args.trace_command)]
                    trace_log = (directory / "trace.log").open("wb")
                    trace = subprocess.Popen(command, stdout=trace_log, stderr=subprocess.STDOUT)
                device.command("bloom.benchmark.start",
                               [token, args.warmup, args.duration, case["fixture"], args.quality], token)
                status = device.wait(lambda s: s.get("token") == token and s.get("state") in ("complete", "error"),
                                     args.warmup + args.duration + 90)
                if status.get("error") or status.get("state") == "error":
                    raise RuntimeError(status.get("error", "capture failed"))
                result = json.loads(device.request("query.bloom.benchmark.result"))
                if result["metadata"]["token"] != token:
                    raise RuntimeError("Stale benchmark result.")
                write_json(directory / "result.json", result)
                capture_text(device, directory / "thermal-after.txt", "shell", "dumpsys", "thermalservice")
                if not args.no_screenshots:
                    (directory / "screenshot.png").write_bytes(device.run("exec-out", "screencap", "-p", binary=True))
                pid = device.run("shell", "pidof", args.package).strip()
                if pid.isdigit():
                    capture_text(device, directory / "logcat.txt", "logcat", "-d", f"--pid={pid}", "-v", "threadtime")
                measured_case = dict(case, msaa=args.msaa, eye_scale=args.eye_scale)
                problems = validate_result(result, measured_case)
                if not args.no_screenshots:
                    stereo = stereo_halo_check(directory / "screenshot.png", result["metadata"])
                    write_json(directory / "stereo.json", stereo)
                    if stereo.get("missingEye"):
                        problems.append("stereo glow missing from one eye")
                rows.append(summary_row(result, measured_case, problems))
                write_json(directory / "validation.json", dict(issues=problems))
                with (out / "summary.csv").open("w", newline="", encoding="utf-8") as handle:
                    writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
                    writer.writeheader()
                    writer.writerows(rows)
                print(f"{number}/{len(plan)} {token} {case['profile']} {case.get('scene', case['fixture'])}: "
                      f"GPU mean={rows[-1]['gpu_mean_ms']} ms"
                      f"{'; '+', '.join(problems) if problems else ''}", flush=True)
                stop_token = f"stop{token}"
                device.command("bloom.benchmark.stop", [stop_token], stop_token)
            finally:
                if trace is not None:
                    try:
                        trace.wait(timeout=20)
                    except subprocess.TimeoutExpired:
                        trace.terminate()
                        trace.wait(timeout=10)
                    trace_log.close()
            time.sleep(args.cooldown)
    finally:
        # Restore only our isolated preferences, never the Open Brush config or app data.
        try:
            if original:
                stop_token = "restorestop"
                device.command("bloom.benchmark.stop", [stop_token], stop_token)
                pending = original.get("pending") or original["active"]
                token = "restoreprofile"
                device.command("bloom.benchmark.configure",
                               [pending["Profile"], pending["Msaa"], pending["EyeScale"], token], token)
                device.restart(pending["Profile"])
        except Exception as error:
            (out / "RESTORE-FAILED.txt").write_text(
                f"{error}\nRun bloom.benchmark.configure=default,4,1,restore and restart to clear the override.\n",
                encoding="utf-8")
            print(f"RESTORE FAILED: {error}", flush=True)
        finally:
            device.run("forward", "--remove", local)


def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--package", default="org.icosa.openbrush")
    p.add_argument("--activity", help="Optional package/activity component")
    p.add_argument("--adb", default=shutil.which("adb") or "adb")
    p.add_argument("--serial")
    p.add_argument("--apk", help="Install this APK once before running; otherwise use the installed app")
    p.add_argument("--port", type=int, default=40075, help="Unused local port forwarding to device port 40074")
    p.add_argument("--profiles", nargs="+", choices=PROFILES, default=PROFILES)
    p.add_argument("--fixtures", nargs="+", choices=["scene", "sparse", "dense", "white"], default=["sparse", "dense", "white"])
    p.add_argument("--scenes-json", type=str, help="JSON array of named scenes: name, load (load.named/user/featured/liked/drive), value, optional view [{key,value}]. Reloaded after every profile restart.")
    p.add_argument("--scene-timeout", type=float, default=300)
    p.add_argument("--scene-settle", type=float, default=10, help="Seconds to settle after the sketch finishes loading")
    p.add_argument("--levels", nargs="+", type=int, choices=range(1, 6), default=[3])
    p.add_argument("--downsample", nargs="+", type=int, choices=[2, 3, 4], default=[2])
    p.add_argument("--repeats", type=int, default=3)
    p.add_argument("--seed", type=int, default=7319)
    p.add_argument("--warmup", type=float, default=20)
    p.add_argument("--duration", type=float, default=30)
    p.add_argument("--cooldown", type=float, default=15)
    p.add_argument("--launch-timeout", type=float, default=180)
    p.add_argument("--quality", type=int, default=0)
    p.add_argument("--msaa", type=int, choices=[1, 2, 4, 8], default=4)
    p.add_argument("--eye-scale", type=float, default=1)
    p.add_argument("--amount", type=float, default=1)
    p.add_argument("--threshold", type=float)
    p.add_argument("--native-iterations", type=int, choices=range(1, 11), default=2)
    p.add_argument("--native-half", action="store_true")
    p.add_argument("--native-hq", action="store_true")
    p.add_argument("--native-scatter", type=float, default=0.35)
    p.add_argument("--native-intensity", type=float)
    p.add_argument("--trace-command", help="JSON argument array for an optional external trace tool; placeholders: app, serial, duration(ms), output")
    p.add_argument("--no-screenshots", action="store_true")
    p.add_argument("--output", default=f"bloom-results-{time.strftime('%Y%m%d-%H%M%S')}")
    p.add_argument("--dry-run", action="store_true")
    return p


def main():
    p = parser()
    args = p.parse_args()
    args.scenes = []
    if args.scenes_json:
        args.scenes = json.loads(Path(args.scenes_json).read_text(encoding="utf-8-sig"))
        if not isinstance(args.scenes, list) or not args.scenes:
            p.error("scenes-json must contain a nonempty array")
        names = set()
        for scene in args.scenes:
            if not isinstance(scene, dict) or not isinstance(scene.get("name"), str) or not scene["name"] or scene["name"] in names:
                p.error("each scene requires a unique nonempty name")
            names.add(scene["name"])
            if scene.get("load") not in ("load.named", "load.user", "load.featured", "load.liked", "load.drive") or "value" not in scene:
                p.error("each scene requires a supported load endpoint and value")
            for command in scene.get("view", []):
                if command.get("key") not in ("scene.scale.to", "user.move.to", "user.turn.y") or "value" not in command:
                    p.error("scene view supports scene.scale.to, user.move.to, user.turn.y")
        args.fixtures = ["scene"]
    if args.repeats < 1 or args.quality < 0:
        p.error("repeats must be positive and quality non-negative")
    for name, low, high in [("warmup", 0, 300), ("duration", 1, 300), ("cooldown", 0, 3600),
                           ("eye_scale", 0.25, 2), ("amount", 0, 1), ("native_scatter", 0, 1),
                           ("scene_timeout", 5, 1800), ("scene_settle", 0, 300)]:
        value = getattr(args, name)
        if not math.isfinite(value) or not low <= value <= high:
            p.error(f"{name} must be finite in {low}..{high}")
    plan = make_plan(args)
    if args.dry_run:
        print(json.dumps(dict(runs=len(plan),
                             estimatedMinutes=round(len(plan)*(args.warmup+args.duration+args.cooldown)/60, 1),
                             firstCases=plan[:5]), indent=2))
    else:
        run(args, plan)


if __name__ == "__main__":
    main()
