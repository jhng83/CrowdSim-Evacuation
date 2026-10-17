# FIX13 - Bounded active-alarm panic propagation

## Scope

This revision builds on the locally validated FIX12 baseline (29/29 tests passed). It keeps FIX12 body-clear navigation and the FIX11 purple congestion heat, while correcting the over-stabilised panic behaviour observed after FIX12.

## Problem observed in FIX12

FIX12 correctly stopped low residual panic from recursively re-amplifying, but the social-source gate was too aggressive. After the blast, similarly panicked agents contributed little differential social stimulus and the normal 10% panic decay could drive the visible red panic rings away too quickly.

The required behaviour is different from either permanent panic or permanent calm:

1. a real event creates alarm;
2. alarm can spread through visible, genuinely alarmed people and coherent alarmed runners;
3. each relayed alarm is finite;
4. when the initiating event/cascade is over, residual panic decays and cannot recreate the same alarm episode later.

## Active alarm is separate from panic level

Each agent now tracks a finite active-alarm source window independently of `PanicLevel`:

- `ActiveAlarmUntilFrame`
- `ActiveAlarmPanicFloor`
- `ActiveAlarmEpisodeId`
- `LastRelayedAlarmEpisodeId`

Direct shock/debris exposure starts or refreshes a direct alarm window for the current event episode. While active, mobile agents may transmit social/running alarm and their panic is held above a bounded display/behaviour floor rather than disappearing immediately.

Residual panic may remain after the source window expires, but it is no longer allowed to act as a contagion source.

## One relay per event episode

A socially/running-triggered agent may relay a given alarm episode only once. This is the key anti-feedback guardrail.

An agent whose relay window expires cannot be reactivated later by the same event episode, so staggered agents cannot ping-pong the same residual panic indefinitely. A new direct event episode could still create a new alarm in future if the simulator is extended to support multiple independent events.

## Running-crowd guardrail

Fast movement alone no longer counts as a panic-running signal. A runner must also be inside an active alarm window. This prevents ordinary calm evacuees from being misread as panic sources late in the scenario.

## Default bounded timings

The defaults are abstract crowd-simulation parameters:

- direct active-alarm signal: 5.0 s;
- relayed active-alarm signal: 3.0 s;
- direct active-alarm panic floor: 0.50;
- relayed active-alarm panic floor: 0.30.

These are behavioural simulation parameters, not medical or real-world blast calibration values.

## Regression coverage

FIX13 updates the running-crowd tests so panic running requires an active alarm source and adds three tests:

- `CalmFastEvacuees_DoNotCreateRunningPanicWithoutActiveAlarm`
- `RelayedAlarm_CannotReactivateFromSameEpisodeAfterExpiry`
- `DirectAlarmWindow_MaintainsVisiblePanicAfterShockArrival`

Expected total after local validation: **32 tests**.

## Preserved behaviour

Unchanged from FIX12/FIX11:

- body-clear wall-end navigation and progress detection;
- purple congestion heat overlay;
- red/orange individual panic rings;
- alarm exclamation indicator;
- panic-influenced SFM;
- exit yield/push anti-banging logic;
- wall-aware blast zones, debris, casualties and visibility effects;
- Agent POV and existing numerical/collision guardrails.
