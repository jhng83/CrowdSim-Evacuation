# M1 local Unity acceptance

Use **Unity 6000.3.26f1**. Test only the downloaded milestone project; no access to
your computer is needed by Codex. Keep your existing .NET project/saves separate.
No .NET SDK or original application is required for these Unity steps.

1. Download the milestone commit ZIP, extract it, and add its **UnityProject/**
   folder through Unity Hub. Open it with 6000.3.26f1. Allow the first package
   import/compilation to finish. Decline automatic Editor upgrades.
   Unity may generate its standard URP global settings/default volume assets on
   first import. Expected: no red Console errors or unresolved packages. First-import package
   resolution is a required local check, not a cloud-verified result.
2. Open **Assets/CrowdSim/Scenes/Bootstrap.unity** and select the **Game** tab.
   Enter Play Mode. Expected: dark CrowdSim startup page, two scenario roadmap
   cards, welcome overview, project checks and the M1 footer. There are no agents
   or simulation controls in M1.
3. Click **Explore Explosion roadmap**, then **Explore Toxic Fume roadmap**.
   Expected: the overview title/description and selected card change immediately;
   they explain the later delivery milestones. Click **Return to welcome**.
   Expected: the welcome overview returns without creating a simulation.
4. Click **Run startup checks**. Expected: PASS for the pinned Editor, Universal
   Render Pipeline and two bundled reference examples. These three startup checks
   do not assert full package, rendering or simulation parity.
5. Resize the Game view to **1600x900**, **1280x720**, then **800x600**.
   Expected: text wraps, cards wrap when needed, and the vertical scrollbar keeps
   the lower checks/buttons accessible. Tab through controls and activate a button
   using Enter/Space. Note keyboard/mouse or readability failures.
6. Exit and re-enter Play Mode twice. Expected: welcome is restored, one interface
   appears, buttons still work once per activation, and Console has no new errors.
7. Open **Window > General > Test Runner**. On **EditMode**, run
   **CrowdSim.Foundation.EditModeTests**. Expected: **3 tests pass**. These check
   the Editor/API/pipeline configuration, scene script references, and imported
   resources/example files. Save changes to any unrelated open scene first.
8. On **PlayMode**, run **CrowdSim.Foundation.PlayModeTests**.
   Expected: **1 test passes** covering overview changes, reset and enable/disable
   lifecycle without duplicated UI. This sends UI events; step 5 still checks
   actual local mouse/keyboard behavior.
9. Stop Play Mode, close and reopen the project, then repeat steps 2–4.
   Expected: clean reopening, functional controls, no missing scripts or assets.

Report your OS, Editor version, package/import errors, EditMode/PlayMode counts,
and which numbered step failed. Console error text is especially useful. If all
steps pass, say **M1 accepted**. Optional: provide CPU/GPU/RAM for later scale targets.

M1 has not been accepted until these checks are reported. No standalone build,
simulation parity, final visual fidelity or runtime recording is claimed here.
