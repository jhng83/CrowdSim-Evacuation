# M10 local Unity acceptance

M1–M9 were reported successful by the user. This is **M10 only**, including the
requested adjustable concert stage. Use **Unity 6000.3.26f1**, **URP 17.3.0** and the
committed package graph. No original .NET executable/SDK is needed locally.

1. **Download and open.** Extract the delivered commit ZIP into a fresh folder.
   Add **UnityProject/** to Unity Hub; this folder contains **Assets/**, **Packages/**
   and **ProjectSettings/**. Open **Assets/CrowdSim/Scenes/Bootstrap.unity**, press Play.
   **Expect:** M10 banner, all prior examples and black readable text/light panels,
   off-white outlined pillars and a visible off-white 1.2 m gridded floor; no errors.

2. **Run supplied tests.** Stop Play; Window → General → Test Runner. Run EditMode
   then interactive PlayMode with **Game view visible/focused**. **Expect:**
   **51 EditMode / 21 PlayMode** supplied cases, including RecordingContractTests
   and RecordingInteractionTests. The actual complete-window capture case is
   intentionally ignored in batchmode/null graphics; an ignored case does not
   establish capture acceptance. Save XML/logs. No Unity tests ran in cloud.

3. **Stage dimensions and facing.** Open an empty Explosion workspace. Before any
   placement, set room width 20 m / length 16 m. Choose the **Concert stage** asset.
   Enter length **8 m**, depth/width **4 m**. Hover clear floor near room centre,
   rotate using R/90°, then place. **Expect:** exact matching preview/deck footprint;
   a raised charcoal deck with metal truss, blue/purple LED backdrop, speaker stacks,
   warm lamp lenses and a readable front lip. Quarter turns rotate the concert rig.
   Only one stage is allowed; its deck stays fixed and non-walkable/unoccupied.

4. **Resize the placed stage.** Keep it selected in the picker. Change to **10 × 5 m**.
   Try **1000**, **0**, negative or invalid values in either field; try a size that
   would extend beyond the room at the stage's existing centre. **Expect:** valid
   dimensions update the physical/rendered footprint in place; invalid edits revert
   with an explanation and no partial change. UI limits track the rotated room axes,
   retaining the **0.6 m physical perimeter inset**. Geometry/facing lock on Finalise.
   Existing-stage centre/facing are fixed; Undo removes placements in original order,
   rather than acting as a size-edit history.

5. **Conflict, room and minimum-size guards.** In fresh layouts test a 10×12 m room,
   1×1 m stage, quarter turns and near-perimeter placements. Place a pillar/chair or
   people beside a small stage and attempt to enlarge over them or an exit clearance.
   **Expect:** clear valid previews and unchanged rejected footprints; no overlaps,
   second stage or size exceeding usable room bounds. Rig parts remain inside the
   deck footprint and add no physical obstacles/shielding above its existing model.

6. **Save/load/replay sizing.** Save a custom-size stage with an exit and people.
   Load that save from Files; Finalise/Start, trigger a hazard, Pause and Replay.
   **Expect:** identical dimensions, original starts/intact furniture and frame zero.
   Custom sizes save as **schema v5**, supported by Unity. The preserved original
   .NET app rejects v5; it cannot represent custom-size stages. Old v1–v4 files still
   load; ordinary/default 15×6 m stage saves remain native-compatible v4.

7. **Arm and record Explosion.** Open the furnished Explosion example, open
   **Recording**, enable **Record next Start**, Finalise, then Start. Arm/detonate
   Explosion on clear floor near **(18,12)**. Change Camera/inspector tabs, toggle
   diagnostics, hide/show inspector and use Follow/POV while recording.
   **Expect:** persistent **RECORDING** badge, increasing frame count, queue ≤3,
   capture/encode timings and a full output path. The Debug metrics show recording
   cost/counters separately from simulation CPU. Pause stays visible in the video.
   The checkbox arms only a fresh Start, not Resume after manual stop.

8. **Stop and play.** Return to Recording → **Stop and save video**, then
   **Open recording folder**. Play the displayed `.avi` in an MJPEG-compatible player
   such as VLC. **Expect:** **1280×720, 20 fps MJPEG AVI**, correctly oriented world
   **and UI** including panels, text and control changes. Check top/bottom/left/right,
   text legibility, hazard/people animation and final frames. No audio is expected.
   If overload causes drops, playback is shorter; the fixed-step model is not slowed
   to satisfy the video encoder. At least one frame is needed for a playable video.

9. **Resize and record Fume.** Open the 205-person Fume example, Compute dispersion,
   Finalise, use **Start recording now**, Start and Release Toxic Fume. Record at
   1280×720 and 1600×900, then resize to a tall/narrow Game view during recording.
   Stop and inspect output. **Expect:** entire composed window remains included,
   fitted with letterboxing without cropping/stretching, correct orientation, same
   output dimensions/rate and existing Fume controls/exposure/replay functionality.
   A window over 16 million pixels or 8192 per side stops capture with a clear error.

10. **Lifecycle cleanup.** While recording, Replay; start another recording and use
    Files → New (save/discard as needed); record again and Return to startup; finally
    record and stop Editor Play. Check each output after its transition.
    **Expect:** accepted frames are drained and AVI/index/file handles finalized;
    no orphan recording, worker or render texture is retained. New sessions are
    disarmed; cancelled dirty prompts keep the existing session. Verify scene-exit
    cleanup with the supplied PlayMode case. Actual standalone OS-quit remains M11.

11. **Check overhead and failures.** Compare Debug frame/capture/encoder/queue/drops
    on identical moderate and large crowds; graphics stalls may increase frame time.
    **Expect:** bounded queue and explicit drops, fixed 0.15 s model integration,
    responsive Stop/error reporting and no physics values altered by rendering.
    Disk/permission/encoder errors are shown in Recording; previous files are not
    overwritten. Writer stops at **1 GiB or 100,000 frames**, whichever comes first;
    the accepted partial recording is finalized if storage remains writable.

12. **Report acceptance.** Send the commit, OS, Editor patch, Console/XML errors,
    tested dimensions/facing, videos' codec/size/rate/orientation/UI inclusion and
    any resize/lifecycle problems. Include crowd size, observed FPS, capture/encode
    cost and drops if performance is an issue. Fix M10 issues before advancing.

Cloud verified the portable worker/muxer with known frames, independent FFmpeg
decode, geometry/persistence/queue/lifecycle contracts and unchanged reference
traces. **Unity import/frontend compilation/native JPEG encoding/composed capture,
orientation/concert rendering/input and player builds were not executed.** The
Editor may suspend WaitForEndOfFrame when Game view loses focus to Scene view;
keep Game view active for video tests. Capture uses synchronous GPU readback and
can lower FPS. A forced kill/power loss or full disk can leave an incomplete AVI.
No standalone build is supplied in M10; M11 follows only after your acceptance.
