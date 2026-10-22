#!/usr/bin/env python3
"""Require exact baseline/candidate hashes and summarize repeated paired CPU trials."""
import csv
import json
from pathlib import Path
import statistics
import sys

root = Path(sys.argv[1]).resolve()
trials = int(sys.argv[2])
rows = []
for trial in range(1, trials + 1):
    base, opt = root / f"base-{trial}", root / f"opt-{trial}"
    hashes = list(base.glob("*.sha256"))
    assert len(hashes) == 8, f"Incomplete baseline trial {trial}"
    for path in hashes:
        assert path.read_bytes() == (opt / path.name).read_bytes(), f"State drift: trial {trial}, {path.name}"
    with (base / "phases.csv").open() as f:
        old = list(csv.DictReader(f))
    with (opt / "phases.csv").open() as f:
        new = list(csv.DictReader(f))
    keys = sorted(set((r["hazard"], r["layout"], r["hybrid"]) for r in old))
    assert len(keys) == 8
    for key in keys:
        a = [r for r in old if (r["hazard"], r["layout"], r["hybrid"]) == key]
        b = [r for r in new if (r["hazard"], r["layout"], r["hybrid"]) == key]
        assert len(a) == len(b) and len(a) > 0
        rows.append({"trial": trial, "hazard": key[0], "layout": key[1], "hybrid": key[2], "ticks": len(a),
                     "base_mean_ms": statistics.mean(float(r["total_ms"]) for r in a),
                     "opt_mean_ms": statistics.mean(float(r["total_ms"]) for r in b),
                     "base_p95_ms": sorted(float(r["total_ms"]) for r in a)[int(.95 * (len(a) - 1))],
                     "opt_p95_ms": sorted(float(r["total_ms"]) for r in b)[int(.95 * (len(b) - 1))],
                     "base_response_ms": statistics.mean(float(r["response_ms"]) for r in a),
                     "opt_response_ms": statistics.mean(float(r["response_ms"]) for r in b),
                     "base_sfm_ms": statistics.mean(float(r["sfm_nested_ms"]) for r in a),
                     "opt_sfm_ms": statistics.mean(float(r["sfm_nested_ms"]) for r in b)})
with (root / "summary.csv").open("w", newline="") as f:
    writer = csv.DictWriter(f, fieldnames=list(rows[0]), lineterminator="\n"); writer.writeheader(); writer.writerows(rows)
report = []
for key in sorted(set((r["hazard"], r["layout"], r["hybrid"]) for r in rows)):
    group = [r for r in rows if (r["hazard"], r["layout"], r["hybrid"]) == key]
    a = statistics.median(r["base_mean_ms"] for r in group)
    b = statistics.median(r["opt_mean_ms"] for r in group)
    report.append(dict(zip(("hazard", "layout", "hybrid"), key), base_median_of_trial_means_ms=a,
                       opt_median_of_trial_means_ms=b, ratio=a / b, matched_trials=len(group)))
(root / "summary.json").write_text(json.dumps(report, indent=2) + "\n")
for r in report:
    print(f"{r['hazard']} {r['layout']} Hybrid={r['hybrid']}: {r['base_median_of_trial_means_ms']:.2f} -> {r['opt_median_of_trial_means_ms']:.2f} ms ({r['ratio']:.2f}x); hashes match")
print("CPU-only .NET timings; Unity Mono/rendering/GPU/FPS have NOT been measured.")
