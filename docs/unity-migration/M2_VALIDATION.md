# M2 validation evidence (delivery snapshot)

Executed in Codex cloud on 2026-10-18, Linux x64, SDK 8.0.415/runtime 8.0.21.
M1 was accepted by the user before this task; its delivery results are retained in
[M1_VALIDATION.md](M1_VALIDATION.md). No local Editor logs were supplied with that acceptance.

| Check | Actual result |
| --- | --- |
| Static asset/project/package/binding checks | Passed: 103 unique GUIDs, serialized references, both scenes, bundled scenarios and exact parity data |
| Reference preservation | Passed: SHA-256 manifest for 106 original files, unchanged original source/tests/scenarios/solution/dependency pins and M1 goldens |
| Original Release solution build | Passed |
| Original xUnit regressions | **404 passed, 0 failed, 0 skipped** |
| Linked portable xUnit regressions | **376 passed, 0 failed, 0 skipped**; same original core test source/assertions, portable engine reference |
| Foundation/diagnostic xUnit tests | **32 passed, 0 failed, 0 skipped**; six sessions/replay, random/queue differential checks, JSON guards, detached frames, scheduler and M1 checks |
| Production portable compilation | Passed: all engine/compatibility/diagnostic source, C# 9 / netstandard2.1, no UnityEngine or reference-engine dependency |
| Original seeded capture | Passed: all six cases, all 121 tick hashes each, readable checkpoints/replay and compatibility oracle match unchanged M1 baselines |
| Portable seeded capture | Passed: same six-case and compatibility comparisons, exact match in pinned cloud runtime |
| Unity JSON package metadata | Inspected version 3.2.2 / codec 13.0.2; explicit precompiled DLL assembly reference |
| Shell syntax and Git whitespace | Passed |
| GitHub Actions | Workflow updated; remote run result **not verified** |
| Unity first import/UPM/frontend C# compilation | **Not executed — no Unity Editor installed** |
| Unity EditMode tests | **9 supplied, not executed** (3 foundation + 6 host-runtime parity cases) |
| Unity PlayMode tests | **2 supplied, not executed** (foundation + actual diagnostic-scene lifecycle) |
| Unity appearance/native input/resizing/Mono numeric results | **Not executed** |
| Standalone player build/run | **Not executed**, final acceptance belongs to M11 |

The original and linked original tests can emit the existing xUnit2031 advisory.
Original assertions were not changed, and no tests were disabled or skipped. The
28 frontend-helper cases excluded from the portable-core project still run in
the 404-case original suite; their Unity frontend replacements arrive later.

## Reproduce cloud checks

From the repository root with .NET SDK 8.0.415/runtime 8.0.21 installed:

```bash
bash Migration/Tools/validate.sh
```

Private SDK example:

```bash
DOTNET=/workspace/crowdsim-tools/dotnet/dotnet bash Migration/Tools/validate.sh
```

The script enters Migration/ to enforce its SDK pin, restores tooling with committed
lockfiles, builds/tests the original and portable engines and verifies both captures
without writing baselines. Actual TRX files go to ignored `Migration/artifacts/tests/`;
the GitHub workflow uploads them when executed. Baseline capture pins runtime 8.0.21
and disables runtime roll-forward. See [the capture contract](../../Migration/Parity/README.md).

M2's six cloud diagnostic sessions also execute the same production checkpoint
comparator used by the Unity viewer. Exact cloud hashes and tolerant Unity runtime
checks are distinct evidence. Unity results must not be inferred from cloud passes.

## Required local acceptance

Follow [M2_LOCAL_TESTING.md](M2_LOCAL_TESTING.md) in Unity **6000.3.26f1**. Exercise
all three presets with Hybrid off and on, transport/reset/startup navigation,
resize, and both Unity test suites. Report any Console error or checkpoint mismatch
with the preset, mode, tick, failure paths and actual values. Do not overwrite
reference data. M2 stays awaiting acceptance until the actual Editor checks pass.

The user subsequently reported M2 tested successfully and authorized M3. Local
Editor logs were not supplied; the cloud and supplied-test evidence above is historical.
