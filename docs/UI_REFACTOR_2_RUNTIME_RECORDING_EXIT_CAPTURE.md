# UI Refactor 2 - Runtime controls, recording and exit capture

- Behaviour tuning labels are rendered above full-width controls so text is never clipped.
- Restart from Beginning reconstructs the same scenario at frame 0 using the same random seed.
- Record this run arms a 1280x720, 20 FPS Motion-JPEG AVI before Start Simulation. Recordings are written to `recordings/`.
- Final-approach exit capture suppresses herding only after a body-clear doorway route is recovered, uses stable lanes for wide exits, and preserves bottleneck/contact guardrails.
