# Compatibility decisions and evidence

## Approved baseline

- Unity **6000.3.26f1**, URP **17.3.0**, API compatibility **.NET Standard 2.1**.
- UI Toolkit runtime UI; Input System **1.14.2**, both input backends enabled.
  M3 routes focus-aware keyboard and pointer events through UI Toolkit.
- Unity Test Framework **1.4.6**; Unity Newtonsoft JSON package **3.2.2**.
- Portable source uses C# 9, block namespaces and explicit imports. The cloud
  `Migration/Build/CrowdSim.Portable.csproj` compiles the same production files
  under `UnityProject/Packages/com.crowdsim.core/Runtime/` as netstandard2.1.
- Migration build settings and central dependency declarations are isolated from
  the original .NET 8/C# 12 settings. The only new NuGet pin is Newtonsoft.Json
  **13.0.2**, matching Unity's package, under `Migration/Directory.Packages.props`.

Pins were checked against
[Unity's Editor source](https://github.com/Unity-Technologies/UnityCsReference/tree/6000.3.26f1)
and [URP metadata](https://github.com/Unity-Technologies/Graphics/blob/6000.3/staging/Packages/com.unity.render-pipelines.universal/package.json).
M1–M9 were accepted by the user. M10 recording and stage extensions require
local Editor acceptance; a static lock check cannot establish package resolution.

## M2 source and runtime adaptations

All 41 original core files are represented in `Runtime/Engine/`, preserving
namespaces and subsystem order. Production changes are maintained there; the
reference `src/`, tests, solution, examples and dependency pins remain unchanged.
`Migration/Tools/port_reference.py` is an initial mechanical import aid targeting
an empty directory, not a generator for the now manually adapted production source.

- Six positional record structs and the oriented rectangle record struct become
  readonly value structs with equality/hash/operators and positional deconstruction
  where applicable. Record classes and init-only properties stay immutable using
  an internal `IsExternalInit` metadata shim; Unity's C# 9 supports their syntax.
- Newer argument guards, enum APIs, array clearing, OS checks, elapsed-time APIs,
  collection-expression syntax and `MathF.Tau` use equivalent Standard-compatible
  code. Timing is diagnostic, never simulation integration time.
- `ReferenceRandom` implements .NET 8's seeded subtractive sequence, independent
  of the host Mono random implementation. Differential tests cover five seeds,
  1,000 mixed calls each, wide integer ranges and both byte APIs. Unseeded sessions
  create an unpredictable integer seed; seeded runs remain repeatable.
- `ReferencePriorityQueue` retains the reference's quaternary heap and equal-key
  dequeue order. A stable tie-breaker would change routes. A 4,000-operation
  differential test and the unchanged compatibility oracle protect this behavior.
- Native System.Text.Json is replaced with `ScenarioJson` over Newtonsoft 13.0.2.
  Camel-case output, case-insensitive input, defaults, numeric enum identities,
  v1–v4 upgrades and authored-state semantics are retained. Lexical/schema checks
  reject comments, nonstandard numbers/strings, trailing commas, primitive coercion,
  integer overflow and nonfinite values rather than accepting Newtonsoft extensions.
  Existing-file replacement uses `File.Replace`; new files use `File.Move`.
- Unity's JSON package supplies **precompiled `Newtonsoft.Json.dll`**, not a
  `Unity.Newtonsoft.Json` asmdef. The core asmdef explicitly references that DLL
  with `overrideReferences`, and keeps `noEngineReferences: true`. Package 3.2.2
  metadata was inspected; no duplicate codec DLL is vendored into the project.

The portable production assembly does not reference UnityEngine or the reference
net8.0 engine. Simulation owns physics, random ordering, contacts, navigation,
furniture, hazard caches and exit admission. Presentation receives detached,
read-only frames and maps ground XY to Unity XZ; it cannot edit engine entities.
Primitive colliders are removed and Unity physics never advances this model.

The diagnostic scheduler follows the reference's configured step, elapsed clamp,
speed multiplication, catch-up cap and CPU budget. Pause stops scheduling and
clears accumulated time; engine `Running` stays true for reference query semantics.
Single-step advances exactly once. The fast check performs up to four steps per
render frame without changing integration dt. All preset runs stop at tick 120.

## Parity and tolerances

Cloud capture verifies all 121 observed tick hashes, complete initial traits,
readable checkpoints and replay against unchanged M1 goldens in six cases. The
376 applicable original regressions run against portable production source with
unchanged assertions. The other 28 original tests exercise desktop frontend helpers
and remain covered by the unchanged 404-case reference suite; frontend parity
is assigned to later milestones.

Unity diagnostics compare seven checkpoints (0/1/10/11/30/60/120), all initial traits,
and replay, using the readable baseline schema. Floats use absolute **1e-4** plus
relative **1e-5** tolerance; integers, strings, booleans, states and counts are exact.
Readable checkpoints sample six agents and retain full furniture/debris/metrics;
they are not a complete serialized runtime state. Full ordered observed-population
hashes are compared in cloud. No bitwise cross-runtime guarantee is claimed.
A Unity checkpoint failure blocks acceptance and must be investigated without
rewriting goldens or weakening assertions. See [the capture contract](../../Migration/Parity/README.md).

## M10 runtime recording and limits

The unchanged reference pins SharpAvi 3.0.1, SharpAvi.ImageSharp 3.0.0 and transitive
ImageSharp 1.0.4. The M1 probe remains a cloud-only compatibility/known-frame test;
none of those codec binaries/dependencies are added to the Unity runtime.

M10 instead uses Unity's **thread-safe `ImageConversion.EncodeArrayToJPG`**, quality
78, with RGBA32 input, and the production portable **MjpegAviWriter**. The exact
[6000.3.26f1 API declaration](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3.26f1/Modules/ImageConversion/ScriptBindings/ImageConversion.bindings.cs)
was checked. This avoids an Editor-only recorder or an extra managed codec/plugin.
Built-in **imageconversion** and **screencapture 1.0.0** modules are explicitly
included in manifest/lock. Editor/URP/other pins and the original project stay fixed.

An end-of-frame coroutine captures the complete composed screen into a source
RenderTexture, fits it with letterboxing into 1280×720, reads RGBA pixels on the
main thread and queues at most three buffers for worker encoding/muxing. One worker
can own one additional active buffer. Texture/GPU APIs never run on that worker.
Source texture is recreated on resize and bounded to ≤16,777,216 pixels, ≤8192 per
side; output/readback dimensions stay fixed. Whole-window composition/native JPEG
orientation across graphics APIs remain actual Editor/player acceptance checks.
No Editor Recorder dependency, shader-based physics, model random calls or video
clock inputs to simulation are introduced.

Capture cadence is real-time 20 Hz independent of model dt/playback speed. Missed
slots and full-queue frames are dropped/counted; accepted frames play at 20 fps,
so overloaded recordings are shorter than wall time, matching the reference's
best-effort capture semantics. Video includes paused controls and has no audio.
Start/arm/stop/path/status/folder and capture/encode/queue/drop diagnostics are
supplied. Replay/new/load/leave/disable/quit drain accepted frames and finalize the
AVI/index; failures are surfaced. A 1 GiB AVI 1.0 limit and 100,000-frame index bound
stop recording with an explanation and preserve accepted frames when storage works.
OpenDML/multisegment recording is not supplied. Synchronous GPU readback can cost
frame time; forced termination/disk-full can prevent finalization. Editor capture
can suspend if Game view switches to Scene. Mono/IL2CPP/platform results are unrun.

Cloud injects the existing ImageSharp probe for known-frame JPEG encoding and
independently decodes the production worker/muxer's output with ffprobe/ffmpeg.
That does not execute Unity's capture or native JPEG encoder. **No Unity Editor is
installed**: [VALIDATION.md](VALIDATION.md) distinguishes all checks actually run.

## M3 workspace boundary

`Runtime/Workspace/WorkspaceSession` owns an instance of the accepted portable
engine. Only session commands mutate it. Preview applies the same engine placement
command to an independent authored-definition reconstruction, so hovering consumes
neither the live RNG nor live undo history. Candidate geometry is detached before
rendering, including exact population positions. Finalisation spawns at those starts;
geometry/tuning/Hybrid setup then lock. Pause resets accumulated time; speed changes
schedule fixed steps without changing model dt. The one-shot basic Explosion command
retains the engine's event processing and requires an active run.

The pure DashboardLayout and InspectorPanelState helpers retain their historical
`CrowdSim.OpenGL.UI` namespace solely to link unchanged original tests. They do not
reference OpenGL/ImGui/Unity assemblies. Layout is represented as a C#9 readonly
struct; inspector behavior is unchanged. Workspace commands use that inspector
state and the Unity adapter uses that layout. UI Toolkit event routing plus a pure
shortcut policy replaces the old window-event adapter; no custom InputAction asset
is required for this UI-driven milestone. Alt-modified keys and focused text-entry
controls bypass simulator shortcuts.

M3 adds planning snapshots via the optional `includePlanned` flag; M2's calls retain
the existing default. Exit marker dimensions now use twice Exit.Size because that
engine property is a half-width. This corrects presentation only and leaves physical
openings, capacity, engine source and reference trace hashes unchanged.

The world renderer owns pooled primitives/materials and never holds mutable agents.
Room-grid overlays and selection remain presentation state when the inspector is
hidden. Basic fitted isometric camera parameters are independent of model state.
The common Fume builder deliberately keeps the source/cache finalisation guard;
its full run-authoring workflow remains M7, while M2 diagnostics still run Fume.
The discard confirmation on workspace exit pauses a running session; user file/
replay/dirty-state workflows remain M5. No persistent user files are written by M3.

The workspace UIDocument is a separate scene root with its own PanelSettings. It
must not be parented under the startup UIDocument: Unity automatically inherits
parent panels, which would hide the workspace with startup. The bootstrap controller
owns destruction of this root on disable and the exit action. This rule was checked
against the pinned UIDocument source; actual lifecycle tests remain unexecuted here.

## M4 furniture authoring and physical views

The final original frontend helper, BuilderAssetSelection, is ported as C#9 source
under Runtime/Workspace and retains the historical CrowdSim.OpenGL.UI namespace.
Its enum/picker are public for the Unity adapter, but session-owned mutable state
is private. Standard-compatible enum/array APIs replace newer BCL calls. All original
picker and barricade-angle assertions now run against this production helper;
the portable suite links all original test files. No new dependency or pin is added.

WorkspaceSession owns asset selection, per-asset facing and snap settings, and
locks changes at finalisation. The unified picker preserves the original eight
options. Quarter-turn normalization and R steps are the original helper's behavior;
free-angle chairs/round sets and barricades retain exact normalized input. Furniture
previews call the engine's CanPlaceFurniture and, for parallel barricades, the same
SnapBarricadePlacement operation used by the commit path. Proposed set geometry and
seat views are detached even on invalid red previews. Hovering never inserts into
the live undo stack, consumes the live RNG or bypasses stage/seat/overlap guards.

PresentationFrame/FurniturePresentation emit reference-shaped compound furniture,
seated/rising parts and immutable FurnitureBodyFrame status. Anchors use live XY
position/height/heading. Upright and tipped profiles follow the original renderer's
lightweight approximation; round tops and rotated rectangular tops preserve actual
dimensions. Stages/barricades stay fixed. Destroyed bodies are omitted from views
but retained in status/counts. Agent posture, timed stand-up progress and chair
occupancy come from the engine; presentation cannot make someone stand or recover.
Basic seated parts yield to walking capsules after departure; full humanoids/poses,
interpolation and zoom-dependent detail remain M8.

The renderer replaces a pooled primitive when a reused ID changes shape, so rapid
undo/replacement or preview switching cannot retain a round mesh for a rectangular
table. Snapshot status/counts refer to physical bodies rather than the number of
render parts. Source XY maps to Unity XZ with negative Unity-Y yaw as before. No
Unity rigid-body driver or collider-based integration is introduced. Portable
engine algorithm files, original code/tests/examples/pins and all goldens remain
unchanged; both original and portable captures still match exactly in cloud.

WorkspaceExamples.Furniture builds an editable example through ordinary commands;
it is not a user-file loading/replay implementation. It has five furniture sets,
26 people (15 floor, 11 seated), one exit and all obstacle choices. The existing
Bootstrap scene hosts it using the M3 independent-panel lifecycle. M4 adds no new
scene, Editor version change, file workflow or standalone target.

## M5 files and finalized replay

WorkspaceFiles coordinates a session and an injected ScenarioLibrary. The Unity
adapter chooses `Path.Combine(Application.persistentDataPath, "scenarios")` as its
sole write root and displays the resolved path in Files. Company/product identifiers
are unchanged, so the per-user save directory is stable between project downloads.
Read-only roots include `Application.streamingAssetsPath/Scenarios`, the native
`ScenarioFileStore.DefaultDirectory` and `ScenarioFileStore.LegacyDirectory`.
Desktop imports accept an explicit absolute JSON path as a read-only source unless
it is directly within the designated writable directory. No native file-picker
plugin is needed. Mobile/WebGL packaged StreamingAssets are outside this desktop
milestone; standalone target acceptance remains M11.

Duplicate basenames remain separate entries with source/path labels. Save names
normalize whitespace, controls and cross-platform forbidden characters, trim edge
dots/underscores, limit length and prefix reserved Windows device names with
`scenario_`. The UI displays the actual normalized target before writing. Saves
validate through ScenarioJson and use the existing atomic ScenarioFileStore path;
Save As or a different existing target requires confirmation. Sources outside the
write root are never write targets. Normal Save to the current writable entry
updates it directly. IO/permission/schema errors retain the session and dirty state.

Loading requires pause, then builds an independent config/layout-validated candidate
before any dirty decision. Only confirmation replaces the session. Spawn/finalisation
validation occurs when finalising; failure leaves the builder intact. Authored
configuration/layout/behavior changes set dirty; camera, selection, preview, transport,
runtime motion and damage do not. New/load/startup/diagnostics transitions use
Save and continue / Discard / Keep working; a failed save cannot complete a pending
transition. Loading opens a clean paused builder with no replay checkpoint. New
starts empty. Runtime saves never convert injuries or current motion into starts.
Stopping Editor Play Mode is controlled by Unity and does not present an application
save dialog: save authored work through Files before stopping.

Finalisation captures the existing FinalizedSceneCheckpoint privately and creates
a validated ready simulation before replacing the old model. Runtime export reads
its authored Definition. Replay reconstructs from that checkpoint into a temporary
model before replacement, resets scheduler/selection, pauses at tick zero, restores
furniture/seats and regenerates an unreleased Fume cache. Legacy files without exact
positions preserve seeded random placement and requested agent count. Export is
schema 4; original upgrade/default/numeric enum/unit/blue-wall behavior is retained.
A saved Fume source is sufficient for M5 finalisation/precompute and the minimal
Release saved Fume button. Source placement/settings/preview/plume UI remain M7.

FileInterop runs both actual native and portable stores/codecs on .NET 8 using extern
aliases and compares canonical authored definitions for five cases across schemas
1–4. New v1/v3 examples are independently generated from native definitions and
verified byte-for-byte; bundled original v2/v4 fixtures and M1 goldens are unchanged.
Cloud interop is not a Unity Mono filesystem result. Eight new EditMode and two
new PlayMode cases exercise the actual-host path using temporary save roots; none
were run here. File.Replace permissions/platform behavior, real persistent storage,
focus/modal/layout and package resolution still require local Unity acceptance.

## M6 Explosion controls and detached analysis

The Explosion inspector follows the reference's five radii, color legend, 0.10 m
neighbor gap, 0.10 m increments and 200 m outer ceiling. UI Toolkit TextFields keep
a normal caret and staged text. Invariant numeric text commits on Enter or blur;
Escape restores the committed value. Failed commits keep the model and authored
checkpoint intact. Visibility toggles remain independent of radius locking. The
reference allows radii changes before detonation even after finalisation/start;
M6 follows that rule instead of treating radii as locked builder geometry.

An accepted runtime radius edit reconstructs the private authored checkpoint,
changes only its radii and recaptures it before changing live radii. Current
runtime positions, damage and panic never become authored starts. Replay therefore
retains the latest pre-detonation settings with the original layout. No-op/invalid
commits do not mark dirty; accepted configuration changes do. After detonation,
commands and fields reject editing/reset until Replay or a new/loaded builder.
The existing float setter can reject extremely packed radii near 200 m through
nested clamp rounding. Neighbor bounds and a throwaway setter probe reject such
inputs before touching live configuration; the engine algorithm is preserved.
An old file whose config cannot be constructed still fails transactionally on load.

Arming is explicit session state. Only an active Explosion run without a prior
event can arm. Cancel/Escape/pause/replay clears it; rejected placement retains it.
The Unity adapter applies only an armed click, checks finite/in-room coordinates
and uses the same IsPointBlocked margin as the native UI before calling the engine.
The underlying TriggerExplosion command remains available to programmatic tests.
Physics, ordering, shielding, direct outcomes, debris hits and furniture damage are
still computed by the unchanged portable engine; Unity adds no rigid-body driver.

ExplosionFrame detaches event age/front radius, outcomes, shielding flags, average
mobile visibility, debris counters, five zone meshes, transient front mesh and dust
samples. The persistent annuli reproduce the native renderer's 128-sector fill,
144-segment outlines, point/midpoint visibility tests and boundary inset. This is
conservative sampled clipping, not an analytic Boolean geometry operation: whole
sectors can be omitted near walls/room edges. Static rectangle/circle line-of-sight
rules ignore furniture, including stage/barricade, exactly as the reference does.
Physically transparent perimeter geometry remains in those tests. The cache keys
model identity, wall layout version, event origin and radii; toggles change display
only and never invoke event physics. Replay/new/load cannot reuse old event meshes.

The front uses reference-step-scaled elapsed ticks and the native speed/end timing.
Dust shading is an additional ground analysis view of the existing VisibilitySystem,
sampled on a 1 m grid into eight opacity buckets with wall/room clipping. It is not
a volumetric smoke solver. Dust builds/holds/decays on model time, freezes on pause,
and is removed after the field's maximum lifetime. Hiding it leaves perception
and routing untouched. Debris uses detached live position, velocity heading, energy
and the native prism dimensions; Unity quantizes its display color into nine shades
to bound material growth. These view changes do not alter physical debris.

WorkspaceExplosionView owns reusable transparent URP Unlit meshes/materials with
alpha blending, culling off, depth testing and depth writes off. Static geometry is
uploaded only when its immutable reference changes. Dynamic meshes follow model
snapshots; no render frame advances physical time. Materials/meshes are destroyed
with the workspace. M6 adds no package dependency or Editor/project target change.
Actual import/shader appearance, text focus, native input and resource lifecycle
still require the supplied Unity tests/manual acceptance. Complete humanoids/cameras
remain M8 and complete general overlays/scale/instrumentation remain M9.

## M7 Toxic Fume and requested presentation refinement

WorkspaceSession owns source/config/compute/preview/release commands. FumeControls
exposes all ten native controls and bounds: initial radius, dispersion speed, maximum
extent, release duration, drift direction/bias, awareness/high-threat concentration
and impairment/incapacitation exposure. SetValues retains the original dependency
clamps and revision behavior. Nonfinite input is rejected at the command boundary.
The fixed 0.50 m field grid and 24 s decay remain unchanged. Setup locks after
finalisation. Source placement uses the native clear-floor/inset rule and additionally
checks finiteness; resize cannot exclude an existing source. Source movement is not
part of the environment's furniture/placement undo stack, matching the reference.

Successful geometry/undo/resize/source/fume-setting edits invalidate the field and
require explicit recomputation. Behavior/Hybrid/display changes do not alter the
propagation cache. Compute runs the unchanged setup-time engine pass, catches errors
and reports actual counts/bytes/arrival/CPU time. M5's tested unedited saved-source
shortcut remains: Finalise can compute it automatically. A user can instead Compute
to preview before finalising. Newly authored/edited sources require Compute. This
preserves accepted M5 behavior while delivering the full M7 authoring workflow.

Builder preview only changes the existing PreviewTimeSeconds and detached FumeFrame.
It does not step, spawn, integrate exposure, dirty the definition or consume model
randomness. Finalisation/replay regenerates the cache, returns preview/release time
to zero and restores authored starts/furniture. Runtime release requires Running and
a valid unreleased cache. Incapacity/perception/risk navigation remain engine-owned;
there is no new chemical/CFD model. ReleaseSavedFume remains as a programmatic alias;
the current UI says Release Toxic Fume.

FumeFrame detaches actual cache status, source/radius, compute statistics, display/
release time and native exposure/response metrics. Full 0.5 m cell samples at the
reference's 0.025 display threshold are retained without stride aggregation, avoiding
large display cells across thin obstacles. Quads use those cells and room bounds;
color/opacity use eight concentration buckets with each bucket's average. This is
a display approximation to the original green/yellow palette, not quantized physics.
Grid lines are optional and the source marker remains when the plume is hidden.
Ready-but-unreleased scenes show the initial field at time zero, as in the reference;
labels distinguish preview, unreleased and live exposure. Hidden UI/field/grid flags
never change the model. Each effect owns an independent child root for cleanup and
component tests. The shared transparent overlay helper preserves M6 material/depth
settings and supports UInt32 indices for large meshes.

The user requested more professional, intuitive UX/assets with this milestone.
Scenario cards expose both builders and direct examples; diagnostics are collapsible.
Selected tabs, phase badges, action hierarchy, hazard accents and source/compute/
preview/release guidance replace development-placeholder copy. Existing entry/control
IDs remain where applicable. Lit URP materials add restrained wood grain, slate tile,
safety stripes and dark metal; two owned directional lights reveal volumetric shapes.
Tables/chairs/barricades use a shared chamfered box mesh with identical unit bounds;
the fixed stage keeps its exact rectangular view. Floor, markers, physical source
geometry and engine collision/placement dimensions remain unchanged. View surfaces,
textures and lights are procedural/original and require no external asset license,
new package, user file, model RNG or Unity physics. Meshes/materials/textures are
owned and disposed with the workspace. Native appearance, shadows, import and GPU
behavior still require local Editor review. Complete humanoids/poses/cameras remain
M8; the current walking representation remains volumetric capsules at every zoom.


## M8 humanoids, cameras and ongoing user-centred design

AgentVisualFrame is an immutable value snapshot with position, normalised facing,
velocity/base-speed ratio (both per reference step), health, posture/rise, panic,
assistance target/attendance and the reference WaitingToReact alarm cue. Heading
uses velocity, desired direction, exit, then +X fallback; seated people use chair
start heading; helpers face active targets. No Agent or mutable collection escapes.
Existing Bodies remain diagnostic anchors for accepted portable/M2 tests; Bootstrap
suppresses those runtime capsules and renders the detached Agents collection.
Builder planning markers remain separate. The M2 SimulationParity viewer stays a
primitive trace diagnostic, not the M8 application presentation.

HumanGeometry builds positive-volume boxes in local X-forward/Y-up/Z-left coordinates.
Unity maps ground XY to XZ and reverses ground yaw. Health takes priority over seat/
help posture: dead/incapacitated/temporary/fallen are prone, crawling is low, then
seated/rising, timed assistance and upright locomotion. Near detail includes neck,
pelvis/feet and articulated low limbs; far figures retain solid torso/head/four limbs.
Selections retain detail. This reproduces the reference low-poly pose meanings,
not its exact OpenGL rasterisation. HumanMotion interpolates only between detached
render positions/rise progress without extrapolation; render-only gait scales with playback,
freezes on pause/modal and resets on replay. Physics still advances solely through
the fixed-step session scheduler and seeded engine, never a view or Unity physics.

Unity views reuse people/parts, cached state/shade materials and owned bevel meshes.
An included URP front-culled hull shader adds dark silhouettes. Rendered oriented-box
picking supports low/seated bodies without colliders; it does not alter physical
collision radii. Panic/selection have separate ground rings; alarm/help/ID/state
labels are clipped to the camera viewport and use pale text on dark panels.
Panic does not recolour a body. State hue meanings are preserved with restrained
Unity palette values; labels, poses and symbols supplement colour. Ring dimensions,
outline thickness and the 48-pixel near threshold are cosmetic Unity choices.
Resources are released with the world; inactive pooled people retire from picking,
labels, and drawing. Large-crowd optimisation/timings remain M9.

AgentCameraRig is pure math shared by tests and the actual Camera adapter. Overview
retains M3 room fitting/azimuth/elevation/0.45–2.5 zoom. Follow centres the selected
render position with the reference 1.35x follow multiplier. POV uses the native
72° vertical FOV, 0.04 m near plane, fixed 1.496 m eye and heading/downward pitch,
including for low poses; its own body/indicators are hidden. Floor picking/wheel zoom
are disabled in POV, as in the native camera. Explicit Exit POV returns to Follow;
clear/agent exit/replay/new/load return to overview. Entering Follow/POV cancels the
armed Explosion tool. Hiding inspector never clears selection/mode or forces reveal.
The in-viewport mode/leave controls remain available. Camera operations are view-only
and never dirty authored definitions or recapture runtime damage into checkpoints.

The separate HumanoidPresentation scene feeds 14 deterministic fixtures into the
same production renderer; it instantiates no Simulation and is not hazard evidence.
The bundled unmodified OFL Atkinson Hyperlegible Regular is loaded into both UI
Toolkit roots and world labels, avoiding local font installation. See DESIGN.md for
source/hash/license and the user's ongoing font/colour convention. Key authored
solid text/background pairs, including primary hover/focus, pass 4.5:1. This static
check does not certify actual font import, lighting, translucent overlay contrast,
layout, native input, Unity compilation or accessibility; local review is required.
No Editor/.NET target/UPM dependency pin or simulation algorithm changed.


## M9 diagnostics, scale and the requested architectural theme

PopulationMetrics translates the actual native UiMetrics.Capture body into C#9
without changing its denominators. In-room counts all active people; mobile excludes
fatal/fallen/temporary/permanent incapacity but includes crawling. Panic/visibility/
congestion/intent averages use that mobile denominator, with native empty defaults.
Fume exposure counts/mean include exited people's accumulated exposure. Exit density
is the active count within ten agent radii (2 m by default); flow is admitted count
per most recent dt, not a long-term rate or people/m². DiagnosticFrame exposes native
Hybrid/group/shared-field and engine/furniture/route counters without inferring
performance. AgentInspection translates the native selected-agent/model readouts,
including conditional navigation, slow-blocker, assistance and casualty-helper rows.
Speeds divide per-reference-step velocity/desired speed by 0.15 for m/s. The portable
boundary captures immutable numbers/strings/vectors/collections; no Agent escapes.
Snapshots are lazy-cached until model/selection changes, and capture is read-only.

Eight DiagnosticOverlay flags are session-owned. Pure geometry builds batched layers
for desired/avoidance/herd/SFM/blast arrows, vision cones, purple congestion disks and
trajectory trails. Arrows retain native 32/30/28/45-cap/26 legacy-unit metre scaling,
force scaling /160, and the 1.78 m display height. Vision uses native range/half-angle
and 18 arc segments at 0.3 m, without occlusion clipping. Panic remains independent.
Congestion uses actual LocalCongestion above 0.08 with world-space disk radius and
uniform layer opacity, rather than reproducing OpenGL screen-space circles exactly.
Trails sample every third tick, cap forty points, remove exited people and reset on
replay/new/load; uniform layer opacity is a cosmetic difference from native fade.
POV excludes its own agent. Existing GroundOverlayLayer uploads one owned line/fill
mesh per enabled category with 32-bit indices as needed; hiding the inspector never
disables the view. No overlay invokes a simulation command or consumes seeded RNG.

SetPausedHybrid permits changes only while not Running. For a finalised scene it
rebases only the Hybrid flag on the private authored checkpoint, changes the live
configuration and marks it dirty. Tick, live positions, damage, RNG/order, furniture,
exposure and numerical guards are untouched. Replay/save therefore use the latest
flag with original authored starts. The existing builder-only SetHybrid API remains
compatible. Hybrid off still uses native SFM and navigation; cross-mode trajectories
can differ as before. Same-mode original/portable traces remain unchanged.

CrowdScaleLayout shares seed-731 authored fixtures between the Unity Debug workflow
and a locked net8 cloud CLI: four five-person exits, 220/500/1000 total people,
open/eight-table furnished rooms with 80 seats included, both hazards and modes.
Independent layout RNG fixes coordinates across modes without consuming model trait
randomness. Scenario files/checkpoints use existing schema 4 and codecs. Comparison
creation uses the existing save/discard/keep-working transition, not an unguarded
session replacement. Timed CLI results exclude setup/precompute/warm-up and report
step CPU/allocations separately from pure snapshot/geometry CPU/allocations. They
establish no Unity/GPU/FPS or calibrated physical performance. See M9_BENCHMARK.md.

The Unity controller reports observed wall-frame interval/FPS and bounded 120-frame
p50/p95, CPU spent in physics commands (including session snapshot work), native last
tick/furniture/route CPU, world-command CPU and UI-command CPU (excluding UI Toolkit
paint). Main-thread managed bytes are measured between updates; they are not total
process/GPU memory. GPU timing is explicitly unmeasured and recorder integration is
M10 pending. Replay/new/load resets measurement history. View optimisations cull
posing outside the camera frustum, cache renderers/material keys/unchanged poses,
reuse rings and pooled people, and disable small-part shadow casting. Geometry stays
volumetric at all zooms and rendering never changes engine update order or bodies.
Actual graphical scale acceptance is local; no FPS threshold is invented.

After accepting M8 the user explicitly requested black text, off-white square/round
pillars with black outlines, and an off-white gridded floor. Startup/workspace/
validation styles and world/pose-gallery labels now use black type on pale surfaces;
hazard colours move to swatches and border cues. Higher-specificity input/hover/
focus/disabled rules retain black text. The workspace root remains transparent to
avoid masking the camera. Square Blockages gain detached IsPillar metadata; legacy
blue walls remain separate. Pillars retain original mesh bounds and get owned black
edge meshes and a front-culled silhouette hull; outline geometry is view-only, with
no collider/model geometry. The mineral-finish off-white floor has the existing
1.2 m grid enabled by default and still adjustable on Debug. Key/fill lighting is
restrained for pale surfaces. A black selection ring and pale label plates preserve
contrast; state-body colours/poses remain meaningful. Planning people get silhouettes.
No simulation/scientific parameter, original reference file, Engine algorithm,
Editor/.NET target, schema or UPM dependency pin changed. Package version is 0.9.0.

## M10 user-authorized stage extension

The user explicitly requested editable stage width/length constrained by the room,
and a concert appearance after accepting M9. Only portable `Furniture.cs`,
`SimulationEnvironment.Furniture.cs`, the overlap helper in `SimulationEnvironment.cs`
and `ScenarioDefinition.cs` extend the imported engine for that request. The original
`src/`, tests/examples/pins and main are unchanged. Default-size scenarios retain
all exact trace hashes and the original 15×6×0.8 m stage behavior.

Length is stage-local X and depth/width stage-local Y; quarter turns exchange world
axes. UI maxima follow the rotated usable room spans, including the original 0.6 m
perimeter inset, and both dimensions must be finite and ≥1 m. Placement and existing
stage resizing use exact rectangles and retain all boundary/exit/obstacle/planned-
person/occupied-set reservation guards. Existing resizing replaces only that stage,
retains centre/facing, rebuilds wall/navigation/fume dependencies, marks authored
state dirty and preserves original placement undo order. Finalisation locks edits.
The stage stays single, fixed, non-walkable and unoccupied. Truss/screens/speakers/
lamps are read-only visuals inside the footprint; no new hazard-shielding geometry.

Ordinary saves, including default-size stages, retain schema **v4** and omit optional
stage fields. Custom-size stages use explicit **v5** with paired `stageLength` and
`stageWidth` values. Unity imports v1–v4 with unchanged legacy defaults and reads/writes
v5; its checkpoints/save/load/replay preserve custom dimensions. The original codec
rejects v5 rather than silently restoring 15×6 m. V4 carrying custom fields, unpaired/
nonfinite/too-small fields and fields on non-stage furniture are rejected by Unity.

The cloud trace harness's System.Text.Json resolver honors the production codec's
omission of those two zero/default extension fields only. All existing fields,
nonzero custom values, ordered physical hashes/checkpoints and unchanged goldens
are retained. The real-codec interop harness still runs all five v1–v4 cases and
additionally verifies v5 Unity round-trip plus native rejection.

Readback RGBA buffers use ArrayPool to avoid a full managed image allocation on
every frame. Ownership returns after encode, rejection, worker failure and stop
drain. Native JPEG allocations and actual graphics memory still require profiling.

## M10 performance-fix scope

The user explicitly requested using M10 as the base to address slow simulation CPU
and use GPU rendering. This is a fix iteration before M11, package **0.10.1**.
No Editor/URP/runtime dependency pin changes or original source changes are needed.
Physics remains portable CPU C#, with fixed dt/seed/order/guards intact; no Jobs,
Burst or compute-shader solver is introduced.

Portable `Simulation.Step.cs`/`Simulation.cs` add read-only exclusive phase clocks
and a per-tick architecture-only sight snapshot, safely disabled in finally.
Existing sight semantics already exclude furniture, which can move during a tick;
architecture cannot be authored mid-tick. Outside-Step queries consult live lists.
Physical traversability/collision retains all furniture. `SfmEngine.cs` reuses exact
Exp(-60)/Exp(20) clamp values for circle forces and bypasses magnitude square roots
only when a conservative L1 bound proves the original vector stays below the cap.
No far-field force is truncated. `CircleGeometry.cs`, `Simulation.Collisions.cs`
and one circular-follow candidate loop in `EvacuationSystem.cs` add conservative
padded axis rejection before unchanged exact distance tests. Original order and
exact physical acceptance/correction paths remain. MoveAgent's timing wrapper
includes all return paths; nested SFM/move-collision/route timings are subsets.

The frontend uses **Graphics.RenderMeshInstanced<Matrix4x4>**, matching the exact
6000.3.26f1 Graphics/RenderParams API. Per-material/mesh blocks hold ≤256 matrices,
conservatively below the default nonuniform-scale instancing constant-buffer limit;
no uniform-scaling assumption corrupts body normals. The custom URP crowd shader
and silhouette shader declare instanced vertex input/setup. Both are explicitly
retained in GraphicsSettings so player variant stripping cannot remove a shader
used only through runtime submission. GPU bodies keep the same detached geometry,
poses, material-state shades, outlines and CPU interpolation. Annulus instances
replace per-agent ring renderers. Labels remain black and readable. Matrix-based
picking uses the same normalized oriented part boxes in both render modes. GPU mode
retains lightweight person roots for anchors/visibility but no body-part renderer
hierarchy. Unsupported shaders/devices or a disabled Debug checkbox use the original
compatibility renderer. Actual shader/GPU execution remains unrun here.

`InstanceBatchPlan` and the valid DenseCrowdLayout are portable/testable helpers.
The latter creates 800 people (320 seated at 32 tables) with the persisted furniture
clearance margin, four exits and seed 731. It is a representative fixture, not a
reconstruction of the user's exact screenshot. UI creation respects dirty prompts.
Three paired CPU trials use identical authored inputs and exact ordered output
hashes; all 24 pairs and unchanged original traces match. See the benchmark report
for actual measured scopes, baseline instrumentation and reproduction.

Runtime shader preservation also sets GraphicsSettings **Instancing Variants =
Keep All** (`m_InstancingStripping: 2`), verified against the exact Editor source
enum. This prevents variants used only by scripted submissions from being stripped;
actual player shader compilation remains unexecuted and part of local/M11 checks.

Instancing targets the runtime crowd. Builder planning markers and authored
furniture/stage use the existing world renderer; those objects are not converted
to the new crowd batches in this fix. All still render through Unity's graphics
pipeline. Local profiling should include their cost in heavily furnished scenes.

The recording NativeArray copy explicitly uses **global::Unity.Collections**.
Inside `namespace CrowdSim.Unity.Workspace`, an unqualified `Unity.Collections`
binds to `CrowdSim.Unity` and causes CS0234, even with valid packages. This is a
namespace lookup correction, not a missing assembly reference or a new dependency.
Package pins and assembly definitions remain unchanged. A separate local Input
System registry authentication failure and missing package DLLs require Hub sign-in
and a clean import; see [M10_IMPORT_RECOVERY.md](M10_IMPORT_RECOVERY.md).
