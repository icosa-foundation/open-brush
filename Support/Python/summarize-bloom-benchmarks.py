#!/usr/bin/env python3
"""Summarize completed device captures, keeping per-repeat baselines and invalid runs explicit."""
import argparse
import json
import statistics
from collections import defaultdict
from pathlib import Path

BASELINES = {
    "encoded-both": "encoded-off",
    "encoded-alternate": "encoded-off",
    "encoded-reproject": "encoded-off",
    "native-ldr": "off-ldr",
    "native-hdr32": "off-hdr32",
    "native-hdr64": "off-hdr64",
    "native-hdr32-opaque": "off-hdr32-opaque",
}


def summarize(root):
    plan = json.loads((root / "plan.json").read_text(encoding="utf-8"))
    cases = {case["token"]: case for case in plan["cases"]}
    groups = defaultdict(list)
    invalid = []
    by_repeat = {}
    formats = defaultdict(set)
    for directory in sorted(root.glob("run*")):
        path = directory / "result.json"
        if not path.exists():
            continue
        report = json.loads(path.read_text(encoding="utf-8"))
        metadata = report["metadata"]
        profile = metadata["active"]["Profile"]
        issues = []
        validation = directory / "validation.json"
        if validation.exists():
            issues += json.loads(validation.read_text())["issues"]
        stereo = directory / "stereo.json"
        if stereo.exists() and json.loads(stereo.read_text()).get("missingEye"):
            issues.append("stereo glow missing from one eye")
        if issues or report.get("appGpu") is None:
            invalid.append(dict(run=directory.name, profile=profile, issues=sorted(set(issues))))
            continue
        # The plan supplies repeat and requested pyramid settings; reports supply actual settings.
        case = cases[directory.name]
        group = (profile, case.get("scene", case["fixture"]), case["levels"], case["downsample"])
        groups[group].append((directory.name, report))
        for target in metadata.get("targets", []):
            formats[group].add(f"{target['format']} {target['width']}x{target['height']} slices={target['slices']} MSAA={target['msaa']}")
    for group, runs in groups.items():
        profile = group[0]
        for token, report in runs:
            case = cases[token]
            # Do not combine different pyramid/fixture settings when subtracting baselines.
            by_repeat[(profile, case["repeat"], case.get("scene", case["fixture"]))] = report["appGpu"]["mean"]
    output = []
    for group, runs in sorted(groups.items()):
        profile, fixture, levels, downsample = group
        means = [report["appGpu"]["mean"] for _, report in runs]
        deltas = []
        for token, report in runs:
            case = cases[token]
            baseline = by_repeat.get((BASELINES.get(profile), case["repeat"], case.get("scene", case["fixture"])))
            if baseline is not None:
                deltas.append(report["appGpu"]["mean"] - baseline)
        dropped = []
        refresh = []
        for _, report in runs:
            samples = report.get("samples", [])
            counts = [s["droppedFrames"] for s in samples if s.get("droppedFrames") is not None]
            if len(counts) > 1:
                dropped.append(max(0, counts[-1] - counts[0]))
            refresh += [s["refreshHz"] for s in samples if s.get("refreshHz")]
        hz = statistics.mean(refresh) if refresh else None
        mean = statistics.mean(means)
        output.append(dict(profile=profile, fixture=fixture, levels=levels, downsample=downsample, runs=len(runs), meanAppGpuMs=mean,
                           minRunMeanMs=min(means), maxRunMeanMs=max(means),
                           meanBaselineDeltaMs=statistics.mean(deltas) if deltas else None,
                           baselinePairs=len(deltas), actualTargets=sorted(formats[group]),
                           refreshHz=hz, meanGpuHeadroomMs=1000 / hz - mean if hz else None,
                           droppedFrames=sum(dropped) if dropped else None,
                           droppedFrameCoverage=len(dropped)))
    return dict(profiles=output, invalidRuns=invalid, plannedRuns=len(plan["cases"]),
                completedRuns=sum(len(runs) for runs in groups.values()) + len(invalid),
                note="Total application GPU timing reported by XR; samples may repeat. Different visual bloom shapes are not matched quality.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    result = summarize(args.directory)
    (args.directory / "comparison.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"Completed {result['completedRuns']}/{result['plannedRuns']} captures; invalid {len(result['invalidRuns'])}")
    for row in result["profiles"]:
        delta = row["meanBaselineDeltaMs"]
        suffix = f", baseline delta {delta:.3f} ms ({row['baselinePairs']} pairs)" if delta is not None else ""
        print(f"{row['profile']} {row['fixture']} L{row['levels']}/D{row['downsample']}: {row['meanAppGpuMs']:.3f} ms, {row['runs']} runs{suffix}")
    for row in result["invalidRuns"]:
        print(f"Excluded {row['run']} {row['profile']}: {', '.join(row['issues'])}")


if __name__ == "__main__":
    main()
