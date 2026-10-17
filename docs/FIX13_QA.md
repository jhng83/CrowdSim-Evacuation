# FIX13 QA notes

## Baseline

FIX12 was locally validated by the user at 29/29 tests before this revision.

## Changes reviewed in this environment

- added finite direct and relayed active-alarm windows separate from residual `PanicLevel`;
- direct shock/debris exposure starts a bounded alarm source for the current event episode;
- visible social panic and coherent running contagion only use currently active alarm sources;
- calm fast evacuees no longer act as panic-running sources;
- socially relayed alarm can activate an observer and be relayed onward for a bounded window;
- an agent may relay the same event episode only once, preventing late ping-pong reactivation;
- active alarm applies a bounded panic floor during its finite source window;
- FIX12 body-clear navigation and FIX11 purple congestion remain unchanged;
- three regression tests added and existing running/social tests updated; expected suite total is 32.

## Static checks

- all C# source/test files have balanced braces;
- all new alarm fields/methods/configuration values have matching references;
- no stale call to the superseded generic `ActivateAlarm` helper remains;
- expected xUnit `[Fact]` count is 32;
- archive integrity is checked during packaging.

## Limitation

The execution environment used to prepare this branch does not contain the .NET SDK. Local `dotnet restore`, `dotnet build`, and `dotnet test` remain required before FIX13 is treated as a validated checkpoint.
