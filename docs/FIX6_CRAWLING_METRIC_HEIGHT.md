# FIX6 - Crawling Agent Metric Height Correction

## Problem
Crawling agents were rendered with `z0 = 5.5f`. After the world conversion to metres, this legacy render-unit offset was incorrectly interpreted as 5.5 metres, making yellow crawling agents appear airborne and, under the isometric camera, visually displaced beyond walls.

## Fix
`WorldRenderer.DrawCrawlingHumanoid` now uses:

```csharp
float z0 = 5.5f * LegacyRenderUnitMeters;
```

With a 1.70 m humanoid and the original 42-unit render basis, the crawler base is approximately 0.223 m above the floor.

## Scope
Renderer only. No simulation state, collision, evacuation, panic, helping, routing, or casualty logic changed.
