"""Summarize reproducible triangle-ID microbenchmarks (Python standard library only)."""
import argparse
import json
import math
import statistics
from pathlib import Path


def percentile95(values):
    return sorted(values)[math.ceil(len(values) * 0.95) - 1]


def load_runs(paths):
    runs = [json.loads(Path(path).read_text(encoding="utf-8-sig")) for path in paths]
    for run in runs:
        if run["schemaVersion"] != 1 or len(run["cases"]) != 5:
            raise ValueError(f"Incomplete/unknown results: {run.get('label')}")
        for case in run["cases"]:
            for variant in ("legacy", "current"):
                if len(case[variant]) != run["samples"]:
                    raise ValueError(f"Missing samples: {run['label']}/{case['name']}")
    return runs


def summarize(before, after):
    identity = ("unity", "platform", "cpu", "graphicsApi", "isEditor", "incrementalGc")
    for run in before + after:
        for key in identity:
            if run[key] != before[0][key]:
                raise ValueError(f"Environment mismatch: {key} in {run['label']}")
    rows = []
    for i, original in enumerate(before[0]["cases"]):
        cases_before = [run["cases"][i] for run in before]
        cases_after = [run["cases"][i] for run in after]
        for case in cases_before + cases_after:
            for key in ("name", "fixtureSha256", "vertices", "indices", "callsPerSample"):
                if case[key] != original[key]:
                    raise ValueError(f"Fixture mismatch: {original['name']}/{key}")
        if any(case["currentIncorrectIds"] for case in cases_after):
            raise ValueError(f"Incorrect current output: {original['name']}")
        old = [s["milliseconds"] for c in cases_before for s in c["current"]]
        new = [s["milliseconds"] for c in cases_after for s in c["current"]]
        paired_old = [s["milliseconds"] for c in cases_after for s in c["legacy"]]
        allocations_old = [n for c in cases_before for n in c["currentAllocationProbes"]]
        allocations_new = [n for c in cases_after for n in c["currentAllocationProbes"]]
        rows.append(
            {
                "case": original["name"],
                "before_median_ms": statistics.median(old),
                "after_median_ms": statistics.median(new),
                "before_p95_ms": percentile95(old),
                "after_p95_ms": percentile95(new),
                "paired_legacy_median_ms": statistics.median(paired_old),
                "paired_speedup": statistics.median(paired_old) / statistics.median(new),
                "after_run_median_range_ms": [
                    min(statistics.median(s["milliseconds"] for s in c["current"]) for c in cases_after),
                    max(statistics.median(s["milliseconds"] for s in c["current"]) for c in cases_after),
                ],
                "before_allocation_range_bytes": [min(allocations_old), max(allocations_old)],
                "after_allocation_range_bytes": [min(allocations_new), max(allocations_new)],
                "before_incorrect_ids": original["currentIncorrectIds"],
                "after_incorrect_ids": 0,
                "mesh_bytes_before": original["currentMeshBytes"],
                "mesh_bytes_after": cases_after[0]["currentMeshBytes"],
            }
        )
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", nargs="+", required=True)
    parser.add_argument("--after", nargs="+", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    before, after = load_runs(args.before), load_runs(args.after)
    rows = summarize(before, after)
    report = {
        "before_runs": [run["label"] for run in before],
        "after_runs": [run["label"] for run in after],
        "environment": {key: before[0][key] for key in ("unity", "platform", "cpu", "graphicsDevice", "graphicsApi", "os")},
        "note": "Per-operation CPU microbenchmark, not FPS. Timing uses pooled samples; p95 is nearest-rank. Repeated runs are on one machine/session. Allocation probes are untimed global counter deltas; ranges expose noise.",
        "results": rows,
    }
    Path(args.output).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    for row in rows:
        print(
            f"{row['case']}: {row['before_median_ms']:.4f} -> "
            f"{row['after_median_ms']:.4f} ms; paired {row['paired_speedup']:.1f}x; "
            f"alloc {row['before_allocation_range_bytes']} -> "
            f"{row['after_allocation_range_bytes']} B"
        )


if __name__ == "__main__":
    main()

