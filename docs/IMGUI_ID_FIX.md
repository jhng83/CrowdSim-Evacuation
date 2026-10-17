# FIX6 - Dear ImGui ID collision

The red Dear ImGui programmer warning was caused by two visible controls resolving to the same internal ID: both displayed `Exit Agent POV` in the same ImGui window.

Changes:

- The camera-panel exit control now uses the visible label `Exit Agent POV` with hidden ID suffix `##CameraControls`.
- The selected-agent follow control has a stable hidden ID suffix `##SelectedAgentFollow`.
- `Enter Agent POV` has hidden ID suffix `##SelectedAgentPov`.
- While POV is active, the selected-agent panel no longer draws a second Exit POV button. It shows a short status line directing the user to the canonical 3D CAMERA exit control.

No simulation-core, panic-SFM, evacuation, collision, explosion, wall, casualty, or guardrail logic was changed.
