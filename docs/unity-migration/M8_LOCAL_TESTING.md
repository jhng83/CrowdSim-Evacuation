# M8 local Unity acceptance

M1–M7 were reported successful by the user. This delivery implements **M8 only**.
Use **Unity 6000.3.26f1**, URP **17.3.0**, the committed package lock and a new extraction
folder. No original .NET SDK/executable is required on your computer. First import
requires Unity package-registry access. Keep existing authored files/backups separate.

1. **Download and open.** Extract the delivered commit ZIP. In Unity Hub, Add the
   extracted repository's **UnityProject/** directory, the one containing **Assets/**,
   **Packages/** and **ProjectSettings/**. Open it with 6000.3.26f1. Open
   **Assets/CrowdSim/Scenes/Bootstrap.unity** and press Play.
   **Expect:** M8 milestone text, readable Atkinson Hyperlegible UI, both scenario
   entry cards, no missing font/shader/script or pink-material errors in Console.

2. **Run supplied Unity tests.** Stop Play. Open Window → General → Test Runner.
   Run EditMode, then PlayMode. **Expect:** 41 EditMode and 15 PlayMode cases pass,
   including HumanoidContractTests/HumanoidInteractionTests. Save the result XML and
   any failure stack traces. These cases have **not** been executed in cloud.

3. **Inspect every pose deterministically.** Open
   **Assets/CrowdSim/Scenes/Validation/HumanoidPresentation.unity** and press Play,
   with Game view at least 1280×720. Use Previous/Next through all 14 fixtures:
   standing, walking, running, waiting/alarm, injured, crawling, fallen, temporary
   incapacity, permanent incapacity, fatal casualty, seated, standing-up, approaching
   help and kneeling assistance. Use Centre selected and zoom for close inspection;
   uncheck it for the overview. **Expect:** block heads/torsos/limbs with lit faces
   and dark silhouettes; appropriate low or seated poses; rising cycles smoothly;
   helper kneels and reaches forward; running strides exceed walking strides.
   Fixtures contain **no simulation** and do not certify hazard outcomes.

4. **Check detail and pause.** In that scene compare zoom **0.45×, 1×, 2.5×**, changing
   azimuth to see side/depth. Inspect unselected people too (selection retains detail).
   Pause/Animate. **Expect:** distant figures still have a solid torso, head and four
   limbs; close detail adds neck/pelvis/feet and articulated low poses. No sprite/flat
   human fallback. Gait freezes on pause; rising fixture freezes as well. Alarm text,
   help labels/target IDs, panic ring and selected ID/state/panic readout remain legible.
   At extreme distance pixel resolution limits detail; ring/label overlap needs review.

5. **Explosion and furniture.** Stop, return to **Bootstrap**, choose **Open furnished
   example**, Finalise, Start, Arm Explosion and detonate on clear floor around
   **(18,12)**. Inspect reaction/standing-up, walking/running and resulting casualties.
   Pause/resume and repeat the three zoom checkpoints. **Expect:** correct state
   colours/poses, pose priority for casualties, read-only panic/alarm/help indicators,
   moving furniture/debris and retained blast overlays. Temporary/permanent incapacity
   share the native prone pose/colour; the selected state text distinguishes them.
   Perimeter walls stay visually transparent; authored internal walls remain visible
   and people still respect the physical boundary/real exit openings.

6. **Real picking and hidden inspector.** Pause. Click standing, seated (before it
   rises), crawling or prone bodies when available. Hide the inspector using F4 or
   its button, then select another person. Click empty floor. Select again and use
   **Show selected agent**. **Expect:** nearest intersected rendered body is selected,
   with white ring, #ID, state and panic percentage; empty floor clears it. Selection
   alone never opens the hidden inspector. Explicit reveal opens Agent details and
   a text-labelled legend. Debug/hazard view flags survive hiding and viewport expands.
   Try native mouse input: supplied synthetic tests are not a substitute for this.

7. **Isometric, Follow and POV.** Select a person. Use Camera or Agent inspector:
   Follow selected agent, Enter agent POV, Exit POV to Follow, Stop following and
   Reset camera. Try camera azimuth/elevation and wheel/slider zoom. **Expect:** Follow
   centres that person's rendered position while it moves; POV is perspective at
   **1.496 m** with movement/desired/exit heading and slight downward pitch, hiding its
   own body. Exit POV returns to Follow. Stop following/reset returns to the room.
   The in-viewport mode/exit controls work with the inspector hidden. POV disables
   floor picking, wheel zoom and Explosion arming; leave POV before floor operations.

8. **Toxic Fume at all zooms.** Return to startup (resolve the dirty prompt), choose
   **Open 205-person Fume example**, Compute dispersion, Finalise, Start and Release
   Toxic Fume. Repeat 0.45×/1×/2.5×, picking, Follow/POV and inspector hiding.
   **Expect:** 205 people and 80 seated initially; original fume exposure/response,
   field/grid overlays and body states remain consistent. Panic stays a separate ring,
   never a body tint; selection/Follow/POV do not change exposure or physical outcomes.
   Replay returns to paused frame zero, restored starts/furniture and zero exposure.
   Repeat representative runs with Hybrid off and on; navigation remains active in both.

9. **Exit and lifecycle cleanup.** In a new Explosion builder place a five-person exit
   at **(0,17)** and one person at **(3,17)**; Finalise/Start and detonate at **(12,17)**.
   Select the person and Follow/enter POV; use 4× playback if desired. Allow reaction
   and routing to finish (up to four minutes of model time). **Expect:** when the
   person exits, its body/rings/labels disappear and selection, Follow and POV clear
   to overview automatically. Replay clears selection/mode and shows exactly one
   restored body. New/load/return to startup must not retain old bodies or cameras.
   Dead/incapacitated people remain selectable; those states are not exits.

10. **Readability, focus and resizing.** Check 1280×720 and 1600×900, inspector shown/
    hidden, both hazard accents, selected tabs, disabled controls and keyboard focus.
    Tab through controls and type numeric values/file names; try F4 while editing and
    Alt+F4 as appropriate. **Expect:** readable labels/numbers, visible focus boundaries,
    scroll access, retained text-entry policy, no missing glyph boxes or clipped
    essential actions. Legends use high-contrast words beside swatches; selected state
    text and symbols supply alternatives to colour. Review with reduced colour
    discrimination if available. Actual font/lighting/contrast still need this review.

11. **Regression and feedback.** Save an authored scene after a run, load it and replay
    as in M5; verify it does not start with runtime injury/moved positions. Retest M6/M7
    hazard visibility controls. **Expect:** original workflows remain intact. Report
    Editor/OS, scene, exact steps, expected/actual result, Console logs and test XML;
    include screenshots for visual problems. M8 fixes precede any M9 work.

Known limits: procedural low-poly humans preserve behaviour/pose meanings, not the
native renderer's exact rasterisation or a skeletal character pack. Panic-ring width,
near-detail threshold and outline thickness are Unity presentation choices; review at
your resolution. Follow can crop distant room edges. POV keeps the reference fixed
head height even for low poses and does not support floor picking. The pose gallery
is a diagnostic, not an evacuation scene. Legacy SimulationParity remains the frozen
M2 trace/primitive viewer; use Bootstrap for complete M8 humans/cameras. Large-crowd
render optimisation, diagnostic overlays and timing remain M9; recording M10;
standalone acceptance M11. No Unity import, frontend compilation, tests, graphics,
native input, player/IL2CPP build or GPU performance check was executed in cloud.
