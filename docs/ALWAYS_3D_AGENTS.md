# 3D agents at every zoom level

Base: CrowdSimCs_TransparentPerimeterWalls.zip. The transparent room perimeter and existing professional UI are retained.

## Behaviour

Agents now remain world-space 3D humanoids when zoomed out. Zooming in adds the existing near-model detail. Large populations and faster playback continue to use reduced detail, without switching to a flat screen-space figure.

The reduced model has a shaded torso, block head, two legs and two arms, all built from volumetric body parts. Its existing walking/running animation, state colours and black outlines are retained. Seated, crawling, prone and assisting poses continue through their original 3D rendering paths.

This changes presentation only. All 42 core source/project files, all tests, dependency pins, scenario fixtures, physics, panic/SFM, navigation, collisions, hazard effects, selection, save/load, replay and recording code remain unchanged. Builder planning circles and placement markers are retained as planning aids.

## How it works

1. The renderer measures projected agent height and evaluates its existing distance/load thresholds.
2. Small or distant upright agents use DrawReducedHumanoid, which projects actual 3D body parts and their shaded faces.
3. Near agents retain the more detailed humanoid, including small parts such as neck, pelvis and feet.
4. The former flat DrawFastScreenHumanoid routine and its unused screen-quad outline helper are removed. The 3D silhouette outline implementation remains.

No package or GUI framework changes were needed. The existing Silk.NET/ImGui rendering integration remains in use.

## Verification

- Release solution build succeeded with .NET SDK 8.0.415.
- All **404 existing xUnit cases passed**, with 0 failures or skips, using the real xUnit 2.9.3 execution engine in-process.
- Native OpenGL screenshots checked an Explosion scene with **300 agents** at **0.45×, 1.00× and 2.50×** zoom. This population also exercises the existing larger-crowd render threshold.
- Start/pause was exercised with moving agents.
- A Toxic Fume scene with **205 occupants** was loaded, its plume precomputed and the scene finalised. Default and minimum-zoom views were checked.
- Source comparison confirms that the only changed C# files are WorldRenderer.cs and WorldRenderer.AgentOutlines.cs. Perimeter-wall transparency remains present.

The existing xUnit2031 advisory in RectangularTableBlastTests.cs is retained. Windows-specific rendering and hardware frame-rate benchmarks were not performed. Drawing 3D bodies costs more than the former flat fallback; the reduced body and existing visibility culling limit that cost. At very low zoom, pixel resolution naturally limits visible detail, but the geometry no longer becomes a billboard.

- [Default zoom, 300 agents](previews/default-300-agents-3d.png)
- [Minimum zoom](previews/minimum-zoom-300-agents-3d.png)
- [Maximum zoom](previews/maximum-zoom-300-agents-3d.png)
- [Toxic Fume scene](previews/fume-runtime-205-agents-3d.png)

## User check

Extract to a new folder and rebuild using run_instructions.md. Finalise a scene, open **Camera**, and move **Zoom** between 0.45× and 2.50×. Agents should retain the block-shaped 3D body throughout. Start and pause a run to check animated limbs. The surrounding wall remains invisible and all physical interactions continue as before.
