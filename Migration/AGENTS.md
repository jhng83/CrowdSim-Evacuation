# Migration tooling

Read ../docs/unity-migration/ROADMAP.md and STATUS.md. Preserve the original .NET
implementation/tests. Code under this directory is the cloud harness; production
portable source lives in UnityProject/Packages/com.crowdsim.core/Runtime.

Directory.Build.props intentionally isolates these projects from the root build.
Use C# 9 and block namespaces in the portable project; C# 12 is permitted in
net8.0-only tooling. Baselines describe the unmodified reference, not Unity parity.
Do not overwrite baseline outputs to hide failures. Diagnose drift first.
Run python3 Migration/Tools/validate_project.py from the repository root and
bash Migration/Tools/validate.sh with DOTNET pointing at an installed .NET 8 SDK.
Do not claim Unity checks were executed without an actual licensed Editor.
