# FIX9 - Assistance progress timer correction

## Problem
FIX8 considered either goal-distance improvement or 0.15 m of arbitrary displacement to be assistance progress. A blocked helper could shuffle laterally in congestion and repeatedly reset `AssistLastProgressFrame`, preventing the 10-second abandonment guard from firing.

## Correction
- Only a meaningful reduction in distance to the active assistance position counts as progress.
- Route retry and alternate-side recovery do not themselves reset the no-progress clock.
- When the alternate side is selected, the distance baseline is updated for that new goal without changing `AssistLastProgressFrame`.
- Subsequent real progress toward the new side may then reset the clock normally.

The existing regression `BlockedHelper_AbandonsAssistanceAfterBoundedNoProgressWindow` remains intact and should now pass without weakening its assertion.
