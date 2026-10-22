# CrowdSim Unity migration — M10

Open **UnityProject/** with **Unity 6000.3.26f1**, URP **17.3.0**, then
**Assets/CrowdSim/Scenes/Bootstrap.unity** and press Play. **Assets/**, **Packages/**
and **ProjectSettings/** are all inside UnityProject. No original .NET executable
or SDK is required locally; first import needs Unity package-registry access.

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
   including **51 EditMode / 21 PlayMode** supplied cases and actual video review.

Custom-size stages use **schema v5**: Unity reads/writes it; the original .NET app
explicitly rejects it. Older v1–v4 imports and default-size v4 exports are preserved.
The stage remains fixed/non-walkable/unoccupied; concert details have no extra
physical or hazard-shielding effect. Black fonts, off-white outlined pillars,
off-white metric-grid floor, volumetric people and all previous tools remain.

M1–M9 were accepted; M10 awaits local testing. Cloud passes 404 original, 415 portable
and 217 migration cases, exact unchanged simulation traces/oracle, five legacy file
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
