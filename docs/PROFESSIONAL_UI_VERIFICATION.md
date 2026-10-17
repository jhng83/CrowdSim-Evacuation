# UI verification

Base: the user's CrowdSimCs(10).zip attachment. Date: 2026-10-01.

## Build

The complete Release solution built successfully with .NET SDK **8.0.415** (MSBuild 17.11.48). The application, core library and tests all compiled. The delivery environment prevents the ordinary dotnet CLI from querying process start time, so MSBuild.dll was invoked directly; this is an environment workaround, not a project change.

## Regression suite

**404 passed, 0 failed, 0 skipped**, including **12 new DashboardLayoutTests cases**. Tests were discovered and run by the actual **xUnit 2.9.3 execution engine** in-process. No test bodies or assertions were replaced. The normal VSTest process-host launcher failed in this restricted environment before executing tests, so this result is not described as a successful dotnet test/VSTest run.

The new cases cover bounded allocation at widths 320, 640, 1000, 1280, 1600 and 2560; exact viewport expansion; stable left width; oversized spacing; retained inspector destination; selection while hidden; explicit reveal; and invalid dimensions. Tiny-width tests verify allocation arithmetic, not usability of the full desktop interface at those sizes.

The full suite also covers existing blast behaviour, fumes, navigation, replay/persistence, asset placement, crowd behaviour and geometry. Actual per-case results are in validation/ui-regression-results.json.

## Native UI checks

The compiled application ran in a real Silk.NET/ImGui **OpenGL window** using Xvfb and a Mesa software driver. The included PNGs are application screenshots, not mockups.

- Checked 1600×900 and 1280×720 layouts, readable labels and independent page scrolling.
- Created scenarios, opened a saved scene, placed a 50-agent group, finalised it, started and paused a run.
- Hid/restored the right panel by button and F4. The scene expanded, and the left control width stayed stable.
- Returned to the same Debug page with the Desired direction checkbox still enabled.
- Selected an agent, hid/restored the inspector and opened it via the viewport's Agent button.
- Checked the Toxic Fume page and hide/restore at 1280×720.
- Rebuilt and re-ran the interface after correcting clipped camera labels and guidance.

Inspection was performed on Linux. Windows-specific font appearance, AVI capture and large-population latency were not re-tested in this revision.

## Preservation checks

All **42 CrowdSim.Core source/project files** are byte-identical to the attachment; their SHA-256 hashes are in validation/professional-ui-verification.json. Directory.Packages.props and requirements.txt are also byte-identical. Original tests and the included saved scene were retained.

Only the native application shell, UI layout/state, viewport guidance and test-project links were changed. The new regression test file is DashboardLayoutTests.cs. No simulation model, JSON schema or recording implementation was edited.
