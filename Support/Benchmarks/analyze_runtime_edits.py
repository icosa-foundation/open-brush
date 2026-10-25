"""Validate and summarize loaded-sketch editing runs, with optional before/after comparison."""

import argparse
import json
from pathlib import Path
import statistics


def percentile(values, fraction):
    if not values:
        return None
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, int((len(ordered) - 1) * fraction))]


def validate_runs(runs):
    reference = runs[0]
    keys = (
        "schemaVersion",
        "harnessSha256",
        "fixturePaths",
        "fixtureSha256",
        "unity",
        "cpu",
        "gpu",
        "graphicsApi",
        "sdkMode",
        "batching",
        "eviction",
        "screenWidth",
        "screenHeight",
        "qualityLevel",
        "targetFrameRate",
        "vSync",
        "geometryShaders",
        "autosaveDisabled",
    )
    shape = [(p["fixture"], p["name"], p["sample"]) for p in reference["phases"]]
    if len(shape) != 69:
        raise ValueError("Expected all 69 measured phases")
    manifest = json.loads(reference["sourceManifest"])
    for run in runs:
        if run["status"] != "completed" or run.get("error"):
            raise ValueError(f"Incomplete run: {run['label']}")
        if any(run[k] != reference[k] for k in keys):
            raise ValueError("Protocol or environment changed within group")
        if [(p["fixture"], p["name"], p["sample"]) for p in run["phases"]] != shape:
            raise ValueError("Phase order changed")
        if json.loads(run["sourceManifest"])["installed"] != manifest["installed"]:
            raise ValueError("Instrumented source changed within group")
        for phase, original in zip(run["phases"], reference["phases"]):
            for key in ("cameraPose", "affectedStrokes", "brushGuid", "controlPoints"):
                if phase[key] != original[key]:
                    raise ValueError(f"Workload input changed: {key}")
            for key in ("strokes", "batchedStrokes", "vertices", "indices", "batches"):
                if phase["census"][key] != original["census"][key]:
                    raise ValueError(f"Geometry census changed: {key}")
    return keys, manifest


def summarize(paths):
    runs = [json.loads(Path(p).read_text(encoding="utf-8-sig")) for p in paths]
    reference = runs[0]
    keys, manifest = validate_runs(runs)
    rows = []
    groups = dict.fromkeys((p["fixture"], p["name"]) for p in reference["phases"])
    for fixture, name in groups:
        phases = [
            p
            for r in runs
            for p in r["phases"]
            if (p["fixture"], p["name"]) == (fixture, name)
        ]
        frames = [f for p in phases for f in p["frames"]]
        row = {
            "fixture": fixture,
            "phase": name,
            "samples": len(phases),
            "frames": len(frames),
        }
        row["inputs"] = [
            {
                key: p[key]
                for key in (
                    "sample",
                    "cameraPose",
                    "affectedStrokes",
                    "brushGuid",
                    "brushName",
                    "controlPoints",
                    "qualityLevel",
                )
            }
            for p in reference["phases"]
            if (p["fixture"], p["name"]) == (fixture, name)
        ]
        for key in (
            "directCallCpuMs",
            "directCallAllocatedBytes",
            "managedBytesBefore",
            "managedBytesAfter",
            "nativeBytesBefore",
            "nativeBytesAfter",
            "sampledPeakNativeBytes",
        ):
            row[key] = statistics.median(p[key] for p in phases)
        row["directCallCpuMsRange"] = [
            min(p["directCallCpuMs"] for p in phases),
            max(p["directCallCpuMs"] for p in phases),
        ]
        row["directCallCpuMsRuns"] = [
            statistics.median(
                p["directCallCpuMs"]
                for p in r["phases"]
                if (p["fixture"], p["name"]) == (fixture, name)
            )
            for r in runs
        ]
        for key in (
            "intervalMs",
            "mainThreadMs",
            "renderThreadMs",
            "gpuMs",
            "cpuMainFrameMs",
            "cpuPresentWaitMs",
            "cpuRenderFrameMs",
            "gcBytes",
            "drawCalls",
            "batches",
            "triangles",
        ):
            values = [f[key] for f in frames if f[key] > 0]
            row[key] = {
                "validSamples": len(values),
                "median": statistics.median(values) if values else None,
                "p95": percentile(values, 0.95),
                "max": max(values) if values else None,
            }
        row["counters"] = [
            {
                "name": c["name"],
                **{
                    key: statistics.median(p["counters"][i][key] for p in phases)
                    for key in ("cpuMs", "calls", "vertices", "indices")
                },
            }
            for i, c in enumerate(phases[0]["counters"])
        ]
        row["census"] = {
            key: statistics.median(p["census"][key] for p in phases)
            for key in phases[0]["census"]
        }
        rows.append(row)
    return {
        "labels": [r["label"] for r in runs],
        "environment": {k: reference[k] for k in keys},
        "source": manifest,
        "scope": reference["measurementScope"],
        "results": rows,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", nargs="+", required=True)
    parser.add_argument("--after", nargs="+")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    before = summarize(args.before)
    result = {"before": before}
    if args.after:
        after = summarize(args.after)
        if before["environment"] != after["environment"]:
            raise ValueError("Before/after protocol or environment mismatch")
        for b_row, a_row in zip(before["results"], after["results"]):
            if (b_row["fixture"], b_row["phase"]) != (a_row["fixture"], a_row["phase"]):
                raise ValueError("Before/after phases mismatch")
            if b_row["inputs"] != a_row["inputs"]:
                raise ValueError("Before/after camera or edit inputs mismatch")
            for key in ("strokes", "batchedStrokes", "vertices", "indices", "batches"):
                if b_row["census"][key] != a_row["census"][key]:
                    raise ValueError(f"Before/after geometry mismatch: {key}")
        result["after"] = after
    Path(args.output).write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(
        f"Validated {len(args.before)} baseline runs, {len(args.after or [])} candidate runs; {len(before['results'])} operation groups"
    )
    for row in before["results"]:
        if row["phase"] in ("select_32", "release_selection", "repaint_32"):
            print(
                f"{Path(row['fixture']).name} {row['phase']}: {row['directCallCpuMs']:.3f} ms direct CPU"
            )


if __name__ == "__main__":
    main()
