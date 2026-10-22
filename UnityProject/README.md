# CrowdSim Unity migration — M10 performance update 2

Open **UnityProject/** with **Unity 6000.3.26f1**, URP **17.3.0**, then
**Assets/CrowdSim/Scenes/Bootstrap.unity** and press Play. **Assets/**, **Packages/**
and **ProjectSettings/** are all inside UnityProject. No original .NET executable
or SDK is required locally; first import needs Unity package-registry access.

This update uses M10 fix `38ab742` as its base. Startup says **PERFORMANCE UPDATE 2**.
Default GPU instancing batches volumetric crowd bodies, outlines and selection/panic
rings without per-body-part Unity renderers. Debug's **GPU instanced crowd rendering**
checkbox allows comparison with the compatibility renderer. Matrix-based picking,
poses, state colours, labels, hidden inspector, Follow and POV remain available.
Physics stays on the CPU; exact sight/force/contact optimizations preserve behavior.
Debug supplies exclusive phase/nested SFM/collision readouts and a valid dense
800-person/32-table layout. Package version is **0.10.2**. Update 2 also removes
wall-query/force overhead and temporary closures, reuses unchanged world views and
crowd materials, and shares orthographic detail projections. Debug adds updated/
reused scene counters and the current Editor C# optimization mode. While stopped,
**CrowdSim → Performance → Use Release Code Optimization** explicitly switches the
Editor setting and recompiles; **Use Debug Code Optimization** restores script
debugging. Opening the project never changes the setting automatically.

Use the numbered [new checklist](../docs/unity-migration/M10_PERFORMANCE_2_LOCAL_TESTING.md).
Cloud passes **404 original / 415 portable / 239 migration** cases, unchanged goldens,
AVI decode and **24 exact paired CPU comparison hashes** against `38ab742`. Open
1000 .NET CPU ratios are **1.28–1.34×**, dense 800 **0.99–1.18×**, including one case
within timing noise. **Unity Mono/GPU/FPS, Editor menus and frontend/shader compilation
were not executed**. **52 EditMode / 24 PlayMode** cases are supplied but unrun.
[New CPU evidence](../docs/unity-migration/M10_PERFORMANCE_2_BENCHMARK.md) distinguishes
these measurements from actual Unity rendering performance. All M10 features below
remain included; M11 is not implemented.

M10 adds full-window recording including the application UI: **1280×720, 20 fps,
MJPEG AVI**, arm/start/stop, output folder, queue/drop/timing counters, letterboxing
on resize and replay/new/exit cleanup. Runtime recording uses Unity's thread-safe
JPEG encoder and a portable bounded AVI worker, without an Editor Recorder package.
The builder's single stage has adjustable length/depth, including resizing the
placed stage before finalising. Rotated footprints must fit inside the physical
room perimeter and avoid exits/obstacles/people. A charcoal deck, metal truss,
LED backdrop, speaker stacks and warm lamp lenses provide the concert appearance.

1. Use the asset picker → Concert stage; edit length/depth before placing, or
   resize it in place. Dimensions are in metres; invalid edits revert with a reason.
2. Finalise an example, open **Recording**, arm **Record next Start** and Start,
   or choose **Start recording now**. Stop and use **Open recording folder**.
3. Follow the numbered [M10 checklist](../docs/unity-migration/M10_LOCAL_TESTING.md),
   including **52 EditMode / 24 PlayMode** supplied cases and actual video review.

Custom-size stages use **schema v5**: Unity reads/writes it; the original .NET app
explicitly rejects it. Older v1–v4 imports and default-size v4 exports are preserved.
The stage remains fixed/non-walkable/unoccupied; concert details have no extra
physical or hazard-shielding effect. Black fonts, off-white outlined pillars,
off-white metric-grid floor, volumetric people and all previous tools remain.

M1–M9 were accepted; M10 awaits local testing. Cloud passes 404 original, 415 portable
and 239 migration cases, exact unchanged simulation traces/oracle, five legacy file
round trips plus v5 explicit native rejection, and independent FFmpeg known-frame
decoding. **Unity import/frontend compilation, Unity tests, full-window capture,
orientation, visual/input checks and standalone/IL2CPP builds were not executed.**

Video has no audio. Dropped frames shorten playback; the writer stops at 1 GiB or
100,000 frames. Synchronous GPU readback may reduce observed FPS. Editor capture
requires the Game view; switching to Scene can suspend end-of-frame capture.
See [validation](../docs/unity-migration/VALIDATION.md),
[compatibility](../docs/unity-migration/COMPATIBILITY.md),
[design](../docs/unity-migration/DESIGN.md) and [status](../docs/unity-migration/STATUS.md).
Final standalone acceptance/distribution remains M11.
