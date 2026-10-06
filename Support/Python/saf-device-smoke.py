#!/usr/bin/env python3
"""Exercise an installed SAF build over ADB and the existing HTTP API.

Requires an unlocked device and a persistent grant to --shared-root. Start on a
disposable sketch: --exercise draws strokes and changes the current scene. It
never installs/uninstalls apps, clears app data, or deletes shared files.
Run through host PowerShell on Windows so ADB sees the host's device connection.
Evidence is local; all device outputs use a unique SAFTEST_ prefix.
"""

import argparse
import hashlib
import json
from pathlib import Path
import re
import shlex
import struct
import subprocess
import time
import urllib.parse
import urllib.request
import zipfile


class Runner:
    def __init__(self, args):
        self.args = args
        self.name = f"SAFTEST_{time.strftime('%Y%m%d_%H%M%S')}"
        self.output = args.output / self.name
        self.output.mkdir(parents=True, exist_ok=False)
        self.results = []
        self.pid = None
        self.findings = []

    def adb(self, *args, check=True):
        result = subprocess.run(
            [self.args.adb, "-s", self.args.serial, *args],
            capture_output=True,
            text=True,
            timeout=90,
            check=False,
        )
        if check and result.returncode:
            raise RuntimeError(f"ADB {args[0]} failed: {result.stderr.strip()}")
        return result.stdout.strip()

    def shell(self, command, check=True):
        return self.adb("shell", command, check=check)

    def http(self, path):
        # Never use environment HTTP proxies for the forwarded loopback endpoint.
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        with opener.open(
            f"http://127.0.0.1:{self.args.port}{path}", timeout=10
        ) as response:
            return response.read().decode()

    def command(self, name, value=""):
        # Include a query so void commands produce a response body on player builds.
        # Completion is still established by the resulting files, not this queue count.
        self.http(f"/api/v1?{name}={urllib.parse.quote(value, safe='')}&query.queue")

    def wait(self, description, probe, timeout=90):
        deadline = time.monotonic() + timeout
        last_error = None
        while time.monotonic() < deadline:
            try:
                result = probe()
                if result:
                    return result
            except (OSError, RuntimeError, ValueError) as exc:
                last_error = str(exc)
            time.sleep(1)
        raise RuntimeError(
            f"Timed out waiting for {description}: {last_error or 'not ready'}"
        )

    def record(self, name, details):
        self.results.append({"name": name, "status": "passed", "details": details})
        print(f"[SAF_DEVICE_TEST] PASS {name}", flush=True)

    def pull_stable(self, relative, previous_hash=None):
        remote = f"{self.args.shared_root.rstrip('/')}/{relative}"
        last = None

        def settled():
            nonlocal last
            value = self.shell(f"sha256sum {shlex.quote(remote)}", check=False).split()
            digest = (
                value[0] if value and re.fullmatch(r"[0-9a-f]{64}", value[0]) else None
            )
            stable = digest and digest != previous_hash and digest == last
            last = digest
            return digest if stable else None

        digest = self.wait(relative, settled)
        local = self.output / relative
        local.parent.mkdir(parents=True, exist_ok=True)
        self.adb("pull", remote, str(local))
        if hashlib.sha256(local.read_bytes()).hexdigest() != digest:
            raise RuntimeError(f"File changed while pulling {relative}")
        return local, digest

    def sketch(self, relative, previous_hash=None):
        path, digest = self.pull_stable(relative, previous_hash)
        with zipfile.ZipFile(path) as archive:
            bad = archive.testzip()
            if bad:
                raise RuntimeError(f"Invalid sketch archive member: {bad}")
            metadata = json.loads(archive.read("metadata.json"))
            strokes = archive.read("data.sketch")
            thumbnail = archive.read("thumbnail.png")
            if not thumbnail.startswith(b"\x89PNG\r\n\x1a\n"):
                raise RuntimeError("Sketch thumbnail is not a PNG")
        return digest, hashlib.sha256(strokes).hexdigest(), metadata

    def save_as(self, name):
        self.command("save.as", name)
        directory = f"{self.args.shared_root.rstrip('/')}/Sketches"

        def saved_name():
            names = self.shell(
                f"ls -1 {shlex.quote(directory)}", check=False
            ).splitlines()
            matches = [
                value
                for value in names
                if re.fullmatch(rf"{re.escape(name)}\d*\.tilt", value)
            ]
            return f"Sketches/{matches[0]}" if matches else None

        return self.wait("generated sketch filename", saved_name)

    def preflight(self):
        if self.args.shared_root.rstrip("/").rsplit("/", 1)[-1] != "Open Brush":
            raise RuntimeError("The SAF root folder must be named 'Open Brush'")
        if "mWakefulness=Awake" not in self.shell(
            "dumpsys power"
        ) or "mDreamingLockscreen=true" in self.shell("dumpsys window"):
            raise RuntimeError(
                "Device is asleep or locked; Unity cannot process test commands"
            )
        self.pid = self.shell(f"pidof {shlex.quote(self.args.package)}")
        if not re.fullmatch(r"\d+", self.pid):
            raise RuntimeError("Expected exactly one running app process")
        package = self.shell(f"dumpsys package {shlex.quote(self.args.package)}")
        (self.output / "package.txt").write_text(package, encoding="utf8")
        if (
            self.args.expected_version
            and f"versionName={self.args.expected_version}" not in package
        ):
            raise RuntimeError("Installed build does not match --expected-version")
        if "android.permission.MANAGE_EXTERNAL_STORAGE" in package:
            raise RuntimeError(
                "Installed app requests broad storage access; SAF build not established"
            )
        self.adb("forward", f"tcp:{self.args.port}", "tcp:40074")
        self.wait("command API", lambda: self.http("/api/v1?query.queue"))
        commands = self.wait("HTTP API", lambda: self.http("/help/commands?raw"))
        (self.output / "commands.txt").write_text(commands, encoding="utf8")
        for command in (
            "brush.draw",
            "save.as",
            "save.overwrite",
            "load.named",
            "capture.snapshot",
        ):
            if command not in commands.splitlines():
                raise RuntimeError(f"Missing API command {command}")
        self.record(
            "installed_build_and_api",
            {"pid": self.pid, "version": self.args.expected_version},
        )

    # Keep the ordered save/reload/restart assertions together as one device scenario.
    # pylint: disable=too-many-locals,too-many-statements
    def exercise(self):
        self.command("brush.draw", "0.5")
        # Commands are queued. Leave a frame interval before global save captures the scene.
        time.sleep(2)
        original = self.save_as(self.name)
        first_hash, first_strokes, _ = self.sketch(original)
        self.record(
            "shared_save_archive_and_thumbnail",
            {"path": original, "sha256": first_hash},
        )
        self.command("brush.turn.y", "90")
        self.command("brush.draw", "0.5")
        time.sleep(2)
        self.command("save.overwrite")
        second_hash, second_strokes, _ = self.sketch(original, first_hash)
        if first_strokes == second_strokes:
            raise RuntimeError("Overwrite did not persist the additional stroke")
        self.record("shared_overwrite", {"sha256": second_hash})
        self.command("new")
        time.sleep(2)
        self.command("load.named", Path(original).stem)
        # Reload is asynchronous; repeated saves establish completion by stroke equality.
        copy_name = f"{self.name}_reloaded"
        copy_path = f"Sketches/{copy_name}.tilt"

        def restored():
            time.sleep(3)
            nonlocal copy_path
            copy_path = self.save_as(copy_name)
            _, strokes, _ = self.sketch(copy_path)
            return strokes == second_strokes

        self.wait("reloaded stroke data", restored, timeout=120)
        self.record(
            "shared_reload_stroke_roundtrip",
            {"path": copy_path, "strokes_sha256": second_strokes},
        )
        self.command("capture.snapshot", f"{self.name}.png,320,240,1,false")
        png, digest = self.pull_stable(f"Snapshots/{self.name}.png")
        data = png.read_bytes()
        if not data.startswith(b"\x89PNG\r\n\x1a\n") or struct.unpack(
            ">II", data[16:24]
        ) != (320, 240):
            raise RuntimeError("Snapshot is not a 320x240 PNG")
        self.record("direct_shared_snapshot", {"sha256": digest})
        self.command("export.current")
        exports_root = f"{self.args.shared_root.rstrip('/')}/Exports"

        def exported_glbs():
            files = self.shell(
                f"find {shlex.quote(exports_root)} -type f", check=False
            ).splitlines()
            return [
                path
                for path in files
                if f"/{copy_name}" in path and path.endswith(".glb")
            ]

        exported = self.wait("shared GLB export", exported_glbs, timeout=180)
        for remote in exported:
            relative = remote[len(self.args.shared_root.rstrip("/")) + 1 :]
            glb, _ = self.pull_stable(relative)
            data = glb.read_bytes()
            magic, version, size = struct.unpack("<4sII", data[:12])
            if magic != b"glTF" or version != 2 or size != len(data):
                raise RuntimeError(f"Invalid GLB export: {relative}")
            chunk_length, chunk_type = struct.unpack("<II", data[12:20])
            if chunk_type != 0x4E4F534A or not json.loads(
                data[20 : 20 + chunk_length]
            ).get("meshes"):
                raise RuntimeError(f"GLB contains no exported meshes: {relative}")
        self.record("direct_shared_glb_export", {"files": exported})
        self.capture_logs()
        if self.shell(f"pidof {shlex.quote(self.args.package)}") != self.pid:
            raise RuntimeError(
                "App process changed before restart; refusing to stop a different process"
            )
        self.shell(f"am force-stop {shlex.quote(self.args.package)}")
        self.shell(
            f"am start -n {shlex.quote(self.args.package)}/com.unity3d.player.UnityPlayerGameActivity"
        )
        self.wait(
            "API after restart without another folder grant",
            lambda: self.http("/api/v1?query.queue"),
        )
        self.pid = self.shell(f"pidof {shlex.quote(self.args.package)}")
        self.command("load.named", Path(original).stem)
        restart_name = f"{self.name}_restart"

        def persisted():
            time.sleep(3)
            restart_path = self.save_as(restart_name)
            _, strokes, _ = self.sketch(restart_path)
            return strokes == second_strokes

        self.wait("saved strokes after process restart", persisted, timeout=120)
        self.record("grant_and_sketch_persist_across_restart", {"pid": self.pid})

    def capture_logs(self):
        if self.pid:
            logs = self.adb("logcat", "-d", f"--pid={self.pid}", "-v", "threadtime")
            (self.output / f"app-{self.pid}-logcat.txt").write_text(
                logs, encoding="utf8"
            )

    def media(self):
        fixtures = self.args.fixtures
        relative_dir = self.name
        for source, area in (
            (fixtures / "image.png", "Images"),
            (fixtures / "video.mp4", "Videos"),
        ):
            if not source.is_file():
                raise RuntimeError(f"Missing fixture: {source}")
            directory = f"{self.args.shared_root}/Media Library/{area}/{relative_dir}"
            self.shell(f"mkdir -p {shlex.quote(directory)}")
            self.adb("push", str(source), f"{directory}/{source.name}")
            if area == "Videos":
                self.adb("push", str(source), f"{directory}/video2.mp4")
        models_dir = f"{self.args.shared_root}/Media Library/Models/{relative_dir}"
        self.shell(f"mkdir -p {shlex.quote(models_dir)}")
        self.adb("push", f'{fixtures / "models"}/.', models_dir)
        self.command("image.import", f"{relative_dir}/image.png")
        self.command("model.import", f"{relative_dir}/triangle.gltf")
        time.sleep(5)
        self.command("model.import", f"{relative_dir}/triangle.obj")
        time.sleep(5)
        self.command("video.import", f"{relative_dir}/video.mp4")
        time.sleep(3)
        self.command("video.import", f"{relative_dir}/video2.mp4")
        latency = []
        # Allow Android's extractor retries to finish and cover more than one
        # 30-second fixture loop; a quick response does not prove successful playback.
        for _ in range(45):
            started = time.monotonic()
            self.http("/api/v1?query.queue")
            latency.append(time.monotonic() - started)
            time.sleep(1)
        media_name = f"{self.name}_media"
        media_path = self.save_as(media_name)
        _, _, metadata = self.sketch(media_path)
        references = json.dumps(metadata)
        for filename in ("image.png", "triangle.gltf", "triangle.obj", "video.mp4"):
            if filename not in references:
                raise RuntimeError(f"Media reference was not saved: {filename}")
        if len(metadata.get("Videos", [])) < 2:
            raise RuntimeError("Sketch did not retain two video widgets")
        self.record(
            "shared_media_references_and_api_responsiveness",
            {
                "path": media_path,
                "max_api_seconds": max(latency),
                "note": "References and API responsiveness checked; visual rendering/playback not asserted",
            },
        )

    def evidence(self):
        # Capture only this app, without clearing another process's log buffers.
        self.capture_logs()
        signatures = {
            "gltf_shader_missing": "UnityGLTF/PBRGraph not found",
            "video_extractor_failure": "AndroidVideoMedia::OpenExtractor failed",
            "obj_material_missing": "PopulateMeshes mat:",
            "inactive_reference_panel_coroutine": "Coroutine couldn't be started because the the game object 'ReferencePanel",
        }
        for log in self.output.glob("app-*-logcat.txt"):
            lines = log.read_text(encoding="utf8").splitlines()
            for name, signature in signatures.items():
                matches = [line for line in lines if signature in line]
                if matches:
                    self.findings.append(
                        {
                            "name": name,
                            "log": log.name,
                            "count": len(matches),
                            "example": matches[0],
                        }
                    )
        for finding in self.findings:
            print(f"[SAF_DEVICE_TEST] FINDING {finding['name']}", flush=True)
        (self.output / "results.json").write_text(
            json.dumps(
                {
                    "serial": self.args.serial,
                    "package": self.args.package,
                    "shared_root": self.args.shared_root,
                    "results": self.results,
                    "runtime_findings": self.findings,
                    "not_completed": sorted(
                        {
                            "shared_save_archive_and_thumbnail",
                            "shared_overwrite",
                            "shared_reload_stroke_roundtrip",
                            "direct_shared_snapshot",
                            "direct_shared_glb_export",
                            "grant_and_sketch_persist_across_restart",
                        }
                        - {
                            result["name"]
                            for result in self.results
                            if result["status"] == "passed"
                        }
                    ),
                    "not_tested": [
                        "visual rendering",
                        "controller interactions",
                        "media playback/import",
                        "non-GLB exports",
                        "crash recovery",
                        "low storage",
                    ],
                },
                indent=2,
            ),
            encoding="utf8",
        )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--serial", required=True)
    parser.add_argument("--package", default="foundation.icosa.openbrush")
    parser.add_argument("--adb", default="adb")
    parser.add_argument("--port", type=int, default=40075)
    parser.add_argument(
        "--shared-root",
        required=True,
        help="ADB-visible path to the already granted folder",
    )
    parser.add_argument("--expected-version")
    parser.add_argument(
        "--output", type=Path, default=Path("Temp/DeviceTesting/results")
    )
    parser.add_argument(
        "--exercise",
        action="store_true",
        help="Draw/save/reload a disposable sketch and capture a PNG",
    )
    parser.add_argument(
        "--fixtures",
        type=Path,
        help="Optional media fixture directory containing image.png, video.mp4 and models/",
    )
    args = parser.parse_args()
    runner = Runner(args)
    try:
        runner.preflight()
        if args.exercise:
            runner.exercise()
        if args.fixtures:
            runner.media()
    except (
        OSError,
        RuntimeError,
        ValueError,
        zipfile.BadZipFile,
        subprocess.SubprocessError,
    ) as exc:
        runner.results.append({"name": "run", "status": "failed", "error": str(exc)})
        print(f"[SAF_DEVICE_TEST] FAIL {exc}", flush=True)
        return 1
    finally:
        runner.evidence()
        print(f"[SAF_DEVICE_TEST] Evidence: {runner.output}", flush=True)
    return 1 if runner.findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
