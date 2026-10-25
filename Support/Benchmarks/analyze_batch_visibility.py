"""Compare production Batch visibility benchmark runs using only Python's standard library."""
import argparse
import json
import math
import statistics
from pathlib import Path


def load(paths):
    return [json.loads(Path(path).read_text(encoding="utf-8-sig")) for path in paths]


def compare(before, after):
    keys = ("schemaVersion", "unity", "platform", "cpu", "graphicsDevice", "graphicsApi", "incrementalGc", "samples", "warmups", "benchmarkSha256")
    for group in (before, after):
        for run in group:
            for key in ("productionSha256", "geometryPoolSha256", "batchPoolSha256"):
                if run.get(key) != group[0].get(key):
                    raise ValueError(f"Source changed within measurement group: {key}")
    for run in before + after:
        if len(run["cases"]) != {1: 7, 2: 10}.get(run["schemaVersion"]):
            raise ValueError(f"Incomplete run: {run['label']}")
        for key in keys:
            if run[key] != before[0][key]:
                raise ValueError(f"Protocol/environment mismatch: {key}")
    rows = []
    for i, reference in enumerate(before[0]["cases"]):
        old_cases = [run["cases"][i] for run in before]
        new_cases = [run["cases"][i] for run in after]
        for case in old_cases + new_cases:
            for key in ("name", "fixtureSha256", "callsPerSample", "vertexCount", "indexCount", "changedIndices"):
                if case[key] != reference[key]:
                    raise ValueError(f"Fixture mismatch: {reference['name']}/{key}")
            if len(case["milliseconds"]) != before[0]["samples"]:
                raise ValueError("Missing samples")
            if case["hiddenErrors"] or case["restoredErrors"]:
                raise ValueError(f"Incorrect indices: {case['name']}")
        if not all(case["paddedBoundsPreserved"] for case in new_cases):
            raise ValueError(f"Changed bounds: {reference['name']}")
        old = [value for case in old_cases for value in case["milliseconds"]]
        new = [value for case in new_cases for value in case["milliseconds"]]
        old_alloc = [value for case in old_cases for value in case["allocatedBytes"]]
        new_alloc = [value for case in new_cases for value in case["allocatedBytes"]]
        rows.append({
            "case": reference["name"],
            "before_median_ms": statistics.median(old),
            "after_median_ms": statistics.median(new),
            "speedup": statistics.median(old) / statistics.median(new),
            "before_p95_ms": sorted(old)[math.ceil(len(old) * 0.95) - 1],
            "after_p95_ms": sorted(new)[math.ceil(len(new) * 0.95) - 1],
            "before_allocated_bytes": [min(old_alloc), max(old_alloc)],
            "after_allocated_bytes": [min(new_alloc), max(new_alloc)],
            "before_run_medians_ms": [statistics.median(case["milliseconds"]) for case in old_cases],
            "after_run_medians_ms": [statistics.median(case["milliseconds"]) for case in new_cases],
            "mesh_bytes_before": reference["meshBytes"],
            "mesh_bytes_after": new_cases[0]["meshBytes"],
            "index_count": reference["indexCount"],
            "changed_indices": reference["changedIndices"],
        })
        for key in ("geometryResidentAfterCycle", "cachedIndexPayloadBytes", "cachedVertexPayloadBytes"):
            if key in reference:
                rows[-1][f"before_{key}"] = [case[key] for case in old_cases]
                rows[-1][f"after_{key}"] = [case[key] for case in new_cases]
    return {
        "before": [run["label"] for run in before],
        "after": [run["label"] for run in after],
        "environment": {key: before[0][key] for key in ("unity", "platform", "cpu", "graphicsDevice", "graphicsApi")},
        "scope": before[0]["scope"],
        "results": rows,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", nargs="+", required=True)
    parser.add_argument("--after", nargs="+", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    report = compare(load(args.before), load(args.after))
    Path(args.output).write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    for row in report["results"]:
        print(
            f"{row['case']}: {row['before_median_ms']:.4f} -> "
            f"{row['after_median_ms']:.4f} ms ({row['speedup']:.2f}x); "
            f"allocation {row['before_allocated_bytes']} -> {row['after_allocated_bytes']} B"
        )


if __name__ == "__main__":
    main()
