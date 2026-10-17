# Delivery verification record

Revision: adaptive explosion navigation, 14 September 2026.

## Executed in the delivery environment

- Extracted and inspected the actual uploaded CrowdSimCs(6).zip, not a reconstructed project.
- Reviewed new navigation call sites against the existing simulation, spatial queries,
  route planner, physics units and test accessors.
- Parsed all 12 project/props XML files and all 7 JSON files successfully.
- Checked lexical delimiter balance in all 16 added/modified C# files after excluding
  comments and string tokens. No delimiter mismatches were found.
- Confirmed the pinned NuGet inventory and Directory.Packages.props are unchanged.
- Included original project source, scenario fixtures and assets; omitted stale bin/obj outputs.

Lexical/XML checks are NOT C# compilation, type checking, successful xUnit execution,
behavioural simulation, or performance measurements.

## Not executed

- `dotnet restore`, `dotnet build`, or `dotnet test`.
- OpenGL application launch or Windows GUI/recording checks.
- New 600-agent latency benchmark, evacuation trials, or GPU measurements.

Reason: no .NET SDK/runtime/compiler exists in the delivery environment; an attempted
SDK download could not be completed. There are no new measured FPS, CPU latency or
unit-test pass counts to report.

## Included executable verification specifications

- `tests/CrowdSim.Tests/NaturalNavigationTests.cs`: new navigation regression cases.
- Existing suite retained, with two documented expectation/configuration adjustments.
- `scripts/verify-natural-navigation.ps1`: restore, build and tests, stopping on failure;
  optional paired benchmark via `-Benchmark`.
- `benchmarks/NaturalNavigationLatency`: 600-agent, 24-table, three-seed comparison,
  per-step traces and p95/p99/max summaries after local execution.
- `docs/NATURAL_NAVIGATION.md`: commands, scope, model limitations and visual checks.

Run the full suite and visual acceptance cases before treating this source revision as
a validated replacement. Older benchmark files in the repository are historical only.
