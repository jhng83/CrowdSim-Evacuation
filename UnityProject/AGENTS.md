# Unity migration

The user approved the roadmap in ../docs/unity-migration/ROADMAP.md. Implement one
approved milestone at a time on unity-migration. Preserve src/, tests/, scenarios/,
the .NET solution and its dependency declarations as the behavioral reference.
Never merge or push main without an explicit request.

Use Unity 6000.3.26f1, URP 17.3.0 and the committed package versions. Runtime and
portable package source must compile as C# 9 against .NET Standard 2.1. This
overrides the root C# 12/file-scoped-namespace convention within UnityProject.
Use block namespaces, explicit using directives, four spaces and LF. Keep
UnityEngine references out of Packages/com.crowdsim.core/Runtime.

Keep .meta GUIDs stable and commit new asset and folder metadata. Do not commit
Library, Temp, UserSettings, generated IDE projects, recordings or player builds.
Do not claim Editor import, Unity test execution, rendering or player builds
passed unless an actual Unity Editor executed them. Dotnet compilation is a
separate check. Follow ../docs/unity-migration/VALIDATION.md.

Each cloud task is isolated; use this existing checkout, without a new worktree
unless explicitly requested. No access to the user's computer is authorized.
After implementation and available validation, update migration status/parity,
make one milestone-level commit, push unity-migration, deliver local test steps,
and stop for the user's results. Fix reported problems before advancing.

The user requires user-centred colours and typography in subsequent milestones.
Use the bundled OFL Atkinson Hyperlegible face, readable text sizes, high-contrast
text/surfaces and visible keyboard focus. The M9 user request explicitly requires
black text on light surfaces, off-white square/circular architectural pillars with
black outlines and an off-white floor with a default-on metric grid. Keep the
workspace UI root transparent so the simulation camera stays visible. Pair state colours with labels, shapes
or poses; keep state meaning consistent across legends and views. Preserve
scrolling and the expanded viewport when the inspector is hidden. Verify actual
font import, wrapping, contrast and input locally when Unity is available.
