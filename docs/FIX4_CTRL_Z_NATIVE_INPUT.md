# FIX4 - Reliable Ctrl+Z Undo

## Problem

Ctrl+Z was read from Dear ImGui keyboard state. Depending on widget focus/key ownership,
ImGui could capture the chord and the builder never received the undo request.

## Fix

- Poll `Silk.NET.Input.IKeyboard` directly from `CrowdSimApplication`.
- Accept either left or right Control plus `Z`.
- Use a rising-edge latch so one physical press causes one undo.
- Forward the request to `SimulationUi.TryUndoLastPlacementShortcut`, which uses the same
  `UndoLastPlacement` path as the visible Undo button.
- Ignore the hotkey while a text field is actively accepting text.

No simulation physics, builder geometry, persistence, or rendering behavior was changed.
