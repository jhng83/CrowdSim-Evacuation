# Exit Congestion Anti-Banging Behaviour

This revision replaces repeated doorway pushback with a congestion episode model.

- The first over-capacity attempt receives one small inward setback.
- Low/moderate-panic pedestrians then yield and retry after a short panic-dependent, staggered delay.
- High-panic evacuating/injured pedestrians keep their normal panic-SFM intent toward the exit rather than entering the yield pause.
- High panic does not bypass exit capacity, wall constraints, or collision/contact logic.
- Rejected pushers are constrained at the interior doorway threshold and only their outward velocity component is removed; tangential motion is retained. This produces sustained pressure rather than forward/backward oscillation.
- The congestion episode resets if the pedestrian changes exit or moves well away from the doorway.
- Crawling agents always use the yielding path.

The red alarm indicator is also rendered with a custom stem/dot approximately twice the previous stroke width.
