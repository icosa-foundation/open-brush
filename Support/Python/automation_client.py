"""External authoring and execution of Open Brush Virtual Input plans (standard library only)."""

import base64
import copy
import json
import time
import urllib.parse
import urllib.request
import uuid


class AutomationClient:
    def __init__(self, url="http://127.0.0.1:40074"):
        self.url = f"{url.rstrip('/')}/api/v1"

    def command(self, name, value="", timeout=10):
        data = urllib.parse.urlencode({name: value}).encode("utf-8")
        with urllib.request.urlopen(self.url, data=data, timeout=timeout) as response:
            return response.read().decode("utf-8")

    def status(self):
        response = self.command("query.automation")
        try:
            return json.loads(response)
        except json.JSONDecodeError as error:
            raise RuntimeError(f"{self.url} did not return automation status. Check the running Open Brush instance.") from error

    def run(self, plan, timeout=600, progress=print):
        """Upload one complete shot; motion timing is executed by Open Brush, not HTTP sleeps."""
        if plan.get("version") != 1 or not plan.get("steps"):
            raise ValueError("A plan needs version 1 and a nonempty steps array.")
        initial = self.status()
        if initial.get("Mode") != "VirtualInput":
            raise RuntimeError("Open Brush must be running in Virtual Input mode.")
        if initial.get("Recording") or initial.get("Phase") != "Stopped":
            raise RuntimeError("Another automation or recording is active.")
        run_id = uuid.uuid4().hex
        payload = base64.urlsafe_b64encode(json.dumps(plan, allow_nan=False).encode("utf-8")).decode("ascii")
        deadline = time.monotonic() + timeout
        last_phase = None
        try:
            self.command("automation.script", f"{run_id},{payload}", timeout=timeout)
            while time.monotonic() < deadline:
                current = self.status()
                if current.get("RunId") != run_id:
                    time.sleep(0.2)
                    continue
                if current.get("Error"):
                    raise RuntimeError(current["Error"])
                phase = current.get("Phase")
                if phase != last_phase and progress:
                    progress(f"{phase}: {current.get('Frames', 0)} frames")
                    last_phase = phase
                if phase == "Stopped" and not current.get("Recording"):
                    return current
                time.sleep(0.2)
            raise TimeoutError("Timed out waiting for the automation plan.")
        except (KeyboardInterrupt, OSError, RuntimeError, TimeoutError):
            try:
                if self.status().get("RunId") == run_id:
                    self.command("automation.stop")
            except OSError:
                pass
            raise


def with_output(plan, width=None, height=None):
    result = copy.deepcopy(plan)
    output = result.setdefault("output", {})
    if not isinstance(output, dict):
        raise ValueError("Plan output must be an object with width and height.")
    output["width"] = width if width is not None else output.get("width", 1440)
    output["height"] = height if height is not None else output.get("height", 1080)
    for key in ("width", "height"):
        value = output[key]
        if not isinstance(value, int) or not 16 <= value <= 8192 or value % 2:
            raise ValueError(f"Output {key} must be an even integer between 16 and 8192 pixels.")
    return result


def with_capture(plan, name, fps=30, width=None, height=None):
    if not 1 <= fps <= 120:
        raise ValueError("FPS must be between 1 and 120.")
    result = with_output(plan, width, height)
    result["capture"] = {"name": name, "fps": fps}
    return result
