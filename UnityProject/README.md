# CrowdSim Unity migration — M1

Open this directory, not the repository root, with **Unity 6000.3.26f1**.
The project uses **URP 17.3.0**, UI Toolkit and .NET Standard 2.1-compatible source.

1. Add `UnityProject/` through Unity Hub and let packages import.
2. Open `Assets/CrowdSim/Scenes/Bootstrap.unity`.
3. Enter Play Mode. Explore the Explosion and Toxic Fume roadmap cards, return
   to Welcome, and run the startup checks.
4. Follow [the complete M1 local checklist](../docs/unity-migration/M1_LOCAL_TESTING.md).

M1 is the startup foundation. It does not run crowds, author scenes, release
hazards, load scenarios into a simulation or record video. These features retain
their assigned milestones in [the approved roadmap](../docs/unity-migration/ROADMAP.md).
The bundled v2 and 205-person JSON examples are read-only reference data at M1.

The original .NET project remains under `../src/` and `../tests/` and runs
independently. No .NET SDK is required to open this Unity project. Internet access
to Unity's package registry is needed on first import. The package lock and assets
have static cloud validation, but the cloud environment has no Unity Editor;
first import, package resolution and actual UI rendering await your local test.

Both input backends are enabled for this foundation so UI Toolkit can use its
default runtime event handling. Project-wide simulation input is introduced in M3.
The built-in Unity physics timestep is unchanged; M2 adds the simulation's separate
0.15-second accumulator rather than binding the model to FixedUpdate.
