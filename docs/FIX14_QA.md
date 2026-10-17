# FIX14 QA

## Scope

FIX14 changes only the alarm-to-reaction handoff plus regression coverage/documentation. It does not change base walking speed, panic weights, exit capacity, collision handling, body-clear navigation, blast zones, casualty logic, or debug-overlay colours.

## Static checks performed in the packaging environment

- Verified the FIX13 -> FIX14 source diff is limited to:
  - `src/CrowdSim.Core/Systems/EvacuationSystem.cs`
  - `tests/CrowdSim.Tests/GuardrailTests.cs`
  - documentation/README additions.
- Confirmed the test suite contains 34 `[Fact]` tests.
- Confirmed project XML and VS Code JSON files parse successfully.
- Confirmed modified C# files have balanced delimiters under a lightweight static scan.
- Confirmed the output ZIP can be listed/tested without archive errors.

## Local validation still required

The packaging environment does not contain the .NET SDK, so `dotnet build` and `dotnet test` must be run on the user's .NET 8 machine. Expected test total: **34**.
