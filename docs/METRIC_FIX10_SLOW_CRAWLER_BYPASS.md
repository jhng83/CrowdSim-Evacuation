# Metric FIX10 - slow crawler bypass

This pass targets the observed case where mobile green/blue pedestrians can remain directly behind a yellow crawling casualty even when usable side space exists.

## Behaviour changes

- A `Crawling` agent remains a physical dynamic SFM/collision obstacle and may still transmit finite panic/alarm cues.
- Crawlers are no longer used as normal movement leaders by either general herding or the `FollowCrowd` evacuation-flow direction.
- Mobile `Evacuating`/`Injured` pedestrians can identify a materially slower crawler directly ahead.
- After a short 0.55 s observation window, the pedestrian evaluates two lateral bypass points around that crawler.
- A bypass point is accepted only when the path and short forward continuation are body-clear of static geometry and the local endpoint is not blocked by another casualty.
- Local occupancy scores both sides; the clearer side wins. A deterministic parity preference prevents otherwise identical pedestrians from all choosing the same side.
- The chosen side is committed for 1.8 s to avoid left/right oscillation.
- SFM/contact/collision remains active throughout; the bypass is local desired-direction guidance, not teleportation and not dynamic A*.
- Bypass logic is disabled in final exit-capture behaviour and for assistance/threat-escape intents so validated doorway and casualty workflows retain priority.

## UI diagnostics

The Selected Agent inspector reports either `Watching crawler N` during the short observation window or `Crawler N` plus the current bypass goal while a bypass is committed.

## Regression coverage

Two tests were added:

1. Crawling agents remain panic-capable but do not act as herd movement leaders.
2. A mobile pedestrian directly behind a slow crawler chooses a lateral bypass when side space is clear.

Expected source test count: 78.
