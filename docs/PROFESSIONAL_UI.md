# Professional workspace UI

## Design blueprint

The existing native Silk.NET/ImGui frontend is retained. The update improves the shell around the simulator rather than introducing another GUI framework.

| Component | Choice and purpose |
| --- | --- |
| Layout | A pure DashboardLayout allocator bounds panel widths to the available window and gives the hidden inspector's width plus its gap back to the viewport. Left control width stays stable during toggling. |
| Inspector state | A small InspectorPanelState holds visibility and the active page independently of selected agents and rendering options. Drawing is conditional; hiding does not clear domain state. |
| Navigation | Explicit Build/Run, Camera and Files destinations keep scene authoring, view controls and persistence easy to find. The inspector similarly has stable Overview, Hazard, Agent and Debug destinations. |
| Primary actions | Fixed footer actions keep Finalise, transport and recording accessible while the selected control page scrolls. |
| Readability | Responsive columns, wrapped guidance, labelled full-width sliders, fitted status strings with full-text tooltips, consistent spacing and action-button contrast. |
| Shortcuts | F4 toggles the inspector on a physical key-down edge. It does not repeatedly toggle while held, does not consume Alt+F4, and is ignored while entering text. |

## Library roles

| Pinned library | Role and rationale |
| --- | --- |
| Silk.NET.Windowing/Input/OpenGL 2.23.0 | Keeps the current native window, input and graphics integration. It supports the existing simulation display without a platform/framework migration. |
| Silk.NET.OpenGL.Extensions.ImGui 2.23.0 and ImGui.NET 1.91.6.1 | Reuse the existing immediate-mode controls and renderer; tab/page state and responsive sizing can be added with no new dependencies. |
| SharpAvi 3.0.1 and SharpAvi.ImageSharp 3.0.0 | Preserve current full-interface AVI capture. |
| xUnit 2.9.3 | Reuses the existing regression suite; new pure layout/state tests run without a graphics context. |

Directory.Packages.props and requirements.txt are unchanged. A locally available Segoe UI, DejaVu Sans or Arial font is loaded at 16 px; the bundled ImGui default font is scaled when no supported local font is available.

## How it works

1. At each UI frame, the top bar draws the inspector toggle before workspace sizing.
2. DashboardLayout computes control, viewport and optional inspector widths from the available region. It never reserves an off-screen fixed minimum width.
3. The chosen left page scrolls within its own child region; its primary action footer remains outside that region.
4. The world renderer receives the expanded or restored viewport. Simulation stepping and scene data follow the existing code paths.
5. If visible, the inspector draws its remembered destination. Its page child has a stable identity, retaining scroll position as well as the destination during toggling.
6. Selecting a person changes the inspector destination to Agent. It honours a hidden inspector; clicking the Agent N viewport button explicitly reveals it.

Inspector visibility is session state and is not added to saved-scenario JSON. Debug options continue rendering while the panel is hidden. Automatic selection invalidation when a selected agent exits remains part of the original application behaviour.

## Changed implementation

- CrowdSimApplication.cs: dashboard theme, local font selection and F4 input.
- SimulationUi.Dashboard.cs: top bar, navigation, transport footer, inspector destinations, overview cards and status text.
- DashboardLayout.cs / InspectorPanelState.cs: pure allocation and panel state.
- SimulationUi.cs / SimulationUi.Layout.cs / SimulationUi.ToxicFume.cs: grouped authoring controls, labelled camera controls, wrapped guidance and scenario entry cards.
- WorldRenderer.cs: human-readable viewport guidance only.
- DashboardLayoutTests.cs: viewport expansion, bounded allocation and inspector continuity.

The CrowdSim.Core files are byte-identical to the uploaded project. Dependencies, schema, recordings and simulation models were not changed.

## Preview images

- [Workspace](previews/workspace.png)
- [Inspector hidden](previews/inspector-hidden.png)
- [Compact camera page](previews/compact-camera.png)
- [Toxic Fume inspector](previews/toxic-fume-inspector.png)
