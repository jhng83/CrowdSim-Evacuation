# FIX12 QA notes

## Baseline

FIX11 was locally validated by the user at 25/25 tests before this revision.

## Changes reviewed in this environment

- added body-clear segment-vs-inflated-wall navigation test;
- exploration cancellation and waypoint reachability now use body clearance;
- close-range exit commitment respects body-clear wall geometry;
- stuck detection uses windowed net translation/exit progress;
- residual social panic uses source gating plus differential contagion;
- coherent running-crowd contagion remains separate and unchanged;
- purple congestion heat from FIX11 remains unchanged;
- four regression tests added; expected suite total is 29.

## Static checks

- C# brace-balance check passed for all `src` and `tests` files;
- all new configuration and agent fields have matching references;
- exploration test geometry was independently checked for a case where visual LOS is clear while body-clear traversal is blocked, with at least one body-clear sampled recovery direction available;
- ZIP integrity is checked during packaging.

## Limitation

The execution environment used to prepare this branch does not contain the .NET SDK. Local `dotnet restore`, `dotnet build`, and `dotnet test` remain required before this revision is treated as validated.
