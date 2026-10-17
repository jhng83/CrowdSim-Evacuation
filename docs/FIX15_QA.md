# FIX15 QA

## Added regression coverage

FIX15 adds six tests on top of the 34-test FIX14 suite:

1. calm evacuees retain the configured evacuation urgency speed floor;
2. an active social alarm remains a meaningful panic signal even when the observer already has moderate panic;
3. coherent calm evacuation flow triggers behavioural awareness without creating a panic alarm;
4. an obvious surrounding evacuation crowd triggers awareness even when congestion has slowed/stalled local movement;
5. evacuation awareness alone does not create a late panic episode;
6. scenario JSON save/load round-trip preserves agent count, random seed, blast radii, exits and blockages.

Expected total: **40 tests**.

## Static checks performed in the packaging environment

- Confirmed 40 `[Fact]` tests are present.
- Confirmed the new persistence source files are included automatically by the SDK-style project.
- Confirmed scenario load creates a fresh configuration/simulation and does not restore runtime casualty/panic state.
- Confirmed `SfmEngine` now consumes `Agent.CurrentDesiredSpeed`, so the evacuation urgency floor is applied consistently to both the desired force and final velocity clamp.
- Confirmed evacuation awareness does not call `ActivateDirectAlarm` or `TryActivateRelayedAlarm`.
- Confirmed the one-relay-per-event field remains intact and is not cleared on alarm expiry.
- Confirmed project XML/VS Code JSON files parse successfully and the ZIP can be integrity-tested.

## Local validation still required

The packaging environment does not contain the .NET SDK. Run restore/build/test on the Windows .NET 8 machine before visual validation. Expected test total: **40**.
